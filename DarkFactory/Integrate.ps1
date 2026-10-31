# Integrating a lane's work: the fast tests, green checks, repairing a break, rebasing onto
# the shared branch, resolving conflicts, and -TestIntegrateLock. Dot-sourced by
# RunDarkFactory.ps1 in its order; see the loader there.

function Invoke-FastTests {
    # One fast-test run of this checkout: whether it was green, and what failed. A hung test
    # would hold the integrate lock, and so every lane, for ever: the blame collector kills
    # a test host that stops making progress, and the run counts as red. With -Projects only
    # those test projects run, still side by side as in the whole solution's run, through a
    # solution filter written outside the worktree (BL-2967); without them, every one.
    param([string[]]$Projects = @())
    $solution = @(Get-ChildItem -Path $Root -File -Filter '*.slnx') + @(Get-ChildItem -Path $Root -File -Filter '*.sln') | Select-Object -First 1
    $target = $Root
    $filter = ''
    if ($Projects.Count -and $solution) {
        $filter = Join-Path ([IO.Path]::GetTempPath()) "df-affected-$PID-$Lane.slnf"
        $paths = @($Projects | ForEach-Object { "$_\$_.csproj" })
        [IO.File]::WriteAllText($filter, (@{ solution = @{ path = $solution.FullName; projects = $paths } } | ConvertTo-Json -Depth 4))
        $target = $filter
    }
    $tested = if ($filter) { $Projects } else { Get-UnitTestProjectNames $Root }
    try {
        $output = @(& dotnet test $target --no-build -nologo --filter 'TestCategory!=Integration' --blame-hang-timeout 10m 2>&1 | ForEach-Object { "$_" })
        $green = $LASTEXITCODE -eq 0
    } finally { if ($filter) { Remove-Item -LiteralPath $filter -Force -ErrorAction SilentlyContinue } }
    return [pscustomobject]@{ Green = $green; Failed = (Get-FailedTestNames $output); Projects = @(Get-FailedTestProjects $output $tested) }
}

function Test-GreenAlone {
    # Runs only the named test projects' fast tests, with no other test project competing
    # for the machine. True when every one is green, false when any is red or cannot be found.
    param([string[]]$Projects)
    if (-not $Projects.Count) { return $false }
    foreach ($project in $Projects) {
        $path = Join-Path $Root "$project\$project.csproj"
        if (-not (Test-Path $path)) { return $false }
        & dotnet test $path --no-build -nologo --filter 'TestCategory!=Integration' --blame-hang-timeout 10m 2>&1 | Out-Null
        if ($LASTEXITCODE -ne 0) { return $false }
    }
    return $true
}

function Test-Green {
    # Build and fast tests in this checkout, after a rebase put other lanes' work under ours.
    # Returns '' when green, or why not. Under six lanes' load a timing-sensitive test can
    # fail once and pass on the next run, which is no reason to throw a finished task away,
    # so a red run is run once more and only red twice counts (BL-898), unless the projects
    # that failed pass when run alone (AF-0092). The whole solution is built, but only the
    # test projects the commits since -Base can affect are run (BL-2967): every one of them
    # under the lock took every lane's turn, and CI runs them all on three platforms anyway.
    param([string]$Id = '-', [string]$Base = "origin/$Branch")
    & dotnet build $Root -nologo -v q 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) { return 'build failed' }
    $affected = Get-AffectedTestProjects -Repo $Root -Base $Base
    Write-Trace $Id 'verify' $affected.Why
    if (-not $affected.All -and -not $affected.Projects.Count) { return '' }
    $tests = if ($affected.All) { @() } else { @($affected.Projects) }
    $first = Invoke-FastTests -Projects $tests
    if ($first.Green) { return '' }
    Write-Trace $Id 'flaky?' "fast tests red ($($first.Failed)); running them once more" 'DarkYellow'
    $second = Invoke-FastTests -Projects $tests
    if ($second.Green) {
        Write-Trace $Id 'flaky' "failed once, passed on the rerun: $($first.Failed)" 'Yellow'
        return ''
    }
    # A concurrency-sensitive test can stay red through two full runs while every lane builds
    # at once, and then park every finished task in the window (AF-0092: BL-1647 and eight
    # more, on EveryMember_ManyConcurrentCallers_...; AF-0101: BL-1627, red on Curl.Http2 and
    # then on that cookie test, and redone by another lane; AF-0103: BL-1632, red on
    # Curl.Protocol.Smtp.UnitTests and then with no test named, and redone by another lane). So the projects that failed are run
    # once more on their own: green alone means the red came from the machine's load, not
    # from this task's change, which red alone would still show.
    # A project counts as failed when the run names it so or never reports it passed (AF-0093:
    # BL-1649 parked twice on runs red with no test named); a run that broke as a whole
    # vouches for nothing smaller, so both runs must leave some project in doubt.
    $projects = @(@($first.Projects) + @($second.Projects) | Select-Object -Unique)
    if ($first.Projects.Count -and $second.Projects.Count -and (Test-GreenAlone $projects)) {
        Write-Trace $Id 'flaky' "failed twice in the full run, passed alone ($($projects -join ', ')): $($first.Failed); then $($second.Failed)" 'Yellow'
        return ''
    }
    return "fast tests failed twice ($($first.Failed); then $($second.Failed))"
}

function Repair-IntegrationBreak {
    # One repair run for a finished task that went red only on the rebased tree (AF-0051),
    # while this lane still holds the integrate lock, so no other lane's push moves the tree
    # under it. Returns '' once build and fast tests are green and committed, or why not.
    param([string]$Id, [string]$Red)
    Write-Trace $Id 'repair' "$Red after the rebase; one run to fix it on the combined tree" 'DarkYellow'
    Set-HeartbeatStep @('repair', "$Red on the shared branch")
    $text = $RepairPrompt.Replace('{RED}', $Red)
    Invoke-TaskRun -Id $Id -Text $text -Deny $LaneForbidden -Suffix '-repair' -Minutes 30 | Out-Null
    # The repair's work counts only once committed: what is pushed is HEAD, not the worktree.
    if (Test-WorktreeDirty $Root) {
        Invoke-Git @('add', '-A') | Out-Null
        Invoke-Git @('commit', '-q', '-m', "fix: repair $Id on top of the other lanes' work") | Out-Null
    }
    $still = Test-Green -Id $Id -Base "origin/$Branch"
    if ($still) { return "$Red, and still $still after a repair run" }
    Write-Trace $Id 'repair' 'green on the combined tree after the repair run' 'Green'
    return ''
}

function Invoke-RebaseOntoShared {
    # Fetches the shared branch and rebases this lane's commits onto it. Returns 'clean',
    # 'conflict' (git stopped on conflicts and the rebase is still in progress), 'fetch failed'
    # or 'rebase failed'. -Unlocked fetches into this worktree's own FETCH_HEAD and leaves
    # refs/remotes/origin/<branch>, which every lane's worktree shares, alone: a lane rebasing
    # outside the integrate lock never holds that ref's lock against the lane holding it.
    param([switch]$Unlocked)
    $fetch = if ($Unlocked) { @('fetch', '-q', '--refmap=', 'origin', $Branch) } else { @('fetch', '-q', 'origin', $Branch) }
    $onto = if ($Unlocked) { 'FETCH_HEAD' } else { "origin/$Branch" }
    if (-not (Invoke-Git $fetch)) { return 'fetch failed' }
    if (Invoke-Git @('rebase', '-q', $onto)) { return 'clean' }
    if (Test-Rebasing) { return 'conflict' }
    return 'rebase failed'
}

function Resolve-RebaseConflictUnlocked {
    # One resolver run for the rebase in progress, made while this lane does NOT hold the
    # integrate lock, so the other lanes go on integrating for the 6 to 8 minutes it takes
    # (BL-2967). True once the rebase has finished; false after aborting one it left unsettled.
    param([string]$Id)
    Write-Trace $Id 'resolve' 'rebase conflict; resolving with the integrate lock released' 'DarkYellow'
    Set-HeartbeatStep @('resolve', 'rebase conflict, lock released')
    & $script:ResolveRebaseConflict -Id $Id
    if (-not (Test-Rebasing)) { return $true }
    & git -C $Root rebase --abort 2>&1 | Out-Null
    return $false
}

function Update-BoardBeforePush {
    # Returns '' when the board is settled, or why it is not: the caller must not push then (BL-3834).
    param([string]$Id)
    # The board housekeeping every push of this lane's commits needs, run under the integrate lock.
    # Lanes number the tasks they file from their own copy of the board, so two
    # lanes can file the same ID. The other lane's is already shared and keeps it;
    # ours are renumbered, with our own references, before anyone else sees them.
    $dedupe = (Invoke-Board @('dedupe', '-Since', "origin/$Branch")) | Where-Object { $_ -match '->' }
    if ($dedupe) {
        Invoke-Git @('add', '-A') | Out-Null
        Invoke-Git @('commit', '-q', '-m', "chore(tasks): renumber task IDs another lane took first`n`n$($dedupe -join "`n")") | Out-Null
        foreach ($line in $dedupe) { Write-Trace $Id 'renum' $line 'DarkYellow' }
    }
    # ADRs get the same treatment: one this lane added under a number the branch already uses
    # takes the next free number, with its citations (BL-3834). A failure stops the push.
    try {
        foreach ($line in @(Invoke-AdrRenumber -Repo $Root -Base "origin/$Branch")) { Write-Trace $Id 'renum' $line 'DarkYellow' }
    } catch {
        Write-Trace $Id 'renum' "ADR renumbering failed: $_" 'Red'
        return "its ADR numbers could not be checked against the branch (ADR renumbering failed: $_)"
    }
    # Keep Done short enough for Stewart to read at a glance. Only the lane holding
    # this lock archives, on top of every other lane's work, so two lanes never
    # move the same finished tasks into different folders.
    $archived = (Invoke-Board @('archive', '-WhenDoneIsLong')) | Where-Object { $_ -match '^Archived ' }
    if ($archived) {
        Invoke-Git @('add', '-A', '--', 'Tasks/Done') | Out-Null
        Invoke-Git @('commit', '-q', '-m', "chore(tasks): archive Done once it grew past the short list`n`n$archived") | Out-Null
        Write-Trace $Id 'archive' $archived
    }
    return ''
}

function Test-IncomingIsBoardOnly {
    # True when every file that changed between two commits of the shared branch is a task
    # file, which no test selection covers. False when git cannot say.
    param([string]$From, [string]$To)
    $names = @(git -C $Root diff --name-only --no-renames $From $To 2>$null | Where-Object { $_ })
    if ($LASTEXITCODE -ne 0) { return $false }
    return @($names | Where-Object { -not $_.StartsWith('Tasks/') }).Count -eq 0
}

function Invoke-PushRetryWithoutVerify {
    # After a push origin refused on work already verified: rebases onto what was pushed meanwhile
    # and pushes again, with no verify and no attempt spent, for as long as everything pushed
    # meanwhile is task files (a board-only push, a shift-end review push). Returns 'pushed';
    # 'conflict' (the rebase is in progress); or 'verify' when anything else came in, git failed,
    # or $Most tries were not enough: the caller's verify-and-attempt path takes over.
    param([string]$Id, [int]$Most = 10)
    for ($try = 0; $try -lt $Most; $try++) {
        $before = "$(git -C $Root rev-parse "origin/$Branch" 2>$null)".Trim()
        $rebase = Invoke-RebaseOntoShared
        if ($rebase -eq 'conflict') { return 'conflict' }
        if ($rebase -ne 'clean') { return 'verify' }
        $after = "$(git -C $Root rev-parse "origin/$Branch" 2>$null)".Trim()
        if (-not $before -or -not (Test-IncomingIsBoardOnly $before $after)) { return 'verify' }
        Write-Trace $Id 'push' 'refused after another push that changes only task files; pushing again without a new verify' 'DarkYellow'
        if (Update-BoardBeforePush -Id $Id) { return 'verify' }
        if (Invoke-Git @('push', '-q', 'origin', "HEAD:$Branch")) { return 'pushed' }
    }
    return 'verify'
}

function Invoke-Integrate {
    # Rebases this lane's commits onto the shared branch, checks them, and pushes. Returns
    # '' on success or why it could not. A rebase conflict is resolved with the integrate lock
    # released (BL-2967): first in a rebase made before taking the lock, then, should the
    # rebase under the lock still conflict, by releasing it, resolving and taking it again, up
    # to -MaxResolves resolver runs in all. Nothing is pushed but a rebase made under the lock.
    param([string]$Id, [string]$State, [int]$MaxResolves = 3)
    $unsettled = 'rebase conflict the resolver could not settle'
    Set-HeartbeatTask $Id
    # Finished work is parked only for what origin refused, never because origin was out of
    # reach (AF-0025): each rebase starts once origin answers.
    if (-not (Wait-RemoteReachable -Id $Id)) { return 'origin stayed out of reach for an hour' }
    # The rebase before the lock puts the other lanes' work under ours now, so the rebase
    # under the lock only meets what was pushed meanwhile and is almost always clean. Only a
    # conflict counts here; any other failure is met again, and reported, under the lock.
    Write-Heartbeat 'integrate' 'rebase onto the shared branch before the lock'
    $resolves = 0
    if ((Invoke-RebaseOntoShared -Unlocked) -eq 'conflict') {
        $resolves++
        if (-not (Resolve-RebaseConflictUnlocked -Id $Id)) { return $unsettled }
    }
    $attempt = 0
    # A CI fix goes before the ordinary integrations waiting for the lock (ADR-1158).
    $ciFix = Test-CiFixTitle (Get-TaskTitle $Id)
    while ($true) {
        Write-Heartbeat 'integrate' 'waiting for the integrate lock'
        $lock = Enter-Lock -Priority:$ciFix
        Write-Heartbeat 'integrate'
        $conflict = $false
        try {
            # Three attempts for a fetch or a push origin refused; a conflict is no attempt.
            while ($attempt -lt 3) {
                $attempt++
                if (-not (Wait-RemoteReachable -Id $Id)) { return 'origin stayed out of reach for an hour' }
                $rebase = Invoke-RebaseOntoShared
                if ($rebase -eq 'fetch failed') { Start-Sleep -Seconds 10; continue }
                if ($rebase -eq 'rebase failed') { return 'rebase failed' }
                if ($rebase -eq 'conflict') { $attempt--; $conflict = $true; break }
                $boardWhy = Update-BoardBeforePush -Id $Id
                if ($boardWhy) { return $boardWhy }
                if ($State -eq 'Done') {
                    Set-HeartbeatStep @('verify', 'build and fast tests on the shared branch')
                    $red = Test-Green -Id $Id -Base "origin/$Branch"
                    if ($red) { $red = Repair-IntegrationBreak -Id $Id -Red $red }
                    if ($red) { return "$red after rebasing onto the other lanes' work" }
                    Write-Trace $Id 'verify' 'build and fast tests green on the shared branch'
                }
                if (Invoke-Git @('push', '-q', 'origin', "HEAD:$Branch")) { return '' }
                # Refused after a verify: when only task files came in meanwhile, push again
                # at once, without a new verify and without spending another attempt.
                $retry = Invoke-PushRetryWithoutVerify -Id $Id
                if ($retry -eq 'pushed') { return '' }
                if ($retry -eq 'conflict') { $attempt--; $conflict = $true; break }
            }
            if (-not $conflict) { return 'push kept being refused' }
        } finally { $lock.Dispose() }
        # Work pushed while this lane resolved conflicts with its own: the lock is released
        # again for the resolve, and taken again for a fresh rebase once it has finished.
        if ($resolves -ge $MaxResolves) { & git -C $Root rebase --abort 2>&1 | Out-Null; return $unsettled }
        $resolves++
        if (-not (Resolve-RebaseConflictUnlocked -Id $Id)) { return $unsettled }
    }
}

if ($TestIntegrateLock) {
    # Lane 9 integrates in a throwaway repository with a bare origin, while a second clone
    # stands in for the other lanes. The stand-in resolver settles a conflict with git, notes
    # whether the integrate lock was held as it ran, and can push more work from the other
    # clone meanwhile, as a lane integrating during the resolve would (BL-2967).
    $temp = Join-Path ([IO.Path]::GetTempPath()) "df-integrate-lock-$PID"
    $origin = Join-Path $temp 'origin.git'
    $repo = Join-Path $temp 'lane-9'
    $other = Join-Path $temp 'other'
    New-Item -ItemType Directory -Force -Path $temp | Out-Null
    git init -q --bare -b work $origin 2>&1 | Out-Null
    git clone -q $origin $repo 2>&1 | Out-Null
    git -C $repo config user.name t; git -C $repo config user.email t@t
    git -C $repo checkout -q -b work 2>&1 | Out-Null
    foreach ($state in 'Doing', 'Backlog', 'Blocked', 'Deferred', 'Done') { New-Item -ItemType Directory -Force -Path (Join-Path $repo "Tasks\$state") | Out-Null; Set-Content -Path (Join-Path $repo "Tasks\$state\.gitkeep") -Value '' }
    Set-Content -Path (Join-Path $repo 'c.txt') -Value 'base'
    git -C $repo add -A 2>&1 | Out-Null
    git -C $repo commit -q -m base 2>&1 | Out-Null
    git -C $repo push -q origin work 2>&1 | Out-Null
    git clone -q -b work $origin $other 2>&1 | Out-Null
    git -C $other config user.name o; git -C $other config user.email o@o
    $script:Root = $repo; $script:Branch = 'work'; $script:Lane = 9; $script:Stamp = 'test'
    $script:LanesDir = $temp; $script:LockFile = Join-Path $temp 'integrate.lock'; $script:LogDir = Join-Path $temp 'logs'
    # No heartbeat is written, and the task it names is set already: the lane state files
    # Set-HeartbeatTask reads are defined in LaneState.ps1, loaded after this file.
    $script:WritesHeartbeat = $false; $script:Beat.Task = 'BL-001'
    $env:CLAUDE_PROJECT_DIR = $repo
    $testLockHeld = { try { ([IO.File]::Open($script:LockFile, 'OpenOrCreate', 'ReadWrite', 'None')).Dispose(); $false } catch { $true } }
    # Commits c.txt as -Text in a clone, pushing it from the other one.
    $commit = {
        param([string]$Dir, [string]$Text)
        if ($Dir -eq $other) { git -C $other pull -q --rebase origin work 2>&1 | Out-Null }
        Set-Content -Path (Join-Path $Dir 'c.txt') -Value $Text
        git -C $Dir add -A 2>&1 | Out-Null; git -C $Dir commit -q -m $Text 2>&1 | Out-Null
        if ($Dir -eq $other) { git -C $other push -q origin work 2>&1 | Out-Null }
    }
    $fresh = { git -C $repo fetch -q origin work 2>&1 | Out-Null; git -C $repo reset -q --hard origin/work 2>&1 | Out-Null }
    $shared = { git -C $repo fetch -q origin work 2>&1 | Out-Null; "$(git -C $repo show origin/work:c.txt)".Trim() }
    # The stand-in resolver notes whether the lock was held, has the other lanes push first
    # on the resolves -PushesOn names, and settles the conflict unless -Settles is false.
    $script:Resolves = @()
    $script:ResolveRebaseConflict = {
        param([string]$Id)
        $script:Resolves += , (& $testLockHeld)
        if ($script:Resolves.Count -in $script:PushesOn) { & $commit $other "theirs-$($script:Resolves.Count + 1)" }
        if (-not $script:Settles) { return }
        Set-Content -Path (Join-Path $repo 'c.txt') -Value "resolved-$($script:Resolves.Count)"
        git -C $repo add c.txt 2>&1 | Out-Null
        git -C $repo -c core.editor=true rebase --continue 2>&1 | Out-Null
    }
    $integrate = {
        param([bool]$Settles = $true, [int[]]$PushesOn = @())
        $script:Resolves = @(); $script:Settles = $Settles; $script:PushesOn = $PushesOn
        # Git's refusals on a conflict are expected here, so their traces are not printed.
        return (Invoke-Integrate -Id 'BL-001' -State 'Blocked' 6>$null)
    }
    # A race during the verify (BL-3060): the stand-in verify counts its runs and, on the runs
    # -Races names ('board' = a task-file-only push, 'code' = a push changing a file the tests
    # cover), pushes from the other clone before the lane's own push. Redefining Test-Green
    # here replaces the real one only for this self-test, which exits when it has run.
    $script:Verifies = 0; $script:Races = @{}; $script:Pushed = 0; $script:RaceCase = 0
    function Test-Green {
        param([string]$Id = '-', [string]$Base = '')
        $script:Verifies++
        $kind = $script:Races[$script:Verifies]
        if ($kind) {
            $script:Pushed++
            $path = if ($kind -eq 'board') { "Tasks/Backlog/BL-9$($script:Pushed)-race.md" } else { "code-$($script:Pushed).txt" }
            git -C $other pull -q --rebase origin work 2>&1 | Out-Null
            New-Item -ItemType Directory -Force -Path (Split-Path (Join-Path $other $path)) | Out-Null
            Set-Content -Path (Join-Path $other $path) -Value "race $($script:Pushed)"
            git -C $other add -A 2>&1 | Out-Null; git -C $other commit -q -m "race $($script:Pushed)" 2>&1 | Out-Null
            git -C $other push -q origin work 2>&1 | Out-Null
        }
        return ''
    }
    $lanePushes = {
        param([hashtable]$Races)
        & $fresh
        $script:Verifies = 0; $script:Races = $Races; $script:RaceCase++
        Set-Content -Path (Join-Path $repo "ours-case$($script:RaceCase).txt") -Value 'ours'
        git -C $repo add -A 2>&1 | Out-Null; git -C $repo commit -q -m 'ours' 2>&1 | Out-Null
        $r = Invoke-Integrate -Id 'BL-001' -State 'Done' 6>$null
        $git = "$(git -C $repo ls-tree --name-only origin/work | Where-Object { $_ -eq "ours-case$($script:RaceCase).txt" })"
        return "'$r' $($script:Verifies) verifies, lane work shared: $([bool]$git)"
    }
    $source = { param([string]$File) [IO.File]::ReadAllText((Join-Path $PSScriptRoot $File)) }
    $failed = 0
    try {
        $cases = @(
            ,@('a task-file-only push during the verify is met by a second push, with no second verify', "'' 1 verifies, lane work shared: True", {
                & $lanePushes @{ 1 = 'board' } })
            ,@('a push changing a covered file during the verify makes the lane verify again', "'' 2 verifies, lane work shared: True", {
                & $lanePushes @{ 1 = 'code' } })
            ,@('three covered pushes in a row still end with the push kept being refused', "'push kept being refused' 3 verifies, lane work shared: False", {
                & $lanePushes @{ 1 = 'code'; 2 = 'code'; 3 = 'code' } })
            ,@('a task-file-only race on the third attempt still pushes: the fast retry spends no attempt', "'' 3 verifies, lane work shared: True", {
                & $lanePushes @{ 1 = 'code'; 2 = 'code'; 3 = 'board' } })
            ,@('the shift-end review push is made under the integrate lock, not by the reviewer', 'True False', {
                $end = & $source 'ShiftEnd.ps1'
                "$($end -match 'Push-ReviewCommitsUnderLock') $($end -match '(?m)^4\..*\band push to origin')" })
            ,@('the coordinator takes the integrate lock around its board-move push', 'True', {
                $text = (& $source 'ShiftCoordinator.ps1').Replace("`r`n", "`n")
                "$($text -match '(?s)Enter-Lock\r?\n[^\n]*git -C \$Root push -q origin \"HEAD:\$branch\"')" })
            ,@('the probe sees the integrate lock while it is held', 'True False', {
                $lock = Enter-Lock; $held = & $testLockHeld; $lock.Dispose()
                "$held $(& $testLockHeld)" })
            ,@('a CI fix waiting with -Priority holds other lanes back; its stale marker or its own does not (ADR-1158)', 'True False False True False', {
                $other3 = Get-LockPriorityPath 3
                Set-Content -Path $other3 -Value 'x'
                $fresh3 = Test-LockPriorityWaiting
                (Get-Item $other3).LastWriteTime = (Get-Date).AddMinutes(-2)
                $stale3 = Test-LockPriorityWaiting
                Remove-Item $other3
                Set-Content -Path (Get-LockPriorityPath) -Value 'x'
                $own = Test-LockPriorityWaiting
                Remove-Item (Get-LockPriorityPath)
                $lock = Enter-Lock -Priority; $held = & $testLockHeld; $lock.Dispose()
                "$fresh3 $stale3 $own $held $(Test-Path (Get-LockPriorityPath))" })
            ,@('an integration of a CI fix asks for the lock with -Priority', 'True', {
                "$((& $source 'Integrate.ps1') -match 'Enter-Lock -Priority:\$ciFix')" })
            ,@('a clean integration pushes, with no resolve', "'' ours-1 0 resolves", {
                & $commit $repo 'ours-1'
                $r = & $integrate
                "'$r' $(& $shared) $($script:Resolves.Count) resolves" })
            ,@('a conflict is resolved with the lock released, and the rebased work pushed', "'' resolved-1 lock held: False", {
                & $fresh; & $commit $other 'theirs-1'; & $commit $repo 'ours-2'
                $r = & $integrate
                "'$r' $(& $shared) lock held: $($script:Resolves -join ',')" })
            ,@('work pushed during a resolve is resolved again, the lock released each time', "'' resolved-2 lock held: False,False", {
                & $fresh; & $commit $other 'theirs-a'; & $commit $repo 'ours-3'
                $r = & $integrate -PushesOn @(1)
                "'$r' $(& $shared) lock held: $($script:Resolves -join ',')" })
            ,@('a lane that meets a new conflict after every resolve gives up after 3', "'rebase conflict the resolver could not settle' 3 resolves, rebasing: False", {
                & $fresh; & $commit $other 'theirs-x'; & $commit $repo 'ours-4'
                $r = & $integrate -PushesOn @(1, 2, 3)
                "'$r' $($script:Resolves.Count) resolves, rebasing: $(Test-Rebasing)" })
            ,@('a resolver that fails leaves no rebase in progress and pushes nothing', "'rebase conflict the resolver could not settle' rebasing: False, shared: theirs-y", {
                & $fresh; & $commit $other 'theirs-y'; & $commit $repo 'ours-5'
                $r = & $integrate -Settles $false
                "'$r' rebasing: $(Test-Rebasing), shared: $(& $shared)" }))
        foreach ($case in $cases) {
            $got = & $case[2]
            if ($case[1] -ceq $got) { Write-Host "PASS $($case[0]): $got" -ForegroundColor Green }
            else { Write-Host "FAIL $($case[0]): expected $($case[1]), got $got" -ForegroundColor Red; $failed++ }
        }
    } finally {
        Remove-Item -Recurse -Force -Path $temp -ErrorAction SilentlyContinue
    }
    exit $(if ($failed) { 1 } else { 0 })
}

# This file ran to its end; RunDarkFactory.ps1 goes on to the next one.
$AreaFileFinished = $true
