# One runner, a lone shift or a lane: claim the next task, run it, integrate or park it, and
# repeat until the shift ends. It always ends in exit. Dot-sourced by RunDarkFactory.ps1 in
# its order; see the loader there.

# ------------------------------------------------ one runner: this checkout, or a lane

try { $Host.UI.RawUI.WindowTitle = if ($Lane) { "Dark factory - lane $Lane" } else { 'Dark factory - running' } } catch { }

if ($Lane) {
    if (-not $Branch) { Write-Trace '-' 'refuse' 'a lane needs -Branch' 'Red'; exit 1 }
    $branch = $Branch
} else {
    if ($ShiftBranch) { $moved = Restore-ShiftBranch -Branch $ShiftBranch; if ($moved) { Write-Trace '-' 'branch' $moved 'Red' } }
    $branch = (git -C $Root rev-parse --abbrev-ref HEAD).Trim()
    if ($branch -in 'master', 'main') { Stop-ShiftStart "on $branch; switch to a feature branch"; exit 1 }
    if (Wait-ForCleanTree) { Stop-ShiftStart 'working tree not clean; commit or stash first'; exit 1 }
    $stuck = @(Get-ChildItem (Join-Path $Root 'Tasks\Doing') -Filter 'BL-*.md' -ErrorAction SilentlyContinue | Where-Object { -not (Test-InteractiveOnlyTask $_.FullName) })
    if ($stuck.Count) { Stop-ShiftStart "task already in Doing: $($stuck[0].Name)"; exit 1 }
}

Write-Trace '-' 'shift' "start  branch=$branch model=$Model until $($shiftEnd.ToString('HH:mm'))  $LaneMarkerTrace" 'Cyan'

$done = 0; $blocked = 0; $deferred = 0; $requeued = 0; $stalls = @(); $failStreak = 0; $attempted = @{}
# Parks whose move to Backlog never reached the shared branch, for the lane's summary.
$parkLines = @()
$stopWhy = ''
# The task to run again once the usage limit resets; it is still claimed.
$resumeId = ''
# How many times in a row the current task's run died on the API.
$apiRetries = 0
# Script errors met on a task the lane still held in Doing; each is resumed, twice at most (BL-2036).
$scriptErrorResumes = 0
# The task whose run was killed at -TaskMinutes in Doing and now gets one overtime run of
# $overtimeMinutes with its work in place, instead of a stash and Blocked (AF-0042).
$overtimeId = ''
$overtimeMinutes = [math]::Max(30, [int]($TaskMinutes / 4))

Write-Heartbeat 'starting'
if ($Lane) {
    Set-LaneState 'pid' "$PID"
    # A restarted or adopted lane finishes the task it held, from the work in its worktree.
    $held = Get-LaneState $Lane 'task'
    if ($held -and (Get-TaskState $held) -eq 'Doing') {
        $resumeId = $held
        Write-Trace $held 'resume' 'this lane held the task when it stopped; carrying on from its work' 'Green'
    } elseif ($held) {
        # It stopped after the run but before its work reached the shared branch: integrate
        # those commits now, before a claim resets the worktree and loses them.
        $ahead = [int](git -C $Root rev-list --count "origin/$branch..HEAD" 2>$null)
        $heldState = Get-TaskState $held
        # Whatever stopped the lane, a task it held that is back in Backlog is not claimed again this shift (AF-0091).
        if ($heldState -eq 'Backlog') { Add-ShiftRequeued $held }
        if ($ahead -gt 0 -and $heldState -in 'Done', 'Blocked', 'Backlog') {
            $problem = Invoke-Integrate -Id $held -State $heldState
            if ($problem) {
                $unpushed = Invoke-Park -Id $held -Why $problem
                if ($unpushed) { $parkLines += $unpushed; Write-Trace $held 'PARKED' "$problem; the move to Backlog was not pushed either" 'Red' }
                else { Write-Trace $held 'PARKED' "$problem; back to Backlog" 'Yellow' }
            }
            else { Write-Trace $held 'push' "integrated the stopped lane's work into $branch" }
        }
        Set-LaneState 'task' ''
    }
}

while ($true) {
  try {
    # A task cut off by the usage limit is finished first, whatever else says stop.
    $resuming = [bool]$resumeId
    if (-not $resuming) {
        if ((Get-Date) -gt $shiftEnd) { $stopWhy = 'time up'; break }
        if ($MaxTasks -gt 0 -and ($done + $blocked + $stalls.Count) -ge $MaxTasks) { $stopWhy = 'max tasks'; break }
        if ($failStreak -ge 2) { $stopWhy = 'runs failing'; break }
        # The coordinator asked this lane to retire; its last task is already integrated.
        if ($Lane -and (Test-Path (Get-LaneStatePath $Lane 'retire'))) { $stopWhy = 'retired'; break }
        # A red CI is fixed even past the usage stop: a lane that claims nothing leaves the red
        # spell to last until the window resets (AF-0308: 43 min unclaimed at 97% weekly use).
        # Running out of tokens mid-task is already waited out, so only CI-fix tasks go on.
        $low = Get-UsageStop
        $ciFixOnly = $false
        if ($low) {
            $ciFixWaiting = [bool](Get-ChildItem (Join-Path $Root 'Tasks\Backlog') -Filter 'BL-*.md' -ErrorAction SilentlyContinue | Select-String -Pattern '^title: .*(Fix CI failure|Fix flaky CI test)' -List)
            if (-not ($Lane -and $ciFixWaiting)) { $stopWhy = "tokens low: $low"; break }
            $ciFixOnly = $true
        }
    }

    if ($resuming) {
        $id = $resumeId
        $resumeId = ''
    } elseif ($Lane) {
        Set-HeartbeatTask ''
        Set-OwnTabLabel 'empty'
        Write-Heartbeat 'claim'
        $claim = Invoke-Claim -Skip (@($attempted.Keys) + @(Get-ShiftRequeued) | Select-Object -Unique) -CiFixOnly:$ciFixOnly
        if ($claim.None) { $stopWhy = $(if ($ciFixOnly) { "tokens low: $low" } else { 'nothing ready' }); break }
        if ($claim.Wait) {
            if ($claim.Why -match '^No task is ready') {
                $holdNote = Get-InteractiveHoldNote (Join-Path $Root 'Tasks\Backlog')
                if ($holdNote) { $claim.Why = "$($claim.Why) Backlog is $holdNote" }
            }
            Write-Trace '-' 'wait' $claim.Why 'DarkGray'; Write-Heartbeat 'wait' (Get-Short $claim.Why 80); Start-Sleep -Seconds 60; continue }
        $id = $claim.Id
        # A claim past the shift's end is never started: it goes back to Backlog at once (BL-4347).
        if ((Get-Date) -gt $shiftEnd) {
            Write-Trace $id 'release' 'claimed after the shift time was up; back to Backlog unstarted' 'Yellow'
            $unreleased = Invoke-ReleaseClaim -Id $id
            if ($unreleased) { $parkLines += $unreleased; Write-Trace $id 'RELEASE FAILED' $unreleased 'Red' }
            Add-ShiftRequeued $id
            Set-LaneState 'task' ''
            $stopWhy = 'time up'; break
        }
    } else {
        Set-HeartbeatTask ''
        Write-Heartbeat 'claim'
        $requeued = @(Invoke-Requeue)
        if ($requeued.Count) {
            git -C $Root add -A Tasks 2>&1 | Out-Null
            git -C $Root commit -q -m "chore(tasks): requeue $($requeued -join ', ') - blockers Done" 2>&1 | Out-Null
            git -C $Root push -q 2>&1 | Out-Null
        }
        $next = (Invoke-Board @('next')) -join "`n"
        $id = Get-NextTaskId $next
        if (-not $id) { $stopWhy = 'nothing ready'; break }
        if ($attempted.ContainsKey($id)) { $stopWhy = "$id offered twice"; $stalls += "$id offered again after a run"; break }
    }
    $attempted[$id] = $true
    # Before the lane's task file is rewritten, whose time a resumed task's start comes from.
    Set-HeartbeatTask $id
    if ($Lane) { Set-LaneState 'task' $id }

    Set-ModelChoice $id
    if (-not $resuming) { Write-Trace $id 'claim' "$(Get-Short (Get-TaskTitle $id))$(Format-ModelChoice)" 'Cyan' }
    if ($Lane) { Set-OwnTabLabel "$id $(Get-TaskTitle $id)" }
    Write-Heartbeat 'run'
    $inOvertime = $overtimeId -eq $id
    $overtimeId = ''
    # A fresh claim of a task whose runs of the last day already spent its cost cap is not
    # run again: it goes to Blocked like a run that reached the cap (AF-0096).
    $capWhy = if ($resuming -or $inOvertime) { '' } else { Get-TaskCapSpentWhy $id }
    if ($capWhy) {
        Write-Trace $id 'cost cap' $capWhy 'Yellow'
        $script:RunResult = $null
        $script:LimitResetAt = $null
        $run = [pscustomobject]@{ TimedOut = $false; ExitCode = 0 }
    } else {
        # A fresh claim of a task that came back with work in progress starts from that work (AF-0091).
        $stashNote = if ($resuming) { '' } else { Restore-TaskStash $id }
        $run = if ($inOvertime) { Invoke-TaskRun $id -Minutes $overtimeMinutes -Overtime } else { Invoke-TaskRun $id -Resume:$resuming -Note $stashNote }
    }
    $state = Get-TaskState $id

    # A run killed at its time limit keeps its claim and its work for one overtime run, so
    # work with passing tests is finished rather than stashed and Blocked (AF-0042).
    if ($run.TimedOut -and $state -eq 'Doing' -and -not $inOvertime) {
        Write-Trace $id 'overtime' "timed out after $TaskMinutes min; one more run of $overtimeMinutes min with its work in place" 'Yellow'
        $overtimeId = $id
        $resumeId = $id
        continue
    }

    # Out of tokens is not a stall. The task keeps its claim and its partial work - nothing
    # is stashed or blocked - the shift waits for the new session and runs it again, and
    # the wait is added to the shift so it costs no working time.
    $until = if ($run.TimedOut) { $null } else { Get-OutOfTokensUntil }
    if ($until -and $state -in 'Doing', 'Backlog') {
        $waited = Wait-ForNewSession -Id $id -Until $until
        $shiftEnd = $shiftEnd.Add($waited)
        Write-Trace '-' 'shift' "waited $(Format-Span $waited) for tokens; shift now ends $($shiftEnd.ToString('HH:mm'))" 'Cyan'
        $resumeId = $id
        continue
    }
    # A run that died on the API without naming the usage limit gets the same treatment,
    # three times in a row at most: wait until Claude answers, then run it again.
    if ($state -eq 'Doing' -and (Test-ApiFailure $run) -and $apiRetries -lt 3) {
        $apiRetries++
        $waited = Wait-ForTokensByProbe -Id $id
        $shiftEnd = $shiftEnd.Add($waited)
        $resumeId = $id
        continue
    }
    $apiRetries = 0

    # A run that reached the turn cap is too big for one run (BL-3001): it is split now, like one
    # that reached its cost cap, not sent back to Backlog for the next claim to split (ADR-1155:
    # BL-4112, BL-4328 and BL-4341 went back that way on 2026-10-29 and 2026-10-30).
    $turnCapped = $state -eq 'Doing' -and (Test-TurnCapReached)

    if ($state -eq 'Doing') {
        $why = if ($capWhy) { $capWhy }
            elseif ($turnCapped) { "stopped at its $MaxRunTurns-turn cap, so the task is too big for one run" }
            elseif ($run.TimedOut -and $inOvertime) { "timed out after $TaskMinutes min and again after $overtimeMinutes min of overtime" }
            elseif ($run.TimedOut) { "timed out after $TaskMinutes min" }
            elseif (Test-BudgetSpent) { "stopped at its $($script:RunBudgetUsd) US dollar cost cap (4 times the median run, at least $3, at most -TaskBudgetUsd, less its runs of the last day), so split the task" } else { "run ended in Doing, exit $($run.ExitCode)" }
        Save-StrayChanges $id
        # A task stopped by its cost cap is split by one bounded run (BL-3091), which defers or
        # blocks it itself; one already offered for a split today is blocked as before.
        $capped = $capWhy -or $turnCapped -or (-not $run.TimedOut -and (Test-BudgetSpent))
        $cause = if ($turnCapped) { 'too big for one run' } else { 'over its cost cap' }
        $split = if ($capped) { Invoke-TaskSplit $id $why -Cause $cause -LeaveForIntegration:([bool]$Lane) } else { 'skipped' }
        if (Test-TaskSplitLeavesBlockToCaller $split) {
            $seeLog = if ($capWhy) { Join-Path $LogDir "$id-*.jsonl" } else { Join-Path $LogDir "$id-$Stamp$LaneTag.jsonl" }
            Invoke-Board @('move', '-Id', $id, '-To', 'Blocked', '-Reason', "Stewart: dark factory $why; see $seeLog") | Out-Null
            git -C $Root add -A Tasks 2>&1 | Out-Null
            git -C $Root commit -q -m "chore(tasks): block $id - dark factory $why" 2>&1 | Out-Null
            if (-not $Lane) { git -C $Root push -q 2>&1 | Out-Null }
        }
        $state = Get-TaskState $id
    }
    # A task a run sent back to Backlog a second time is split now rather than offered a third
    # claim (ADR-1155, AF-0465 to AF-0474: 64 requeues on 2026-10-29 and 2026-10-30, and BL-4124,
    # BL-4132 and BL-4137 claimed 3 times). A split that cannot run blocks it for Stewart to triage.
    if ($state -eq 'Backlog' -and (Get-TaskRequeueCount (Get-TaskFileLines $id)) -ge 2) {
        $why = "sent back to Backlog a second time, last for: $(Get-Short (Get-LastLogLine $id) 160)"
        Save-StrayChanges $id
        $split = Invoke-TaskSplit $id $why -Cause 'sent back to Backlog twice' -LeaveForIntegration:([bool]$Lane)
        if (Test-TaskSplitLeavesBlockToCaller $split) {
            Invoke-Board @('move', '-Id', $id, '-To', 'Blocked', '-Reason', "Stewart: dark factory $why; the automatic split returned '$split', so triage it rather than claim it a third time") | Out-Null
            git -C $Root add -A Tasks 2>&1 | Out-Null
            git -C $Root commit -q -m "chore(tasks): block $id - sent back to Backlog twice and not split" 2>&1 | Out-Null
            if (-not $Lane) { git -C $Root push -q 2>&1 | Out-Null }
        }
        $state = Get-TaskState $id
    }
    Register-PrerequisiteRequeueRun $id $state
    Save-StrayChanges $id

    if ($Lane -and $state -in 'Done', 'Blocked', 'Deferred', 'Backlog') {
        $problem = Invoke-Integrate -Id $id -State $state
        if ($problem) {
            $unpushed = Invoke-Park -Id $id -Why $problem
            if ($unpushed) { $parkLines += $unpushed; Write-Trace $id 'PARKED' "$problem; the move to Backlog was not pushed either" 'Red' }
            else { Write-Trace $id 'PARKED' "$problem; back to Backlog" 'Yellow' }
            $state = 'Parked'
        } else {
            Write-Trace $id 'push' "integrated into $branch"
        }
    }

    if ($Lane) { Set-LaneState 'task' '' }

    if ($state -eq 'Done') {
        $done++; $failStreak = 0
        $outcome = 'done'
        Write-Trace $id 'DONE' (Get-Short (Get-LastLogLine $id)) 'Green'
    } elseif ($state -eq 'Blocked') {
        $blocked++
        # A task blocked at its spent cap was never run, so it says nothing about the runs.
        if ($capWhy) { } elseif ($null -eq $script:RunResult -or $run.TimedOut) { $failStreak++ } else { $failStreak = 0 }
        $outcome = 'BLOCKED (see Tasks\Blocked)'
        Write-Trace $id 'BLOCKED' (Get-Short (Get-LastLogLine $id)) 'Yellow'
    } elseif ($state -eq 'Deferred') {
        # Deferred is a valid /task-run outcome (the task needs nothing more, or cannot be
        # done yet): integrated like Blocked, reported, and no sign the runs are failing.
        $deferred++; $failStreak = 0
        $outcome = 'DEFERRED (see Tasks\Deferred)'
        Write-Trace $id 'DEFERRED' (Get-Short (Get-LastLogLine $id)) 'Yellow'
    } elseif ($state -in 'Backlog', 'Parked') {
        # Waiting on other tasks, a widened touches, or work that would not integrate: back
        # in the queue for a later run, not a stall.
        $requeued++; $failStreak = 0
        $outcome = 'requeued'
        if ($Lane) { Add-ShiftRequeued $id }
        Write-Trace $id 'REQUEUE' (Get-Short (Get-LastLogLine $id)) 'Yellow'
    } else {
        $failStreak++
        $stalls += "$id STALLED  ended in $state, exit $($run.ExitCode)"
        $outcome = "STALLED (ended in $state)"
        Write-Trace $id 'STALL' "ended in $state, exit $($run.ExitCode)" 'Red'
    }
    # The run's output is in its .jsonl log and the trace; the tab shows only what is next.
    Show-LaneEmpty "$id $outcome at $(Get-Date -Format 'HH:mm'); empty, waiting for the next task. Trace: $TraceFile"
  } catch {
    # A lane that dies after its run sent the task back never reached Add-ShiftRequeued, and
    # the lane restarted in its place starts with no memory of it (AF-0091: BL-1850 claimed 5
    # times on lane 9 of one shift).
    if ($Lane -and $id -and (Get-TaskState $id) -in 'Backlog', 'Parked') { Add-ShiftRequeued $id }
    # A lane whose script failed with its task still in Doing carries on with that task from
    # the work in place, as the next shift would, instead of ending the shift with it
    # stranded in Doing (BL-2036: BL-2035 sat there 1.5 hours).
    if ($Lane -and $id -and $scriptErrorResumes -lt 2 -and (Get-TaskState $id) -eq 'Doing') {
        $scriptErrorResumes++
        Write-Trace $id 'ERROR' "$(Get-Short $_.Exception.Message); resuming the task ($scriptErrorResumes of 2)" 'Red'
        $resumeId = $id
        continue
    }
    $stopWhy = 'script error'
    $stalls += "FACTORY SCRIPT ERROR  $($_.Exception.Message)"
    Write-Trace '-' 'ERROR' (Get-Short $_.Exception.Message) 'Red'
    break
  }
}

Write-Trace '-' 'shift' "end ($stopWhy)  done=$done blocked=$blocked deferred=$deferred requeued=$requeued stalled=$($stalls.Count)" 'Cyan'
Write-Trace '-' 'shift' (Get-ModelCostSummary $LogDir "^BL-\d+-$Stamp$LaneTag[-.]") 'Cyan'
if ($stopWhy -eq 'runs failing') { $stalls = @('FACTORY STALLED - two runs in a row failed; check ' + $LogDir) + $stalls }
Set-HeartbeatTask ''
Write-Heartbeat 'finished' (Get-Short $stopWhy 80)
if (-not $Lane) { Publish-BoardStatus -Branch $branch -State 'ended' }

if ($Lane) {
    # The coordinator raises one alarm for every lane; a lane only reports.
    # Every line after SUMMARY reaches the coordinator's end-of-shift report: stalls, and
    # parks whose move to Backlog was never pushed (BL-1071).
    Write-LaneSummary (@("SUMMARY lane $Lane ended ($stopWhy): done=$done blocked=$blocked deferred=$deferred requeued=$requeued stalled=$($stalls.Count)  $(Get-ModelCostSummary $LogDir "^BL-\d+-$Stamp$LaneTag[-.]")") + $stalls + $parkLines)
    # A clean lane leaves an empty tab to close; one that blocked, stalled or could not park says read.
    $endLine = "Lane $Lane ended at $(Get-Date -Format 'HH:mm') ($stopWhy): done=$done blocked=$blocked deferred=$deferred requeued=$requeued stalled=$($stalls.Count)"
    if (-not $stalls.Count -and -not $parkLines.Count -and -not $blocked) { Show-LaneEmpty $endLine 'empty, close' }
    else { Show-LaneEmpty (@($endLine) + $stalls + $parkLines -join "`n") $(if ($stalls.Count -or $parkLines.Count) { 'STALLED, read' } else { 'BLOCKED, read' }) }
    try { $Host.UI.RawUI.WindowTitle = "Dark factory - lane $Lane finished" } catch { }
    exit 0
}

$reasons = @($stalls) + @(Get-WaitingOnStewart)
if ($reasons.Count -gt 0) {
    Invoke-Alarm -Reasons $reasons
    exit 2
}
try { $Host.UI.RawUI.WindowTitle = 'Dark factory - shift complete' } catch { }
exit 0
