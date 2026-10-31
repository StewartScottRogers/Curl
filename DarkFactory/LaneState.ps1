# Lane state files, tasks a lane sent back this shift, whether a lane lives, whether tokens
# are available, and -TestHeartbeat. Dot-sourced by RunDarkFactory.ps1 in its order; see the
# loader there.

function Write-LaneSummary {
    param([string[]]$Lines)
    $dir = Join-Path $LogDir "lanes-$Stamp"
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
    Set-Content -Path (Join-Path $dir "lane-$Lane.txt") -Value $Lines -Encoding UTF8
}

# Lane state, in <repo>.logs\lanes-<stamp>\: lane-<n>.pid (the lane's process, so the coordinator
# can tell a dead lane from a busy one) and lane-<n>.task (the task it holds in Doing, so a
# restarted lane - or the next shift - resumes it instead of losing it).
function Get-LaneStatePath {
    param([int]$N, [string]$Kind, [string]$ForStamp = $Stamp)
    return (Join-Path (Join-Path $LogDir "lanes-$ForStamp") "lane-$N.$Kind")
}

function Set-LaneState {
    param([string]$Kind, [string]$Value)
    $path = Get-LaneStatePath $Lane $Kind
    New-Item -ItemType Directory -Force -Path (Split-Path $path) | Out-Null
    if ($Value) { Set-Content -Path $path -Value $Value -Encoding ASCII } else { Remove-Item $path -ErrorAction SilentlyContinue }
}

function Get-LaneState {
    param([int]$N, [string]$Kind, [string]$ForStamp = $Stamp)
    $path = Get-LaneStatePath $N $Kind $ForStamp
    if (Test-Path $path) { return "$(Get-Content $path -TotalCount 1)".Trim() }
    return ''
}

# Tasks a lane of this shift sent back to Backlog (requeued or parked), one ID a line in
# <repo>.logs\lanes-<stamp>\requeued.txt. No lane of the same shift claims one again: a
# task that fell short once - BL-1525 missed its timing target under eight lanes' load -
# would only fall short again on the next lane, handing its work through the stash each
# time (AF-0072). The next shift tries it afresh.
function Get-ShiftRequeuedPath { return (Join-Path (Join-Path $LogDir "lanes-$Stamp") 'requeued.txt') }

function Add-ShiftRequeued {
    param([string]$Id)
    $path = Get-ShiftRequeuedPath
    New-Item -ItemType Directory -Force -Path (Split-Path $path) | Out-Null
    Add-Content -Path $path -Value $Id -Encoding ASCII
}

function Get-ShiftRequeued {
    $path = Get-ShiftRequeuedPath
    if (-not (Test-Path $path)) { return @() }
    return @(Get-Content $path | ForEach-Object { "$_".Trim() } | Where-Object { $_ } | Select-Object -Unique)
}

function Test-LaneAlive {
    # True while lane <n>'s process runs. No pid file yet means it is still starting.
    param([int]$N, [string]$ForStamp = $Stamp)
    $lanePid = Get-LaneState $N 'pid' $ForStamp
    if (-not $lanePid) { return $true }
    $p = Get-Process -Id ([int]$lanePid) -ErrorAction SilentlyContinue
    return [bool]($p -and $p.ProcessName -match 'powershell|pwsh')
}

function Test-TokensAvailable {
    # Asks Claude for one word. $true when it answers, $false when the usage limit (or any
    # API failure) refuses it. Used where a run failed without saying why.
    try {
        $psi = New-Object System.Diagnostics.ProcessStartInfo
        Set-FactoryShellCommand $psi "claude -p --model $ProbeModel --output-format stream-json --verbose --max-turns 1 2>$(Get-FactoryNullDevice)"
        $psi.WorkingDirectory = $Root
        $psi.UseShellExecute = $false
        $psi.RedirectStandardInput = $true
        $psi.RedirectStandardOutput = $true
        $p = [System.Diagnostics.Process]::Start($psi)
        $p.StandardInput.Write('Reply with the single word OK.')
        $p.StandardInput.Close()
        $read = $p.StandardOutput.ReadToEndAsync()
        if (-not $p.WaitForExit(120000)) { Stop-FactoryProcessTree -ProcessId $p.Id; return $false }
        $out = $read.Result
    } catch { return $false }
    return ($out -match '"type":"result"' -and $out -notmatch '"status":"rejected"' -and $out -notmatch '"is_error":true')
}

function Test-ApiFailure {
    # A run that died on the API rather than on the work: no result at all, or an error
    # result the API gave (the usage limit in a form Get-OutOfTokensUntil did not know, an
    # overload, an outage).
    param($Run)
    if ($Run.TimedOut) { return $false }
    $r = $script:RunResult
    if ($null -eq $r) { return ($Run.ExitCode -ne 0) }
    return ([bool]($r.PSObject.Properties['is_error'] -and $r.is_error) -and
        ([bool]$r.PSObject.Properties['api_error_status'] -or "$($r.terminal_reason)" -eq 'api_error'))
}

function Test-BudgetSpent {
    # Whether the last run stopped because it reached the -TaskBudgetUsd cost cap.
    $r = $script:RunResult
    return ($null -ne $r -and "$($r.subtype)" -eq 'error_max_budget_usd')
}

function Wait-ForTokensByProbe {
    # Holds this runner until a one-word probe is answered, checking every 5 minutes, and
    # returns how long it waited.
    param([string]$Id)
    $began = Get-Date
    Write-Trace $Id 'tokens' 'run ended with no result (not necessarily the API); waiting until Claude answers a probe' 'Yellow'
    Write-Heartbeat 'tokens' 'waiting until Claude answers again'
    while (-not (Test-TokensAvailable)) {
        if ($Lane) { try { $Host.UI.RawUI.WindowTitle = "Dark factory - lane $Lane waiting for the API" } catch { } }
        # Refreshed every minute of the wait, so the page never shows this lane as stale.
        foreach ($minute in 1..5) { Write-Heartbeat; Start-Sleep -Seconds 60 }
    }
    Write-Trace $Id 'resume' 'Claude answers again; running the task again' 'Green'
    return ((Get-Date) - $began)
}

if ($TestHeartbeat) {
    # Lane 1 through its phases in a temporary log root, printing its file after each. No
    # board, git or Claude: the task is a made-up BL-000 with a title given here.
    $Lane = 1
    $LogDir = Join-Path ([IO.Path]::GetTempPath()) "DarkFactoryHeartbeat-$Stamp"
    $WritesHeartbeat = $true
    $heartbeatFile = Get-LaneStatePath $Lane 'heartbeat.json'
    try {
        Write-Heartbeat 'starting'
        Get-Content -Raw $heartbeatFile
        Write-Heartbeat 'claim'
        Get-Content -Raw $heartbeatFile
        Set-HeartbeatTask 'BL-000' -Title 'Rehearse the heartbeat file'
        # The model fields Set-ModelChoice fills from the task file (BL-1705).
        $script:Beat.Model = 'sonnet'; $script:Beat.ModelWhy = 'direct pipeline'
        Write-Heartbeat 'run'
        Set-HeartbeatStep @('build', '')
        Get-Content -Raw $heartbeatFile
        Write-Heartbeat 'integrate'
        Get-Content -Raw $heartbeatFile
        Set-HeartbeatTask ''
        Write-Heartbeat 'finished'
        Get-Content -Raw $heartbeatFile
        # Three made-up lanes, written out of order, merged into status.json and built into
        # the board branch's commit. Never pushed: lanes are denied git push, and the first
        # real push is the next shift's.
        foreach ($fake in @(
            @{ Lane = 3; Task = $null; Title = $null; Phase = 'wait'; Step = 'nothing can start yet' },
            @{ Lane = 1; Task = 'BL-001'; Title = 'Rehearse lane one'; Phase = 'run'; Step = 'build' },
            @{ Lane = 2; Task = 'BL-002'; Title = 'Rehearse lane two'; Phase = 'integrate'; Step = '' })) {
            $Lane = $fake.Lane
            $script:Beat.Task = $null
            Set-HeartbeatTask $fake.Task -Title $fake.Title
            if ($fake.Task) { $script:Beat.Model = 'opus'; $script:Beat.ModelWhy = 'feature pipeline' }
            Write-Heartbeat $fake.Phase $fake.Step
        }
        # A made-up Auto step, so the merged file carries an autoLanes object.
        Set-AutoLanesStatus -Lanes 4 -Target 5.24 -Binding 'weekly pace' -Reason 'lanes 3 -> 4 (weekly pace allows 5.2)'
        $json = Get-BoardStatusJson -Branch 'work/dark-factory'
        $json
        New-BoardCommit $json
        # The idle -Continuous coordinator's poll (BL-3092). ShiftCoordinator.ps1 loads after
        # this file; with $Lane set its coordinator block is skipped, so loading it here only
        # defines its functions. Its last line marks it finished; this file is not, so the
        # loader still stops at this file's exit below.
        . (Join-Path $FactoryScriptDir 'DarkFactory\ShiftCoordinator.ps1')
        $AreaFileFinished = $false
        $Lane = 4
        Set-HeartbeatTask ''
        Write-IdlePollHeartbeat
        $idleBeat = Get-Content -Raw (Get-LaneStatePath $Lane 'heartbeat.json')
        $idleBeat
        $idleObject = $idleBeat | ConvertFrom-Json
        $now = [datetime]'2026-10-22 02:20'
        $deadline = [datetime]'2026-10-22 09:45'
        $poll = { param($At, $Usage, $Id, $Capped) (Get-IdlePollDecision -Now $At -Deadline $deadline -UsageStop $Usage -ClaimableId $Id -CappedIds $Capped).Action }
        $cases = @(
            ,@('the polling heartbeat is phase idle, step polling', 'idle/polling', "$($idleObject.phase)/$($idleObject.step)")
            ,@('nothing claimable and time left waits', 'wait', (& $poll $now '' '' @()))
            ,@('something claimable starts the next shift', 'start', (& $poll $now '' 'BL-3011' @()))
            ,@('past the -Hours deadline stops', 'stop', (& $poll $deadline.AddMinutes(1) '' 'BL-3011' @()))
            ,@('usage over the stop threshold stops', 'stop', (& $poll $now 'session tokens 86% used, reset 04:00' 'BL-3011' @()))
            ,@('only cost-capped tasks waits', 'wait', (& $poll $now '' '' @('BL-2969')))
        )
        # A ready CI fix past the -Hours deadline starts the next shift (BL-4412): shift
        # 20261030-060635 ended at 14:07, its lanes at 14:44, and its merge-wait polls said stop
        # with BL-4369 ready until the merge gave up at 15:21. Usage still stops it.
        $ciDeadline = [datetime]'2026-10-30 14:07'
        $ciPoll = Get-IdlePollDecision -Now ([datetime]'2026-10-30 14:49') -Deadline $ciDeadline -UsageStop '' -ClaimableId 'BL-4369' -CiFixId 'BL-4369'
        $cases += ,@('a ready CI fix past the deadline starts the next shift', 'start', $ciPoll.Action)
        $cases += ,@('its trace names the CI fix and the deadline', 'BL-4369 is a ready CI fix, which the -Hours deadline 14:07 does not hold (ADR-1158)', $ciPoll.Why)
        $cases += ,@('a ready CI fix past the deadline still stops at the usage limit', 'stop', (Get-IdlePollDecision -Now ([datetime]'2026-10-30 14:49') -Deadline $ciDeadline -UsageStop 'session tokens 86% used' -ClaimableId 'BL-4369' -CiFixId 'BL-4369').Action)
        $cases += ,@('ordinary work past the deadline still stops', 'stop', (Get-IdlePollDecision -Now ([datetime]'2026-10-30 14:49') -Deadline $ciDeadline -UsageStop '' -ClaimableId 'BL-4300' -CiFixId '').Action)
        $cases += ,@('before the deadline a CI fix starts as any claimable task does', 'BL-4300 is claimable', (Get-IdlePollDecision -Now ([datetime]'2026-10-30 13:49') -Deadline $ciDeadline -UsageStop '' -ClaimableId 'BL-4300' -CiFixId 'BL-4369').Why)
        # The merge-wait poll leaves out what this shift requeued (AF-0369): BL-3151 is ready
        # but was sent back by a lane of this shift, so BL-3152 is offered; with BL-3151 alone
        # nothing is claimable, and a task nobody requeued stays claimable.
        $offerReady = { param([string[]]$s) $i = @('BL-3151', 'BL-3152') | Where-Object { $s -notcontains $_ } | Select-Object -First 1; if ($i) { "$i  pipeline: direct  Tasks\Backlog\$i-x.md" } else { 'No task is ready.' } }
        $offerOne = { param([string[]]$s) if ($s -contains 'BL-3151') { 'No task is ready.' } else { 'BL-3151  pipeline: direct  Tasks\Backlog\BL-3151-x.md' } }
        $freshOffer = Get-MergeWaitClaimableTaskId -OfferNext $offerReady -NotClaimableWhy { '' }
        Add-ShiftRequeued 'BL-3151'
        $skipOffer = Get-MergeWaitClaimableTaskId -OfferNext $offerReady -NotClaimableWhy { '' }
        $skipOnly = Get-MergeWaitClaimableTaskId -OfferNext $offerOne -NotClaimableWhy { '' }
        $cases += ,@('with nothing requeued the merge wait offers the first ready task', 'BL-3151', "$($freshOffer.Id)")
        $cases += ,@('the merge wait skips a task this shift requeued', 'BL-3152', "$($skipOffer.Id)")
        $cases += ,@('the merge wait finds nothing when only a requeued task is ready', '', "$($skipOnly.Id)")        # The same poll during the shift-end merge's CI wait (BL-3142): CI stays pending, a task
        # is claimable at the second poll, and the next shift starts once while the wait goes
        # on to its own end. gh, git, Get-Date and Start-Sleep are stubbed with a fake clock.
        $script:fakeNow = [datetime]'2026-10-22 14:20'
        $script:polls = 0
        $script:started = 0
        function gh { '[{"status":"in_progress","conclusion":null}]' }
        function git { '' }
        function Get-Date { param($Format) $script:fakeNow }
        function Start-Sleep { param($Seconds) $script:fakeNow = $script:fakeNow.AddSeconds($Seconds) }
        try {
            $onPoll = { $script:polls++; if ($script:polls -eq 2) { $script:started++ } }
            $wait = Wait-MergeCiRun -Branch 'factory/test' -Head ('a' * 40) -OnPoll $onPoll -PollMinutes 5
        } finally { Remove-Item function:gh, function:git, function:Get-Date, function:Start-Sleep }
        $cases += ,@('a pending merge wait polls the board every poll interval', '11', "$($script:polls)")
        $cases += ,@('the next shift starts once, at the second poll', '1', "$($script:started)")
        $cases += ,@('the merge wait still ends with its own line', 'not merged: CI did not finish on aaaaaaa within 60 min', "$($wait.Failure)")
        # The 20261030-060635 shift end again (BL-4412): lanes ended at 14:44, past the 14:07
        # deadline, with BL-4369 ready. The coordinator's poll starts the next shift at the
        # first poll, 14:49, and only once.
        $script:fakeNow = [datetime]'2026-10-30 14:44'
        $script:ciStartedAt = @()
        $ciHandOver = @{ Done = $false }
        function gh { '[{"status":"in_progress","conclusion":null}]' }
        function git { '' }
        function Get-Date { param($Format) $script:fakeNow }
        function Start-Sleep { param($Seconds) $script:fakeNow = $script:fakeNow.AddSeconds($Seconds) }
        try {
            $ciFixPoll = {
                if ($ciHandOver.Done) { return }
                $p = Get-IdlePollDecision -Now (Get-Date) -Deadline ([datetime]'2026-10-30 14:07') -UsageStop '' -ClaimableId 'BL-4369' -CiFixId 'BL-4369'
                if ($p.Action -eq 'start') { $script:ciStartedAt += (Get-Date).ToString('HH:mm'); $ciHandOver.Done = $true }
            }
            Wait-MergeCiRun -Branch 'factory/test' -Head ('a' * 40) -OnPoll $ciFixPoll -PollMinutes 5 -HandOver $ciHandOver | Out-Null
        } finally { Remove-Item function:gh, function:git, function:Get-Date, function:Start-Sleep }
        $cases += ,@('a CI fix ready past the deadline starts the next shift at the merge wait''s first poll, once', '14:49', "$($script:ciStartedAt -join ',')")
        # A poll that throws, or writes output, leaves the wait and its result intact (BL-3150).
        $script:fakeNow = [datetime]'2026-10-22 14:20'
        $script:throwPolls = 0
        function Write-Trace { param($Id, $Kind, $Text, $Color) }
        function gh { if ($script:fakeNow -ge [datetime]'2026-10-22 14:31') { '[{"status":"completed","conclusion":"success","databaseId":7}]' } else { '[{"status":"in_progress","conclusion":null}]' } }
        function git { '' }
        function Get-Date { param($Format) $script:fakeNow }
        function Start-Sleep { param($Seconds) $script:fakeNow = $script:fakeNow.AddSeconds($Seconds) }
        try {
            $noisyPoll = { $script:throwPolls++; 'stray output'; if ($script:throwPolls -eq 1) { throw 'herdr said no' } }
            $okWait = Wait-MergeCiRun -Branch 'factory/test' -Head ('a' * 40) -OnPoll $noisyPoll -PollMinutes 5
        } finally { Remove-Item function:gh, function:git, function:Get-Date, function:Start-Sleep, function:Write-Trace }
        $cases += ,@('a throwing, noisy poll still leaves the wait one hashtable', 'System.Collections.Hashtable', "$($okWait.GetType().FullName)")
        $cases += ,@('a throwing poll is tried again at the next interval', '2', "$($script:throwPolls)")
        $cases += ,@('the wait returns the run after a throwing poll', '7', "$($okWait.Run.databaseId)")
        # A cancelled run and a newer head (BL-3155): after a hand-over, code commits since the
        # original head stop the wait; board-only commits, or no hand-over, are followed (ADR-0693).
        function Get-FollowWaitFailure {
            param([bool]$HandedOver, [string]$CommitsSince, [string]$BoardCommitsSince)
            $script:fakeNow = [datetime]'2026-10-22 14:20'
            $script:newHeadAsked = 0
            $script:commitsSince = $CommitsSince
            $script:boardCommitsSince = $BoardCommitsSince
            function gh { if ($args -contains 'run' -and $args -contains 'list') { if ($args -contains ('b' * 40)) { '[{"status":"in_progress","conclusion":null}]' } else { '[{"status":"completed","conclusion":"cancelled"}]' } } }
            function git { $a = "$args"; if ($a -match 'rev-parse') { 'b' * 40 } elseif ($a -match '--grep') { $script:boardCommitsSince } elseif ($a -match 'rev-list') { $script:commitsSince } else { '' } }
            function Get-Date { param($Format) $script:fakeNow }
            function Start-Sleep { param($Seconds) $script:fakeNow = $script:fakeNow.AddSeconds($Seconds) }
            try { (Wait-MergeCiRun -Branch 'factory/test' -Head ('a' * 40) -PollMinutes 5 -HandOver @{ Done = $HandedOver }).Failure }
            finally { Remove-Item function:gh, function:git, function:Get-Date, function:Start-Sleep }
        }
        $cases += ,@('after a hand-over, a newer head with code commits is not followed', 'not merged: the next shift was handed over and pushed code to factory/test after aaaaaaa, so its work is not merged with this shift''s', "$(Get-FollowWaitFailure $true '3' '1')")
        $cases += ,@('after a hand-over, a newer head of board commits only is followed', 'not merged: CI did not finish on bbbbbbb within 60 min', "$(Get-FollowWaitFailure $true '2' '2')")
        $cases += ,@('without a hand-over, a newer head with code commits is followed', 'not merged: CI did not finish on bbbbbbb within 60 min', "$(Get-FollowWaitFailure $false '3' '1')")

        # A failed start of the next shift is not a hand-over (BL-3154): a start that throws, or
        # returns neither a tab nor a process, reports Started false and says why.
        $startCases = @(
            ,@('a herdr tab counts as started', 'True/t1', { @{ Tab = 't1' } })
            ,@('a process counts as started', 'True/p', { @{ Process = 'p' } })
            ,@('a start that throws is not started', 'False/herdr said no', { throw 'herdr said no' })
            ,@('a start that returns neither is not started', 'False/neither a herdr tab nor a process was started', { @{ } })
            ,@('a start that returns nothing is not started', 'False/neither a herdr tab nor a process was started', { })
        )
        foreach ($startCase in $startCases) {
            $script:fakeStart = $startCase[2]
            function Start-Detached { param($Label, $Dir, $ScriptArgs) & $script:fakeStart }
            try { $startResult = Start-NextShiftDetached -Label 'x' -Dir '.' -ScriptArgs @() } finally { Remove-Item function:Start-Detached }
            $cases += ,@($startCase[0], $startCase[1], "$($startResult.Started)/$(if ($startResult.Started) { "$($startResult.Tab)$($startResult.Process)" } else { $startResult.Failure })")
        }
        $failed = 0
        foreach ($case in $cases) {
            if ($case[1] -ceq $case[2]) { Write-Host "PASS $($case[0]): $($case[2])" -ForegroundColor Green }
            else { Write-Host "FAIL $($case[0]): expected $($case[1]), got $($case[2])" -ForegroundColor Red; $failed++ }
        }
    } finally { Remove-Item $LogDir -Recurse -Force -ErrorAction SilentlyContinue }
    exit $(if ($failed) { 1 } else { 0 })
}

# This file ran to its end; RunDarkFactory.ps1 goes on to the next one.
$AreaFileFinished = $true
