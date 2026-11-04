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
      CI jobs:    each failed run's jobs (gh run view <id> --json jobs) with each non-success
                  job's annotations (gh api repos/StewartScottRogers/Curl/check-runs/<job id>/
                  annotations), or -CiJobsJson (default: a ci-jobs.json beside -CiRunsJson, when
                  there is one), a saved copy: a JSON array of {runId, jobs}, each job
                  {databaseId, name, conclusion, steps [{name, status, conclusion}],
                  annotations [{message}]}. A failed run whose jobs were not read counts as red.

    Metrics (the names are the process auditor's report metrics, Audit/Instructions/
    Report-Format.md):
      tasksDone                 tasks whose trace reached DONE since -Since
      medianTaskMinutes         median minutes from a done task's last claim to its DONE
      p90TaskMinutes            the same at the 90th percentile (nearest rank)
      tasksClaimedMoreThanOnce  tasks claimed more than once since -Since: redone work. A claim
                                is one `claim` trace line in a DarkFactory-*.log file. Not
                                counted: `race` lines, the older "claim   lost the race;
                                picking again" lines, and any mention of "claim BL-nnn" in a
                                run transcript (.jsonl files are read only for cost and tokens)
      requeues                  REQUEUE lines: a task sent back from Doing to Backlog
      resumedRuns               task runs whose log is a -resumed.jsonl
      ciRedMinutes              minutes from the end of the first failed CI run after a
                                success to the end of the next successful run, summed;
                                cancelled and unfinished runs are ignored, and a streak still
                                red at the last finished run ends there; ciRunsFrom says when
                                the oldest CI run read was created, since 500 runs (most of
                                them cancelled) may not reach back to -Since. A failed run counts
                                as red only when one of its jobs has a failing test or a build
                                error (ADR-1235): a failed run whose non-success jobs are all
                                infrastructure - cancelled because no runner was acquired ("The
                                job was not acquired by Runner"), cancelled as superseded by a
                                newer commit ("higher priority waiting request"), or run into
                                timeout-minutes ("exceeded the maximum execution time") with no
                                test or build step (a step whose name holds test, build or
                                coverage) left unsuccessful - neither starts nor ends a spell; a
                                test or build step that hangs into the timeout still counts, and
                                so does any other failed job, such as a failed test or build step
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
      ciInfrastructureRuns      each failed run left out under ADR-1235: runId, ended (UTC) and
                                jobs, one "<job name>: <reason>" per non-success job (runner not
                                acquired, superseded, timeout testing nothing)
      unfinishedRuns            run-log file names (<ID>-<stamp>-L<n>.jsonl) with no result
                                event and no FACTORY: DONE or FACTORY: BLOCKED line, of a shift
                                that had ended (or whose shift log is not there)
      runningRuns               the same, of a shift still running when the logs were read: its
                                DarkFactory-<stamp>.log has no "shift end" line (AF-0269)
    and tasks: one row per task claimed since -Since: id, claims, minutes (last claim to its
    end, or to the last line seen when still open), costUsd (its runs), outcome (done,
    requeued, blocked, parked or open).

.PARAMETER Since
    The first day to count, e.g. 2026-09-29, in local time. Logs, runs and CI runs before it
    are skipped. A CI run's UTC start is compared with -Since converted to UTC, so a run from
    the evening before, whose UTC date is already -Since's, is not counted (BL-2055).

.PARAMETER LogRoot
    The factory's log folder. Default: <repo>.logs beside the repository.

.PARAMETER CiRunsJson
    A file holding gh run list's JSON. When given, gh is not called for the run list.

.PARAMETER CiJobsJson
    A file holding each failed run's jobs and annotations (the CI jobs format above). Default:
    ci-jobs.json beside -CiRunsJson when there is one; otherwise gh is called for them.

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
    [string]$CiJobsJson,
    [string]$OutFile,
    [switch]$SelfTest
)

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$MaxWaitMinutes = 60
# A step that builds or tests: one that hangs into its job's timeout-minutes is a failing test and
# still counts as red under ADR-1235.
$TestOrBuildStep = '(?i)test|build|coverage'

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

# Whether the shift a run log belongs to was still running when the logs were read: its shift
# log (DarkFactory-<stamp>.log, the stamp in the run log's name) is there and has no "shift end"
# line. A run of a live shift with no result yet is still running, not unfinished (AF-0269). A
# run whose shift log is not there keeps the old reading.
function Test-ShiftLive([string]$Root, [string]$RunFile) {
    if ($RunFile -notmatch '-(\d{8}-\d{6})-L\d+') { return $false }
    $shiftLog = Join-Path $Root "DarkFactory-$($Matches[1]).log"
    if (-not (Test-Path -LiteralPath $shiftLog)) { return $false }
    foreach ($line in [IO.File]::ReadLines($shiftLog)) {
        if ($line -match '^\d\d:\d\d:\d\d -\s+shift\s+end\b') { return $false }
    }
    return $true
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
        $runs += [pscustomobject]@{ Id = $id; File = $file.Name; Resumed = $file.Name -like '*-resumed.jsonl'; CostUsd = $cost; TokensIn = $in; TokensOut = $out; HasResult = [bool]$resultLine; Finished = ([bool]$resultLine) -or $factoryEnd; ShiftLive = (Test-ShiftLive $Root $file.Name) }
    }
    return $runs
}

function Get-CiJobInfrastructureReason([object]$Job) {
    # Why a CI job that did not succeed is infrastructure under ADR-1235, or $null when it is a
    # failing test or build error (or a failure the rule does not name, which stays red).
    if ("$($Job.conclusion)" -in 'success', 'skipped', 'neutral') { return 'succeeded' }
    $messages = @($Job.annotations | Where-Object { $_ } | ForEach-Object { "$($_.message)" })
    if ($messages -match 'was not acquired by Runner') { return 'runner not acquired' }
    if ($messages -match 'higher priority waiting request|superseded') { return 'superseded' }
    if ($messages -match 'exceeded the maximum execution time') {
        # A test or build step that hangs into the timeout is a failing test: it still counts.
        $hung = @($Job.steps | Where-Object { $_ -and ("$($_.conclusion)" -notin 'success', 'skipped') -and "$($_.name)" -match $TestOrBuildStep })
        if ($hung.Count) { return $null }
        return 'timeout testing nothing'
    }
    return $null
}

function Get-CiRunInfrastructure([object[]]$Jobs) {
    # For a failed run's jobs: "name: reason" for each non-success job when every one of them is
    # infrastructure under ADR-1235, else $null (the run is red). No jobs read: $null.
    if (-not $Jobs -or -not $Jobs.Count) { return $null }
    $reasons = @()
    foreach ($job in $Jobs) {
        $reason = Get-CiJobInfrastructureReason $job
        if (-not $reason) { return $null }
        if ($reason -ne 'succeeded') { $reasons += "$($job.name): $reason" }
    }
    if (-not $reasons.Count) { return $null }
    return $reasons
}

function Get-CiRedSpells([object[]]$Runs, [datetime]$From, [hashtable]$Jobs = @{}) {
    # Each red spell: from the end of the first failed run after a success to the end of the next
    # successful run (or of the last finished run, when still red), with the run that turned it
    # red, so the process auditor can name the spell, not only the total (BL-1366). A failed run
    # whose non-success jobs are all infrastructure (ADR-1235) neither starts nor ends a spell.
    $finished = @($Runs | Where-Object { $_.status -eq 'completed' -and $_.conclusion -in 'success', 'failure' } |
        Where-Object { -not ($_.conclusion -eq 'failure' -and (Get-CiRunInfrastructure $Jobs["$($_.databaseId)"])) } |
        ForEach-Object { [pscustomobject]@{ Id = $_.databaseId; Conclusion = $_.conclusion; Created = ([datetimeoffset]$_.createdAt).UtcDateTime; Ended = ([datetimeoffset]$_.updatedAt).UtcDateTime } } |
        Where-Object { $_.Created -ge $From.ToUniversalTime() } | Sort-Object Created)
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

function Get-CiRedMinutes([object[]]$Runs, [datetime]$From, [hashtable]$Jobs = @{}) {
    $minutes = @(Get-CiRedSpells $Runs $From $Jobs | ForEach-Object { [double]$_.minutes })
    return [math]::Round([double]($minutes | Measure-Object -Sum).Sum, 2)
}

function Get-CiInfrastructureRuns([object[]]$Runs, [datetime]$From, [hashtable]$Jobs = @{}) {
    # The failed runs left out under ADR-1235, each with its non-success jobs' classification.
    return @($Runs | Where-Object { $_.status -eq 'completed' -and $_.conclusion -eq 'failure' -and ([datetimeoffset]$_.createdAt).UtcDateTime -ge $From.ToUniversalTime() } |
        Sort-Object { ([datetimeoffset]$_.createdAt).UtcDateTime } |
        ForEach-Object {
            $reasons = Get-CiRunInfrastructure $Jobs["$($_.databaseId)"]
            if ($reasons) { [ordered]@{ runId = $_.databaseId; ended = ([datetimeoffset]$_.updatedAt).UtcDateTime.ToString('yyyy-MM-ddTHH:mm:ssZ'); jobs = @($reasons) } }
        })
}

function ConvertTo-CiJobTable([object[]]$Entries) {
    # ci-jobs.json's entries ({runId, jobs}) as a table keyed by the run id's text.
    $table = @{}
    foreach ($entry in $Entries) { if ($entry) { $table["$($entry.runId)"] = @($entry.jobs) } }
    return $table
}

function Read-CiJobsFromGh([object[]]$Runs, [datetime]$From) {
    # Each failed run's jobs (gh run view --json jobs), with each non-success job's annotations
    # (the check run's annotations API), as ci-jobs.json entries. A run gh cannot read is left
    # out, so it counts as red.
    $entries = @()
    $failed = @($Runs | Where-Object { $_.status -eq 'completed' -and $_.conclusion -eq 'failure' -and ([datetimeoffset]$_.createdAt).UtcDateTime -ge $From.ToUniversalTime() })
    foreach ($run in $failed) {
        $ErrorActionPreference = 'Continue'
        $raw = & gh run view $run.databaseId --json jobs 2>$null
        $ErrorActionPreference = 'Stop'
        if ($LASTEXITCODE -ne 0 -or -not $raw) { Write-Warning "gh run view $($run.databaseId) failed; the run counts as red."; continue }
        $parsed = $raw | Out-String | ConvertFrom-Json
        $jobs = @()
        foreach ($job in @($parsed.jobs)) {
            $annotations = @()
            if ("$($job.conclusion)" -notin 'success', 'skipped', 'neutral') {
                $ErrorActionPreference = 'Continue'
                $rawNotes = & gh api "repos/StewartScottRogers/Curl/check-runs/$($job.databaseId)/annotations" 2>$null
                $ErrorActionPreference = 'Stop'
                if ($LASTEXITCODE -eq 0 -and $rawNotes) { $notes = $rawNotes | Out-String | ConvertFrom-Json; $annotations = @($notes | ForEach-Object { [ordered]@{ message = "$($_.message)" } }) }
            }
            $jobs += [ordered]@{ databaseId = $job.databaseId; name = $job.name; conclusion = $job.conclusion; steps = @($job.steps | ForEach-Object { [ordered]@{ name = $_.name; status = $_.status; conclusion = $_.conclusion } }); annotations = $annotations }
        }
        $entries += [pscustomobject]@{ runId = $run.databaseId; jobs = $jobs }
    }
    return $entries
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

function Measure-Process([string]$Root, [datetime]$From, [object[]]$CiRuns, [hashtable]$CiJobs = @{}) {
    $events = @(Get-LaneEvents $Root $From)
    $runs = @(Get-RunResults $Root $From)

    # Tasks: claims, and how the last claim ended.
    $tasks = [ordered]@{}
    $terminal = @{ DONE = 'done'; REQUEUE = 'requeued'; BLOCKED = 'blocked'; PARKED = 'parked' }
    foreach ($e in $events) {
        if ($e.Id -notmatch '^BL-\d+$') { continue }
        # A claim is a pushed claim. Older factory logs traced a refused claim push as a
        # "claim   lost the race; picking again" line (BL-1281 now traces it as verb race), so
        # that text is a lost race, not a claim (BL-1286).
        if ($e.Verb -eq 'claim' -and $e.Text -notmatch '^lost the race') {
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
        ciRedMinutes = Get-CiRedMinutes $CiRuns $From $CiJobs
        ciRedSpells = @(Get-CiRedSpells $CiRuns $From $CiJobs)
        ciInfrastructureRuns = @(Get-CiInfrastructureRuns $CiRuns $From $CiJobs)
        laneMinutes = [math]::Round($laneMinutes, 2)
        laneIdleMinutes = [math]::Round($overlap + $nothing + $other, 2)
        waitOverlapMinutes = [math]::Round($overlap, 2)
        waitNothingReadyMinutes = [math]::Round($nothing, 2)
        waitOtherMinutes = [math]::Round($other, 2)
        tokensInput = $tokensIn
        tokensOutput = $tokensOut
        costUsd = [math]::Round($cost, 4)
        costUsdPerTaskDone = $(if ($done) { [math]::Round($cost / $done, 4) } else { $null })
        unfinishedRuns = @($runs | Where-Object { -not $_.Finished -and -not $_.ShiftLive } | ForEach-Object { $_.File } | Sort-Object)
        runningRuns = @($runs | Where-Object { -not $_.Finished -and $_.ShiftLive } | ForEach-Object { $_.File } | Sort-Object)
        tasks = $rows
    }
}

if ($SelfTest) {
    $fixture = Join-Path $PSScriptRoot 'Fixtures\process'
    $ci = Get-Content -LiteralPath (Join-Path $fixture 'ci-runs.json') -Raw | ConvertFrom-Json
    $jobs = Get-Content -LiteralPath (Join-Path $fixture 'ci-jobs.json') -Raw | ConvertFrom-Json
    $m = Measure-Process (Join-Path $fixture 'lane-output') ([datetime]'2026-01-01') @($ci) (ConvertTo-CiJobTable @($jobs))
    $expected = [ordered]@{
        tasksDone = 4; medianTaskMinutes = 30; p90TaskMinutes = 45; tasksClaimedMoreThanOnce = 1; requeues = 1; resumedRuns = 1
        ciRedMinutes = 150; laneIdleMinutes = 30; waitOverlapMinutes = 20; waitNothingReadyMinutes = 10; waitOtherMinutes = 0
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
    # AF-0269: a run with no result whose shift log has no "shift end" line was still running.
    $ok = (@($m.runningRuns) -join ',') -eq 'BL-006-20260102-100000-L1.jsonl'
    if (-not $ok) { $failed++ }
    Write-Host "$(if ($ok) { 'PASS' } else { 'FAIL' }) runningRuns: $(@($m.runningRuns) -join ',')"
    $spells = @($m.ciRedSpells)
    $ok = $spells.Count -eq 3 -and $spells[0].runId -eq 2 -and "$($spells[0].minutes)" -eq '60' -and $spells[0].start -eq '2026-01-01T10:10:00Z' -and $spells[1].runId -eq 6 -and $spells[1].end -eq '2026-01-01T13:05:00Z' -and -not $spells[1].stillRed -and $spells[2].runId -eq 11 -and $spells[2].start -eq '2026-01-01T15:00:00Z' -and "$($spells[2].minutes)" -eq '30' -and -not $spells[2].stillRed
    if (-not $ok) { $failed++ }
    Write-Host "$(if ($ok) { 'PASS' } else { 'FAIL' }) ciRedSpells: $(($spells | ForEach-Object { "$($_.runId):$($_.minutes)" }) -join ', ')"
    # ADR-1235 (BL-4825): run 9's only failure is a macOS job no runner acquired and run 10's is the
    # audit guard's checkout running past its timeout, so neither starts a spell; run 11's failing
    # test step does.
    $left = @($m.ciInfrastructureRuns)
    $ok = (@($left | ForEach-Object { $_.runId }) -join ',') -eq '9,10' -and (@($left[0].jobs) -join ';') -eq 'Build and test (macos-latest): runner not acquired' -and (@($left[1].jobs) -join ';') -eq 'Audit paths untouched by the dark factory: timeout testing nothing'
    if (-not $ok) { $failed++ }
    Write-Host "$(if ($ok) { 'PASS' } else { 'FAIL' }) ciInfrastructureRuns: $(($left | ForEach-Object { "$($_.runId) ($(@($_.jobs) -join '; '))" }) -join ', ')"
    $hungTest = [pscustomobject]@{ name = 'Build and test (ubuntu-latest)'; conclusion = 'cancelled'; steps = @([pscustomobject]@{ name = 'Fast tests'; status = 'completed'; conclusion = 'cancelled' }); annotations = @([pscustomobject]@{ message = 'The job has exceeded the maximum execution time of 30m0s' }) }
    $superseded = [pscustomobject]@{ name = 'Build and test (windows-latest)'; conclusion = 'cancelled'; steps = @(); annotations = @([pscustomobject]@{ message = "Canceling since a higher priority waiting request for 'CI-work/dark-factory' exists" }) }
    $failedBuild = [pscustomobject]@{ name = 'Build and test (windows-latest)'; conclusion = 'failure'; steps = @([pscustomobject]@{ name = 'Build'; status = 'completed'; conclusion = 'failure' }); annotations = @() }
    $ok = $null -eq (Get-CiJobInfrastructureReason $hungTest) -and (Get-CiJobInfrastructureReason $superseded) -eq 'superseded' -and $null -eq (Get-CiJobInfrastructureReason $failedBuild) -and $null -eq (Get-CiRunInfrastructure @())
    if (-not $ok) { $failed++ }
    Write-Host "$(if ($ok) { 'PASS' } else { 'FAIL' }) job classification: a test step hung into its timeout and a build error count, a superseded job does not, and a run with no jobs read stays red"
    $ok = "$($m.laneMinutes)" -eq '941.42'
    if (-not $ok) { $failed++ }
    Write-Host "$(if ($ok) { 'PASS' } else { 'FAIL' }) laneMinutes: $($m.laneMinutes) (expected 941.42)"
    # BL-1286: a lost race (the older "claim   lost the race" line or a race line) is no claim.
    $bl003 = @($m.tasks | Where-Object { $_.id -eq 'BL-003' })[0]
    $ok = $bl003.claims -eq 1
    if (-not $ok) { $failed++ }
    Write-Host "$(if ($ok) { 'PASS' } else { 'FAIL' }) task row BL-003 lost-race and race lines not counted: claims $($bl003.claims) (expected 1)"
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
# Each failed run's jobs, to leave out infrastructure failures (ADR-1235): from -CiJobsJson, else a
# ci-jobs.json beside -CiRunsJson, else from gh.
if (-not $CiJobsJson -and $CiRunsJson) {
    $beside = Join-Path (Split-Path -Parent (Resolve-Path -LiteralPath $CiRunsJson).Path) 'ci-jobs.json'
    if (Test-Path -LiteralPath $beside) { $CiJobsJson = $beside }
}
if ($CiJobsJson) { $jobEntries = Get-Content -LiteralPath $CiJobsJson -Raw | ConvertFrom-Json }
else { $jobEntries = Read-CiJobsFromGh $ciRuns $Since }
$measured = Measure-Process (Resolve-Path $LogRoot).Path $Since $ciRuns (ConvertTo-CiJobTable @($jobEntries))
# The CI runs read may not reach back to -Since: say where they start.
$oldest = @($ciRuns | ForEach-Object { ([datetimeoffset]$_.createdAt).UtcDateTime } | Sort-Object)[0]
$measured.ciRunsFrom = if ($oldest) { $oldest.ToString('yyyy-MM-ddTHH:mm:ssZ') } else { $null }
$json = [pscustomobject]$measured | ConvertTo-Json -Depth 5
if ($OutFile) { [IO.File]::WriteAllText($OutFile, $json) }
Write-Output $json
