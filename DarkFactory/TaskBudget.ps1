# The per-task cost cap: median run cost, a run's budget, what a task spent in the last day,
# and -TestTaskBudget. Dot-sourced by RunDarkFactory.ps1 in its order; see the loader there.

function Get-MedianCost {
    # The median of a list of run costs in US dollars, or 0 for an empty list.
    param([double[]]$Costs)
    $sorted = @($Costs | Sort-Object)
    if ($sorted.Count -eq 0) { return 0.0 }
    $mid = [int][math]::Floor($sorted.Count / 2)
    if ($sorted.Count % 2) { return [double]$sorted[$mid] }
    return ([double]$sorted[$mid - 1] + [double]$sorted[$mid]) / 2
}

# The least a run is worth starting with (BL-3079, ADR-0760): a requeued claim left $1.02
# of its cap ran 5 turns and requeued again (AF-0326: BL-2970), so a task's runs of the last
# day must leave at least this much of the cap or the task is not run again.
$MinRunBudgetUsd = 2.0

function Get-RunBudgetUsd {
    # The cost cap for the next headless run (AF-0033, ADR-0407, ADR-0478): 4 times the
    # median of the recent task runs' costs, so a task that needs a few more turns than most
    # still finishes, never above -TaskBudgetUsd and never below $3. Fewer than 10
    # recent costs leaves -TaskBudgetUsd as it is; -TaskBudgetUsd 0 means no cap.
    # -SpentUsd is what the task's earlier runs of the last day already cost (AF-0095): it
    # comes off the cap, down to $MinRunBudgetUsd ($2), so a task requeued and run again stays near one cap
    # in all rather than two.
    param([double]$Ceiling, [double[]]$RecentCosts, [double]$SpentUsd = 0)
    if ($Ceiling -le 0) { return 0.0 }
    $costs = @($RecentCosts | Where-Object { $_ -gt 0 })
    $cap = if ($costs.Count -lt 10) { $Ceiling } else { [math]::Min($Ceiling, [math]::Max(3.0, [math]::Round(4.0 * (Get-MedianCost $costs), 2))) }
    if ($SpentUsd -le 0) { return $cap }
    return [math]::Max($MinRunBudgetUsd, [math]::Round($cap - $SpentUsd, 2))
}

# List-price estimates in US dollars per million tokens, used only for a run whose log has no
# result event or a total_cost_usd of 0 (a killed or restarted run, BL-3001). Fitted to the
# finished runs in Z:\repos\Curl.logs from 2026-10-19 17:00 to 2026-10-21: cost divided by usage
# weighted input 1, cache write (1-hour) 2, cache read 0.1, output 5 gave a base price of about
# 3.08 (median of 169 Opus runs) and 1.65 (median of 35 Sonnet runs) per million. Haiku had one
# finished run, too few to fit, so its base price is the Sonnet one scaled by the list-price
# ratio of one third. These are estimates, not billed amounts.
$script:RunPriceBaseUsdPerMillion = @{ opus = 3.08; sonnet = 1.65; haiku = 0.55 }
$script:RunPriceCacheWriteTimes = 2.0
$script:RunPriceCacheReadTimes = 0.1
$script:RunPriceOutputTimes = 5.0

function Get-UsageCostUsd {
    # The estimated cost in US dollars of one model's token counts, from the list-price
    # constants above. An unknown model is priced as Opus, the dearest, so it never undercounts.
    param([string]$Model, [double]$InputTokens, [double]$CacheWrite, [double]$CacheRead, [double]$Output)
    $family = if ($Model -match 'sonnet') { 'sonnet' } elseif ($Model -match 'haiku') { 'haiku' } else { 'opus' }
    $weighted = $InputTokens + $script:RunPriceCacheWriteTimes * $CacheWrite + $script:RunPriceCacheReadTimes * $CacheRead + $script:RunPriceOutputTimes * $Output
    return $weighted * $script:RunPriceBaseUsdPerMillion[$family] / 1e6
}

function Read-SharedLogLines {
    # The lines of a log another process may still hold open for writing, read with shared
    # access and retried briefly on IOException; a log that stays locked reads as no lines (BL-3061).
    param([string]$Path)
    for ($try = 1; $try -le 5; $try++) {
        try {
            $stream = [System.IO.FileStream]::new($Path, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, ([System.IO.FileShare]::ReadWrite -bor [System.IO.FileShare]::Delete))
            try {
                $reader = [System.IO.StreamReader]::new($stream)
                $lines = [System.Collections.Generic.List[string]]::new()
                while ($null -ne ($line = $reader.ReadLine())) { $lines.Add($line) }
                return , $lines.ToArray()
            }
            finally { $stream.Dispose() }
        }
        catch [System.IO.IOException] {
            if ($try -lt 5) { Start-Sleep -Milliseconds 200 }
        }
        catch { break }
    }
    Write-Verbose "Could not read $Path; its cost is not counted."
    return , @()
}

function Get-LogEstimatedCostUsd {
    # A run log's cost in US dollars estimated from the usage of its assistant messages.
    # stream-json repeats a message's id on each of its content blocks, with the output count
    # growing, so each id counts once with its largest count of each kind.
    param([string]$Path)
    $byId = @{}
    foreach ($line in (Read-SharedLogLines $Path)) {
        if (-not $line.StartsWith('{"type":"assistant"')) { continue }
        $evt = try { $line | ConvertFrom-Json } catch { $null }
        $message = if ($evt) { $evt.message } else { $null }
        if (-not $message -or -not $message.id -or -not $message.usage) { continue }
        $usage = $message.usage
        if (-not $byId.ContainsKey($message.id)) { $byId[$message.id] = @{ Model = "$($message.model)"; In = 0.0; Cw = 0.0; Cr = 0.0; Out = 0.0 } }
        $seen = $byId[$message.id]
        $seen.In = [math]::Max($seen.In, [double]$usage.input_tokens)
        $seen.Cw = [math]::Max($seen.Cw, [double]$usage.cache_creation_input_tokens)
        $seen.Cr = [math]::Max($seen.Cr, [double]$usage.cache_read_input_tokens)
        $seen.Out = [math]::Max($seen.Out, [double]$usage.output_tokens)
    }
    $usd = 0.0
    foreach ($seen in $byId.Values) { $usd += Get-UsageCostUsd $seen.Model $seen.In $seen.Cw $seen.Cr $seen.Out }
    return $usd
}

function Get-RunLogCost {
    # What one run log cost: Usd, and Estimated ($true when it comes from the logged usage
    # because the log has no result event or its total_cost_usd is 0), or $null when the log
    # has neither a cost nor any usage. Every reader of a run's cost goes through here.
    param([string]$Path)
    $line = Get-Content $Path -Tail 5 -ErrorAction SilentlyContinue | Where-Object { $_ -match '"total_cost_usd"' } | Select-Object -Last 1
    $evt = if ($line) { try { $line | ConvertFrom-Json } catch { $null } } else { $null }
    if ($evt -and $evt.type -eq 'result' -and [double]$evt.total_cost_usd -gt 0) { return [pscustomobject]@{ Usd = [double]$evt.total_cost_usd; Estimated = $false } }
    $estimate = Get-LogEstimatedCostUsd $Path
    if ($estimate -gt 0) { return [pscustomobject]@{ Usd = $estimate; Estimated = $true } }
    if ($evt -and $evt.type -eq 'result') { return [pscustomobject]@{ Usd = 0.0; Estimated = $false } }
    return $null
}

function Get-TaskSpentUsd {
    # What a task's earlier task and resumed runs in a log folder cost, in US dollars,
    # counting only logs written in the last -Hours (AF-0095): a requeued task's next run
    # gets the rest of one cap, while a task parked days ago starts afresh.
    param([string]$Dir, [string]$Id, [double]$Hours = 24)
    $spent = 0.0
    foreach ($run in @(Get-TaskRunSpends $Dir $Id $Hours)) { $spent += $run.Usd }
    return $spent
}

function Get-TaskRunSpends {
    # Each of a task's task and resumed runs logged in the last -Hours: Time (the log's last
    # write, when it leaves the window -Hours later) and Usd. Get-TaskSpentUsd sums them.
    param([string]$Dir, [string]$Id, [double]$Hours = 24)
    if (-not (Test-Path $Dir)) { return @() }
    $since = (Get-Date).AddHours(-$Hours)
    $pattern = '^' + [regex]::Escape($Id) + '-\d{8}-\d{6}(-L\d+)?(-resumed)?\.jsonl$'
    $runs = @()
    foreach ($log in @(Get-ChildItem $Dir -Filter "$Id-*.jsonl" -ErrorAction SilentlyContinue | Where-Object { $_.Name -match $pattern -and $_.LastWriteTime -ge $since })) {
        # A run that only found the task waiting on other tasks is waived (Register-PrerequisiteRequeueRun).
        if (Test-Path -LiteralPath (Get-PrerequisiteRequeueMarkerPath $Dir ($log.Name -replace '(-resumed)?\.jsonl$', ''))) { continue }
        $cost = Get-RunLogCost $log.FullName
        if ($cost) { $runs += [pscustomobject]@{ Time = $log.LastWriteTime; Usd = $cost.Usd } }
    }
    return $runs
}

# A run that sent its task back to Backlog waiting on other tasks, with no work to keep, did
# none of the task's own work, so its cost does not come off the cap of the claim that does
# it once they are Done (AF-0387, AF-0404, AF-0405, AF-0406: BL-3204, BL-3412, BL-3315 and
# BL-3234 each spent a first run of $0.73 to $0.86 finding a prerequisite, and the claim
# after it ran out of the rest of one cap and was split). The board claims a task only once
# every ID in its depends-on is Done, so a depends-on ID still open when the run ends was
# added by that run; the next claim waits for it, so a waived run is never repeated for free.
function Get-PrerequisiteRequeueMarkerPath {
    # The marker beside a run's log (<ID>-<stamp>[-L<n>]) that waives its cost.
    param([string]$Dir, [string]$RunName)
    return (Join-Path $Dir "$RunName.prerequisite-requeue.txt")
}

function Get-PrerequisiteRequeueWhy {
    # Why the run that just ended is waived from its task's cost cap, or '' when it is not:
    # the task is in Backlog, the run left nothing to stash and no stash of the task waits,
    # and -OpenDependsOn (its depends-on IDs not Done) is not empty. Pure, so -TestTaskBudget proves it.
    param([string]$State, [bool]$LeftWork, [string[]]$OpenDependsOn)
    $open = @($OpenDependsOn | Where-Object { $_ })
    if ($State -ne 'Backlog' -or $LeftWork -or -not $open.Count) { return '' }
    return "went back to Backlog waiting on $($open -join ', ') with no work to keep, so this run does not count toward its cost cap"
}

function Register-PrerequisiteRequeueRun {
    # Called after a task's run, before Save-StrayChanges: writes the marker for this shift's
    # run of -Id when Get-PrerequisiteRequeueWhy says it is waived, and traces it.
    param([string]$Id, [string]$State)
    if ($State -ne 'Backlog') { return }
    $file = Get-ChildItem (Join-Path $Root 'Tasks\Backlog') -Filter "$Id-*.md" -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $file) { return }
    $line = Select-String -LiteralPath $file.FullName -Pattern '^depends-on:\s*\[(.*)\]\s*$' | Select-Object -First 1
    $open = if ($line) { @(Get-TaskIdsNamed $line.Matches[0].Groups[1].Value | Where-Object { $_ -ne $Id -and -not (Test-TaskDone $_) }) } else { @() }
    $leftWork = [bool](Get-Dirty) -or [bool](Get-TaskStashSha $Id)
    $why = Get-PrerequisiteRequeueWhy $State $leftWork $open
    if (-not $why) { return }
    Set-Content -LiteralPath (Get-PrerequisiteRequeueMarkerPath $LogDir "$Id-$Stamp$LaneTag") -Value $why -Encoding UTF8
    Write-Trace $Id 'waive' $why 'Cyan'
}

function Get-CapResetTime {
    # When a task's cost cap frees: the time its oldest runs leave the -Hours window so that
    # what is left no longer spends the cap (Test-TaskCapSpent), or $null when the cap is not
    # spent now. -Runs are Get-TaskRunSpends objects. Pure, so -TestTaskBudget proves it.
    param([object[]]$Runs, [double]$Cap, [double]$Hours = 24)
    $sorted = @($Runs | Sort-Object Time)
    $left = 0.0
    foreach ($run in $sorted) { $left += $run.Usd }
    if (-not (Test-TaskCapSpent $Cap $left)) { return $null }
    foreach ($run in $sorted) {
        $left -= $run.Usd
        if (-not (Test-TaskCapSpent $Cap $left)) { return ([datetime]$run.Time).AddHours($Hours) }
    }
    return $null
}

function Get-CapFreesAt {
    # The earliest time any of the cost-capped tasks -Ids has its cap free, or $null (BL-3090).
    param([string[]]$Ids)
    $cap = Get-RunBudgetUsd $TaskBudgetUsd @(Get-RecentRunCosts $LogDir)
    $times = @($Ids | ForEach-Object { Get-CapResetTime @(Get-TaskRunSpends $LogDir $_) $cap } | Where-Object { $_ } | Sort-Object)
    if ($times.Count) { return $times[0] }
    return $null
}

function Close-UnfinishedRunLogs {
    # Gives a result event to every earlier run log of a task that ends without one: a shift
    # restart or a killed lane stops the run before Invoke-TaskRun can write its result, so
    # the audit's unfinishedRuns listed it for good (AF-0152). Called as the task's next run
    # starts, when no earlier run of the task can still be writing. -Except is the new log.
    param([string]$Dir, [string]$Id, [string]$Except = '')
    if (-not (Test-Path $Dir)) { return }
    $pattern = '^' + [regex]::Escape($Id) + '-\d{8}-\d{6}.*\.jsonl$'
    foreach ($log in @(Get-ChildItem $Dir -Filter "$Id-*.jsonl" -ErrorAction SilentlyContinue | Where-Object { $_.Name -match $pattern -and $_.FullName -ne $Except })) {
        $tail = @(Get-Content $log.FullName -Tail 3 -ErrorAction SilentlyContinue | Where-Object { $_ -match '"type"\s*:\s*"result"' })
        if ($tail.Count) { continue }
        Add-Content -Path $log.FullName -Value ([pscustomobject][ordered]@{ type = 'result'; subtype = 'factory_no_result'; is_error = $true; exit_code = $null; timed_out = $false; result = 'ended with no result event: the shift or lane stopped before the run did'; total_cost_usd = 0 } | ConvertTo-Json -Compress) -Encoding UTF8
    }
}

function Test-TaskCapSpent {
    # Whether a task's runs of the last day leave less than $MinRunBudgetUsd ($2) of its cap
    # (AF-0096, AF-0326). Get-RunBudgetUsd never gives a run less than that, so without this a task requeued
    # again and again got $1 and a turn more each time (BL-1609: five runs, $5.17).
    param([double]$Cap, [double]$SpentUsd)
    return ($Cap -gt 0 -and $SpentUsd -gt 0 -and ($Cap - $SpentUsd) -lt $MinRunBudgetUsd)
}

function Test-CapBlockRequeueDue {
    # Whether a Blocked task may go back to Backlog because the cost cap's daily rule
    # (AF-0096) no longer holds for it (BL-2117): its Blocked reason is the daily-rule
    # refusal ("so it was not run again") and its runs of the last day now leave $MinRunBudgetUsd or more
    # of -Cap. A task blocked because a run itself stopped at the cap ("stopped at its ...
    # cost cap") is too big for one run and stays Blocked to be split.
    param([string]$Reason, [double]$Cap, [double]$SpentUsd)
    if ($Reason -notmatch 'so it was not run again' -or $Reason -match 'stopped at its .* cost cap') { return $false }
    return -not (Test-TaskCapSpent $Cap $SpentUsd)
}

function Get-TaskCapSpentWhy {
    # Why a freshly claimed task must not run again, or '' when it may (AF-0096).
    param([string]$Id)
    $spent = Get-TaskSpentUsd $LogDir $Id
    $cap = Get-RunBudgetUsd $TaskBudgetUsd @(Get-RecentRunCosts $LogDir)
    if (-not (Test-TaskCapSpent $cap $spent)) { return '' }
    $culture = [System.Globalization.CultureInfo]::InvariantCulture
    return "its runs of the last day already cost $($spent.ToString('0.00', $culture)) of its $($cap.ToString('0.00', $culture)) US dollar cost cap, so it was not run again; split the task"
}

function Get-RecentRunCosts {
    # The cost (Get-RunLogCost: the result event's, else estimated from usage) of the newest task runs' logs in a log folder
    # (BL-1377-20261003-145757-L1.jsonl and its -resumed run; resolver runs are not tasks).
    param([string]$Dir, [int]$Count = 40)
    if (-not (Test-Path $Dir)) { return @() }
    $logs = Get-ChildItem $Dir -Filter 'BL-*.jsonl' -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -match '^BL-\d+-\d{8}-\d{6}(-L\d+)?(-resumed)?\.jsonl$' } |
        Sort-Object LastWriteTime -Descending | Select-Object -First $Count
    foreach ($log in $logs) {
        $cost = Get-RunLogCost $log.FullName
        if ($cost) { $cost.Usd }
    }
}

if ($TestTaskBudget) {
    $twelve = [double[]](1.0, 1.0, 1.0, 1.2, 1.2, 1.3, 1.3, 1.4, 1.5, 2.0, 4.0, 5.4)
    $cases = @(
        ,@('median of an odd list', '2', "$(Get-MedianCost 3.0, 1.0, 2.0)")
        ,@('median of an even list', '1.5', "$(Get-MedianCost 1.0, 2.0, 4.0, 1.0)")
        ,@('median of nothing', '0', "$(Get-MedianCost @())")
        ,@('cap at 4 times the median 1.3', '5.2', "$(Get-RunBudgetUsd 10 $twelve)")
        ,@('cap held at -TaskBudgetUsd', '3', "$(Get-RunBudgetUsd 3 $twelve)")
        ,@('cap floor of 3 dollars', '3', "$(Get-RunBudgetUsd 10 ([double[]](0.1, 0.1, 0.1, 0.1, 0.1, 0.1, 0.1, 0.1, 0.1, 0.1)))")
        ,@('fewer than 10 costs keeps -TaskBudgetUsd', '10', "$(Get-RunBudgetUsd 10 ([double[]](1.0, 1.0, 1.0)))")
        ,@('-TaskBudgetUsd 0 is no cap', '0', "$(Get-RunBudgetUsd 0 $twelve)")
        ,@('no log folder gives no costs', '0', "$(@(Get-RecentRunCosts (Join-Path ([IO.Path]::GetTempPath()) 'no-such-dark-factory-logs')).Count)"))
    $dir = Join-Path ([IO.Path]::GetTempPath()) "DarkFactoryBudget-$PID"
    New-Item -ItemType Directory -Force $dir | Out-Null
    Set-Content (Join-Path $dir 'BL-1-20261003-100000-L1.jsonl') '{"type":"assistant"}', '{"type":"result","total_cost_usd":1.25}'
    Set-Content (Join-Path $dir 'BL-2-20261003-100000-L2-resumed.jsonl') '{"total_cost_usd":2.5,"type":"result"}'
    Set-Content (Join-Path $dir 'BL-3-20261003-100000-L3-resolve.jsonl') '{"type":"result","total_cost_usd":9}'
    Set-Content (Join-Path $dir 'BL-4-20261003-100000-L4.jsonl') '{"type":"assistant"}'
    $cases += ,@('costs read from task and resumed runs only', '1.25,2.5', ((@(Get-RecentRunCosts $dir) | Sort-Object) -join ','))
    # A requeued task's next run gets the rest of one cap (AF-0095).
    Set-Content (Join-Path $dir 'BL-2-20261003-110000-L5.jsonl') '{"type":"factory","model":"sonnet"}', '{"type":"result","total_cost_usd":1}'
    Set-Content (Join-Path $dir 'BL-2-20261001-100000-L1.jsonl') '{"type":"result","total_cost_usd":7}'
    (Get-Item (Join-Path $dir 'BL-2-20261001-100000-L1.jsonl')).LastWriteTime = (Get-Date).AddDays(-3)
    Set-Content (Join-Path $dir 'BL-22-20261003-100000-L1.jsonl') '{"type":"result","total_cost_usd":8}'
    Set-Content (Join-Path $dir 'BL-2-20261003-100000-L3-resolve.jsonl') '{"type":"result","total_cost_usd":9}'
    $cases += ,@('a task spent its last day of task and resumed runs only', '3.5', "$(Get-TaskSpentUsd $dir 'BL-2')")
    $cases += ,@('a task with no runs spent nothing', '0', "$(Get-TaskSpentUsd $dir 'BL-9')")
    # A run that only found the task waiting on other tasks does not come off its cap (AF-0387, AF-0404 to AF-0406).
    Set-Content (Join-Path $dir 'BL-2-20261003-110000-L5.prerequisite-requeue.txt') 'waived'
    $cases += ,@('a waived prerequisite requeue run does not count toward the spend', '2.5', "$(Get-TaskSpentUsd $dir 'BL-2')")
    Set-Content (Join-Path $dir 'BL-2-20261003-100000-L2.prerequisite-requeue.txt') 'waived'
    $cases += ,@('the marker waives the resumed run of the same claim too', '0', "$(Get-TaskSpentUsd $dir 'BL-2')")
    Remove-Item (Join-Path $dir 'BL-2-*.prerequisite-requeue.txt')
    $cases += ,@('a requeue waiting on an open depends-on ID with no work is waived', 'True', "$([bool](Get-PrerequisiteRequeueWhy 'Backlog' $false @('BL-3410')))")
    $cases += ,@('the waiver names the IDs waited on', 'True', "$((Get-PrerequisiteRequeueWhy 'Backlog' $false @('BL-3284', 'BL-3288')) -match 'waiting on BL-3284, BL-3288 ')")
    $cases += ,@('a requeue that left work to stash is not waived', '', (Get-PrerequisiteRequeueWhy 'Backlog' $true @('BL-3410')))
    $cases += ,@('a requeue with every depends-on ID Done is not waived', '', (Get-PrerequisiteRequeueWhy 'Backlog' $false @()))
    $cases += ,@('a run that ended Done or Blocked is not waived', ',', "$(Get-PrerequisiteRequeueWhy 'Done' $false @('BL-1')),$(Get-PrerequisiteRequeueWhy 'Blocked' $false @('BL-1'))")
    $cases += ,@('earlier runs come off the cap', '3.7', "$(Get-RunBudgetUsd 10 $twelve 1.5)")
    $cases += ,@('a spent cap still leaves 2 dollars', '2', "$(Get-RunBudgetUsd 10 $twelve 4.73)")
    $cases += ,@('earlier runs come off -TaskBudgetUsd too', '8', "$(Get-RunBudgetUsd 10 ([double[]](1.0, 1.0, 1.0)) 2)")
    $cases += ,@('-TaskBudgetUsd 0 stays no cap after spending', '0', "$(Get-RunBudgetUsd 0 $twelve 2)")
    # A task whose runs of the last day left less than $2 of its cap is not run again (AF-0096).
    $cases += ,@('a cap with 2 dollars or more left is not spent', 'False', "$(Test-TaskCapSpent 3.21 1.21)")
    $cases += ,@('a cap with under 2 dollars left is spent', 'True', "$(Test-TaskCapSpent 3.21 1.22)")
    $cases += ,@('a task with no runs has not spent its cap', 'False', "$(Test-TaskCapSpent 0.5 0)")
    $cases += ,@('-TaskBudgetUsd 0 is never spent', 'False', "$(Test-TaskCapSpent 0 9)")
    $cases += ,@('BL-1609 stops after its second run', 'False,True,True', "$((Test-TaskCapSpent 3.21 0.41), (Test-TaskCapSpent 3.21 1.56), (Test-TaskCapSpent 3.21 3.59) -join ',')")
    # BL-2970's second claim got $1.02 of its cap and its third $0.46 more; now neither runs (AF-0326).
    $cases += ,@('BL-2970 is not run again after its first claim', 'True,1.02', "$(Test-TaskCapSpent 5.29 4.27),$(5.29 - 4.27)")
    # A task the daily rule blocked goes back to Backlog once its 24-hour window has passed;
    # one blocked because a run stopped at the cap stays Blocked (BL-2117).
    $refused = '2026-10-11: Doing -> Blocked. Stewart: dark factory its runs of the last day already cost 1.16 of its 2.10 US dollar cost cap, so it was not run again; split the task; see x'
    $stopped = '2026-10-11: Doing -> Blocked. Stewart: dark factory stopped at its 3.2 US dollar cost cap (4 times the median run, at least $3, at most -TaskBudgetUsd, less its runs of the last day), so split the task; see x'
    # The cap frees when the oldest runs leave the day (BL-3090).
    $t0 = [datetime]'2026-10-22 01:00'
    $twoRuns = @([pscustomobject]@{ Time = $t0.AddHours(2); Usd = 1.5 }, [pscustomobject]@{ Time = $t0; Usd = 1.5 })
    $cases += ,@('two runs in the day free the cap when the oldest leaves it', '2026-10-23 01:00', ((Get-CapResetTime $twoRuns 3.5).ToString('yyyy-MM-dd HH:mm', [System.Globalization.CultureInfo]::InvariantCulture)))
    $cases += ,@('three runs free the cap only when enough leave', '2026-10-23 03:00', ((Get-CapResetTime ($twoRuns + [pscustomobject]@{ Time = $t0.AddHours(4); Usd = 2.0 }) 4.5).ToString('yyyy-MM-dd HH:mm', [System.Globalization.CultureInfo]::InvariantCulture)))
    $cases += ,@('a task not capped has no cap reset time', 'True', "$($null -eq (Get-CapResetTime @([pscustomobject]@{ Time = $t0; Usd = 1.0 }) 3.5))")
    $cases += ,@('a task with no runs has no cap reset time', 'True', "$($null -eq (Get-CapResetTime @() 3.5))")
    $cases += ,@('a refused task is requeued once its last day leaves 2 dollars', 'True,True', "$((Test-CapBlockRequeueDue $refused 3.10 0), (Test-CapBlockRequeueDue $refused 3.10 1.10) -join ',')")
    $cases += ,@('a refused task stays Blocked while its cap is still spent', 'False', "$(Test-CapBlockRequeueDue $refused 2.10 1.16)")
    $cases += ,@('a run that stopped at the cap is never requeued', 'False,False', "$((Test-CapBlockRequeueDue $stopped 3.2 0), (Test-CapBlockRequeueDue $stopped 0 0) -join ',')")
    $cases += ,@('a task blocked for another reason is not requeued by the cap', 'False', "$(Test-CapBlockRequeueDue '2026-10-11: Doing -> Blocked. Stewart: which package?' 3 0)")
    # A run with no result event, or a total_cost_usd of 0, is priced from its logged usage (BL-3001).
    $usageA = '{"type":"assistant","message":{"id":"msg_a","model":"claude-opus-5-5","usage":{"input_tokens":0,"cache_creation_input_tokens":1000000,"cache_read_input_tokens":0,"output_tokens":0}}}'
    $usageB1 = '{"type":"assistant","message":{"id":"msg_b","model":"claude-opus-5-5","usage":{"input_tokens":0,"cache_creation_input_tokens":0,"cache_read_input_tokens":0,"output_tokens":50000}}}'
    $usageB2 = '{"type":"assistant","message":{"id":"msg_b","model":"claude-opus-5-5","usage":{"input_tokens":0,"cache_creation_input_tokens":0,"cache_read_input_tokens":0,"output_tokens":100000}}}'
    $usageSonnet = '{"type":"assistant","message":{"id":"msg_s","model":"claude-sonnet-5-5","usage":{"input_tokens":1000000,"cache_creation_input_tokens":0,"cache_read_input_tokens":0,"output_tokens":0}}}'
    $usd = [System.Globalization.CultureInfo]::InvariantCulture
    Set-Content (Join-Path $dir 'BL-6-20261003-100000-L1.jsonl') $usageA, '{"type":"result","total_cost_usd":0.5}'
    $cases += ,@('a finished run uses its total_cost_usd, not its usage', '0.50,False', ((Get-RunLogCost (Join-Path $dir 'BL-6-20261003-100000-L1.jsonl')) | ForEach-Object { "$($_.Usd.ToString('0.00', $usd)),$($_.Estimated)" }))
    Set-Content (Join-Path $dir 'BL-7-20261003-100000-L1.jsonl') $usageA
    $cases += ,@('a killed run is estimated from its usage', '6.16,True', ((Get-RunLogCost (Join-Path $dir 'BL-7-20261003-100000-L1.jsonl')) | ForEach-Object { "$($_.Usd.ToString('0.00', $usd)),$($_.Estimated)" }))
    Set-Content (Join-Path $dir 'BL-8-20261003-100000-L1.jsonl') $usageA, '{"type":"result","subtype":"factory_no_result","total_cost_usd":0}'
    $cases += ,@('a run closed with total_cost_usd 0 is estimated from its usage', '6.16,True', ((Get-RunLogCost (Join-Path $dir 'BL-8-20261003-100000-L1.jsonl')) | ForEach-Object { "$($_.Usd.ToString('0.00', $usd)),$($_.Estimated)" }))
    Set-Content (Join-Path $dir 'BL-9-20261003-100000-L1.jsonl') $usageA, $usageA, $usageB1, $usageB2, $usageB2
    $cases += ,@('a message id repeated counts once at its largest output', '7.70', ((Get-RunLogCost (Join-Path $dir 'BL-9-20261003-100000-L1.jsonl')).Usd.ToString('0.00', $usd)))
    Set-Content (Join-Path $dir 'BL-10-20261003-100000-L1.jsonl') $usageSonnet
    $cases += ,@('a Sonnet run is priced at the Sonnet rate', '1.65', ((Get-RunLogCost (Join-Path $dir 'BL-10-20261003-100000-L1.jsonl')).Usd.ToString('0.00', $usd)))
    Set-Content (Join-Path $dir 'BL-11-20261003-100000-L1.jsonl') '{"type":"system"}', '{"type":"assistant"}'
    $cases += ,@('a run with neither a cost nor usage has no cost', 'True', "$($null -eq (Get-RunLogCost (Join-Path $dir 'BL-11-20261003-100000-L1.jsonl')))")
    Set-Content (Join-Path $dir 'BL-12-20261003-100000-L1.jsonl') '{"type":"result","total_cost_usd":0}'
    $cases += ,@('a result of 0 with no usage costs 0', '0,False', ((Get-RunLogCost (Join-Path $dir 'BL-12-20261003-100000-L1.jsonl')) | ForEach-Object { "$($_.Usd),$($_.Estimated)" }))
    $cases += ,@('a killed run counts toward its task spend', '6.16', ((Get-TaskSpentUsd $dir 'BL-7').ToString('0.00', $usd)))
    $cases += ,@('a killed run counts toward the recent costs', '6.16', (((@(Get-RecentRunCosts $dir -Count 100) | Sort-Object -Descending) | Where-Object { $_ -gt 6.1 -and $_ -lt 6.2 } | Select-Object -First 1).ToString('0.00', $usd)))
    # A log a running claude still holds open for writing is read (BL-3061).
    $lockedPath = Join-Path $dir 'BL-13-20261003-100000-L1.jsonl'
    $writer = [System.IO.FileStream]::new($lockedPath, [System.IO.FileMode]::Create, [System.IO.FileAccess]::Write, [System.IO.FileShare]::Read)
    $bytes = [Text.Encoding]::UTF8.GetBytes($usageA + "`n"); $writer.Write($bytes, 0, $bytes.Length); $writer.Flush()
    $cases += ,@('a log held open for writing costs what it costs once closed', '6.16', ((Get-LogEstimatedCostUsd $lockedPath).ToString('0.00', $usd)))
    $writer.Dispose()
    $writer = [System.IO.FileStream]::new($lockedPath, [System.IO.FileMode]::Open, [System.IO.FileAccess]::ReadWrite, [System.IO.FileShare]::None)
    $cases += ,@('a log locked exclusively costs 0 and never throws', '0', "$(Get-LogEstimatedCostUsd $lockedPath)")
    $writer.Dispose()
    $cases += ,@('a log released after being locked costs its usage', '6.16', ((Get-LogEstimatedCostUsd $lockedPath).ToString('0.00', $usd)))
    Remove-Item -Recurse -Force $dir
    $failed = 0
    foreach ($case in $cases) {
        if ($case[1] -ceq $case[2]) { Write-Host "PASS $($case[0]): $($case[2])" -ForegroundColor Green }
        else { Write-Host "FAIL $($case[0]): expected $($case[1]), got $($case[2])" -ForegroundColor Red; $failed++ }
    }
    exit $(if ($failed) { 1 } else { 0 })
}

# This file ran to its end; RunDarkFactory.ps1 goes on to the next one.
$AreaFileFinished = $true
