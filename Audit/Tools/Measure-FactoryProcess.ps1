<#
.SYNOPSIS
    Measures how the dark factory worked: time per task, redone work, time with CI red, idle
    lane time and what the idling waited on, and token cost.

.DESCRIPTION
    The process auditor's measurement (BL-1014, ADR-0267). It only reads: the factory's logs,
    and the CI runs from gh (or a saved copy). Nothing is written except -OutFile.

    Sources (RunDarkFactory.ps1's header is the authority on their format):
      <repo>.logs\DarkFactory-<stamp>-L<n>.log   each lane's trace, one line per step:
                  "HH:mm:ss L<n> <ID or -> <verb> <text>". The date is the file stamp's, and a
                  time earlier than the line before it means midnight passed.
                  Verbs used: claim, DONE, REQUEUE, BLOCKED, PARKED (a task's claim and how
                  it ended) and wait (the lane idle, with the reason it logged).
      <repo>.logs\<ID>-<stamp>-L<n>[-resolve|-resumed].jsonl   one task run's stream-json;
                  its "result" event carries total_cost_usd and usage token counts.
      CI:         gh run list --workflow CI --branch work/dark-factory --json
                  databaseId,status,conclusion,headSha,createdAt,updatedAt --limit 500,
                  or -CiRunsJson, a saved copy of that output.

    Metrics (the names are the process auditor's report metrics, Audit/Instructions/
    Report-Format.md):
      tasksDone                 tasks whose trace reached DONE since -Since
      medianTaskMinutes         median minutes from a done task's last claim to its DONE
      p90TaskMinutes            the same at the 90th percentile (nearest rank)
      tasksClaimedMoreThanOnce  tasks claimed more than once since -Since: redone work
      requeues                  REQUEUE lines: a task sent back from Doing to Backlog
      resumedRuns               task runs whose log is a -resumed.jsonl
      ciRedMinutes              minutes from the end of the first failed CI run after a
                                success to the end of the next successful run, summed;
                                cancelled and unfinished runs are ignored, and a streak still
                                red at the last finished run ends there; ciRunsFrom says when
                                the oldest CI run read was created, since 500 runs (most of
                                them cancelled) may not reach back to -Since
      laneIdleMinutes           minutes lanes spent in wait: each wait line lasts until the
                                lane's next line, at most 60 minutes
      waitOverlapMinutes        the part whose reason says every ready task overlaps work in
                                Doing or waits behind it
      waitNothingReadyMinutes   the part whose reason is "No task is ready."
      waitOtherMinutes          the rest: a lost claim race, or a reason the lane did not log
                                (a lane that logged task-board.ps1's duplicate-ID WARNING line
                                instead of the reason lands here)
      tokensInput               input_tokens + cache_creation_input_tokens +
                                cache_read_input_tokens over every run's result event
      tokensOutput              output_tokens over every run's result event
      costUsd                   total_cost_usd over every run's result event
      costUsdPerTaskDone        costUsd / tasksDone
      laneMinutes               every lane log's time from its first line to its last, summed:
                                the lane time the idle rules are a share of
    and these lists (evidence, not metrics; BL-1366):
      ciRedSpells               each red spell behind ciRedMinutes: start, end (UTC), minutes,
                                runId (the CI run that turned it red) and stillRed
      unfinishedRuns            run-log file names (<ID>-<stamp>-L<n>.jsonl) with no result
                                event and no FACTORY: DONE or FACTORY: BLOCKED line
    and tasks: one row per task claimed since -Since: id, claims, minutes (last claim to its
    end, or to the last line seen when still open), costUsd (its runs), outcome (done,
    requeued, blocked, parked or open).

.PARAMETER Since
    The first day to count, e.g. 2026-09-29. Logs, runs and CI runs before it are skipped.

.PARAMETER LogRoot
    The factory's log folder. Default: <repo>.logs beside the repository.

.PARAMETER CiRunsJson
    A file holding gh run list's JSON. When given, gh is not called.

.PARAMETER OutFile
    Write the JSON here as well as to standard output.

.PARAMETER SelfTest
    Run against Audit/Tools/Fixtures/process and check every metric's known value.

.EXAMPLE
    powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-09-29 -OutFile $env:TEMP\process.json
#>
param(
    [datetime]$Since = [datetime]'2000-01-01',
    [string]$LogRoot,
    [string]$CiRunsJson,
    [string]$OutFile,
    [switch]$SelfTest
)

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$MaxWaitMinutes = 60

function Get-StampDate([string]$Name) {
    if ($Name -match '(\d{8})-(\d{6})') { return [datetime]::ParseExact("$($Matches[1])$($Matches[2])", 'yyyyMMddHHmmss', [Globalization.CultureInfo]::InvariantCulture) }
    return $null
}

function Get-LaneEvents([string]$Root, [datetime]$From) {
    # Every lane line as an event with an absolute time, across all lane logs, in time order.
    $events = New-Object System.Collections.Generic.List[object]
    foreach ($file in @(Get-ChildItem -LiteralPath $Root -File -Filter 'DarkFactory-*-L*.log')) {
        $start = Get-StampDate $file.Name
        if (-not $start -or $start.Date -lt $From.Date.AddDays(-1)) { continue }
        $lane = if ($file.Name -match '-L(\d+)\.log$') { [int]$Matches[1] } else { 0 }
        $day = $start.Date
        $previous = [timespan]::Zero
        foreach ($line in [IO.File]::ReadLines($file.FullName)) {
            if ($line -notmatch '^(\d\d):(\d\d):(\d\d) L\d+ (\S+)\s+(\S+)\s*(.*)$') { continue }
            $time = New-Object TimeSpan ([int]$Matches[1]), ([int]$Matches[2]), ([int]$Matches[3])
            if ($time -lt $previous) { $day = $day.AddDays(1) }
            $previous = $time
            $events.Add([pscustomobject]@{ At = $day + $time; Lane = "$($file.Name -replace '\.log$', '')"; LaneNumber = $lane; Id = $Matches[4]; Verb = $Matches[5]; Text = $Matches[6] })
        }
    }
    return @($events | Where-Object { $_.At -ge $From } | Sort-Object At, Lane)
}

function Get-RunResults([string]$Root, [datetime]$From) {
    # One object per task run log with a result event: Id, Resumed, CostUsd, TokensIn, TokensOut.
    $runs = @()
    foreach ($file in @(Get-ChildItem -LiteralPath $Root -File -Filter 'BL-*.jsonl')) {
        $stamp = Get-StampDate $file.Name
        if (-not $stamp -or $stamp.Date -lt $From.Date) { continue }
        $id = if ($file.Name -match '^(BL-\d+)-') { $Matches[1] } else { continue }
        $resultLine = $null
        $factoryEnd = $false
        foreach ($line in [IO.File]::ReadLines($file.FullName)) {
            if ($line.Contains('"type":"result"')) { $resultLine = $line }
            if ($line.Contains('FACTORY: DONE') -or $line.Contains('FACTORY: BLOCKED')) { $factoryEnd = $true }
        }
        $cost = 0.0; $in = [long]0; $out = [long]0
        if ($resultLine) {
            $r = $resultLine | ConvertFrom-Json
            if ($r.PSObject.Properties['total_cost_usd']) { $cost = [double]$r.total_cost_usd }
            if ($r.PSObject.Properties['usage'] -and $r.usage) {
                foreach ($name in 'input_tokens', 'cache_creation_input_tokens', 'cache_read_input_tokens') {
                    if ($r.usage.PSObject.Properties[$name]) { $in += [long]$r.usage.$name }
                }
                if ($r.usage.PSObject.Properties['output_tokens']) { $out = [long]$r.usage.output_tokens }
            }
        }
        $runs += [pscustomobject]@{ Id = $id; File = $file.Name; Resumed = $file.Name -like '*-resumed.jsonl'; CostUsd = $cost; TokensIn = $in; TokensOut = $out; HasResult = [bool]$resultLine; Finished = ([bool]$resultLine) -or $factoryEnd }
    }
    return $runs
}

function Get-CiRedSpells([object[]]$Runs, [datetime]$From) {
    # Each red spell: from the end of the first failed run after a success to the end of the next
    # successful run (or of the last finished run, when still red), with the run that turned it
    # red, so the process auditor can name the spell, not only the total (BL-1366).
    $finished = @($Runs | Where-Object { $_.status -eq 'completed' -and $_.conclusion -in 'success', 'failure' } |
        ForEach-Object { [pscustomobject]@{ Id = $_.databaseId; Conclusion = $_.conclusion; Created = ([datetimeoffset]$_.createdAt).UtcDateTime; Ended = ([datetimeoffset]$_.updatedAt).UtcDateTime } } |
        Where-Object { $_.Created -ge $From.ToUniversalTime() -or $_.Created.Date -ge $From.Date } | Sort-Object Created)
    $spells = @()
    $redSince = $null; $redRun = $null
    foreach ($run in $finished) {
        if ($run.Conclusion -eq 'failure' -and -not $redSince) { $redSince = $run.Ended; $redRun = $run.Id }
        elseif ($run.Conclusion -eq 'success' -and $redSince) {
            $spells += [ordered]@{ start = $redSince.ToString('yyyy-MM-ddTHH:mm:ssZ'); end = $run.Ended.ToString('yyyy-MM-ddTHH:mm:ssZ'); minutes = [math]::Round(($run.Ended - $redSince).TotalMinutes, 2); runId = $redRun; stillRed = $false }
            $redSince = $null
        }
    }
    if ($redSince -and $finished.Count) {
        $end = $finished[-1].Ended
        $spells += [ordered]@{ start = $redSince.ToString('yyyy-MM-ddTHH:mm:ssZ'); end = $end.ToString('yyyy-MM-ddTHH:mm:ssZ'); minutes = [math]::Round([math]::Max(0, ($end - $redSince).TotalMinutes), 2); runId = $redRun; stillRed = $true }
    }
    return $spells
}

function Get-CiRedMinutes([object[]]$Runs, [datetime]$From) {
    $minutes = @(Get-CiRedSpells $Runs $From | ForEach-Object { [double]$_.minutes })
    return [math]::Round([double]($minutes | Measure-Object -Sum).Sum, 2)
}

function Get-Median([double[]]$Values) {
    if (-not $Values.Length) { return $null }
    $s = [double[]]@($Values | Sort-Object); $m = [int][math]::Floor($s.Length / 2)
    if ($s.Length % 2) { return $s[$m] }
    return ($s[$m - 1] + $s[$m]) / 2
}

function Get-Percentile([double[]]$Values, [double]$Fraction) {
    if (-not $Values.Length) { return $null }
    $s = [double[]]@($Values | Sort-Object)
    return $s[[math]::Max(0, [math]::Ceiling($Fraction * $s.Length) - 1)]
}

function Measure-Process([string]$Root, [datetime]$From, [object[]]$CiRuns) {
    $events = @(Get-LaneEvents $Root $From)
    $runs = @(Get-RunResults $Root $From)

    # Tasks: claims, and how the last claim ended.
    $tasks = [ordered]@{}
    $terminal = @{ DONE = 'done'; REQUEUE = 'requeued'; BLOCKED = 'blocked'; PARKED = 'parked' }
    foreach ($e in $events) {
        if ($e.Id -notmatch '^BL-\d+$') { continue }
        if ($e.Verb -eq 'claim') {
            if (-not $tasks.Contains($e.Id)) { $tasks[$e.Id] = [pscustomobject]@{ Id = $e.Id; Claims = 0; LastClaim = $null; Ended = $null; Outcome = 'open' } }
            $t = $tasks[$e.Id]; $t.Claims++; $t.LastClaim = $e.At; $t.Ended = $null; $t.Outcome = 'open'
        }
        elseif ($terminal.ContainsKey($e.Verb) -and $tasks.Contains($e.Id)) {
            $t = $tasks[$e.Id]; $t.Ended = $e.At; $t.Outcome = $terminal[$e.Verb]
        }
    }
    $lastSeen = if ($events.Count) { $events[-1].At } else { $From }
    $rows = @(foreach ($t in $tasks.Values) {
        $end = if ($t.Ended) { $t.Ended } else { $lastSeen }
        $cost = [double](@($runs | Where-Object { $_.Id -eq $t.Id } | Measure-Object CostUsd -Sum).Sum)
        [ordered]@{ id = $t.Id; claims = $t.Claims; minutes = [math]::Round(($end - $t.LastClaim).TotalMinutes, 2); costUsd = [math]::Round($cost, 4); outcome = $t.Outcome }
    })
    $doneMinutes = [double[]]@($rows | Where-Object { $_.outcome -eq 'done' } | ForEach-Object { $_.minutes })

    # Idle: each wait line lasts until the same lane's next line, at most $MaxWaitMinutes.
    $overlap = 0.0; $nothing = 0.0; $other = 0.0
    foreach ($lane in @($events | Group-Object Lane)) {
        $lines = @($lane.Group)
        for ($i = 0; $i -lt $lines.Count - 1; $i++) {
            if ($lines[$i].Verb -ne 'wait') { continue }
            $minutes = [math]::Min($MaxWaitMinutes, ($lines[$i + 1].At - $lines[$i].At).TotalMinutes)
            $why = $lines[$i].Text
            if ($why -match 'overlaps one in Doing|waits behind|overlaps work in progress') { $overlap += $minutes }
            elseif ($why -match '^No task is ready') { $nothing += $minutes }
            else { $other += $minutes }
        }
    }

    # Lane time: each lane log from its first line to its last, so idle minutes have a denominator.
    $laneMinutes = 0.0
    foreach ($lane in @($events | Group-Object Lane)) {
        $lines = @($lane.Group)
        if ($lines.Count -gt 1) { $laneMinutes += ($lines[-1].At - $lines[0].At).TotalMinutes }
    }

    $tokensIn = [long](@($runs | Measure-Object TokensIn -Sum).Sum)
    $tokensOut = [long](@($runs | Measure-Object TokensOut -Sum).Sum)
    $cost = [double](@($runs | Measure-Object CostUsd -Sum).Sum)
    $done = @($rows | Where-Object { $_.outcome -eq 'done' }).Count
    return [ordered]@{
        since = $From.ToString('yyyy-MM-dd'); logRoot = $Root
        tasksDone = $done
        medianTaskMinutes = Get-Median $doneMinutes
        p90TaskMinutes = Get-Percentile $doneMinutes 0.9
        tasksClaimedMoreThanOnce = @($rows | Where-Object { $_.claims -gt 1 }).Count
        requeues = @($events | Where-Object { $_.Verb -eq 'REQUEUE' }).Count
        resumedRuns = @($runs | Where-Object { $_.Resumed }).Count
        ciRedMinutes = Get-CiRedMinutes $CiRuns $From
        ciRedSpells = @(Get-CiRedSpells $CiRuns $From)
        laneMinutes = [math]::Round($laneMinutes, 2)
        laneIdleMinutes = [math]::Round($overlap + $nothing + $other, 2)
        waitOverlapMinutes = [math]::Round($overlap, 2)
        waitNothingReadyMinutes = [math]::Round($nothing, 2)
        waitOtherMinutes = [math]::Round($other, 2)
        tokensInput = $tokensIn
        tokensOutput = $tokensOut
        costUsd = [math]::Round($cost, 4)
        costUsdPerTaskDone = $(if ($done) { [math]::Round($cost / $done, 4) } else { $null })
        unfinishedRuns = @($runs | Where-Object { -not $_.Finished } | ForEach-Object { $_.File } | Sort-Object)
        tasks = $rows
    }
}

if ($SelfTest) {
    $fixture = Join-Path $PSScriptRoot 'Fixtures\process'
    $ci = Get-Content -LiteralPath (Join-Path $fixture 'ci-runs.json') -Raw | ConvertFrom-Json
    $m = Measure-Process (Join-Path $fixture 'lane-output') ([datetime]'2026-01-01') @($ci)
    $expected = [ordered]@{
        tasksDone = 4; medianTaskMinutes = 30; p90TaskMinutes = 45; tasksClaimedMoreThanOnce = 1; requeues = 1; resumedRuns = 1
        ciRedMinutes = 120; laneIdleMinutes = 30; waitOverlapMinutes = 20; waitNothingReadyMinutes = 10; waitOtherMinutes = 0
        tokensInput = 5295; tokensOutput = 240; costUsd = 4.5; costUsdPerTaskDone = 1.125
    }
    $failed = 0
    foreach ($name in $expected.Keys) {
        $ok = "$($m[$name])" -eq "$($expected[$name])"
        if (-not $ok) { $failed++ }
        Write-Host "$(if ($ok) { 'PASS' } else { 'FAIL' }) ${name}: $($m[$name]) (expected $($expected[$name]))"
    }
    $bl002 = @($m.tasks | Where-Object { $_.id -eq 'BL-002' })[0]
    $ok = $bl002.claims -eq 2 -and "$($bl002.minutes)" -eq '20' -and "$($bl002.costUsd)" -eq '0.75' -and $bl002.outcome -eq 'done'
    if (-not $ok) { $failed++ }
    Write-Host "$(if ($ok) { 'PASS' } else { 'FAIL' }) task row BL-002: claims $($bl002.claims), minutes $($bl002.minutes), costUsd $($bl002.costUsd), $($bl002.outcome)"
    # BL-1366: the run that never finished, each red spell named by the run that turned it red, and
    # lane time as the denominator for idle time.
    $ok = (@($m.unfinishedRuns) -join ',') -eq 'BL-004-20260101-100000-L2.jsonl'
    if (-not $ok) { $failed++ }
    Write-Host "$(if ($ok) { 'PASS' } else { 'FAIL' }) unfinishedRuns: $(@($m.unfinishedRuns) -join ',')"
    $spells = @($m.ciRedSpells)
    $ok = $spells.Count -eq 2 -and $spells[0].runId -eq 2 -and "$($spells[0].minutes)" -eq '60' -and $spells[0].start -eq '2026-01-01T10:10:00Z' -and $spells[1].runId -eq 6 -and $spells[1].end -eq '2026-01-01T13:05:00Z' -and -not $spells[1].stillRed
    if (-not $ok) { $failed++ }
    Write-Host "$(if ($ok) { 'PASS' } else { 'FAIL' }) ciRedSpells: $(($spells | ForEach-Object { "$($_.runId):$($_.minutes)" }) -join ', ')"
    $ok = "$($m.laneMinutes)" -eq '941.42'
    if (-not $ok) { $failed++ }
    Write-Host "$(if ($ok) { 'PASS' } else { 'FAIL' }) laneMinutes: $($m.laneMinutes) (expected 941.42)"
    $bl004 = @($m.tasks | Where-Object { $_.id -eq 'BL-004' })[0]
    $ok = $bl004.outcome -eq 'open'
    if (-not $ok) { $failed++ }
    Write-Host "$(if ($ok) { 'PASS' } else { 'FAIL' }) task row BL-004 still open: $($bl004.outcome)"
    exit $(if ($failed) { 1 } else { 0 })
}

if (-not $LogRoot) { $LogRoot = "$repo.logs" }
if (-not (Test-Path -LiteralPath $LogRoot)) { throw "No factory logs at $LogRoot; pass -LogRoot." }
# Parse into a variable first, then wrap: Windows PowerShell 5.1's ConvertFrom-Json emits a JSON
# array as one object, so @(... | ConvertFrom-Json) would be a one-item list holding the array.
if ($CiRunsJson) { $parsed = Get-Content -LiteralPath $CiRunsJson -Raw | ConvertFrom-Json }
else {
    $ErrorActionPreference = 'Continue'
    $raw = & gh run list --workflow CI --branch work/dark-factory --json databaseId,status,conclusion,headSha,createdAt,updatedAt --limit 500 2>$null
    $ErrorActionPreference = 'Stop'
    if ($LASTEXITCODE -ne 0) { throw 'gh run list failed; pass -CiRunsJson with a saved copy.' }
    $parsed = $raw | Out-String | ConvertFrom-Json
}
$ciRuns = @($parsed)
$measured = Measure-Process (Resolve-Path $LogRoot).Path $Since $ciRuns
# The CI runs read may not reach back to -Since: say where they start.
$oldest = @($ciRuns | ForEach-Object { ([datetimeoffset]$_.createdAt).UtcDateTime } | Sort-Object)[0]
$measured.ciRunsFrom = if ($oldest) { $oldest.ToString('yyyy-MM-ddTHH:mm:ssZ') } else { $null }
$json = [pscustomobject]$measured | ConvertTo-Json -Depth 5
if ($OutFile) { [IO.File]::WriteAllText($OutFile, $json) }
Write-Output $json
