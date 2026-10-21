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

function Get-RunBudgetUsd {
    # The cost cap for the next headless run (AF-0033, ADR-0407, ADR-0478): 4 times the
    # median of the recent task runs' costs, so a task that needs a few more turns than most
    # still finishes, never above -TaskBudgetUsd and never below $3. Fewer than 10
    # recent costs leaves -TaskBudgetUsd as it is; -TaskBudgetUsd 0 means no cap.
    # -SpentUsd is what the task's earlier runs of the last day already cost (AF-0095): it
    # comes off the cap, down to $1, so a task requeued and run again stays near one cap
    # in all rather than two.
    param([double]$Ceiling, [double[]]$RecentCosts, [double]$SpentUsd = 0)
    if ($Ceiling -le 0) { return 0.0 }
    $costs = @($RecentCosts | Where-Object { $_ -gt 0 })
    $cap = if ($costs.Count -lt 10) { $Ceiling } else { [math]::Min($Ceiling, [math]::Max(3.0, [math]::Round(4.0 * (Get-MedianCost $costs), 2))) }
    if ($SpentUsd -le 0) { return $cap }
    return [math]::Max(1.0, [math]::Round($cap - $SpentUsd, 2))
}

function Get-TaskSpentUsd {
    # What a task's earlier task and resumed runs in a log folder cost, in US dollars,
    # counting only logs written in the last -Hours (AF-0095): a requeued task's next run
    # gets the rest of one cap, while a task parked days ago starts afresh.
    param([string]$Dir, [string]$Id, [double]$Hours = 24)
    if (-not (Test-Path $Dir)) { return 0.0 }
    $since = (Get-Date).AddHours(-$Hours)
    $pattern = '^' + [regex]::Escape($Id) + '-\d{8}-\d{6}(-L\d+)?(-resumed)?\.jsonl$'
    $spent = 0.0
    foreach ($log in @(Get-ChildItem $Dir -Filter "$Id-*.jsonl" -ErrorAction SilentlyContinue | Where-Object { $_.Name -match $pattern -and $_.LastWriteTime -ge $since })) {
        $line = Get-Content $log.FullName -Tail 5 -ErrorAction SilentlyContinue | Where-Object { $_ -match '"total_cost_usd"' } | Select-Object -Last 1
        if (-not $line) { continue }
        $evt = try { $line | ConvertFrom-Json } catch { $null }
        if ($evt -and $evt.type -eq 'result') { $spent += [double]$evt.total_cost_usd }
    }
    return $spent
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
    # Whether a task's runs of the last day already cost all but $1 of its cap (AF-0096).
    # Get-RunBudgetUsd never gives a run less than $1, so without this a task requeued
    # again and again got $1 and a turn more each time (BL-1609: five runs, $5.17).
    param([double]$Cap, [double]$SpentUsd)
    return ($Cap -gt 0 -and $SpentUsd -gt 0 -and ($Cap - $SpentUsd) -lt 1.0)
}

function Test-CapBlockRequeueDue {
    # Whether a Blocked task may go back to Backlog because the cost cap's daily rule
    # (AF-0096) no longer holds for it (BL-2117): its Blocked reason is the daily-rule
    # refusal ("so it was not run again") and its runs of the last day now leave $1 or more
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
    # total_cost_usd from the result events of the newest task runs' logs in a log folder
    # (BL-1377-20261003-145757-L1.jsonl and its -resumed run; resolver runs are not tasks).
    param([string]$Dir, [int]$Count = 40)
    if (-not (Test-Path $Dir)) { return @() }
    $logs = Get-ChildItem $Dir -Filter 'BL-*.jsonl' -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -match '^BL-\d+-\d{8}-\d{6}(-L\d+)?(-resumed)?\.jsonl$' } |
        Sort-Object LastWriteTime -Descending | Select-Object -First $Count
    foreach ($log in $logs) {
        $line = Get-Content $log.FullName -Tail 5 -ErrorAction SilentlyContinue | Where-Object { $_ -match '"total_cost_usd"' } | Select-Object -Last 1
        if (-not $line) { continue }
        $evt = try { $line | ConvertFrom-Json } catch { $null }
        if ($evt -and $evt.type -eq 'result') { [double]$evt.total_cost_usd }
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
    $cases += ,@('earlier runs come off the cap', '3.7', "$(Get-RunBudgetUsd 10 $twelve 1.5)")
    $cases += ,@('a spent cap still leaves 1 dollar', '1', "$(Get-RunBudgetUsd 10 $twelve 4.73)")
    $cases += ,@('earlier runs come off -TaskBudgetUsd too', '8', "$(Get-RunBudgetUsd 10 ([double[]](1.0, 1.0, 1.0)) 2)")
    $cases += ,@('-TaskBudgetUsd 0 stays no cap after spending', '0', "$(Get-RunBudgetUsd 0 $twelve 2)")
    # A task whose runs of the last day left less than $1 of its cap is not run again (AF-0096).
    $cases += ,@('a cap with 1 dollar or more left is not spent', 'False', "$(Test-TaskCapSpent 3.21 2.21)")
    $cases += ,@('a cap with under 1 dollar left is spent', 'True', "$(Test-TaskCapSpent 3.21 2.22)")
    $cases += ,@('a task with no runs has not spent its cap', 'False', "$(Test-TaskCapSpent 0.5 0)")
    $cases += ,@('-TaskBudgetUsd 0 is never spent', 'False', "$(Test-TaskCapSpent 0 9)")
    $cases += ,@('BL-1609 stops after its third run', 'False,False,True', "$((Test-TaskCapSpent 3.21 0.41), (Test-TaskCapSpent 3.21 1.56), (Test-TaskCapSpent 3.21 3.59) -join ',')")
    # A task the daily rule blocked goes back to Backlog once its 24-hour window has passed;
    # one blocked because a run stopped at the cap stays Blocked (BL-2117).
    $refused = '2026-10-11: Doing -> Blocked. Stewart: dark factory its runs of the last day already cost 1.16 of its 2.10 US dollar cost cap, so it was not run again; split the task; see x'
    $stopped = '2026-10-11: Doing -> Blocked. Stewart: dark factory stopped at its 3.2 US dollar cost cap (4 times the median run, at least $3, at most -TaskBudgetUsd, less its runs of the last day), so split the task; see x'
    $cases += ,@('a refused task is requeued once its last day leaves 1 dollar', 'True,True', "$((Test-CapBlockRequeueDue $refused 2.10 0), (Test-CapBlockRequeueDue $refused 2.10 1.10) -join ',')")
    $cases += ,@('a refused task stays Blocked while its cap is still spent', 'False', "$(Test-CapBlockRequeueDue $refused 2.10 1.16)")
    $cases += ,@('a run that stopped at the cap is never requeued', 'False,False', "$((Test-CapBlockRequeueDue $stopped 3.2 0), (Test-CapBlockRequeueDue $stopped 0 0) -join ',')")
    $cases += ,@('a task blocked for another reason is not requeued by the cap', 'False', "$(Test-CapBlockRequeueDue '2026-10-11: Doing -> Blocked. Stewart: which package?' 3 0)")
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
