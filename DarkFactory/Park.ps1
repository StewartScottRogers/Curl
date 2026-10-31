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
    param([string]$Id, [string]$Why, [int[]]$RetrySeconds = @(10, 30, 60, 120, 300))
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
            Invoke-Board @('move', '-Id', $Id, '-To', 'Backlog', '-Reason', "Lane $Lane could not integrate: $Why. The work is on branch $park; start with git cherry-pick --no-commit $park and fix it.") | Out-Null
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
    # branches with commits the shift branch lacks.
    param([string]$Id, [string[]]$ParkedBranches, [int[]]$DirtyLanes, [string[]]$LaneBranchesAhead = @())
    $dirty = @($DirtyLanes | Where-Object { $_ })
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

function Wait-ForCleanTree {
    # Returns the main checkout's dirty paths, or nothing once it is clean. A shift started by
    # a -Continuous hand-over ($ShiftBranch set) traces the dirty paths once and polls every
    # $PollMinutes for up to $WindowMinutes, since an interactive session's commit leaves the
    # tree dirty only briefly (BL-3093); any other start returns at once. It only reads
    # git status: the user's changes are never touched. The caller refuses on a dirty result.
    param(
        [bool]$HandOver = [bool]$ShiftBranch,
        [scriptblock]$GetDirty = { Get-Dirty },
        [scriptblock]$Sleep = { param([int]$Seconds) Start-Sleep -Seconds $Seconds },
        [scriptblock]$Now = { Get-Date },
        [scriptblock]$Trace = { param([string]$Verb, [string]$Detail) Write-Trace '-' $Verb $Detail 'Yellow' },
        [int]$PollMinutes = 2,
        [int]$WindowMinutes = 30)
    $start = & $Now
    $traced = $false
    while ($true) {
        $dirty = @(& $GetDirty | Where-Object { $_ })
        if (-not $dirty.Count -or -not $HandOver) { return $dirty }
        if (-not $traced) {
            $dirty | Select-Object -First 10 | ForEach-Object { & $Trace 'dirty' $_ }
            if ($dirty.Count -gt 10) { & $Trace 'dirty' "and $($dirty.Count - 10) more" }
            & $Trace 'wait' "working tree not clean at hand-over; checking every $PollMinutes minutes for up to $WindowMinutes"
            $traced = $true
        }
        if (((& $Now) - $start).TotalMinutes -ge $WindowMinutes) {
            & $Trace 'wait' "working tree still not clean after $WindowMinutes minutes"
            return $dirty
        }
        & $Sleep ($PollMinutes * 60)
    }
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
    foreach ($id in 'BL-001', 'BL-002', 'BL-004', 'BL-005') {
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
        ,@('a refused start raises the alarm with its reason', 'SHIFT REFUSED  BL-003 is in Doing', {
            $script:Alarmed = @()
            Stop-ShiftStart -Why 'BL-003 is in Doing' -Notify $quiet
            "$($script:Alarmed | Select-Object -First 1)" })
        ,@('a hand-over tree dirty for 3 polls is waited for and returns clean after 3 sleeps of 2 minutes', 'clean 3 sleeps 360s', {
            $clock = @{ T = [datetime]'2026-10-22'; Polls = 0; Slept = @() }
            $r = @(Wait-ForCleanTree -HandOver $true -GetDirty { $clock.Polls++; if ($clock.Polls -le 3) { ' M Curl.slnx' } } `
                -Sleep { param($Seconds) $clock.Slept += $Seconds; $clock.T = $clock.T.AddSeconds($Seconds) } -Now { $clock.T } -Trace { })
            "$(if ($r.Count) { 'dirty' } else { 'clean' }) $($clock.Slept.Count) sleeps $(($clock.Slept | Measure-Object -Sum).Sum)s" })
        ,@('a hand-over tree always dirty returns its paths after exactly 30 minutes (15 polls) and the caller alarms', 'dirty 2 paths 15 sleeps 30 minutes SHIFT REFUSED  working tree not clean', {
            $clock = @{ T = [datetime]'2026-10-22'; Slept = 0 }
            $r = @(Wait-ForCleanTree -HandOver $true -GetDirty { ' M a.cs'; '?? b.cs' } `
                -Sleep { param($Seconds) $clock.Slept++; $clock.T = $clock.T.AddSeconds($Seconds) } -Now { $clock.T } -Trace { })
            $script:Alarmed = @()
            if ($r) { Stop-ShiftStart -Why 'working tree not clean; commit or stash first' -Notify $quiet }
            "dirty $($r.Count) paths $($clock.Slept) sleeps $(($clock.T - [datetime]'2026-10-22').TotalMinutes) minutes $(@($script:Alarmed)[0] -replace ';.*')" })
        ,@('a hand-over tree clean at once does not sleep', 'clean 0 sleeps', {
            $clock = @{ Slept = 0 }
            $r = @(Wait-ForCleanTree -HandOver $true -GetDirty { } -Sleep { param($Seconds) $clock.Slept++ } -Trace { })
            "$(if ($r.Count) { 'dirty' } else { 'clean' }) $($clock.Slept) sleeps" })
        ,@('a start not by hand-over refuses a dirty tree at once without sleeping or tracing', 'dirty 0 sleeps 0 traced', {
            $clock = @{ Slept = 0; Traced = 0 }
            $r = @(Wait-ForCleanTree -HandOver $false -GetDirty { ' M a.cs' } -Sleep { param($Seconds) $clock.Slept++ } -Trace { $clock.Traced++ })
            "$(if ($r.Count) { 'dirty' } else { 'clean' }) $($clock.Slept) sleeps $($clock.Traced) traced" })
        ,@('the dirty paths are traced once, the first 10 then the rest counted, not on every poll', '10 paths, and 2 more, 1 wait, 1 give-up', {
            $clock = @{ T = [datetime]'2026-10-22'; Lines = [System.Collections.Generic.List[string]]::new() }
            $null = Wait-ForCleanTree -HandOver $true -GetDirty { 1..12 | ForEach-Object { " M f$_.cs" } } `
                -Sleep { param($Seconds) $clock.T = $clock.T.AddSeconds($Seconds) } -Now { $clock.T } -Trace { param($Verb, $Detail) $clock.Lines.Add("$Verb|$Detail") }
            $paths = @($clock.Lines | Where-Object { $_ -match '^dirty\| M f\d+\.cs$' }).Count
            $more = @($clock.Lines | Where-Object { $_ -eq 'dirty|and 2 more' }).Count
            $waits = @($clock.Lines | Where-Object { $_ -match '^wait\|.*checking every 2 minutes' }).Count
            $gaveUp = @($clock.Lines | Where-Object { $_ -match '^wait\|.*still not clean after 30 minutes' }).Count
            "$paths paths, and 2 more$(if ($more -ne 1) { " x$more" }), $waits wait, $gaveUp give-up" }))
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
