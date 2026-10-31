# The lane coordinator: a shift of more than one lane (or -Lanes Auto) starts, sizes,
# watches and restarts its lanes, then ends the shift. Dot-sourced by RunDarkFactory.ps1 in
# its order; see the loader there.

function Get-IdlePollDecision {
    # One look of an idle -Continuous coordinator's poll (BL-3092): 'stop' past the -Hours
    # -Deadline or while -UsageStop (Get-UsageStop's reason) says no shift may start, 'start'
    # when -ClaimableId names a task claimable now, else 'wait' - also when only cost-capped
    # tasks (-CappedIds) are ready. Why is the trace text. Pure: the clock, the usage and the
    # board are its arguments, so -TestHeartbeat drives it.
    param([datetime]$Now, [datetime]$Deadline, [string]$UsageStop, [string]$ClaimableId, [string[]]$CappedIds = @())
    if ($Now -ge $Deadline) { return [pscustomobject]@{ Action = 'stop'; Why = "the -Hours deadline $($Deadline.ToString('HH:mm', [System.Globalization.CultureInfo]::InvariantCulture)) has passed" } }
    if ($UsageStop) { return [pscustomobject]@{ Action = 'stop'; Why = $UsageStop } }
    if ($ClaimableId) { return [pscustomobject]@{ Action = 'start'; Why = "$ClaimableId is claimable" } }
    $why = if ($CappedIds.Count) { "only cost-capped tasks ready ($($CappedIds -join ', '))" } else { 'nothing claimable' }
    return [pscustomobject]@{ Action = 'wait'; Why = $why }
}

function Get-MergeWaitClaimableTaskId {
    # Get-ClaimableTaskId for the poll during the shift-end merge's CI wait (BL-3142), leaving
    # out the tasks this shift's lanes sent back to Backlog (Get-ShiftRequeued): a lane requeues
    # one that waits for this very merge to put another task's commit on master, so it is not
    # claimable until the merge lands, and starting a shift for it only repeats the wasted run
    # (AF-0369: BL-3151 was claimed twice). The next shift tries it afresh. -OfferNext and
    # -NotClaimableWhy pass through to Get-ClaimableTaskId.
    param([scriptblock]$OfferNext, [scriptblock]$NotClaimableWhy)
    $ask = @{ Skip = @(Get-ShiftRequeued) }
    if ($OfferNext) { $ask.OfferNext = $OfferNext }
    if ($NotClaimableWhy) { $ask.NotClaimableWhy = $NotClaimableWhy }
    return Get-ClaimableTaskId @ask
}

function Start-NextShiftDetached {
    # Starts the next shift with Start-Detached and says whether it really started: Started is
    # true only when a herdr tab or a process came back. A start that throws, or returns neither,
    # leaves Started false and Failure saying why (BL-3154), so a hand-over is never counted
    # for a shift that is not running.
    param([string]$Label, [string]$Dir, [string[]]$ScriptArgs)
    $next = $null
    $failure = $null
    try { $next = @(Start-Detached -Label $Label -Dir $Dir -ScriptArgs $ScriptArgs | Where-Object { $_ -is [hashtable] })[-1] }
    catch { $failure = "$($_.Exception.Message)" }
    if (-not $failure -and -not ($next -and ($next.Tab -or $next.Process))) { $failure = 'neither a herdr tab nor a process was started' }
    return @{ Started = (-not $failure); Tab = $(if ($next) { $next.Tab }); Process = $(if ($next) { $next.Process }); Failure = $failure }
}

function Invoke-ShiftStartSync {
    # The shift-start sync (-Sync, Sync-CheckoutWithOrigin unless a self-test stands in for
    # it) and whether the commits it brought in change the factory's own code,
    # RunDarkFactory.ps1 or a file under DarkFactory\ (BL-4386): this process dot-sourced the
    # old code before the sync, so a -Restart straight after a push ran the code from before
    # it (2026-10-31, BL-4384's CI-watch fix). Refusal is the sync's refusal text or $null;
    # Restart is the trace text for a restart on the new code, or $null when the sync left
    # HEAD where it was or brought in no factory code.
    param([string]$Repo, [string]$Branch, [scriptblock]$Sync = { param($r, $b) Sync-CheckoutWithOrigin -Repo $r -Branch $b })
    $before = "$(git -C $Repo rev-parse HEAD)".Trim()
    $refusal = & $Sync $Repo $Branch
    $after = "$(git -C $Repo rev-parse HEAD)".Trim()
    $restart = $null
    if (-not $refusal -and $before -ne $after) {
        $changed = @(git -C $Repo diff --name-only $before $after -- RunDarkFactory.ps1 DarkFactory | Where-Object { $_ })
        if ($changed.Count) { $restart = "factory code changed $($before.Substring(0, 9))..$($after.Substring(0, 9)); restarting on it" }
    }
    return [pscustomobject]@{ Refusal = $refusal; Restart = $restart }
}

function Write-IdlePollHeartbeat {
    # The heartbeat of a coordinator polling an idle board: phase 'idle', step 'polling'. A
    # coordinator writes none while it has lanes; once they have ended it writes this one, as
    # lane 0, which Write-Heartbeat publishes on the board branch every -HeartbeatMinutes.
    $script:WritesHeartbeat = $true
    Write-Heartbeat 'idle' 'polling'
}

if ($TestCoordinatorWatchdog) {
    # The stale-stamp decision, the stamp file, which processes a replacement stops, and a
    # timed external call, with no shift, lane or network.
    $results = [System.Collections.Generic.List[object]]::new()
    $check = { param([string]$Name, [bool]$Ok, [string]$Got) $results.Add($Ok); Write-Host "$(if ($Ok) { 'PASS' } else { 'FAIL' }) ${Name}: $Got" -ForegroundColor $(if ($Ok) { 'Green' } else { 'Red' }) }
    $t0 = [datetime]'2026-10-30T12:00:00Z'
    $end = $t0.AddHours(2)
    $d = Get-CoordinatorWatchdogDecision -Now $t0.AddMinutes(8) -StampAt $t0 -ShiftEnd $end -HeartbeatMinutes 3
    & $check 'a stamp under 3 heartbeats old leaves the coordinator alone' ($d.Action -ceq 'none') "$($d.Action) ($($d.Why))"
    $d = Get-CoordinatorWatchdogDecision -Now $t0.AddMinutes(9) -StampAt $t0 -ShiftEnd $end -HeartbeatMinutes 3
    & $check 'a stamp 3 heartbeats old replaces the coordinator' ($d.Action -ceq 'replace') "$($d.Action) ($($d.Why))"
    $d = Get-CoordinatorWatchdogDecision -Now $t0.AddMinutes(9) -StampAt $t0 -ShiftEnd $end -HeartbeatMinutes 0
    & $check 'with publishing off a heartbeat counts as 3 minutes' ($d.Action -ceq 'replace') "$($d.Action)"
    $d = Get-CoordinatorWatchdogDecision -Now $t0.AddMinutes(9) -StampAt $t0 -ShiftEnd $end -HeartbeatMinutes 5
    & $check 'the interval follows -HeartbeatMinutes' ($d.Action -ceq 'none') "$($d.Action)"
    $d = Get-CoordinatorWatchdogDecision -Now $t0.AddMinutes(30) -StampAt $null -ShiftEnd $end -HeartbeatMinutes 3
    & $check 'no stamp, no replacement' ($d.Action -ceq 'none') "$($d.Action)"
    $d = Get-CoordinatorWatchdogDecision -Now $end.AddMinutes(1) -StampAt $t0 -ShiftEnd $end -HeartbeatMinutes 3
    & $check 'a coordinator is not replaced once the shift is over' ($d.Action -ceq 'none') "$($d.Action)"
    $d = Get-CoordinatorWatchdogDecision -Now $t0.AddMinutes(30) -StampAt $t0 -ShiftEnd $end -HeartbeatMinutes 3 -ReplacingSince $t0.AddMinutes(25)
    & $check 'a replacement started 5 minutes ago is waited for' ($d.Action -ceq 'none') "$($d.Action) ($($d.Why))"
    $d = Get-CoordinatorWatchdogDecision -Now $t0.AddMinutes(30) -StampAt $t0 -ShiftEnd $end -HeartbeatMinutes 3 -ReplacingSince $t0.AddMinutes(10)
    & $check 'a replacement that never came up is replaced again' ($d.Action -ceq 'replace') "$($d.Action)"
    $scratch = Join-Path ([IO.Path]::GetTempPath()) "coordinator-watchdog-$PID"
    New-Item -ItemType Directory -Force -Path $scratch | Out-Null
    try {
        $stampFile = Join-Path $scratch 'coordinator.stamp'
        $script:CoordinatorArguments = '-Lanes Auto -Continuous'
        Write-CoordinatorStamp -Path $stampFile -End $end
        $fields = Read-CoordinatorStamp $stampFile
        $back = ConvertFrom-UtcStamp $fields['shiftEnd']
        & $check 'the stamp holds the pid, the arguments and the shift end' ($fields['pid'] -ceq "$PID" -and $fields['args'] -ceq '-Lanes Auto -Continuous' -and $back -eq $end.ToUniversalTime() -and (ConvertFrom-UtcStamp $fields['at'])) "pid=$($fields['pid']) args=$($fields['args']) shiftEnd=$($fields['shiftEnd'])"
        & $check 'the stamp holds the heartbeat interval the lanes use' ($fields['heartbeat'] -ceq "$HeartbeatMinutes") "heartbeat=$($fields['heartbeat'])"
        $script:CoordinatorStampFailing = $false
        $blocker = Join-Path $scratch 'afile'
        [System.IO.File]::WriteAllText($blocker, 'x')
        Write-CoordinatorStamp -Path (Join-Path $blocker 'coordinator.stamp') -End $end
        & $check 'a failed stamp write is traced once' ($script:CoordinatorStampFailing -eq $true) "failing=$($script:CoordinatorStampFailing)"
        Write-CoordinatorStamp -Path $stampFile -End $end
        & $check 'a stamp write that works again clears the failure' ($script:CoordinatorStampFailing -eq $false) "failing=$($script:CoordinatorStampFailing)"
        & $check 'a missing stamp reads as empty' ((Read-CoordinatorStamp (Join-Path $scratch 'none')).Count -eq 0) 'empty'
    } finally { Remove-Item $scratch -Recurse -Force -ErrorAction SilentlyContinue }
    $table = @(
        [pscustomobject]@{ ProcessId = 10; ParentProcessId = 1; CommandLine = 'powershell -File RunDarkFactory.ps1 -Lanes 2' },
        [pscustomobject]@{ ProcessId = 11; ParentProcessId = 10; CommandLine = 'git push origin x' },
        [pscustomobject]@{ ProcessId = 14; ParentProcessId = 11; CommandLine = 'git-remote-https' },
        [pscustomobject]@{ ProcessId = 12; ParentProcessId = 10; CommandLine = 'powershell -File RunDarkFactory.ps1 -Lane 1 -Branch b' },
        [pscustomobject]@{ ProcessId = 13; ParentProcessId = 12; CommandLine = 'dotnet build' })
    $stop = (Get-CoordinatorOwnProcessIds -Table $table -CoordinatorId 10) -join ','
    & $check 'a replacement stops what the coordinator started, deepest first, and no lane' ($stop -ceq '14,11') $stop
    & $check 'git push and gh calls are timed' ((Test-TimedExternalCall 'git' @('-C', 'x y', 'push', '-q')) -and (Test-TimedExternalCall 'gh' @('run', 'list')) -and (Test-TimedExternalCall 'git' @('fetch'))) 'timed'
    $native = (ConvertTo-NativeCallArguments @('run', 'list', '--json', @('databaseId', 'status'), '--limit', 1)) -join '|'
    & $check 'a timed call passes an unquoted a,b as "a,b", as to a native command (BL-4377)' ($native -ceq 'run|list|--json|databaseId,status|--limit|1') $native
    & $check 'local git calls are not timed' (-not (Test-TimedExternalCall 'git' @('-C', 'x', 'status')) -and -not (Test-TimedExternalCall 'git' @())) 'not timed'
    $shell = Get-FactoryPowerShellExe
    $fast = Invoke-ExternalWithTimeout -FilePath $shell -CallArgs @('-NoProfile', '-Command', "'a b'; [Console]::Error.WriteLine('oops'); exit 3") -Seconds 60
    & $check 'a call that answers returns its output, errors and exit code' (-not $fast.TimedOut -and $fast.ExitCode -eq 3 -and ($fast.Output -join '|') -ceq 'a b' -and ($fast.Errors -join '|') -ceq 'oops') "exit $($fast.ExitCode) out=$($fast.Output -join '|') err=$($fast.Errors -join '|')"
    $watch = [System.Diagnostics.Stopwatch]::StartNew()
    $slow = Invoke-ExternalWithTimeout -FilePath $shell -CallArgs @('-NoProfile', '-Command', 'Start-Sleep 60') -Seconds 2
    & $check 'a call that never answers is stopped at the timeout' ($slow.TimedOut -and $slow.ExitCode -eq 124 -and $watch.Elapsed.TotalSeconds -lt 30) "timed out=$($slow.TimedOut) after $([int]$watch.Elapsed.TotalSeconds)s"
    # The shift-start sync, on a throwaway repository, with a stand-in for the fetch and
    # fast-forward that commits what a push would have brought in (BL-4386).
    $repo = Join-Path ([IO.Path]::GetTempPath()) "coordinator-sync-$PID"
    New-Item -ItemType Directory -Force -Path (Join-Path $repo 'DarkFactory') | Out-Null
    try {
        $commit = { param([string]$Path) Set-Content (Join-Path $repo $Path) "$(Get-Random)" -Encoding UTF8; git -C $repo add -A 2>&1 | Out-Null; git -C $repo -c user.name=t -c user.email=t@t -c commit.gpgsign=false commit -q -m $Path 2>&1 | Out-Null }
        git -C $repo init -q 2>&1 | Out-Null
        & $commit 'README.md'
        $none = Invoke-ShiftStartSync -Repo $repo -Branch 'x' -Sync { param($r, $b) $null }
        & $check 'a sync that moves nothing starts the shift as it is' (-not $none.Refusal -and -not $none.Restart) "restart=$($none.Restart)"
        $other = Invoke-ShiftStartSync -Repo $repo -Branch 'x' -Sync { param($r, $b) & $commit 'README.md'; $null }
        & $check 'a sync that brings in no factory code starts the shift as it is' (-not $other.Refusal -and -not $other.Restart) "restart=$($other.Restart)"
        $from = "$(git -C $repo rev-parse HEAD)".Trim()
        $area = Invoke-ShiftStartSync -Repo $repo -Branch 'x' -Sync { param($r, $b) & $commit 'DarkFactory/CiWatch.ps1'; $null }
        $to = "$(git -C $repo rev-parse HEAD)".Trim()
        & $check 'a sync that brings in a DarkFactory file restarts on it, naming both commits' ($area.Restart -ceq "factory code changed $($from.Substring(0, 9))..$($to.Substring(0, 9)); restarting on it") "$($area.Restart)"
        $main = Invoke-ShiftStartSync -Repo $repo -Branch 'x' -Sync { param($r, $b) & $commit 'RunDarkFactory.ps1'; $null }
        & $check 'a sync that brings in RunDarkFactory.ps1 restarts on it' ($main.Restart -match '^factory code changed \w{9}\.\.\w{9}; restarting on it$') "$($main.Restart)"
        $refused = Invoke-ShiftStartSync -Repo $repo -Branch 'x' -Sync { param($r, $b) 'x has diverged from origin/x; push or reconcile first' }
        & $check 'a refused sync is refused, not restarted' ($refused.Refusal -ceq 'x has diverged from origin/x; push or reconcile first' -and -not $refused.Restart) "$($refused.Refusal)"
    } finally { Remove-Item $repo -Recurse -Force -ErrorAction SilentlyContinue }
    exit $(if ($results -contains $false) { 1 } else { 0 })
}

if (($AutoLanes -or $LaneCount -gt 1) -and -not $Lane) {
    try { $Host.UI.RawUI.WindowTitle = "Dark factory - $(if ($AutoLanes) { 'auto' } else { $LaneCount }) lanes" } catch { }
    # The gh and git calls that wait on a network stop at a timeout instead of hanging the loop (BL-4353).
    Enable-ExternalCallTimeouts
    if ($ShiftBranch) { $moved = Restore-ShiftBranch -Branch $ShiftBranch; if ($moved) { Write-Trace '-' 'branch' $moved 'Red' } }
    $branch = (git -C $Root rev-parse --abbrev-ref HEAD).Trim()
    if ($branch -in 'master', 'main') { Stop-ShiftStart "on $branch; switch to a feature branch"; exit 1 }
    if (Wait-ForCleanTree) { Stop-ShiftStart 'working tree not clean; commit or stash first'; exit 1 }
    git -C $Root fetch -q origin $branch
    $sync = Invoke-ShiftStartSync -Repo $Root -Branch $branch
    if ($sync.Refusal) { Stop-ShiftStart $sync.Refusal; exit 1 }
    if ($sync.Restart) {
        # Before anything is claimed, merged or watched, this same process and console run
        # the script again, which dot-sources the new code; its own sync then finds nothing
        # to bring in. It keeps this shift's -ShiftStamp, so it goes on in this trace log. The
        # git and gh wrappers go first: they would outlive this scope and read the new run's
        # tool table before it fills it.
        Write-Trace '-' 'sync' $sync.Restart
        $again = @{}
        foreach ($name in $FactoryParameters.Keys) { $again[$name] = $FactoryParameters[$name] }
        $again['ShiftStamp'] = $Stamp
        Remove-Item function:git, function:gh -ErrorAction SilentlyContinue
        & $FactoryScript @again
        exit $LASTEXITCODE
    }
    # Tasks in Doing are only allowed when a previous shift's lane holds each of them and that
    # lane is dead - a shift stopped mid-task, or killed while waiting for tokens. Those lanes
    # are adopted: their worktrees are left as they are and they resume the task. An orphan,
    # held by no lane, is settled by Get-OrphanAction (BL-1071). An interactive-only task
    # (lane: no) in Doing is an interactive session's, so it is left alone (BL-2069).
    $stuck = @(Get-ChildItem (Join-Path $Root 'Tasks\Doing') -Filter 'BL-*.md' -ErrorAction SilentlyContinue | Where-Object { -not (Test-InteractiveOnlyTask $_.FullName) } | ForEach-Object { Get-TaskIdFromFileName $_.Name })
    $adopt = @{}
    # A coordinator taking over a shift (-TakeOverShift) keeps that shift's running lanes, so
    # nothing in Doing is a previous shift's to adopt.
    if ($stuck.Count -and -not $TakeOverShift) {
        $previous = Get-ChildItem $LogDir -Directory -Filter 'lanes-*' -ErrorAction SilentlyContinue | Sort-Object Name | Select-Object -Last 1
        if ($previous) {
            $prevStamp = $previous.Name.Substring(6)
            foreach ($n in 1..16) {
                $held = Get-LaneState $n 'task' $prevStamp
                if (-not $held -or $stuck -notcontains $held) { continue }
                if ((Get-LaneState $n 'pid' $prevStamp) -and (Test-LaneAlive $n $prevStamp)) { Stop-ShiftStart "lane $n of shift $prevStamp is still running $held"; exit 1 }
                $adopt[$n] = $held
            }
        }
        $orphans = @($stuck | Where-Object { $adopt.Values -notcontains $_ })
        if ($orphans.Count) {
            $dirty = @(1..16 | Where-Object { -not $adopt.ContainsKey($_) -and (Test-WorktreeDirty (Join-Path $LanesDir "lane-$_")) })
            $returned = @()
            $laneBranchesAhead = @(Get-LaneBranchesAhead $branch $Root)
            foreach ($id in $orphans) {
                $action = Get-OrphanAction -Id $id -ParkedBranches (Get-ParkedBranches $id) -DirtyLanes $dirty -LaneBranchesAhead $laneBranchesAhead
                if ($action.Action -eq 'refuse') { Stop-ShiftStart $action.Why; exit 1 }
                if ($action.Action -eq 'adopt') {
                    $adopt[$action.Lane] = $id
                    $dirty = @($dirty | Where-Object { $_ -ne $action.Lane })
                    Write-Trace $id 'adopt' "held by no lane; lane $($action.Lane)'s worktree has uncommitted work, so that lane resumes it" 'Yellow'
                    continue
                }
                $returnReason = if ($action.Branch) { "Returned from Doing at shift start: no lane held it and no lane worktree had uncommitted work. The work is on branch $($action.Branch); start with git cherry-pick --no-commit $($action.Branch) and fix it." }
                    else { 'Returned from Doing at shift start: no lane held it and no work for it exists anywhere (no uncommitted work, no parked branch, no lane branch ahead of the shift branch).' }
                Invoke-Board @('move', '-Id', $id, '-To', 'Backlog', '-Reason', $returnReason) | Out-Null
                if ((Get-TaskState $id) -ne 'Backlog') { Stop-ShiftStart "$id is in Doing and held by no lane, and moving it to Backlog failed"; exit 1 }
                $returned += $id
                Write-Trace $id 'requeue' "held by no lane; back to Backlog$(if ($action.Branch) { ", work on $($action.Branch)" } else { ', no work for it anywhere' })" 'Yellow'
            }
            if ($returned.Count) {
                git -C $Root add -A Tasks 2>&1 | Out-Null
                git -C $Root commit -q -m "chore(tasks): return $($returned -join ', ') to Backlog - in Doing and held by no lane" -m $SkipCiNote 2>&1 | Out-Null
                # The push takes the integrate lock like every lane's, so it cannot land in the
                # middle of a lane's verify and cost that lane its push (BL-3060).
                $boardLock = Enter-Lock
                try { git -C $Root push -q origin "HEAD:$branch" 2>&1 | Out-Null } finally { $boardLock.Dispose() }
                if ($LASTEXITCODE -ne 0) {
                    git -C $Root reset -q --hard "origin/$branch" 2>&1 | Out-Null
                    Stop-ShiftStart "$($returned -join ', ') in Doing and held by no lane; returning them to Backlog could not be pushed to origin/$branch"; exit 1
                }
            }
        }
        if (@($adopt.Keys | Where-Object { $_ -gt $LaneCount }).Count) { $LaneCount = [int]($adopt.Keys | Measure-Object -Maximum).Maximum }
    }

    function Get-ShiftCapacity {
        # The board's capacity on origin/<branch>, read in the auto-board worktree; the
        # coordinator's own checkout is not pulled. -Fallback when it cannot be read, traced once.
        param([int]$Fallback)
        git -C $Root fetch -q origin $branch 2>&1 | Out-Null
        $board = Get-BoardCapacity -Ref "origin/$branch"
        if ($null -ne $board.Capacity) { return $board.Capacity }
        if (-not $script:Auto.CapacityTraced) {
            $script:Auto.CapacityTraced = $true
            Write-Trace '-' 'lanes' "cannot read the board's capacity ($($board.Why)); using $Fallback" 'Yellow'
        }
        return $Fallback
    }

    if ($AutoLanes) {
        # Sized before any lane starts: the machine cap, probed now while nothing builds if
        # machine-lanes.json no longer describes this PC, then the start count from the last
        # Auto shift, capped by the ceilings. Adopted lanes raise it, as with a fixed count.
        $machine = Get-MachineCap -Probe
        $script:Auto.MachineCap = $machine.Cap
        Write-Trace '-' 'lanes' $machine.Text 'Cyan'
        $script:Auto.Saved = Read-JsonFile $AutoLanesFile
        $start = Get-AutoStartCount -Saved $script:Auto.Saved -Capacity (Get-ShiftCapacity -Fallback $MaxLanes) -MachineCap $machine.Cap
        $why = $start.Why
        if ($LaneCount -gt $start.Count) { $why = "lane $LaneCount adopted from the previous shift" }
        $LaneCount = [math]::Max($LaneCount, $start.Count)
        Write-Trace '-' 'lanes' "lanes auto: starting at $LaneCount ($why)" 'Cyan'
        Set-AutoLanesStatus -Lanes $LaneCount -Target $null -Binding 'no burn rate' -Reason "lanes auto: starting at $LaneCount ($why)"
    } elseif ($LaneCount -gt 1) {
        # A fixed count starts no more lanes than the board can run at once (BL-1384, AF-0041),
        # so lanes do not open the shift polling "every ready task overlaps one in Doing";
        # capacity steps add the rest, one every 5 minutes, as the capacity rises.
        $adoptedMax = if ($adopt.Count) { [int]($adopt.Keys | Measure-Object -Maximum).Maximum } else { 0 }
        $start = Get-FixedStartCount -Requested ([int]$Lanes) -Capacity (Get-ShiftCapacity -Fallback ([int]$Lanes)) -Adopted $adoptedMax
        $LaneCount = $start.Count
        Write-Trace '-' 'lanes' "lanes: starting at $LaneCount ($($start.Why))" 'Cyan'
    }

    # The previous shift stopped claiming work near the end of its session; this one starts
    # on a fresh session, and the wait does not count against -Hours.
    # Known from here, so the wait for a fresh session below keeps watching CI (BL-1031).
    $script:CiWatchBranch = $branch
    $weekly = Wait-ForFreshSession
    if ($weekly) { Write-Trace '-' 'shift' $weekly 'Red'; Set-OwnTabLabel 'ALARM, read'; Invoke-Alarm -Reasons @($weekly); exit 2 }
    $shiftEnd = (Get-Date).AddHours($Hours)

    Set-OwnTabLabel ''
    Write-Trace '-' 'shift' "start  $LaneCount lanes$(if ($AutoLanes) { ' (auto)' })  branch=$branch model=$Model until $($shiftEnd.ToString('HH:mm'))  $LaneMarkerTrace" 'Cyan'
    New-Item -ItemType Directory -Force -Path $LanesDir | Out-Null
    New-Item -ItemType Directory -Force -Path (Join-Path $LogDir "lanes-$Stamp") | Out-Null
    # A lane gets the time left in the shift, not a fresh -Hours: -Lanes Auto adds and
    # restarts lanes mid-shift, and each must stop claiming when the shift's time is up.
    $laneArgsFor = {
        param([int]$N)
        $hoursLeft = [Math]::Max(0.01, ($shiftEnd - (Get-Date)).TotalHours).ToString([System.Globalization.CultureInfo]::InvariantCulture)
        @('-Lane', $N, '-Branch', $branch, '-Hours', $hoursLeft, '-MaxTasks', $MaxTasks,
          '-TaskMinutes', $TaskMinutes, '-TaskBudgetUsd', $TaskBudgetUsd.ToString([System.Globalization.CultureInfo]::InvariantCulture), '-Model', $Model, '-MaxRunTurns', $MaxRunTurns, '-LogRoot', "`"$LogDir`"", '-ShiftStamp', $Stamp)
    }
    $procs = @()
    $laneTabs = @{}
    $summaries = Join-Path $LogDir "lanes-$Stamp"
    # The lanes running now; scaling adds and retires lanes, so it changes size mid-shift.
    $activeLanes = New-Object System.Collections.Generic.List[int]
    # Every lane started this shift, retired ones included, for the end-of-shift report.
    $startedLanes = New-Object System.Collections.Generic.List[int]
    # Tabs already closed when their lane retired, so the shift end leaves them alone.
    $closedTabs = @{}
    # The non-SUMMARY lines of summaries a retired lane wrote before it was added again.
    $retiredLines = @()

    function Start-Lane {
        # Starts lane $N in its worktree $LanesDir\lane-<n> and records its process or tab.
        # An adopted lane, or a dead one restarted with -KeepWorktree, keeps its work in
        # place; any other gets a clean factory/lane-<n> at origin/<branch>. Returns where
        # it runs, e.g. "herdr tab 12" or "pid 4242".
        param([int]$N, [string]$Label = 'starting', [switch]$KeepWorktree)
        $dir = Join-Path $LanesDir "lane-$N"
        if ($adopt.ContainsKey($N)) {
            Set-Content -Path (Get-LaneStatePath $N 'task') -Value $adopt[$N] -Encoding ASCII
            Write-Trace '-' 'lane' "lane $N adopts $($adopt[$N]) from the previous shift, work in place"
            $adopt.Remove($N)
        } elseif (-not $KeepWorktree) {
            if (-not (Test-Path (Join-Path $dir '.git'))) {
                git -C $Root worktree add -q --detach $dir "origin/$branch" 2>&1 | Out-Null
            }
            git -C $dir stash push -q --include-untracked -m "darkfactory lane-$N before $Stamp" 2>&1 | Out-Null
            git -C $dir checkout -q -B "factory/lane-$N" "origin/$branch" 2>&1 | Out-Null
            git -C $dir reset -q --hard "origin/$branch" 2>&1 | Out-Null
        }
        $started = Start-Detached -Label (Get-LaneTabLabel $N $Label) -Dir $dir -ScriptArgs (& $laneArgsFor $N)
        $script:procs += $started
        $laneTabs[$N] = $started.Tab
        $closedTabs.Remove($N)
        if (-not $startedLanes.Contains($N)) { $startedLanes.Add($N) }
        if ($started.Tab) { return "herdr tab $($started.Tab)" }
        return "pid $($started.Process.Id)"
    }

    function Add-Lane {
        # Starts one more lane, the lowest free number up to 16, and returns it; $null when
        # 16 are active. A lane that retired earlier this shift has its summary kept for the
        # alarm and renamed out of the lane-*.txt set before it starts again.
        $n = Get-LaneToAdd -Active @($activeLanes) -Max 16
        if ($null -eq $n) { return $null }
        $summary = Join-Path $summaries "lane-$n.txt"
        if (Test-Path $summary) {
            $script:retiredLines += @(Get-Content $summary | Where-Object { $_.Trim() -and $_ -notmatch '^SUMMARY ' })
            Rename-Item -Path $summary -NewName "lane-$n.retired-$(Get-Date -Format 'HHmmss').log"
        }
        Remove-Item (Get-LaneStatePath $n 'retire') -ErrorAction SilentlyContinue
        Remove-Item (Get-LaneStatePath $n 'pid') -ErrorAction SilentlyContinue
        $where = Start-Lane -N $n
        $activeLanes.Add($n)
        Write-Trace '-' 'lane' "lane $n added ($where)"
        return $n
    }

    function Request-LaneRetire {
        # Asks the highest-numbered active lane not already retiring, an idle one first, to
        # stop after its current task, and returns it; $null when every active lane is
        # already retiring.
        $retiring = @($activeLanes | Where-Object { Test-Path (Get-LaneStatePath $_ 'retire') })
        $n = Get-LaneToRetire -Active @($activeLanes) -Retiring $retiring -Idle @(Get-IdleLanes)
        if ($null -eq $n) { return $null }
        Set-Content -Path (Get-LaneStatePath $n 'retire') -Value (Get-Date -Format 'yyyy-MM-dd HH:mm:ss') -Encoding ASCII
        Write-Trace '-' 'lane' "lane $n asked to retire after its current task"
        return $n
    }

    function Read-LaneSummary {
        # Lane $N's summary: its SUMMARY line, and the stall lines after it.
        param([int]$N)
        $laneStalls = @()
        $laneSummary = ''
        foreach ($line in Get-Content (Join-Path $summaries "lane-$N.txt")) {
            if ($line -match '^SUMMARY ') { $laneSummary = $line }
            elseif ($line.Trim()) { $laneStalls += $line }
        }
        return [pscustomobject]@{ Summary = $laneSummary; Stalls = $laneStalls }
    }

    function Test-LaneSummaryClean {
        # A lane that ended cleanly has nothing left to read in its tab. One that blocked a
        # task or stalled keeps its tab for Stewart.
        param($Report)
        return [bool]($Report.Summary -and -not $Report.Stalls.Count -and $Report.Summary -notmatch 'blocked=[1-9]')
    }

    function Invoke-AutoLaneStep {
        # One -Lanes Auto step (ADR-0130 items 2 to 5): sample the usage, meter the rates,
        # and add one lane or retire straight down to the step's count, highest lane numbers
        # first (BL-823). Below the unpaced floor it adds every lane up to it at once. Holds
        # without asking while the tokens are low or the shift's time is up. -Capacity is the
        # board's capacity when the caller has just read it. Then, when the board's capacity
        # bound two steps running, it may file one split task (Invoke-SplitHotspotFiling).
        param($Capacity = $null)
        $retiring = @($activeLanes | Where-Object { Test-Path (Get-LaneStatePath $_ 'retire') })
        $current = @($activeLanes | Where-Object { $retiring -notcontains $_ }).Count
        $low = Get-UsageStop
        $hold = if ($low) { "tokens low: $low" } elseif ((Get-Date) -gt $shiftEnd) { 'shift time up' } else { '' }
        if ($hold) {
            Write-Trace '-' 'lanes' "lanes $current held ($hold)" 'DarkGray'
            Set-AutoLanesStatus -Lanes $current -Target $script:AutoLanesStatus.target -Binding $script:AutoLanesStatus.binding
            return
        }
        $now = Get-Date
        $reading = Get-UsageReading -ThisShift
        if ($reading) {
            $busy = @($activeLanes | Where-Object { (Test-LaneAlive $_) -and (Get-LaneState $_ 'task') }).Count
            $script:Auto.Samples = @(@($script:Auto.Samples) + @(New-UsageSample -At $now -Reading $reading -ActiveLanes $busy) |
                Where-Object { $_.At -ge $now.AddHours(-3) })
        }
        $capacity = if ($null -ne $Capacity) { [int]$Capacity } else { Get-ShiftCapacity -Fallback $current }
        $step = Get-AutoLaneStep -Samples $script:Auto.Samples -Current $current -Capacity $capacity -MachineCap $script:Auto.MachineCap `
            -Saved $script:Auto.Saved -PreviousLow $script:Auto.LastLow
        $script:Auto.FiveHourRate = $step.FiveHourRate
        $script:Auto.WeeklyRate = $step.WeeklyRate
        $script:Auto.CapacityStreak = Get-CapacityStreak -Binding $step.Next.Binding -Previous ([int]$script:Auto.CapacityStreak)
        if ($script:Auto.CapacityStreak -ge 2) { Invoke-SplitHotspotFiling -Branch $branch }
        # A low step that retired a lane starts the count again; one that held arms the next.
        $script:Auto.LastLow = [bool]($step.Next.Low -and -not $step.Next.Changed)
        # status.json's autoLanes follows every step; the reason and time only a change.
        $target = if ($step.Pace) { $step.Next.Desired } else { $null }
        Set-AutoLanesStatus -Lanes $current -Target $target -Binding $step.Next.Binding
        if (-not $step.Next.Changed) { Write-Trace '-' 'lanes' $step.Next.Reason 'DarkGray'; return }
        Write-Trace '-' 'lanes' $step.Next.Reason 'Cyan'
        if ($step.Next.Lanes -gt $current) {
            # One lane above the unpaced floor; every lane up to the floor below it.
            $added = 0
            foreach ($i in 1..($step.Next.Lanes - $current)) {
                if ($added) { Start-Sleep -Seconds 15 }
                if ($null -eq (Add-Lane)) { break }
                $added++
            }
            if (-not $added) { Write-Trace '-' 'lanes' 'no lane to add' 'Yellow'; return }
            $script:LaneCount = $current + $added
        } else {
            # Request-LaneRetire traces each lane it asks; one that finds none left stops the run.
            $retired = 0
            foreach ($i in 1..($current - $step.Next.Lanes)) {
                if ($null -eq (Request-LaneRetire)) { break }
                $retired++
            }
            if (-not $retired) { Write-Trace '-' 'lanes' 'no lane to retire' 'Yellow'; return }
            $script:LaneCount = $current - $retired
        }
        try { $Host.UI.RawUI.WindowTitle = "Dark factory - $($script:LaneCount) lanes (auto)" } catch { }
        Set-AutoLanesStatus -Lanes $script:LaneCount -Target $target -Binding $step.Next.Binding -Reason $step.Next.Reason
        Save-AutoLanes $script:LaneCount
    }

    function Get-IdleLanes {
        # The active lanes holding no task: claiming, waiting on overlapping touches, or starting.
        return @($activeLanes | Where-Object { -not (Get-LaneState $_ 'task') })
    }

    function Invoke-CapacityLaneStep {
        # One capacity step of a fixed -Lanes N shift (BL-1374, AF-0032): idle lanes beyond
        # the board's capacity retire, and lanes come back one a step, up to N, as it rises.
        $retiring = @($activeLanes | Where-Object { Test-Path (Get-LaneStatePath $_ 'retire') })
        $current = @($activeLanes | Where-Object { $retiring -notcontains $_ }).Count
        if ((Get-UsageStop) -or (Get-Date) -gt $shiftEnd) { return }
        $capacity = Get-ShiftCapacity -Fallback $current
        $idle = @(Get-IdleLanes | Where-Object { $retiring -notcontains $_ })
        $step = Get-CapacityLaneCount -Current $current -Capacity $capacity -Requested $requestedLanes -Idle $idle.Count
        if (-not $step.Changed) { return }
        Write-Trace '-' 'lanes' $step.Reason 'Cyan'
        if ($step.Lanes -gt $current) {
            if ($null -eq (Add-Lane)) { Write-Trace '-' 'lanes' 'no lane to add' 'Yellow' }
            return
        }
        foreach ($i in 1..($current - $step.Lanes)) { if ($null -eq (Request-LaneRetire)) { break } }
    }

    $script:CiFixSeen = @{}
    $script:CiFixLaneAdded = @{}
    function Invoke-CiFixLaneStep {
        # Once a minute: a CI fix that waits in Backlog on origin with every lane busy gets a
        # lane of its own (Get-CiFixLaneDecision, ADR-1158), whose first claim takes it, since a
        # claim takes a ready CI fix before anything else (Get-ReadyCiFixTaskId). One whose
        # depends-on names a live task waits for it instead. Not past the shift's time or
        # while it waits for tokens.
        if ((Get-Date) -gt $shiftEnd -or (Test-WaitingForSession)) { return }
        git -C $Root fetch -q origin $branch 2>&1 | Out-Null
        if ($LASTEXITCODE -ne 0) { return }
        $live = @{}
        foreach ($path in @(git -C $Root ls-tree -r --name-only "origin/$branch" -- Tasks/Backlog Tasks/Doing Tasks/Blocked Tasks/Deferred 2>$null)) {
            $name = Split-Path "$path" -Leaf
            if ($name -match '^(BL-\d+)-') { $live[$Matches[1]] = "$path" }
        }
        $now = Get-Date
        $waiting = @{}
        foreach ($id in @($live.Keys | Where-Object { $live[$_] -like 'Tasks/Backlog/*' -and (Test-CiFixFileName (Split-Path $live[$_] -Leaf)) })) {
            $text = (git -C $Root show "origin/${branch}:$($live[$id])" 2>$null) -join "`n"
            $depends = if ($text -match '(?m)^depends-on:[ \t]*\[(.*)\]') { @($Matches[1] -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ }) } else { @() }
            if (@($depends | Where-Object { $live.ContainsKey($_) }).Count) { continue }
            if (-not $script:CiFixSeen.ContainsKey($id)) { $script:CiFixSeen[$id] = $now }
            $waiting[$id] = ($now - $script:CiFixSeen[$id]).TotalMinutes
        }
        foreach ($id in @($script:CiFixSeen.Keys)) { if (-not $waiting.ContainsKey($id)) { $script:CiFixSeen.Remove($id) } }
        $served = @($script:CiFixLaneAdded.Keys | Where-Object { $script:CiFixLaneAdded[$_] -gt $now.AddMinutes(-10) })
        $free = @($activeLanes | Where-Object {
                -not (Test-Path (Get-LaneStatePath $_ 'retire')) -and -not (Test-Path (Join-Path $summaries "lane-$_.txt")) -and
                (Test-LaneAlive $_) -and -not (Get-LaneState $_ 'task') }).Count
        $decision = Get-CiFixLaneDecision -Waiting $waiting -FreeLanes $free -Served $served -Active $activeLanes.Count
        if (-not $decision.Id) { return }
        $script:CiFixLaneAdded[$decision.Id] = $now
        $n = Add-Lane
        if ($null -eq $n) { Write-Trace $decision.Id 'lanes' "no lane to add for this CI fix ($($decision.Why))" 'Yellow'; return }
        Write-Trace $decision.Id 'lanes' "lane $n added for this CI fix: $($decision.Why)" 'Yellow'
    }

    # The lane count a fixed shift asked for; capacity steps never go above it.
    $requestedLanes = if ($AutoLanes) { $LaneCount } else { [math]::Max([int]$Lanes, $LaneCount) }
    # A coordinator taking over this shift's stamp (-TakeOverShift) starts no lane: the lanes
    # that run and have not written a summary are the shift's active ones.
    $takenOver = @()
    if ($TakeOverShift) {
        $takenOver = @(1..16 | Where-Object { (Get-LaneState $_ 'pid') -and -not (Test-Path (Join-Path $summaries "lane-$_.txt")) -and (Test-LaneAlive $_) })
        foreach ($n in $takenOver) { $activeLanes.Add($n); $startedLanes.Add($n) }
        if ($takenOver.Count) { $LaneCount = [math]::Max($LaneCount, ($takenOver | Measure-Object -Maximum).Maximum) }
        Remove-Item (Get-CoordinatorReplacingPath) -ErrorAction SilentlyContinue
        Write-Trace '-' 'watchdog' "took over shift $Stamp from a coordinator that stopped; lanes $($takenOver -join ',') keep running" 'Yellow'
    }
    foreach ($n in $(if ($TakeOverShift) { @() } else { 1..$LaneCount })) {
        $where = Start-Lane -N $n
        $activeLanes.Add($n)
        Write-Trace '-' 'lane' "lane $n started in $(Join-Path $LanesDir "lane-$n") ($where)"
        Start-Sleep -Seconds 15
    }
    $restarts = @{}
    # A lane is finished once it has written its summary. A herdr tab has no process to
    # watch, so the summaries are the signal; a lane that dies without one is given up on
    # after the shift's length plus one task's time limit and its overtime run (AF-0042).
    $giveUp = $shiftEnd.AddMinutes($TaskMinutes + [math]::Max(30, [int]($TaskMinutes / 4)) + 30)
    $tick = Get-Date
    $nextAutoStep = (Get-Date).AddMinutes(15)
    $nextRampCheck = (Get-Date).AddMinutes(2)
    $nextCapacityStep = (Get-Date).AddMinutes(5)
    $nextCiFixLaneCheck = (Get-Date).AddMinutes(1)
    while ((Get-Date) -lt $giveUp) {
        # The lanes watch this stamp and replace a coordinator whose loop stops (BL-4353).
        Write-CoordinatorStamp
        # The coordinator announces the usage limit for every lane, and lanes waiting for a
        # new session add that wait to their shift, so the coordinator waits longer too.
        Update-LimitNotice
        Invoke-LimitProbe
        if (Test-WaitingForSession) { $giveUp = $giveUp.Add((Get-Date) - $tick) }
        $tick = Get-Date
        $finished = @(Get-ChildItem $summaries -Filter 'lane-*.txt' -ErrorAction SilentlyContinue |
            ForEach-Object { [int]($_.BaseName -replace '^lane-', '') })
        # A lane asked to retire that has written its summary is done: it leaves the active
        # set, and its tab closes now if there is nothing in it to read.
        foreach ($n in @($activeLanes)) {
            if ($finished -notcontains $n -or -not (Test-Path (Get-LaneStatePath $n 'retire'))) { continue }
            $activeLanes.Remove($n) | Out-Null
            $report = Read-LaneSummary $n
            Write-Trace '-' 'lane' "lane $n retired ($($report.Summary -replace '^SUMMARY ', ''))" 'Cyan'
            if (Test-LaneSummaryClean $report) {
                Close-HerdrTab $laneTabs[$n] "lane $n retired cleanly" (Get-LaneTabLabel $n)
                $closedTabs[$n] = $true
            }
        }
        if (Test-LanesFinished -Active @($activeLanes) -Finished $finished) { break }
        # A lane whose process is gone without a summary died - killed, crashed, or closed.
        # Start it again in the same worktree; it resumes the task it held. Five tries each.
        foreach ($n in @($activeLanes)) {
            if (Test-Path (Join-Path $summaries "lane-$n.txt")) { continue }
            if (Test-LaneAlive $n) { continue }
            if ($restarts[$n] -ge 5) { continue }
            $restarts[$n] = 1 + [int]$restarts[$n]
            Remove-Item (Get-LaneStatePath $n 'pid') -ErrorAction SilentlyContinue
            $held = Get-LaneState $n 'task'
            Close-HerdrTab $laneTabs[$n] "lane $n died; its restart gets a new tab"
            Start-Lane -N $n -Label "restart $($restarts[$n])$(if ($held) { " $held" })" -KeepWorktree | Out-Null
            Write-Trace '-' 'lane' "lane $n had died; restarted ($($restarts[$n]) of 5)$(if ($held) { ", resuming $held" })" 'Yellow'
        }
        # -Lanes Auto steps every 15 minutes, but never while the shift waits for tokens.
        if ($AutoLanes -and (Get-Date) -ge $nextAutoStep -and -not (Test-WaitingForSession)) {
            Invoke-AutoLaneStep
            $nextAutoStep = (Get-Date).AddMinutes(15)
            $nextRampCheck = (Get-Date).AddMinutes(2)
        }
        # Below the unpaced floor, the board's capacity is read every 2 minutes, and a step
        # runs early when it would add lanes, so a finished task's freed capacity does not
        # wait out the 15-minute interval (2026-10-21). Not while tokens are low or time is up.
        if ($AutoLanes -and (Get-Date) -ge $nextRampCheck -and -not (Test-WaitingForSession)) {
            $nextRampCheck = (Get-Date).AddMinutes(2)
            $rampRetiring = @($activeLanes | Where-Object { Test-Path (Get-LaneStatePath $_ 'retire') })
            $rampCurrent = @($activeLanes | Where-Object { $rampRetiring -notcontains $_ }).Count
            $belowFloor = Test-UnpacedRampDue -Current $rampCurrent -Capacity $MaxLanes -MachineCap $script:Auto.MachineCap -MaxLanes $MaxLanes
            if ($belowFloor -and -not (Get-UsageStop) -and (Get-Date) -le $shiftEnd) {
                $rampCapacity = Get-ShiftCapacity -Fallback $rampCurrent
                if (Test-UnpacedRampDue -Current $rampCurrent -Capacity $rampCapacity -MachineCap $script:Auto.MachineCap -MaxLanes $MaxLanes) {
                    Invoke-AutoLaneStep -Capacity $rampCapacity
                    $nextAutoStep = (Get-Date).AddMinutes(15)
                }
            }
        }
        # A fixed count of lanes steps to the board's capacity every 5 minutes (BL-1374).
        if (-not $AutoLanes -and $requestedLanes -gt 1 -and (Get-Date) -ge $nextCapacityStep -and -not (Test-WaitingForSession)) {
            Invoke-CapacityLaneStep
            $nextCapacityStep = (Get-Date).AddMinutes(5)
        }
        # A CI fix with every lane busy gets a lane of its own (ADR-1158).
        if ((Get-Date) -ge $nextCiFixLaneCheck) {
            $nextCiFixLaneCheck = (Get-Date).AddMinutes(1)
            Invoke-CiFixLaneStep
        }
        # The coordinator is the board branch's one writer for a lane shift.
        Publish-BoardStatusIfDue -Branch $branch
        # And the one that sees every CI run: it files a High task for each new failure.
        Invoke-CiWatch -Branch $branch
        $running =@($procs | Where-Object { $_.Tab -or -not $_.Process.HasExited }).Count
        if ($running -eq 0 -and -not $TakeOverShift) { break }
        Start-Sleep -Seconds 5
    }
    Remove-Item (Get-CoordinatorStampPath) -ErrorAction SilentlyContinue

    # Something may have checked out another branch here during the shift; pulling into it
    # would fast-forward the wrong branch.
    $moved = Restore-ShiftBranch -Branch $branch
    if ($moved) { Write-Trace '-' 'branch' "$moved; not pulling" 'Red' }
    else { git -C $Root pull -q --ff-only origin $branch 2>&1 | Out-Null }
    $stalls = @()
    # Every lane started this shift reports, retired ones included. A lane that blocked a
    # task or stalled keeps its tab for Stewart; so does a lane that never wrote a summary.
    foreach ($file in Get-ChildItem $summaries -Filter 'lane-*.txt' -ErrorAction SilentlyContinue) {
        $n = [int]($file.BaseName -replace '^lane-', '')
        $report = Read-LaneSummary $n
        if ($report.Summary) { Write-Trace '-' 'lane' ($report.Summary -replace '^SUMMARY ', '') 'Cyan' }
        $stalls += $report.Stalls
        if ($closedTabs.ContainsKey($n)) { continue }
        if (Test-LaneSummaryClean $report) {
            Close-HerdrTab $laneTabs[$n] "lane $n ended cleanly" (Get-LaneTabLabel $n)
        }
        elseif ($report.Summary -match 'blocked=[1-9]') { Set-HerdrTabLabel $laneTabs[$n] (Get-LaneTabLabel $n 'BLOCKED, read') }
        else { Set-HerdrTabLabel $laneTabs[$n] (Get-LaneTabLabel $n 'STALLED, read') }
    }
    # Runs, tasks and US dollars per model across every lane of the shift (BL-1705).
    Write-Trace '-' 'shift' (Get-ModelCostSummary $LogDir "^BL-\d+-$Stamp-L\d+[-.]") 'Cyan'
    foreach ($n in @($startedLanes)) {
        if (-not (Test-Path (Join-Path $summaries "lane-$n.txt"))) { Set-HerdrTabLabel $laneTabs[$n] (Get-LaneTabLabel $n 'no report, read') }
    }
    Publish-BoardStatus -Branch $branch -State 'ended'
    Write-Trace '-' 'shift' "end  $LaneCount lanes$(if ($AutoLanes) { ' (auto)' })" 'Cyan'
    if ($AutoLanes) { Save-AutoLanes $LaneCount }
    # The audit office's cadence (BL-1022): say when an audit is due, and hold a milestone's
    # merge until it has run. Other reasons do not hold the merge.
    $auditCadence = Get-AuditCadence (Get-AuditDueFromMaster -Branch $branch)
    if ($auditCadence.Line) { Show-AuditNotice $auditCadence.Line }
    # The one hand-over to the next shift, used when work is still ready, when the idle
    # poll below finds some and when the same poll, run during the merge's CI wait, does
    # (-Beside: this coordinator stays to finish the merge, so it neither reviews nor closes
    # its tab yet). -Why says which.
    # A next shift that wrote no log raises its alarm only after the merge and review, which
    # waiting on the acknowledgement would otherwise hold (BL-3510).
    $script:lateAlarmReasons = $null
    function Invoke-LateAlarm { if ($script:lateAlarmReasons) { Invoke-Alarm -Reasons $script:lateAlarmReasons; $script:lateAlarmReasons = $null } }
    $startNextShift = {
        param([string]$Why, [switch]$Beside)
        # An Auto shift hands on Auto, not the count it ended at; the next one starts from
        # the count auto-lanes.json saved.
        $forward = @('-Lanes', $(if ($AutoLanes) { 'Auto' } else { $LaneCount }), '-Hours', $Hours, '-MaxTasks', $MaxTasks, '-TaskMinutes', $TaskMinutes, '-TaskBudgetUsd', $TaskBudgetUsd.ToString([System.Globalization.CultureInfo]::InvariantCulture), '-Model', $Model, '-MaxRunTurns', $MaxRunTurns, '-HeartbeatMinutes', $HeartbeatMinutes, '-IdlePollMinutes', $IdlePollMinutes, '-Continuous', '-ShiftBranch', $branch)
        if ($AutoLanes) { $forward += @('-MaxLanes', $MaxLanes, '-MinStartLanes', $MinStartLanes) }
        if ($WeeklyPace) { $forward += '-WeeklyPace' }
        if ($QuietAlarm) { $forward += '-QuietAlarm' }
        $startedAt = Get-Date; $keepTab = $false
        $next = Start-NextShiftDetached -Label "DF shift starting" -Dir $Root -ScriptArgs $forward
        if (-not $next.Started) { Write-Trace '-' 'shift' "$Why; the next shift did not start: $($next.Failure)" 'Red'; return $false }
        # A tab that never runs the shift keeps this one's tab open and raises the alarm (BL-3509).
        if ($next.Tab -and -not (Wait-NextShiftLog -Tab $next.Tab -Since $startedAt -LogDir $LogDir -OwnLog $TraceFile -Notify { param([string[]]$Reasons) $script:lateAlarmReasons = $Reasons })) { $keepTab = $true }
        Write-Trace '-' 'shift' "$Why; next shift started ($(if ($next.Tab) { "herdr tab $($next.Tab)" } else { "pid $($next.Process.Id)" }))" 'Cyan'
        # The review runs beside the next shift, which claims what it files (ADR-0710).
        if ($Beside) { $handOver.Tab = $(if (-not $keepTab) { $next.Tab }); return $true }
        if ($review) { Set-OwnTabLabel 'reviewing the merge'; Write-Trace '-' 'review' (Invoke-ShiftReview @review) 'Cyan' }
        Invoke-LateAlarm
        # The next shift has its own tab; this one has only notes, and they are in the log.
        if ($next.Tab -and -not $keepTab) { Close-FinishedHerdrTabsAndTrace; Close-OwnHerdrTab 'shift handed over to the next one' | Out-Null }
        return $true
    }
    # What master held before the merge, so the shift-end review reads only what this shift merged.
    git -C $Root fetch -q origin master 2>&1 | Out-Null
    $masterBefore = "$(git -C $Root rev-parse origin/master 2>$null)".Trim()
    # Under -Continuous the merge's CI wait polls the board like the idle poll below (BL-3142):
    # a task claimable during the wait starts the next shift beside the merge, once.
    $handOver = @{ Done = $false }
    $pollDuringMerge = if ($Continuous) {
        {
            if ($handOver.Done) { return }
            if (-not $moved) { git -C $Root pull -q --ff-only origin $branch 2>&1 | Out-Null }
            $claimable = Get-MergeWaitClaimableTaskId
            $poll = Get-IdlePollDecision -Now (Get-Date) -Deadline $shiftEnd -UsageStop (Get-UsageStop) -ClaimableId $claimable.Id -CappedIds $claimable.Capped
            if ($poll.Action -eq 'start' -and @(& $startNextShift "$($poll.Why) during the merge's CI wait" -Beside)[-1]) { $handOver.Done = $true }
        }
    }
    $mergeLine = if ($auditCadence.HoldMerge) { $auditCadence.MergeMessage } else { Invoke-MergeToMaster -Branch $branch -OnPoll $pollDuringMerge -PollMinutes $IdlePollMinutes -HandOver $handOver }
    Write-Trace '-' 'merge' $mergeLine 'Cyan'
    # The shift-end review (ADR-0710) runs below: beside the next shift when one starts, or
    # before deciding whether to start one when nothing is ready.
    $review = $null
    if ($mergeLine -match '^merged ([0-9a-f]{7,40}) ') {
        $merged = "$(git -C $Root rev-parse --verify "$($Matches[1])^{commit}" 2>$null)".Trim()
        if ($masterBefore -and $merged) { $review = @{ Base = $masterBefore; Head = $merged; Branch = $branch } }
    }
    # The merge waited for CI on the shift's last commit; a failure there finished after the
    # loop above stopped watching, so file it now (BL-1031).
    Invoke-MergeEndCiWatch -Branch $branch -HandedOver $handOver.Done | Out-Null
    # The next shift started during the merge's CI wait: only the review and the tab are left.
    if ($handOver.Done) {
        foreach ($r in @($stalls) + @($retiredLines) + @(Get-WaitingOnStewart)) { Write-Trace '-' 'note' (Get-Short $r 100) 'Yellow' }
        if ($review) { Set-OwnTabLabel 'reviewing the merge'; Write-Trace '-' 'review' (Invoke-ShiftReview @review) 'Cyan' }
        Invoke-LateAlarm
        if ($handOver.Tab) { Close-FinishedHerdrTabsAndTrace; Close-OwnHerdrTab 'shift handed over to the next one' }
        exit 0
    }
    # Those tasks were pushed from the ci-watch worktree; pull them here too, or the board
    # below still says nothing is ready and -Continuous leaves them waiting for the next
    # shift Stewart starts (AF-0090: three CI-fix tasks waited 5 hours).
    if (-not $moved) { git -C $Root pull -q --ff-only origin $branch 2>&1 | Out-Null }
    $reasons = @($stalls) + @($retiredLines) + @(Get-WaitingOnStewart)
    # -Continuous: while the board still has ready work, the next shift starts itself, so
    # the factory keeps going without anyone - Stewart or a Claude session - to restart it.
    # Only a task claimable now counts: one over its cost cap or interactive only does not,
    # or -Continuous starts shift after empty shift (BL-3090).
    $stillReady = Get-ClaimableTaskId
    # Nothing claimable: the review runs now, and what it files can start the next shift.
    if ($review -and -not ($Continuous -and $stillReady.Id)) {
        Write-Trace '-' 'review' (Invoke-ShiftReview @review) 'Cyan'
        $review = $null
        if (-not $moved) { git -C $Root pull -q --ff-only origin $branch 2>&1 | Out-Null }
        $stillReady = Get-ClaimableTaskId
    }
    $capFreesAt = if (-not $stillReady.Id -and $stillReady.Capped.Count) { Get-CapFreesAt $stillReady.Capped } else { $null }
    $end = Get-ShiftEndDecision -Continuous ([bool]$Continuous) -ClaimableId $stillReady.Id -CappedIds $stillReady.Capped -CapFreesAt $capFreesAt
    if ($end.Trace) { Write-Trace '-' 'shift' $end.Trace 'Yellow' }
    if ($end.Continue) {
        foreach ($r in $reasons) { Write-Trace '-' 'note' (Get-Short $r 100) 'Yellow' }
        if (@(& $startNextShift 'work is still ready')[-1]) { exit 0 }
    }
    # -Continuous with nothing claimable: poll the board every -IdlePollMinutes (the merge's CI wait
    # above polls it the same way, BL-3142) and start the
    # next shift once something is claimable, until the -Hours deadline or a usage limit
    # stops it (BL-3092, ADR-0758). What waits on Stewart is shown once now; the alarm that
    # waits for a key rings only when polling stops, so it never holds the poll.
    if ($Continuous) {
        foreach ($r in $reasons) { Write-Trace '-' 'note' (Get-Short $r 100) 'Yellow' }
        Set-OwnTabLabel $(if ($reasons.Count -gt 0) { 'ALARM, read; idle, polling' } else { 'idle, polling' })
        Write-Trace '-' 'shift' "nothing claimable; polling the board every $IdlePollMinutes minutes until $($shiftEnd.ToString('HH:mm'))" 'Cyan'
        while ($true) {
            Write-IdlePollHeartbeat
            $poll = Get-IdlePollDecision -Now (Get-Date) -Deadline $shiftEnd -UsageStop (Get-UsageStop) -ClaimableId $stillReady.Id -CappedIds $stillReady.Capped
            if ($poll.Action -eq 'start' -and @(& $startNextShift $poll.Why)[-1]) { exit 0 }
            if ($poll.Action -eq 'stop') { Write-Trace '-' 'shift' "polling stopped: $($poll.Why)" 'Yellow'; break }
            # The heartbeat is refreshed every minute of the wait, as a lane's is.
            foreach ($minute in 1..$IdlePollMinutes) { Start-Sleep -Seconds 60; Write-Heartbeat }
            if (-not $moved) { git -C $Root pull -q --ff-only origin $branch 2>&1 | Out-Null }
            $stillReady = Get-ClaimableTaskId
        }
        Write-Heartbeat 'finished'
        Publish-BoardStatus -Branch $branch -State 'ended'
    }
    Close-FinishedHerdrTabsAndTrace
    if ($reasons.Count -gt 0) { Set-OwnTabLabel 'ALARM, read'; Invoke-Alarm -Reasons $reasons; exit 2 }
    try { $Host.UI.RawUI.WindowTitle = 'Dark factory - shift complete' } catch { }
    Close-OwnHerdrTab 'shift complete with nothing waiting on Stewart'
    exit 0
}

# This file ran to its end; RunDarkFactory.ps1 goes on to the next one.
$AreaFileFinished = $true
