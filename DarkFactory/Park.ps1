# Parking work that will not integrate on a branch of its own, what to do with an orphaned
# task at shift start, and -TestPark. Dot-sourced by RunDarkFactory.ps1 in its order; see
# the loader there.

function Invoke-Park {
    # Work that will not integrate is kept on a branch of its own, and the task goes to
    # back to Backlog on the shared branch, so a later run picks it up from that branch and
    # fixes what broke. Integration trouble is Claude's to solve, not Stewart's.
    # Returns '' once the task has left Doing on the shared branch, or a line for the lane's
    # summary and the end-of-shift report saying it has not (BL-1071). The push that failed
    # to integrate often failed because the remote was out of reach, so the move is retried
    # for several minutes, waiting -RetrySeconds between tries.
    # A run that ended in Blocked or Deferred (-State) keeps that folder and its reason on the
    # shared branch (BL-4425): only the code is parked, so a block for Stewart still raises the alarm.
    param([string]$Id, [string]$Why, [int[]]$RetrySeconds = @(10, 30, 60, 120, 300), [string]$State = '')
    $keep = $State -in 'Blocked', 'Deferred'
    $keptReason = if ($keep) { (Get-LastLogLine $Id) -replace '^\S+\s+\w+ -> \w+\.\s*', '' } else { '' }
    $park = "factory/$Id-lane-$Lane-$Stamp"
    # A local branch too: lanes may not fetch, but every worktree sees local branches.
    Invoke-Git @('branch', '-f', $park, 'HEAD') | Out-Null
    Invoke-Git @('push', '-q', 'origin', "HEAD:refs/heads/$park") | Out-Null
    $lock = Enter-Lock
    try {
        foreach ($wait in @(0) + $RetrySeconds) {
            if ($wait) { Start-Sleep -Seconds $wait }
            if (-not (Sync-Lane)) { continue }
            if ((Get-TaskState $Id) -ne 'Doing') { return '' }
            if ($keep) {
                Invoke-Board @('move', '-Id', $Id, '-To', $State, '-Reason', "$keptReason (Lane $Lane could not integrate its code: $Why. The code is on branch $park; start with git cherry-pick --no-commit $park and fix it.)") | Out-Null
            } else {
                Invoke-Board @('move', '-Id', $Id, '-To', 'Backlog', '-Reason', "Lane $Lane could not integrate: $Why. The work is on branch $park; start with git cherry-pick --no-commit $park and fix it.") | Out-Null
            }
            Invoke-Git @('add', '-A', 'Tasks') | Out-Null
            Invoke-Git @('commit', '-q', '-m', "chore(tasks): park $Id - $Why", '-m', $SkipCiNote) | Out-Null
            if (Invoke-Git @('push', '-q', 'origin', "HEAD:$Branch")) { return '' }
        }
    } finally { $lock.Dispose() }
    return "$Id PARK NOT PUSHED  $Why, and its move to Backlog never reached $Branch either, so it is still in Doing there. The work is on local branch $park; the next shift returns the task to Backlog."
}

function Get-ParkedBranches {
    # The branches Invoke-Park kept task $Id's work on, local or on origin, by short name.
    param([string]$Id, [string]$Repo = $Root)
    $refs = @(git -C $Repo for-each-ref --format='%(refname:short)' "refs/heads/factory/$Id-lane-*" "refs/remotes/origin/factory/$Id-lane-*" 2>$null)
    return @($refs | Where-Object { $_ } | ForEach-Object { "$_" -replace '^origin/', '' } | Sort-Object -Unique)
}

function Test-WorktreeDirty {
    # Whether the worktree at $Dir exists and has uncommitted work.
    param([string]$Dir)
    if (-not (Test-Path (Join-Path $Dir '.git'))) { return $false }
    return [bool](@(git -C $Dir status --porcelain 2>$null) | Where-Object { $_ })
}

function Get-OrphanAction {
    # What shift start does with task $Id, in Doing but held by no lane of the previous shift
    # (BL-1071): adopt it into the one lane worktree with uncommitted work, so that lane
    # resumes it; return it to Backlog when no worktree has work and a parked branch holds
    # it, or when no work for it exists anywhere (BL-4347: a claim a lane never started);
    # refuse, saying why, when a lane branch holds commits beyond the shift branch, since
    # which lane's work they are cannot be told. -DirtyLanes are the lanes not already
    # adopted whose worktrees have uncommitted work; -LaneBranchesAhead the factory/lane-<n>
    # branches with commits the shift branch lacks. -LaneLastTask maps a lane to the task its
    # newest run log names (Get-LaneLastRunTask): a dirty lane whose last run was $Id is
    # adopted, and one whose last run was another task holds that task's work, never $Id's,
    # so it is no candidate (BL-4650, AF-0544: lane 1 adopted lane 2's task over its own work).
    param([string]$Id, [string[]]$ParkedBranches, [int[]]$DirtyLanes, [string[]]$LaneBranchesAhead = @(), [hashtable]$LaneLastTask = @{})
    $dirty = @($DirtyLanes | Where-Object { $_ })
    $own = @($dirty | Where-Object { $LaneLastTask[[int]$_] -eq $Id })
    if ($own.Count) { return [pscustomobject]@{ Action = 'adopt'; Lane = $own[0]; Branch = ''; Why = '' } }
    $dirty = @($dirty | Where-Object { -not $LaneLastTask[[int]$_] })
    if ($dirty.Count -eq 1) { return [pscustomobject]@{ Action = 'adopt'; Lane = $dirty[0]; Branch = ''; Why = '' } }
    if ($dirty.Count -gt 1) {
        return [pscustomobject]@{ Action = 'refuse'; Lane = 0; Branch = ''; Why = "$Id is in Doing and held by no lane, and lanes $($dirty -join ', ') have uncommitted work; which one is its work cannot be told" }
    }
    $parked = @($ParkedBranches | Where-Object { $_ } | Sort-Object)
    if ($parked.Count) { return [pscustomobject]@{ Action = 'backlog'; Lane = 0; Branch = $parked[-1]; Why = '' } }
    $ahead = @($LaneBranchesAhead | Where-Object { $_ })
    if ($ahead.Count) {
        return [pscustomobject]@{ Action = 'refuse'; Lane = 0; Branch = ''; Why = "$Id is in Doing and held by no lane, no parked branch factory/$Id-lane-* exists, and lane branches $($ahead -join ', ') have commits beyond the shift branch, or git could not say; which one is its work cannot be told" }
    }
    return [pscustomobject]@{ Action = 'backlog'; Lane = 0; Branch = ''; Why = '' }
}

function Get-OrphanBlockReason {
    # An orphan Get-OrphanAction cannot place goes to Blocked rather than refusing the shift
    # (BL-4715, AF-0567: two refused starts over BL-2969 kept the CI watch off for 32 minutes).
    # Its candidate work is kept first, since starting lanes resets their branches and stashes
    # their worktrees: each lane branch in -LaneBranchesAhead is copied to
    # factory/<Id>-lane-<n>-<Stamp>, a parked branch Get-ParkedBranches finds. Returns the
    # Blocked reason, naming -Why, the copies and where each dirty lane's work goes.
    param([string]$Id, [string]$Why, [int[]]$DirtyLanes = @(), [string[]]$LaneBranchesAhead = @(), [string]$Repo = $Root, [string]$ForStamp = $Stamp)
    $kept = @()
    foreach ($ref in @($LaneBranchesAhead | Where-Object { $_ -match '^factory/lane-(\d+)$' })) {
        $copy = "factory/$Id-lane-$($Matches[1])-$ForStamp"
        git -C $Repo branch -f $copy $ref 2>&1 | Out-Null
        if ($LASTEXITCODE -eq 0) { $kept += "$ref is kept as $copy" } else { $kept += "$ref could not be copied (git branch failed); its reflog still holds it" }
    }
    $dirty = @($DirtyLanes | Where-Object { $_ })
    if ($dirty.Count) { $kept += "uncommitted work of lane $($dirty -join ', ') is stashed when each lane starts (git stash list: darkfactory lane-<n> before $ForStamp)" }
    $where = if ($kept.Count) { " Kept: $($kept -join '; ')." } else { '' }
    return "Blocked at shift start instead of refusing the shift: $Why.$where Claude (an interactive session) finds the task's work among these, moves it to Backlog naming it, or Backlog with no work."
}

function Get-LaneLastRunTask {
    # The task lane $N ran last, by its newest run log in $Dir (BL-<id>-<stamp>-L<n>.jsonl, or
    # its -resumed run), or '' when the lane has none. Get-OrphanAction reads it to tell whose
    # work a dirty lane worktree holds (BL-4650).
    param([int]$N, [string]$Dir = $LogDir)
    $pattern = '^(BL-\d+)-\d{8}-\d{6}-L' + $N + '(-resumed)?\.jsonl$'
    $newest = Get-ChildItem $Dir -Filter "BL-*-L$N*.jsonl" -File -ErrorAction SilentlyContinue | Where-Object { $_.Name -match $pattern } |
        Sort-Object LastWriteTimeUtc | Select-Object -Last 1
    if ($newest -and $newest.Name -match $pattern) { return $Matches[1] }
    return ''
}

$UnknownLaneBranches = '(git could not say which lane branches are ahead)'

function Get-LaneBranchesAhead {
    # The local factory/lane-<n> branches with commits that origin/$ShiftBranch lacks. A git
    # query that fails (no origin/$ShiftBranch, say) is not "no commits": the answer then
    # holds $UnknownLaneBranches, so Get-OrphanAction refuses rather than requeues.
    param([string]$ShiftBranch = $branch, [string]$Repo = $Root)
    $refs = @(git -C $Repo for-each-ref --format='%(refname:short)' 'refs/heads/factory/lane-*' 2>$null)
    if ($LASTEXITCODE -ne 0) { return @($UnknownLaneBranches) }
    $ahead = @()
    foreach ($ref in @($refs | Where-Object { $_ })) {
        $count = git -C $Repo rev-list --count "origin/$ShiftBranch..$ref" 2>$null
        if ($LASTEXITCODE -ne 0) { return @($UnknownLaneBranches) }
        if ([int]$count -gt 0) { $ahead += $ref }
    }
    return $ahead
}

function Get-UnknownLaneBranchesRefusal {
    # The reason a shift start stops when -LaneBranchesAhead holds $UnknownLaneBranches (git's
    # query failed, so unpushed lane commits may exist that nothing could keep), or '' when
    # git answered. Only an orphan Get-OrphanAction cannot place among known branches is
    # Blocked and carried on (BL-4784, ADR-1234).
    param([string[]]$LaneBranchesAhead = @())
    if ($LaneBranchesAhead -notcontains $UnknownLaneBranches) { return '' }
    return "git could not say which lane branches are ahead of origin/$branch (git for-each-ref or git rev-list failed); starting the lanes would reset branches that may hold an orphan's only copy of its work"
}

function Invoke-ReleaseClaim {
    # A lane that claimed task $Id and is past the shift's end before starting it returns the
    # claim to Backlog on the shared branch, so no task is left in Doing held by no lane
    # (BL-4347). Returns '' once the task has left Doing there, or a line saying it has not.
    param([string]$Id, [int[]]$RetrySeconds = @(10, 30, 60))
    $lock = Enter-Lock
    try {
        foreach ($wait in @(0) + $RetrySeconds) {
            if ($wait) { Start-Sleep -Seconds $wait }
            if (-not (Sync-Lane)) { continue }
            if ((Get-TaskState $Id) -ne 'Doing') { return '' }
            Invoke-Board @('move', '-Id', $Id, '-To', 'Backlog', '-Reason', "Lane $Lane claimed it after the shift's time was up and never started it.") | Out-Null
            # A move that failed leaves the task in Doing: pushing that would report a release
            # that never happened, so the task's state is checked before anything is committed.
            if ((Get-TaskState $Id) -eq 'Doing') { continue }
            if (-not (Invoke-Git @('add', '-A', 'Tasks'))) { continue }
            if (-not (Invoke-Git @('commit', '-q', '-m', "chore(tasks): release unstarted claim $Id - shift time up", '-m', $SkipCiNote))) { continue }
            if (Invoke-Git @('push', '-q', 'origin', "HEAD:$Branch")) { return '' }
        }
    } finally { $lock.Dispose() }
    return "$Id CLAIM NOT RELEASED  claimed past the shift's end, and its move to Backlog never reached $Branch, so it is still in Doing there."
}

function Stop-ShiftStart {
    # A shift that refuses to start says why: in the trace, and in the alarm that is the
    # end-of-shift notice, so the factory never stops silently (BL-1071). The caller exits.
    param([string]$Why, [scriptblock]$Notify = { param([string[]]$Reasons) Set-OwnTabLabel 'ALARM, read'; Invoke-Alarm -Reasons $Reasons })
    Write-Trace '-' 'refuse' $Why 'Red'
    & $Notify -Reasons @("SHIFT REFUSED  $Why", 'No shift runs until this is cleared and a shift is started again.')
}

function Wait-ForHandOverReady {
    # The wait both start checks share (BL-3093, BL-5592). Returns what $Check returns once it
    # returns nothing, which the caller reads as ready. A start not by hand-over returns the
    # first result at once, so a person who started a shift hears about the trouble at once.
    # A -Continuous hand-over has nobody watching: it traces the first result once and polls
    # every $PollMinutes, running $WhileWaiting on every poll (the coordinator's CI watch, so a
    # failing CI run is still traced and filed while the shift waits, AF-0904). After
    # $WindowMinutes it flags its tab once and goes on waiting; only after $GiveUpMinutes does
    # it return the last result, when the caller refuses with the alarm. Before BL-5592 the
    # hand-over refused at $WindowMinutes and exited, and no shift ran until a person came.
    param(
        [bool]$HandOver,
        [scriptblock]$Check,
        [string]$What,
        [scriptblock]$TraceFirst = { param([object[]]$Found) },
        [scriptblock]$WhileWaiting = { },
        [scriptblock]$Flag = { Set-OwnTabLabel 'WAITING, read' },
        [scriptblock]$Sleep = { param([int]$Seconds) Start-Sleep -Seconds $Seconds },
        [scriptblock]$Now = { Get-Date },
        [scriptblock]$Trace = { param([string]$Verb, [string]$Detail) Write-Trace '-' $Verb $Detail 'Yellow' },
        [int]$PollMinutes = 2,
        [int]$WindowMinutes = 30,
        [int]$GiveUpMinutes = 480)
    $start = & $Now
    $traced = $false
    $flagged = $false
    while ($true) {
        $found = @(& $Check | Where-Object { $_ })
        if (-not $found.Count -or -not $HandOver) { return $found }
        if (-not $traced) {
            & $TraceFirst $found
            & $Trace 'wait' "$What at hand-over; checking every $PollMinutes minutes and watching CI, for up to $GiveUpMinutes minutes"
            $traced = $true
        }
        $waited = ((& $Now) - $start).TotalMinutes
        if ($waited -ge $GiveUpMinutes) {
            & $Trace 'wait' "$What after $GiveUpMinutes minutes; giving up"
            return $found
        }
        if (-not $flagged -and $waited -ge $WindowMinutes) {
            & $Trace 'wait' "$What after $WindowMinutes minutes; still waiting and watching CI"
            & $Flag
            $flagged = $true
        }
        & $WhileWaiting
        & $Sleep ($PollMinutes * 60)
    }
}

function Wait-ForCleanTree {
    # Returns the main checkout's dirty paths, or nothing once it is clean. A shift started by
    # a -Continuous hand-over ($ShiftBranch set) traces the dirty paths once and waits for an
    # interactive session to commit (Wait-ForHandOverReady), for up to the shift's own -Hours;
    # any other start returns at once. It only reads git status: the user's changes are never
    # touched. The caller refuses on a dirty result.
    param(
        [bool]$HandOver = [bool]$ShiftBranch,
        [scriptblock]$GetDirty = { Get-Dirty },
        [scriptblock]$WhileWaiting = { },
        [scriptblock]$Flag = { Set-OwnTabLabel 'WAITING, read' },
        [scriptblock]$Sleep = { param([int]$Seconds) Start-Sleep -Seconds $Seconds },
        [scriptblock]$Now = { Get-Date },
        [scriptblock]$Trace = { param([string]$Verb, [string]$Detail) Write-Trace '-' $Verb $Detail 'Yellow' },
        [int]$PollMinutes = 2,
        [int]$WindowMinutes = 30,
        [int]$GiveUpMinutes = [int]([double]$Hours * 60))
    $traceDirty = {
        param([object[]]$Found)
        $Found | Select-Object -First 10 | ForEach-Object { & $Trace 'dirty' $_ }
        if ($Found.Count -gt 10) { & $Trace 'dirty' "and $($Found.Count - 10) more" }
    }
    return Wait-ForHandOverReady -HandOver $HandOver -Check $GetDirty -What 'working tree not clean' -TraceFirst $traceDirty `
        -WhileWaiting $WhileWaiting -Flag $Flag -Sleep $Sleep -Now $Now -Trace $Trace `
        -PollMinutes $PollMinutes -WindowMinutes $WindowMinutes -GiveUpMinutes $GiveUpMinutes
}

function Wait-ForSyncedCheckout {
    # Returns the start sync's result (Invoke-ShiftStartSync) once it has no refusal. A
    # -Continuous hand-over whose checkout is ahead of or has diverged from origin - an
    # interactive session committed and had not yet pushed (the 2026-11-19 05:10 shift) -
    # waits for the push the same way as for a dirty tree; any other start returns at once.
    param(
        [bool]$HandOver = [bool]$ShiftBranch,
        [scriptblock]$Sync,
        [scriptblock]$WhileWaiting = { },
        [scriptblock]$Flag = { Set-OwnTabLabel 'WAITING, read' },
        [scriptblock]$Sleep = { param([int]$Seconds) Start-Sleep -Seconds $Seconds },
        [scriptblock]$Now = { Get-Date },
        [scriptblock]$Trace = { param([string]$Verb, [string]$Detail) Write-Trace '-' $Verb $Detail 'Yellow' },
        [int]$PollMinutes = 2,
        [int]$WindowMinutes = 30,
        [int]$GiveUpMinutes = [int]([double]$Hours * 60))
    $last = @{ Result = $null }
    $check = { $last.Result = & $Sync; if ($last.Result -and $last.Result.Refusal) { $last.Result.Refusal } }
    $traceRefusal = { param([object[]]$Found) & $Trace 'sync' "$($Found[0])" }
    $null = Wait-ForHandOverReady -HandOver $HandOver -Check $check -What 'checkout not level with origin' -TraceFirst $traceRefusal `
        -WhileWaiting $WhileWaiting -Flag $Flag -Sleep $Sleep -Now $Now -Trace $Trace `
        -PollMinutes $PollMinutes -WindowMinutes $WindowMinutes -GiveUpMinutes $GiveUpMinutes
    return $last.Result
}

if ($TestPark) {
    # Lane 9 of a made-up shift parks two tasks in a throwaway repository with a bare origin:
    # the first while origin answers, the second after origin has gone.
    $temp = Join-Path ([IO.Path]::GetTempPath()) "df-park-$PID"
    $origin = Join-Path $temp 'origin.git'
    $repo = Join-Path $temp 'lane-9'
    New-Item -ItemType Directory -Force -Path $temp | Out-Null
    git init -q --bare -b work $origin 2>&1 | Out-Null
    git clone -q $origin $repo 2>&1 | Out-Null
    git -C $repo config user.name t; git -C $repo config user.email t@t
    git -C $repo checkout -q -b work 2>&1 | Out-Null
    New-Item -ItemType Directory -Force -Path (Join-Path $repo 'Tasks\Doing'), (Join-Path $repo 'Tasks\Backlog') | Out-Null
    New-Item -ItemType Directory -Force -Path (Join-Path $repo 'Tasks\Blocked') | Out-Null
    Set-Content -Path (Join-Path $repo 'Tasks\Blocked\.gitkeep') -Value ''
    foreach ($id in 'BL-001','BL-002', 'BL-004', 'BL-005', 'BL-006') {
        Set-Content -Path (Join-Path $repo "Tasks\Doing\$id-park-me.md") -Encoding UTF8 -Value @(
            '---', "id: $id", 'title: Park me', 'priority: Normal', 'assignee: Claude', 'pipeline: direct',
            'depends-on: []', 'touches: [x]', 'requirement: none', 'created: 2026-10-01', 'completed:', '---',
            "# $id - Park me", '', '## Goal', '', '## Context', '', '## Acceptance criteria', '', '- [x] Parked.', '', '## Notes', '', '## Log', '', '- 2026-10-01: Created.')
    }
    git -C $repo add -A 2>&1 | Out-Null
    git -C $repo commit -q -m board 2>&1 | Out-Null
    git -C $repo push -q origin work 2>&1 | Out-Null
    $script:Root = $repo; $script:Branch = 'work'; $script:Lane = 9; $script:Stamp = 'test'
    $script:LanesDir = $temp; $script:LockFile = Join-Path $temp 'integrate.lock'; $script:LogDir = Join-Path $temp 'logs'
    $env:CLAUDE_PROJECT_DIR = $repo
    $quiet = { param([string[]]$Reasons) $script:Alarmed = $Reasons }
    $failed = 0
    $cases = @(
        ,@('an origin that answers is reachable at once', 'True', { "$(Wait-RemoteReachable -Minutes 0 -PollSeconds 0)" })
        ,@('a park pushes its move to Backlog, naming the branch', "'' Backlog factory/BL-001-lane-9-test", {
            Set-Content -Path (Join-Path $repo 'work.txt') -Value 'work'
            git -C $repo add -A 2>&1 | Out-Null; git -C $repo commit -q -m work 2>&1 | Out-Null
            $r = Invoke-Park -Id 'BL-001' -Why 'push kept being refused' -RetrySeconds @()
            $shared = @(git -C $repo ls-tree -r --name-only origin/work Tasks) -join ' '
            $log = (git -C $repo show 'origin/work:Tasks/Backlog/BL-001-park-me.md') -join ' '
            "'$r' $(if ($shared -match 'Backlog/BL-001-' -and $shared -notmatch 'Doing/BL-001-') { 'Backlog' } else { 'Doing' }) $(if ($log -match 'branch (factory/BL-001-lane-9-test);') { $Matches[1] } else { 'no branch in the Log' })" })
        ,@('a Blocked run whose code cannot integrate stays Blocked on the shared branch with its reason, the code only on the park branch', "'' Blocked Stewart: approve it; park reason in Log; code not shared; code on factory/BL-006-lane-9-test", {
            New-Item -ItemType Directory -Force -Path (Join-Path $repo 'Tasks\Blocked') | Out-Null
            git -C $repo mv 'Tasks/Doing/BL-006-park-me.md' 'Tasks/Blocked/BL-006-park-me.md' 2>&1 | Out-Null
            Add-Content -Path (Join-Path $repo 'Tasks\Blocked\BL-006-park-me.md') -Value '- 2026-10-31: Doing -> Blocked. Stewart: approve it.'
            Set-Content -Path (Join-Path $repo 'blocked-code.txt') -Value 'code'
            git -C $repo add -A 2>&1 | Out-Null; git -C $repo commit -q -m 'blocked run' 2>&1 | Out-Null
            $r = Invoke-Park -Id 'BL-006' -Why 'build failed' -RetrySeconds @() -State 'Blocked'
            $shared = @(git -C $repo ls-tree -r --name-only origin/work) -join ' '
            $log = (git -C $repo show 'origin/work:Tasks/Blocked/BL-006-park-me.md') -join ' '
            $parked = @(git -C $repo ls-tree -r --name-only 'factory/BL-006-lane-9-test') -join ' '
            "'$r' $(if ($shared -match 'Blocked/BL-006-' -and $shared -notmatch 'Doing/BL-006-') { 'Blocked' } else { 'not Blocked' }) $(if ($log -match 'Stewart: approve it') { 'Stewart: approve it;' } else { 'no reason;' }) $(if ($log -match 'could not integrate its code: build failed') { 'park reason in Log' } else { 'no park reason' }); code $(if ($shared -match 'blocked-code.txt') { 'shared' } else { 'not shared' }); code on $(if ($parked -match 'blocked-code.txt') { 'factory/BL-006-lane-9-test' } else { 'no park branch' })" })
        ,@('a claim released past the shift end is back in Backlog on the shared branch', "'' Backlog", {
            $r = Invoke-ReleaseClaim -Id 'BL-004' -RetrySeconds @()
            $shared = @(git -C $repo ls-tree -r --name-only origin/work Tasks) -join ' '
            "'$r' $(if ($shared -match 'Backlog/BL-004-' -and $shared -notmatch 'Doing/BL-004-') { 'Backlog' } else { 'Doing' })" })
        ,@('a release whose board move failed reports the task still in Doing rather than released', 'still in Doing', {
            function Invoke-Board { param([string[]]$BoardArgs) }
            $r = Invoke-ReleaseClaim -Id 'BL-005' -RetrySeconds @() 6>$null
            if ($r -match '^BL-005 CLAIM NOT RELEASED ') { 'still in Doing' } else { "'$r'" } })
        ,@('a lane-branch query that git fails is unknown, and an orphan is refused on it', 'unknown refuse', {
            $ahead = @(Get-LaneBranchesAhead 'no-such-branch' $repo)
            $a = Get-OrphanAction -Id 'BL-003' -ParkedBranches @() -DirtyLanes @() -LaneBranchesAhead $ahead
            "$(if ($ahead -contains $UnknownLaneBranches) { 'unknown' } else { "'$ahead'" }) $($a.Action)" })
        ,@('a failed lane-branch query stops the shift start naming the git query; known branches do not', 'stops naming git, known continue', {
            $r = Get-UnknownLaneBranchesRefusal (Get-LaneBranchesAhead 'no-such-branch' $repo)
            $k = Get-UnknownLaneBranchesRefusal @('factory/lane-9')
            "$(if ($r -match 'git could not say which lane branches are ahead.*git rev-list') { 'stops naming git' } else { "'$r'" }), $(if ($k -eq '') { 'known continue' } else { 'known stop' })" })
        ,@('a park that cannot push says so for the summary', 'BL-002 PARK NOT PUSHED, still in Doing, branch kept', {
            git -C $repo remote set-url origin (Join-Path $temp 'gone.git') 2>&1 | Out-Null
            # The git failures it traces are expected here, so they are not printed.
            $r = Invoke-Park -Id 'BL-002' -Why 'push kept being refused' -RetrySeconds @(0) 6>$null
            "$(if ($r -match '^BL-002 PARK NOT PUSHED ') { 'BL-002 PARK NOT PUSHED' } else { "'$r'" }), $(if ($r -match 'still in Doing') { 'still in Doing' } else { 'state not said' }), $(if ((Get-ParkedBranches 'BL-002' $repo) -contains 'factory/BL-002-lane-9-test') { 'branch kept' } else { 'no branch' })" })
        ,@('an origin out of reach is waited for, then reported unreachable', 'False offline', {
            New-Item -ItemType Directory -Force -Path $script:LogDir | Out-Null
            $script:TraceFile = Join-Path $script:LogDir 'trace.log'
            $r = Wait-RemoteReachable -Minutes 0 -PollSeconds 0 6>$null
            $traced = (Get-Content -Path $TraceFile -ErrorAction SilentlyContinue) -join ' '
            "$r $(if ($traced -match 'offline +origin out of reach') { 'offline' } else { 'not traced' })" })
        ,@('an orphan with a parked branch and clean lanes goes to Backlog', 'backlog factory/BL-002-lane-9-test', {
            $a = Get-OrphanAction -Id 'BL-002' -ParkedBranches (Get-ParkedBranches 'BL-002' $repo) -DirtyLanes @(@(9) | Where-Object { Test-WorktreeDirty $repo })
            "$($a.Action) $($a.Branch)" })
        ,@('an orphan with uncommitted work in one lane is adopted by it', 'adopt 9', {
            Set-Content -Path (Join-Path $repo 'half-done.txt') -Value 'half'
            $a = Get-OrphanAction -Id 'BL-002' -ParkedBranches (Get-ParkedBranches 'BL-002' $repo) -DirtyLanes @(@(9) | Where-Object { Test-WorktreeDirty $repo })
            "$($a.Action) $($a.Lane)" })
        ,@('an orphan is not adopted by the one dirty lane whose last run was another task (AF-0544)', "backlog ''", {
            $a = Get-OrphanAction -Id 'BL-2969' -ParkedBranches @() -DirtyLanes @(1) -LaneLastTask @{ 1 = 'BL-4507' }
            "$($a.Action) '$($a.Branch)'" })
        ,@('an orphan is adopted by the dirty lane whose last run was it, over a dirty lane with no run log', 'adopt 2', {
            $a = Get-OrphanAction -Id 'BL-2969' -ParkedBranches @() -DirtyLanes @(1, 2) -LaneLastTask @{ 2 = 'BL-2969' }
            "$($a.Action) $($a.Lane)" })
        ,@('a lane''s last run task is named by its newest run log, resumed runs included, and is empty with none', 'BL-7 BL-9 ''''', {
            $logs = Join-Path $temp 'runlogs'; New-Item -ItemType Directory -Force -Path $logs | Out-Null
            $old = Join-Path $logs 'BL-5-20261102-083146-L1.jsonl'; Set-Content $old 'x'; (Get-Item $old).LastWriteTimeUtc = [datetime]'2026-11-02T10:00:00Z'
            $new = Join-Path $logs 'BL-7-20261102-083146-L1-resumed.jsonl'; Set-Content $new 'x'; (Get-Item $new).LastWriteTimeUtc = [datetime]'2026-11-02T11:00:00Z'
            Set-Content (Join-Path $logs 'BL-9-20261102-083146-L12.jsonl') 'x'
            "$(Get-LaneLastRunTask 1 $logs) $(Get-LaneLastRunTask 12 $logs) '$(Get-LaneLastRunTask 3 $logs)'" })
        ,@('an orphan with no work anywhere goes to Backlog with no branch', "backlog ''", {
            $a = Get-OrphanAction -Id 'BL-003' -ParkedBranches (Get-ParkedBranches 'BL-003' $repo) -DirtyLanes @() -LaneBranchesAhead (Get-LaneBranchesAhead 'work' $repo)
            "$($a.Action) '$($a.Branch)'" })
        ,@('an orphan with a lane branch holding commits beyond the shift branch is refused, saying why', 'refuse lane branches factory/lane-9', {
            git -C $repo branch factory/lane-9 2>&1 | Out-Null
            git -C $repo checkout -q factory/lane-9 2>&1 | Out-Null
            Set-Content -Path (Join-Path $repo 'lane-work.txt') -Value 'x'
            git -C $repo add lane-work.txt 2>&1 | Out-Null; git -C $repo commit -q -m lanework 2>&1 | Out-Null
            git -C $repo checkout -q work 2>&1 | Out-Null
            $a = Get-OrphanAction -Id 'BL-003' -ParkedBranches @() -DirtyLanes @() -LaneBranchesAhead (Get-LaneBranchesAhead 'work' $repo)
            "$($a.Action) $(if ($a.Why -match 'lane branches (factory/lane-9) have commits') { "lane branches $($Matches[1])" } else { $a.Why })" })
        ,@('an orphan that cannot be placed is Blocked, not a refused shift: the lane branch is kept as a parked branch and the dirty lanes named (AF-0567)', 'kept factory/BL-003-lane-9-test parked lanes 2, 3 named Blocked instead of refusing', {
            $a = Get-OrphanAction -Id 'BL-003' -ParkedBranches @() -DirtyLanes @() -LaneBranchesAhead (Get-LaneBranchesAhead 'work' $repo)
            $why = Get-OrphanBlockReason -Id 'BL-003' -Why $a.Why -DirtyLanes @(2, 3) -LaneBranchesAhead (Get-LaneBranchesAhead 'work' $repo) -Repo $repo -ForStamp 'test'
            $same = (git -C $repo rev-parse 'factory/BL-003-lane-9-test' 2>$null) -eq (git -C $repo rev-parse 'factory/lane-9' 2>$null)
            "$(if ($same -and $why -match 'kept as (factory/BL-003-lane-9-test)') { "kept $($Matches[1])" } else { "'$why'" }) $(if ((Get-ParkedBranches 'BL-003' $repo) -contains 'factory/BL-003-lane-9-test') { 'parked' } else { 'not parked' }) $(if ($why -match 'lane (2, 3) is stashed') { "lanes $($Matches[1]) named" } else { 'lanes not named' }) $(if ($why -match '^Blocked at shift start instead of refusing the shift') { 'Blocked instead of refusing' } else { 'no' })" })
        ,@('a refused start raises the alarm with its reason', 'SHIFT REFUSED  BL-003 is in Doing', {
            $script:Alarmed = @()
            Stop-ShiftStart -Why 'BL-003 is in Doing' -Notify $quiet
            "$($script:Alarmed | Select-Object -First 1)" })
        ,@('a hand-over tree dirty for 3 polls is waited for and returns clean after 3 sleeps of 2 minutes', 'clean 3 sleeps 360s', {
            $clock = @{ T = [datetime]'2026-10-22'; Polls = 0; Slept = @() }
            $r = @(Wait-ForCleanTree -HandOver $true -GetDirty { $clock.Polls++; if ($clock.Polls -le 3) { ' M Curl.slnx' } } `
                -Sleep { param($Seconds) $clock.Slept += $Seconds; $clock.T = $clock.T.AddSeconds($Seconds) } -Now { $clock.T } -Trace { } -Flag { })
            "$(if ($r.Count) { 'dirty' } else { 'clean' }) $($clock.Slept.Count) sleeps $(($clock.Slept | Measure-Object -Sum).Sum)s" })
        ,@('a hand-over tree dirty for 94 minutes is waited for past 30 minutes, flagged once, and starts clean (BL-5592)', 'clean 47 sleeps 1 flag 47 CI watches', {
            # The 2026-11-19 09:40 hand-over: an interactive session's task files stayed uncommitted
            # until 11:14. Before BL-5592 the shift refused at 30 minutes and nothing ran until 13:40.
            $clock = @{ T = [datetime]'2026-11-19T09:40'; Slept = 0; Flags = 0; Watches = 0 }
            $r = @(Wait-ForCleanTree -HandOver $true -GetDirty { if ($clock.T -lt [datetime]'2026-11-19T11:14') { '?? Tasks/Backlog/BL-5555-x.md' } } `
                -Sleep { param($Seconds) $clock.Slept++; $clock.T = $clock.T.AddSeconds($Seconds) } -Now { $clock.T } -Trace { } `
                -Flag { $clock.Flags++ } -WhileWaiting { $clock.Watches++ } -GiveUpMinutes 480)
            "$(if ($r.Count) { 'dirty' } else { 'clean' }) $($clock.Slept) sleeps $($clock.Flags) flag $($clock.Watches) CI watches" })
        ,@('a hand-over tree always dirty returns its paths only after -GiveUpMinutes, and the caller alarms', 'dirty 2 paths 240 sleeps 480 minutes 1 flag SHIFT REFUSED  working tree not clean', {
            $clock = @{ T = [datetime]'2026-10-22'; Slept = 0; Flags = 0 }
            $r = @(Wait-ForCleanTree -HandOver $true -GetDirty { ' M a.cs'; '?? b.cs' } -GiveUpMinutes 480 `
                -Sleep { param($Seconds) $clock.Slept++; $clock.T = $clock.T.AddSeconds($Seconds) } -Now { $clock.T } -Trace { } -Flag { $clock.Flags++ })
            $script:Alarmed = @()
            if ($r) { Stop-ShiftStart -Why 'working tree not clean; commit or stash first' -Notify $quiet }
            "dirty $($r.Count) paths $($clock.Slept) sleeps $(($clock.T - [datetime]'2026-10-22').TotalMinutes) minutes $($clock.Flags) flag $(@($script:Alarmed)[0] -replace ';.*')" })
        ,@('the hand-over give-up defaults to the shift''s -Hours', 'dirty 30 sleeps 60 minutes', {
            $Hours = 1
            $clock = @{ T = [datetime]'2026-10-22'; Slept = 0 }
            $r = @(Wait-ForCleanTree -HandOver $true -GetDirty { ' M a.cs' } `
                -Sleep { param($Seconds) $clock.Slept++; $clock.T = $clock.T.AddSeconds($Seconds) } -Now { $clock.T } -Trace { } -Flag { })
            "$(if ($r.Count) { 'dirty' } else { 'clean' }) $($clock.Slept) sleeps $(($clock.T - [datetime]'2026-10-22').TotalMinutes) minutes" })
        ,@('a hand-over tree clean at once does not sleep, flag or watch CI', 'clean 0 sleeps 0 flags 0 watches', {
            $clock = @{ Slept = 0; Flags = 0; Watches = 0 }
            $r = @(Wait-ForCleanTree -HandOver $true -GetDirty { } -Sleep { param($Seconds) $clock.Slept++ } -Trace { } -Flag { $clock.Flags++ } -WhileWaiting { $clock.Watches++ })
            "$(if ($r.Count) { 'dirty' } else { 'clean' }) $($clock.Slept) sleeps $($clock.Flags) flags $($clock.Watches) watches" })
        ,@('a hand-over checkout ahead of origin waits for the push, watching CI, then returns the clean sync (BL-5592)', 'refusal none restart no 3 polls 2 watches', {
            $clock = @{ T = [datetime]'2026-11-19T05:10'; Polls = 0; Watches = 0 }
            $sync = Wait-ForSyncedCheckout -HandOver $true -Sync {
                $clock.Polls++
                $refusal = if ($clock.Polls -le 2) { 'work/dark-factory is ahead of origin/work/dark-factory; push or reconcile first' } else { $null }
                [pscustomobject]@{ Refusal = $refusal; Restart = $null }
            } -Sleep { param($Seconds) $clock.T = $clock.T.AddSeconds($Seconds) } -Now { $clock.T } -Trace { } -Flag { } -WhileWaiting { $clock.Watches++ }
            "refusal $(if ($sync.Refusal) { $sync.Refusal } else { 'none' }) restart $(if ($sync.Restart) { 'yes' } else { 'no' }) $($clock.Polls) polls $($clock.Watches) watches" })
        ,@('a checkout ahead of origin not at hand-over is refused at once, without waiting', 'refused 1 polls 0 sleeps', {
            $clock = @{ Polls = 0; Slept = 0 }
            $sync = Wait-ForSyncedCheckout -HandOver $false -Sync { $clock.Polls++; [pscustomobject]@{ Refusal = 'x is ahead of origin/x; push or reconcile first'; Restart = $null } } `
                -Sleep { param($Seconds) $clock.Slept++ } -Trace { } -Flag { }
            "$(if ($sync.Refusal) { 'refused' } else { 'started' }) $($clock.Polls) polls $($clock.Slept) sleeps" })
        ,@('a sync that asks for a restart is returned at once with its restart', 'restart factory code changed', {
            $sync = Wait-ForSyncedCheckout -HandOver $true -Sync { [pscustomobject]@{ Refusal = $null; Restart = 'factory code changed' } } -Sleep { } -Trace { } -Flag { }
            "restart $($sync.Restart)" })
        ,@('a start not by hand-over refuses a dirty tree at once without sleeping or tracing', 'dirty 0 sleeps 0 traced', {
            $clock = @{ Slept = 0; Traced = 0 }
            $r = @(Wait-ForCleanTree -HandOver $false -GetDirty { ' M a.cs' } -Sleep { param($Seconds) $clock.Slept++ } -Trace { $clock.Traced++ })
            "$(if ($r.Count) { 'dirty' } else { 'clean' }) $($clock.Slept) sleeps $($clock.Traced) traced" })
        ,@('the dirty paths are traced once, the first 10 then the rest counted, not on every poll', '10 paths, and 2 more, 1 wait, 1 waiting on, 1 give-up', {
            $clock = @{ T = [datetime]'2026-10-22'; Lines = [System.Collections.Generic.List[string]]::new() }
            $null = Wait-ForCleanTree -HandOver $true -GetDirty { 1..12 | ForEach-Object { " M f$_.cs" } } -GiveUpMinutes 60 -Flag { } `
                -Sleep { param($Seconds) $clock.T = $clock.T.AddSeconds($Seconds) } -Now { $clock.T } -Trace { param($Verb, $Detail) $clock.Lines.Add("$Verb|$Detail") }
            $paths = @($clock.Lines | Where-Object { $_ -match '^dirty\| M f\d+\.cs$' }).Count
            $more = @($clock.Lines | Where-Object { $_ -eq 'dirty|and 2 more' }).Count
            $waits = @($clock.Lines | Where-Object { $_ -match '^wait\|.*checking every 2 minutes and watching CI' }).Count
            $waitingOn = @($clock.Lines | Where-Object { $_ -match '^wait\|working tree not clean after 30 minutes; still waiting and watching CI$' }).Count
            $gaveUp = @($clock.Lines | Where-Object { $_ -match '^wait\|working tree not clean after 60 minutes; giving up$' }).Count
            "$paths paths, and 2 more$(if ($more -ne 1) { " x$more" }), $waits wait, $waitingOn waiting on, $gaveUp give-up" }))
    foreach ($case in $cases) {
        $got = & $case[2]
        if ($case[1] -ceq $got) { Write-Host "PASS $($case[0]): $got" -ForegroundColor Green }
        else { Write-Host "FAIL $($case[0]): expected $($case[1]), got $got" -ForegroundColor Red; $failed++ }
    }
    Remove-Item -Recurse -Force -Path $temp -ErrorAction SilentlyContinue
    exit $(if ($failed) { 1 } else { 0 })
}

# This file ran to its end; RunDarkFactory.ps1 goes on to the next one.
$AreaFileFinished = $true
