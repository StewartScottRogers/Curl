# Stray changes a run left behind: saving them, restoring them for the task they belong to,
# and -TestTaskStash. Dot-sourced by RunDarkFactory.ps1 in its order; see the loader there.

function Save-StrayChanges {
    param([string]$Id)
    if (-not (Get-Dirty)) { return }
    git -C $Root stash push --include-untracked -m "darkfactory $Id $Stamp" | Out-Null
    Write-Trace $Id 'stash' "uncommitted work kept: git stash list" 'Yellow'
}

function Find-StashIndex {
    # The index of the stash list entry whose hash is -Sha, -1 for none.
    param([string]$Sha, [string]$Repo = $Root)
    $index = 0
    foreach ($line in @(git -C $Repo stash list --format='%H' 2>$null)) {
        if ($line -eq $Sha) { return $index }
        $index++
    }
    return -1
}

function Restore-TaskStash {
    # A task that went back to Backlog with work in progress left it in the shared stash
    # list as "darkfactory <id> <stamp>" (Save-StrayChanges). A lane's run may not run
    # git stash, not even git stash list (AF-0091: BL-1609's fourth claim was spent finding
    # the third's stash; AF-0099: BL-1683's second claim began by hunting for the first's),
    # so the shift applies the newest such stash itself when the task
    # is claimed again. Returns the note put in front of the run's prompt, '' for none.
    # -BetweenLookupAndDrop runs after the applied stash's index is found and before it is dropped (a test hook).
    param([string]$Id, [string]$Repo = $Root, [scriptblock]$BetweenLookupAndDrop = {})
    $sha = Get-TaskStashSha $Id $Repo
    if (-not $sha) { return '' }
    $applied = $false
    if (-not (@(git -C $Repo status --porcelain) | Where-Object { $_ })) {
        git -C $Repo stash apply -q $sha 2>&1 | Out-Null
        $applied = $LASTEXITCODE -eq 0
        if (-not $applied) {
            # A conflict half-applies it; the worktree was clean, so put it back that way.
            git -C $Repo reset -q --hard HEAD 2>&1 | Out-Null
            git -C $Repo clean -fdq 2>&1 | Out-Null
        }
    }
    if ($applied) {
        # Its work now lives in the worktree and will be committed; a stash left in the list
        # is offered again on the next claim (AF-0327: BL-2968's third claim was told a
        # stash "did not apply cleanly" that its second claim had already applied and committed).
        # A run that goes back to Backlog stashes its work afresh, so dropping loses nothing.
        # The list is shared with every lane, so the index can shift between the lookup and the
        # drop (BL-3103): the drop checks the index still names the applied hash, else looks again.
        $index = Find-StashIndex $sha $Repo
        & $BetweenLookupAndDrop
        if ($index -ge 0 -and "$(git -C $Repo rev-parse "stash@{$index}" 2>$null)".Trim() -ne $sha) { $index = Find-StashIndex $sha $Repo }
        if ($index -ge 0) {
            $dropOutput = git -C $Repo stash drop -q "stash@{$index}" 2>&1
            if ($LASTEXITCODE -ne 0) { Write-Trace $Id 'stash' "dropping the applied stash $($sha.Substring(0, 8)) failed: $dropOutput" 'Red' }
        }
        Write-Trace $Id 'stash' "applied the earlier run's work from stash $($sha.Substring(0, 8))" 'Yellow'
        return @"
STASHED WORK. An earlier run of $Id went back to Backlog with uncommitted work, which the
shift kept as stash $sha and has applied to this worktree. Read git status, git diff and
the task file before changing anything, and carry on from that work rather than starting over.
Do not rebuild or rerun the whole fast test suite to re-verify it (AF-0310: BL-2743's second
run re-ran 33813 and 3573 tests the first had passed, about 38% of $4.75 over two runs): build
once, run only the tests of the work still to be done, and run the full set once at the end.

"@
    }
    Write-Trace $Id 'stash' "stash $($sha.Substring(0, 8)) from an earlier run did not apply cleanly; the run is told its hash" 'Yellow'
    return @"
STASHED WORK. An earlier run of $Id went back to Backlog with uncommitted work, which the
shift kept as stash $sha. It did not apply cleanly to this branch, so this worktree is
clean. ``git diff $sha^1 $sha`` shows its changes to tracked files and
``git show --stat $sha^3`` the files it added (``git show $sha^3:<path>`` prints one);
carry over what still fits rather than starting over.

"@
}

# ---------------------------------------------------------------------------- split

# A task over its cost cap is split by one bounded headless run (BL-3091, ADR-0759): at most
# once per task per day, on Sonnet, with a small cost and turn cap of its own.
$TaskSplitBudgetUsd = 1
$TaskSplitMaxTurns = 30
$TaskSplitMinutes = 20

function Get-TaskStashSha {
    # The hash of the newest "darkfactory <id> <stamp>" stash, '' for none: one written by Save-StrayChanges
    # (git stash push, subject "On <branch>: darkfactory ...") or by Copy-TaskStash (git stash store, bare subject).
    param([string]$Id, [string]$Repo = $Root)
    $pattern = "^(\S+) (?:.*: )?darkfactory $([regex]::Escape($Id)) "
    $entry = @(git -C $Repo stash list --format='%H %gs' 2>$null) | Where-Object { $_ -match $pattern } | Select-Object -First 1
    if (-not $entry) { return '' }
    return ($entry -split ' ', 2)[0]
}

function Copy-TaskStash {
    # Stores a copy of stash -Sha under -Id as "darkfactory <id> <stamp>" and returns the copy's
    # hash. A copy, not the same hash: git stash store writes no entry for a hash already at the
    # top of the list or already stored, so one stash for several tasks needs one commit each.
    param([string]$Id, [string]$Sha, [string]$Stamp, [string]$Repo = $Root, [string]$ParentId = '')
    # -ParentId, the task whose stash is copied, goes into the commit message so a later move knows whose work this is (BL-3112).
    # Throws when any git command fails (another lane may hold the stash ref lock).
    $revList = git -C $Repo rev-list --parents -n 1 $Sha 2>&1
    if ($LASTEXITCODE -ne 0) { throw "git rev-list of stash $Sha failed: $revList" }
    $parents = @("$revList" -split ' ' | Select-Object -Skip 1)
    $commitArgs = @('commit-tree', "$Sha^{tree}") + @($parents | ForEach-Object { '-p'; $_ }) + @('-m', "darkfactory $Id $Stamp") + $(if ($ParentId) { @('-m', "stash-continues $ParentId") } else { @() })
    $copy = "$(git -C $Repo @commitArgs 2>&1)".Trim()
    if ($LASTEXITCODE -ne 0 -or -not $copy) { throw "git commit-tree of stash $Sha failed: $copy" }
    $storeOutput = git -C $Repo stash store -m "darkfactory $Id $Stamp" $copy 2>&1
    if ($LASTEXITCODE -ne 0) { throw "git stash store of $copy for $Id failed: $storeOutput" }
    return $copy
}

function Remove-TaskStashEntries {
    # Drops the stash list entries whose hash is in -Shas, and no others. The list is shared with
    # every lane, so an index can shift at any moment (BL-3105): each drop looks the index up afresh
    # and checks it still names the hash, else looks again. -BetweenLookupAndDrop is a test hook.
    param([string[]]$Shas, [string]$Repo = $Root, [scriptblock]$BetweenLookupAndDrop = {}, [string]$Id = 'split')
    foreach ($sha in $Shas) {
        $index = Find-StashIndex $sha $Repo
        & $BetweenLookupAndDrop
        if ($index -ge 0 -and "$(git -C $Repo rev-parse "stash@{$index}" 2>$null)".Trim() -ne $sha) { $index = Find-StashIndex $sha $Repo }
        if ($index -lt 0) { continue }
        $dropOutput = git -C $Repo stash drop -q "stash@{$index}" 2>&1
        if ($LASTEXITCODE -ne 0) { Write-Trace $Id 'stash' "dropping stash $($sha.Substring(0, 8)) failed: $dropOutput" 'Red' }
    }
}

function Get-ContinuedStashParent {
    # The ID in the "Continues the stashed work of BL-<n>" line of task -Id's file, '' for none.
    param([string]$Id)
    $line = @(Get-ChildItem (Join-Path $Root 'Tasks') -Recurse -Filter "$Id-*.md" | Select-String -Pattern 'Continues the stashed work of (BL-\d+)' | Select-Object -First 1)
    if (-not $line.Count) { return '' }
    return $line[0].Matches[0].Groups[1].Value
}

function Move-RenumberedTaskStash {
    # A split stored a copy of the parent's stash as "darkfactory <old> <stamp>" for a new task, and
    # the task was then renumbered <old> -> <new> as a duplicate ID (Invoke-RenumberDuplicates). The
    # copy follows it: re-stored under <new>, the old entry dropped by hash, so no stash is offered
    # to the other task that kept <old>. Only a copy stored by Copy-TaskStash (bare subject, no
    # "On <branch>:" prefix, which Save-StrayChanges writes for a task's own run) moves, and only
    # the one whose commit message names the parent the renumbered task's file says it continues
    # (BL-3112): two lanes can split different parents into tasks that share <old>.
    # Returns the new hashes. -ParentOf (renumbered ID -> parent ID, '' for none) is a test hook.
    param(
        [string]$OldId, [string]$NewId, [string]$Repo = $Root,
        [scriptblock]$ParentOf = { param($Id) Get-ContinuedStashParent $Id })
    $parent = & $ParentOf $NewId
    if (-not $parent) { return @() }
    $moved = @()
    $pattern = "^(\S+) darkfactory $([regex]::Escape($OldId)) (\S+)$"
    foreach ($entry in @(git -C $Repo stash list --format='%H %gs' 2>$null | Where-Object { $_ -match $pattern })) {
        $null = $entry -match $pattern
        $sha = $Matches[1]; $stamp = $Matches[2]
        $message = @(git -C $Repo show -s --format=%B $sha 2>$null) -join "`n"
        if ($message -notmatch "(?m)^stash-continues $([regex]::Escape($parent))\s*$") { continue }
        $copy = Copy-TaskStash $NewId $sha $stamp $Repo -ParentId $parent
        Remove-TaskStashEntries @($sha) $Repo -Id $NewId
        $moved += $copy
        Write-Trace $NewId 'stash' "moved the stash copy $($sha.Substring(0, 8)) of $OldId to $NewId, which was renumbered from it (now $($copy.Substring(0, 8)))" 'Yellow'
    }
    return $moved
}

function Move-RenumberedStashesAfterPush {
    # Runs -Push, the push of a duplicate-ID renumbering, and only when it returns true moves each
    # renumbered task's stash copy (BL-3112): a refused push is reset away by the next Sync-Lane, so
    # a copy moved before it would sit under an ID origin never saw. A move that throws is traced
    # with the IDs and git's message and the copy stays under the old ID. -Renumbered is the board's
    # "BL-old -> BL-new" lines. Returns the push result.
    param([string]$Id, [string[]]$Renumbered, [scriptblock]$Push)
    if (-not (& $Push)) { return $false }
    foreach ($line in $Renumbered) {
        if ($line -notmatch '^(BL-\d+) -> (BL-\d+)') { continue }
        $oldId = $Matches[1]; $newId = $Matches[2]
        try { $null = Move-RenumberedTaskStash $oldId $newId }
        catch { Write-Trace $Id 'renum' "moving the stash copy of $oldId to $newId failed: $($_.Exception.Message); it stays under $oldId" 'Red' }
    }
    return $true
}

function Register-TaskSplitAttempt {
    # $true the first time -Id is offered for a split on -Now's day, which it records as a
    # marker file in -MarkerDir; $false on every later offer that day.
    param([string]$Id, [datetime]$Now, [string]$MarkerDir)
    $marker = Join-Path $MarkerDir "$Id-$($Now.ToString('yyyyMMdd')).txt"
    if (Test-Path $marker) { return $false }
    New-Item -ItemType Directory -Force -Path $MarkerDir | Out-Null
    Set-Content -Path $marker -Value $Now.ToString('o')
    return $true
}

function Get-TaskSplitArguments {
    # The cmd.exe argument line the split run is launched with.
    param([string[]]$Deny, [string]$Context, [string]$ErrPath)
    $denied = ($Deny | ForEach-Object { "`"Bash($_`:*)`" `"PowerShell($_`:*)`"" }) -join ' '
    $budget = ([double]$TaskSplitBudgetUsd).ToString([System.Globalization.CultureInfo]::InvariantCulture)
    return "claude -p --model sonnet --dangerously-skip-permissions --output-format stream-json --verbose $Context --max-budget-usd $budget --max-turns $TaskSplitMaxTurns --disallowedTools $denied 2>`"$ErrPath`""
}

function Invoke-TaskSplitRun {
    # Runs the split prompt -Text in this checkout and returns @{ ExitCode }; a run past
    # $TaskSplitMinutes is killed and reported as exit -1.
    param([string]$Id, [string]$Text)
    $raw = Join-Path $LogDir "split-$Id-$Stamp$LaneTag.jsonl"
    $err = Join-Path $LogDir "split-$Id-$Stamp$LaneTag.err.txt"
    $psi = New-ClaudeRunStartInfo (Get-TaskSplitArguments $SplitForbidden (Get-LaneContextArguments $Root $LogDir) $err)
    $psi.EnvironmentVariables['ENABLE_CLAUDEAI_MCP_SERVERS'] = 'false'
    $psi.EnvironmentVariables['CLAUDE_CODE_DISABLE_AUTO_MEMORY'] = '1'
    $p = [System.Diagnostics.Process]::Start($psi)
    $p.StandardInput.Write($Text)
    $p.StandardInput.Close()
    $deadline = (Get-Date).AddMinutes($TaskSplitMinutes)
    $pending = $p.StandardOutput.ReadLineAsync()
    while ($true) {
        Write-HeartbeatIfDue
        if ($pending.Wait(1000)) {
            if ($null -eq $pending.Result) { break }
            Add-Content -Path $raw -Value $pending.Result -Encoding UTF8
            $pending = $p.StandardOutput.ReadLineAsync()
        } elseif ((Get-Date) -gt $deadline) {
            Stop-FactoryProcessTree -ProcessId $p.Id
            $p.WaitForExit()
            return @{ ExitCode = -1 }
        }
    }
    $p.WaitForExit()
    return @{ ExitCode = $p.ExitCode }
}

function Test-TaskSplitLeavesBlockToCaller {
    # True when the caller of Invoke-TaskSplit must block the task itself (and commit that): the
    # split was not attempted ('skipped') or left the task out of Blocked ('failed'). False for
    # every other result, 'reset-failed' included: the split commit stays and nothing goes on top.
    param([string]$SplitResult)
    return $SplitResult -in 'skipped', 'failed'
}

function Invoke-TaskSplit {
    # Splits -Id, which is over its cost cap for -Why, into smaller tasks with one split run,
    # then commits and pushes the board change under the integrate lock. Returns 'skipped'
    # when -Id was already offered for a split on -Now's day, 'split' when the run filed new
    # tasks and deferred -Id, 'blocked' when it failed and -Id is now in Blocked, 'failed' when it
    # failed and -Id is not in Blocked (the caller then blocks -Id itself), or 'reset-failed' when the
    # reset of the split commit failed: that commit stays, -Id stays Deferred as in it, and the caller
    # makes no commit and no push on top of it (Test-TaskSplitLeavesBlockToCaller).
    # The stash is only ever copied under the new IDs, never popped.
    param(
        [string]$Id, [string]$Why, [datetime]$Now = (Get-Date), [string]$MarkerDir = (Join-Path $LogDir 'splits'),
        [scriptblock]$RunSplit = { param($SplitId, $Text) Invoke-TaskSplitRun $SplitId $Text },
        [scriptblock]$ListTaskIds = { @(Get-ChildItem (Join-Path $Root 'Tasks') -Recurse -Filter 'BL-*.md' | ForEach-Object { $_.Name -replace '^(BL-\d+).*$', '$1' }) },
        [scriptblock]$StashSha = { param($SplitId) Get-TaskStashSha $SplitId },
        [scriptblock]$ContinuesStash = { param($NewId, $SplitId) @(Get-ChildItem (Join-Path $Root 'Tasks') -Recurse -Filter "$NewId-*.md" | Select-String -SimpleMatch "Continues the stashed work of $SplitId" -List).Count -gt 0 },
        [scriptblock]$StoreStash = { param($NewId, $Sha, $Stamp, $SplitId) Copy-TaskStash $NewId $Sha $Stamp -ParentId $SplitId },
        [scriptblock]$DropStash = { param($Shas) Remove-TaskStashEntries $Shas },
        [scriptblock]$TaskState = { param($SplitId) Get-TaskState $SplitId },
        [scriptblock]$EnterLock = { Enter-Lock },
        [scriptblock]$Git = { param([string[]]$GitArgs) Invoke-Git $GitArgs },
        [scriptblock]$HeadSha = { "$(& git -C $Root rev-parse HEAD 2>$null)".Trim() },
        [scriptblock]$BoardCall = { param([string[]]$BoardArgs) Invoke-Board $BoardArgs },
        [switch]$PullOnRefusal, [switch]$LeaveForIntegration)
    if (-not (Register-TaskSplitAttempt $Id $Now $MarkerDir)) { return 'skipped' }
    $sha = & $StashSha $Id
    $stash = if ($sha) { "The shift kept its unfinished work as stash $sha." } else { 'It left no stashed work, so there is no stash to store.' }
    $text = $SplitPrompt.Replace('{ID}', $Id).Replace('{WHY}', $Why).Replace('{STASH}', $stash).Replace('{SHA}', $(if ($sha) { $sha } else { '<none>' })).Replace('{STAMP}', $Now.ToString('yyyyMMdd-HHmmss'))
    Write-Trace $Id 'split' "over its cost cap; one split run of at most `$$TaskSplitBudgetUsd and $TaskSplitMaxTurns turns" 'Yellow'
    $before = @(& $ListTaskIds)
    $run = & $RunSplit $Id $text
    $new = @(& $ListTaskIds | Where-Object { $before -notcontains $_ } | Sort-Object -Unique)
    $state = & $TaskState $Id
    $failure = if ($run.ExitCode -ne 0) { "the split run exited $($run.ExitCode)" }
        elseif (-not $new.Count) { 'the split run filed no new tasks' }
        elseif ($state -ne 'Deferred') { "the split run left it in $state, not Deferred" } else { '' }
    $lock = & $EnterLock
    $stored = @()
    $headBefore = $null; $rebasedSplit = $null; $committed = $false; $pushed = $false
    try {
        if (-not $failure) {
            # The shift stores the stash once per new task that says it continues the work.
            if ($sha) {
                try { foreach ($newId in $new) { if (& $ContinuesStash $newId $Id) { $stored += @(& $StoreStash $newId $sha $Now.ToString('yyyyMMdd-HHmmss') $Id) } } }
                catch { $failure = "storing the stash for a new task failed: $($_.Exception.Message)" }
                if (-not $failure -and -not $stored.Count) { Write-Trace $Id 'split' "stash $sha is continued by no new task ($($new -join ', ')); its work stays under $Id" 'Red' }
            }
        }
        if (-not $failure) {
            & $Git @('add', '-A', 'Tasks') | Out-Null
            $headBefore = & $HeadSha
            $committed = & $Git @('commit', '-q', '-m', "chore(tasks): split $Id into $($new -join ', ') - over its cost cap", '-m', $SkipCiNote)
            $pushed = $committed -and -not $LeaveForIntegration -and (& $Git @('push', '-q', 'origin', "HEAD:$Branch"))
            # A claim synced this checkout to the shared branch, so it may take what other lanes
            # pushed since; after a run the lane may hold commits of its own, which only the
            # shift's integration may push, so there (-LeaveForIntegration) neither the split commit nor
            # the block commit is pushed and both are left for it.
            if ($committed -and -not $pushed -and $PullOnRefusal) {
                if (& $Git @('pull', '-q', '--rebase', 'origin', $Branch)) { $rebasedSplit = & $HeadSha; $pushed = & $Git @('push', '-q', 'origin', "HEAD:$Branch") }
                else { & $Git @('rebase', '--abort') | Out-Null }
            }
            if ($committed -and ($pushed -or -not $PullOnRefusal -or $LeaveForIntegration)) { Write-Trace $Id 'split' "split into $($new -join ', ')$(if (-not $pushed) { '; left for the shift to integrate' })" 'Cyan'; return 'split' }
            $failure = if ($committed) { 'the push of the split was refused' } else { 'the commit of the split failed' }
            # Only the split's own commit is removed. After a successful pull it was rebased onto
            # the commits the pull brought in, which stay: its parent is the target. Without a
            # rebase it is the hash HEAD had before the commit. A failed commit leaves the split's
            # new task files in the index, so they are unstaged for the checkout below to restore.
            $resetArgs = if ($committed -and $rebasedSplit) { @('reset', '-q', '--hard', "$rebasedSplit~1") }
                elseif ($committed -and $headBefore) { @('reset', '-q', '--hard', $headBefore) }
                elseif (-not $committed) { @('reset', '-q', '--', 'Tasks') }
            if ($resetArgs -and -not (& $Git $resetArgs)) {
                # A split commit that cannot be reset stays; nothing more goes on top of it. A failed
                # unstaging reset has no commit to keep, so the restore below still runs.
                if ($committed) {
                    Write-Trace $Id 'split' "failed: $failure; its reset failed, so the split commit stays and nothing more is committed or pushed on top of it" 'Red'
                    return 'reset-failed'
                }
                Write-Trace $Id 'split' "the unstaging reset failed; the restore of Tasks still runs" 'Yellow'
            }
        }
        # A failed split leaves nothing of its own behind: the board goes back to how it
        # was and the task is blocked as before the split existed, with the stashes it stored dropped.
        if ($stored.Count) { & $DropStash $stored | Out-Null }
        & $Git @('checkout', '-q', '--', 'Tasks') | Out-Null
        & $Git @('clean', '-fdq', '--', 'Tasks') | Out-Null
        & $BoardCall @('move', '-Id', $Id, '-To', 'Blocked', '-Reason', "Stewart: dark factory $Why; the automatic split failed: $failure") | Out-Null
        & $Git @('add', '-A', 'Tasks') | Out-Null
        & $Git @('commit', '-q', '-m', "chore(tasks): block $Id - automatic split failed: $failure", '-m', $SkipCiNote) | Out-Null
        if (-not $LeaveForIntegration) { & $Git @('push', '-q', 'origin', "HEAD:$Branch") | Out-Null }
        if ((& $TaskState $Id) -ne 'Blocked') { Write-Trace $Id 'split' "failed: $failure; the move to Blocked did not take" 'Red'; return 'failed' }
        Write-Trace $Id 'split' "failed: $failure; blocked" 'Red'
        return 'blocked'
    } catch {
        # A step that threw (the board, the stash drop, git) leaves no half-changed Tasks
        # folder and no unpushed split commit behind: each undo is tried on its own, then the
        # task is blocked with the exception text (BL-3110).
        $thrown = Get-Short $_.Exception.Message 200
        $undo = { param([scriptblock]$Step) try { & $Step | Out-Null } catch { Write-Trace $Id 'split' "undo after the throw failed: $(Get-Short $_.Exception.Message 100)" 'Red' } }
        $resetArgs = if ($committed -and -not $pushed -and $rebasedSplit) { @('reset', '-q', '--hard', "$rebasedSplit~1") }
            elseif ($committed -and -not $pushed -and $headBefore) { @('reset', '-q', '--hard', $headBefore) }
            elseif (-not $committed) { @('reset', '-q', '--', 'Tasks') }
        if ($resetArgs) {
            $reset = @{ Ok = $false }
            & $undo { $reset.Ok = [bool](& $Git $resetArgs) }
            if (-not $reset.Ok -and $committed) {
                Write-Trace $Id 'split' "threw: $thrown; its reset failed, so the split commit stays and nothing more is committed or pushed on top of it" 'Red'
                return 'reset-failed'
            }
            if (-not $reset.Ok) { Write-Trace $Id 'split' "threw: $thrown; the unstaging reset failed; the restore of Tasks still runs" 'Yellow' }
        }
        if ($stored.Count) { & $undo { & $DropStash $stored } }
        & $undo { & $Git @('checkout', '-q', '--', 'Tasks') }
        & $undo { & $Git @('clean', '-fdq', '--', 'Tasks') }
        & $undo { & $BoardCall @('move', '-Id', $Id, '-To', 'Blocked', '-Reason', "Stewart: dark factory $Why; the automatic split threw: $thrown") }
        & $undo { & $Git @('add', '-A', 'Tasks') }
        & $undo { & $Git @('commit', '-q', '-m', "chore(tasks): block $Id - automatic split threw: $thrown", '-m', $SkipCiNote) }
        if (-not $LeaveForIntegration) { & $undo { & $Git @('push', '-q', 'origin', "HEAD:$Branch") } }
        $after = @{ State = '' }
        & $undo { $after.State = & $TaskState $Id }
        if ($after.State -ne 'Blocked') { Write-Trace $Id 'split' "threw: $thrown; the move to Blocked did not take" 'Red'; return 'failed' }
        Write-Trace $Id 'split' "threw: $thrown; blocked" 'Red'
        return 'blocked'
    } finally { $lock.Dispose() }
}

if ($TestTaskStash) {
    $repo = Join-Path ([IO.Path]::GetTempPath()) "df-task-stash-$PID"
    New-Item -ItemType Directory -Force -Path $repo | Out-Null
    git -C $repo init -q -b master 2>&1 | Out-Null
    git -C $repo config user.name t; git -C $repo config user.email t@t
    Set-Content -Path (Join-Path $repo 'a.txt') -Value 'one'
    git -C $repo add a.txt; git -C $repo commit -q -m one 2>&1 | Out-Null
    $failed = 0
    $check = { param($Name, $Expected, $Got)
        if ($Expected -ceq $Got) { Write-Host "PASS ${Name}: $Got" -ForegroundColor Green }
        else { Write-Host "FAIL ${Name}: expected $Expected, got $Got" -ForegroundColor Red; $script:failed++ } }
    $read = { param($f) $p = Join-Path $repo $f; if (Test-Path $p) { (Get-Content $p -Raw).Trim() } else { '-' } }
    try {
        & $check 'no stash, no note' '' (Restore-TaskStash -Id 'BL-1' -Repo $repo)
        # An earlier run's work: a tracked change and a new file, stashed as the shift does.
        Set-Content -Path (Join-Path $repo 'a.txt') -Value 'two'
        Set-Content -Path (Join-Path $repo 'b.txt') -Value 'new'
        git -C $repo stash push -q --include-untracked -m 'darkfactory BL-1 20260101-000000' 2>&1 | Out-Null
        & $check 'other task''s stash is left alone' '' (Restore-TaskStash -Id 'BL-10' -Repo $repo)
        & $check 'other task''s claim stays clean' 'one -' "$(& $read 'a.txt') $(& $read 'b.txt')"
        $note = Restore-TaskStash -Id 'BL-1' -Repo $repo
        & $check 'its own stash is applied' 'two new' "$(& $read 'a.txt') $(& $read 'b.txt')"
        & $check 'the note says it was applied' 'True' "$($note -match 'has applied to this worktree')"
        & $check 'the note says not to re-verify finished work' 'True' "$($note -match 'Do not rebuild or rerun')"
        & $check 'an applied stash is dropped from the list' '0' "$(@(git -C $repo stash list).Count)"
        # Back to clean, then a branch that moved under a stash: it cannot apply.
        git -C $repo reset -q --hard HEAD 2>&1 | Out-Null; git -C $repo clean -fdq 2>&1 | Out-Null
        Set-Content -Path (Join-Path $repo 'a.txt') -Value 'three'
        git -C $repo commit -q -am three 2>&1 | Out-Null
        Set-Content -Path (Join-Path $repo 'a.txt') -Value 'four'
        git -C $repo stash push -q -m 'darkfactory BL-1 20260102-000000' 2>&1 | Out-Null
        git -C $repo reset -q --hard HEAD 2>&1 | Out-Null
        Set-Content -Path (Join-Path $repo 'a.txt') -Value 'five'
        git -C $repo commit -q -am five 2>&1 | Out-Null
        $note = Restore-TaskStash -Id 'BL-1' -Repo $repo
        & $check 'a conflict leaves the worktree clean' "five - True" "$(& $read 'a.txt') $(& $read 'b.txt') $(-not (git -C $repo status --porcelain))"
        $sha = "$(git -C $repo rev-parse 'stash@{0}')".Trim()
        & $check 'the note gives the hash to read it by' 'True' "$($note.Contains("git diff $sha^1 $sha"))"
        # Another lane stashes between the lookup and the drop (BL-3103): only the applied stash goes.
        git -C $repo reset -q --hard HEAD 2>&1 | Out-Null; git -C $repo clean -fdq 2>&1 | Out-Null; git -C $repo stash clear
        Set-Content -Path (Join-Path $repo 'a.txt') -Value 'mine'
        git -C $repo stash push -q -m 'darkfactory BL-5 20260104-000000' 2>&1 | Out-Null
        $mine = "$(git -C $repo rev-parse 'stash@{0}')".Trim()
        $otherLane = { Set-Content -Path (Join-Path $repo 'a.txt') -Value 'theirs'; git -C $repo stash push -q -m 'darkfactory BL-6 20260104-000000' 2>&1 | Out-Null }
        $null = Restore-TaskStash -Id 'BL-5' -Repo $repo -BetweenLookupAndDrop $otherLane
        $left = @(git -C $repo stash list --format='%gs')
        & $check 'a stash pushed before the drop survives, the applied one is dropped' '1 True' "$($left.Count) $($left[0] -match 'darkfactory BL-6' -and (Find-StashIndex $mine $repo) -eq -1)"
        git -C $repo reset -q --hard HEAD 2>&1 | Out-Null; git -C $repo clean -fdq 2>&1 | Out-Null; git -C $repo stash clear
        # Another lane stashes between the lookup and the drop (BL-3105): only the listed hashes go.
        Set-Content -Path (Join-Path $repo 'a.txt') -Value 'one'
        git -C $repo stash push -q -m 'darkfactory BL-9 20260105-000000' 2>&1 | Out-Null
        Set-Content -Path (Join-Path $repo 'a.txt') -Value 'two'
        git -C $repo stash push -q -m 'darkfactory BL-9 20260105-000001' 2>&1 | Out-Null
        $listed = @(git -C $repo stash list --format='%H')
        $pushed = $false
        $otherLane2 = { if ($pushed) { return }; $script:pushed = $true; Set-Content -Path (Join-Path $repo 'a.txt') -Value 'theirs'; git -C $repo stash push -q -m 'darkfactory BL-10 20260105-000000' 2>&1 | Out-Null }
        Remove-TaskStashEntries $listed $repo -BetweenLookupAndDrop $otherLane2
        $left = @(git -C $repo stash list --format='%gs')
        & $check 'a stash pushed between lookup and drop survives, the listed ones are gone' '1 True' "$($left.Count) $($left[0] -match 'darkfactory BL-10')"
        git -C $repo reset -q --hard HEAD 2>&1 | Out-Null; git -C $repo clean -fdq 2>&1 | Out-Null; git -C $repo stash clear
        # The split of a task over its cost cap (BL-3091), with every step injected.
        # Prompts.ps1 is loaded after this file; its last line marks a file finished, which this one is not.
        . (Join-Path $PSScriptRoot 'Prompts.ps1'); $AreaFileFinished = $false
        $markers = Join-Path $repo 'split-markers'
        $script:splitSteps = [System.Collections.Generic.List[string]]::new()
        $script:splitIds = @('BL-1')
        $script:splitState = 'Deferred'
        $script:splitPushes = $true
        $runs = [System.Collections.Generic.List[string]]::new()
        $okRun = { param($SplitId, $Text) $runs.Add($Text); $script:splitIds = @('BL-1', 'BL-2', 'BL-3'); @{ ExitCode = 0 } }
        $lockStub = { $script:splitSteps.Add('lock'); $o = [pscustomobject]@{}; $o | Add-Member ScriptMethod Dispose { $script:splitSteps.Add('unlock') }; $o }
        $gitStub = { param([string[]]$GitArgs) $script:splitSteps.Add($GitArgs[0]); if ($GitArgs[0] -eq 'push') { $script:splitPushes } else { $true } }
        $boardStub = { param([string[]]$BoardArgs) $script:splitSteps.Add("board $($BoardArgs[4]): $($BoardArgs[6])") }
        $stateStub = { if (@($script:splitSteps | Where-Object { $_ -like 'board Blocked*' }).Count) { 'Blocked' } else { $script:splitState } }
        $split = { param($Day, $RunStub, [switch]$Pull, [switch]$Leave)
            $script:splitSteps.Clear(); $script:splitIds = @('BL-1')
            Invoke-TaskSplit 'BL-1' 'cost cap: spent $9' -Now ([datetime]$Day) -MarkerDir $markers -RunSplit $RunStub -ListTaskIds { $script:splitIds } `
                -StashSha { 'abc123' } -ContinuesStash { param($n, $s) $n -ne 'BL-3' } -StoreStash { param($n, $h, $st) $script:splitSteps.Add("store $n"); "copy-$n" } -DropStash { param($h) $script:splitSteps.Add("drop $($h -join ',')") } -TaskState $stateStub -EnterLock $lockStub -Git $gitStub -BoardCall $boardStub -PullOnRefusal:$Pull -LeaveForIntegration:$Leave }
        & $check 'first capped sighting today runs the split' 'split 1' "$(& $split '2026-10-22 09:00' $okRun) $($runs.Count)"
        & $check 'a second sighting the same day does not' 'skipped 1' "$(& $split '2026-10-22 23:59' $okRun) $($runs.Count)"
        & $check 'the next day it runs again' 'split 2' "$(& $split '2026-10-23 00:01' $okRun) $($runs.Count)"
        & $check 'a stash is stored for each continuing task before the commit, inside the lock' 'lock store BL-2 add commit push unlock' ($script:splitSteps -join ' ')
        $text = (Get-Command Invoke-TaskSplit).Definition
        & $check 'the code commits and pushes between Enter-Lock and Dispose' 'True' "$($text -match '(?s)\$lock = & \$EnterLock.*''commit''.*''push''.*\$lock\.Dispose\(\)')"
        & $check 'the prompt asks for a Notes line, not a git stash command' 'True' "$($runs[0].Contains('Continues the stashed work') -and -not $runs[0].Contains('git stash store'))"
        & $check 'the prompt names Deferred and depends-on repointing' 'True' "$($runs[0] -match 'move -To Deferred' -and $runs[0] -match 'Repoint depends-on')"
        & $check 'the split run may never run git stash store' 'True' "$($SplitForbidden -contains 'git stash store')"
        $line = Get-TaskSplitArguments $SplitForbidden '--strict-mcp-config' 'err.txt'
        & $check 'the split run uses --model sonnet' 'True' "$($line.Contains('claude -p --model sonnet '))"
        $usd = if ($line -match '--max-budget-usd (\S+)') { [double]::Parse($Matches[1], [System.Globalization.CultureInfo]::InvariantCulture) } else { 99 }
        & $check 'the split run costs at most 2 US dollars' 'True' "$($usd -gt 0 -and $usd -le 2)"
        & $check 'the split run has a turn cap' 'True' "$($line -match '--max-turns [1-9]\d*')"
        & $check 'the split run may never pop a stash' 'True' "$($line.Contains('Bash(git stash pop:*)'))"
        $code = ((Get-Command Invoke-TaskSplit, Invoke-TaskSplitRun, Register-TaskSplitAttempt, Get-TaskStashSha | ForEach-Object Definition) -join "`n") + $SplitPrompt
        & $check 'no stash pop in the split code or prompt' 'False' "$($code -match 'stash pop')"
        & $check 'a failed run is blocked with its exit code' 'blocked True' "$(& $split '2026-10-24' { param($a, $b) @{ ExitCode = 3 } }) $(@($script:splitSteps | Where-Object { $_ -match '^board Blocked: .*split run exited 3' }).Count -eq 1)"
        & $check 'a run that files nothing is blocked' 'blocked True' "$(& $split '2026-10-25' { param($a, $b) @{ ExitCode = 0 } }) $(@($script:splitSteps | Where-Object { $_ -match '^board Blocked: .*filed no new tasks' }).Count -eq 1)"
        $script:splitState = 'Backlog'
        & $check 'a run that leaves it undeferred is blocked' 'blocked True' "$(& $split '2026-10-26' $okRun) $(@($script:splitSteps | Where-Object { $_ -match '^board Blocked: .*left it in Backlog' }).Count -eq 1)"
        $script:splitState = 'Deferred'
        & $check 'a successful split blocks nothing' 'split 0' "$(& $split '2026-10-27' $okRun) $(@($script:splitSteps | Where-Object { $_ -like 'board*' }).Count)"
        $script:splitPushes = $false
        & $check 'a claim''s refused push is blocked after a pull' 'blocked True' "$(& $split '2026-10-28' $okRun -Pull) $(($script:splitSteps -join ' ') -match 'push pull push reset drop copy-BL-2 checkout clean board Blocked: .*push of the split was refused')"
        & $check 'after a run a refused push is left to integration' 'split 0' "$(& $split '2026-10-29' $okRun) $(@($script:splitSteps | Where-Object { $_ -like 'board*' }).Count)"
        # A failed commit or a conflicting pull resets only the split's own commit and aborts the rebase (BL-3102).
        $script:splitCmds = [System.Collections.Generic.List[string]]::new()
        $script:failGit = ''
        $cmdStub = { param([string[]]$GitArgs) $script:splitCmds.Add($GitArgs -join ' '); $GitArgs[0] -ne $script:failGit -and $GitArgs[0] -ne 'push' }
        $cmdSplit = { param($Day, $Fail)
            $script:splitCmds.Clear(); $script:splitSteps.Clear(); $script:splitIds = @('BL-1'); $script:failGit = $Fail
            Invoke-TaskSplit 'BL-1' 'cost cap' -Now ([datetime]$Day) -MarkerDir $markers -RunSplit $okRun -ListTaskIds { $script:splitIds } `
                -StashSha { '' } -TaskState $stateStub -EnterLock $lockStub -Git $cmdStub -HeadSha { 'cafe01' } -BoardCall $boardStub -PullOnRefusal }
        & $check 'a failed commit is blocked, unstaging Tasks and never resetting hard' 'blocked True False' "$(& $cmdSplit '2026-11-10' 'commit') $(($script:splitCmds -join '|') -match 'reset -q -- Tasks\|checkout' -and ($script:splitCmds -join '|') -notmatch 'hard') $(($script:splitCmds -join '|') -match 'HEAD~1')"
        & $check 'a failed commit never pulls or pushes the split' 'False' "$(($script:splitCmds -join '|') -match '^pull|\|pull|push.*push')"
        & $check 'a conflicting pull aborts the rebase before the reset to the recorded hash' 'blocked True False' "$(& $cmdSplit '2026-11-11' 'pull') $(($script:splitCmds -join '|') -match 'pull[^|]*\|rebase --abort\|reset -q --hard cafe01') $(($script:splitCmds -join '|') -match 'HEAD~1')"
        # Real git (BL-3107): a commit that fails after add leaves the block commit only the Blocked move;
        # a refused push after a successful pull keeps the pulled commits.
        $tasksDir = Join-Path $repo 'Tasks'; New-Item -ItemType Directory -Force -Path $tasksDir | Out-Null
        Set-Content -Path (Join-Path $tasksDir 'BL-1-x.md') -Value 'task'
        git -C $repo add Tasks; git -C $repo commit -q -m tasks 2>&1 | Out-Null
        $script:realFailCommit = $false; $script:realPull = $false
        $realGit = { param([string[]]$GitArgs)
            if ($GitArgs[0] -eq 'push') { return $false }
            if ($GitArgs[0] -eq 'commit' -and $script:realFailCommit) { $script:realFailCommit = $false; return $false }
            if ($GitArgs[0] -eq 'pull') {
                if (-not $script:realPull) { return $false }
                $split = "$(git -C $repo rev-parse HEAD)".Trim()
                git -C $repo reset -q --hard HEAD~1 2>&1 | Out-Null
                Set-Content -Path (Join-Path $repo 'pulled.txt') -Value 'pulled'
                git -C $repo add pulled.txt; git -C $repo commit -q -m 'pulled commit' 2>&1 | Out-Null
                git -C $repo cherry-pick $split 2>&1 | Out-Null
                return $true }
            git -C $repo @GitArgs 2>&1 | Out-Null; $LASTEXITCODE -eq 0 }
        $realRun = { param($a, $b) Set-Content -Path (Join-Path $tasksDir 'BL-2-y.md') -Value 'new'; $script:splitIds = @('BL-1', 'BL-2'); @{ ExitCode = 0 } }
        $realBoard = { param([string[]]$BoardArgs) Set-Content -Path (Join-Path $tasksDir 'blocked.txt') -Value 'blocked'; $script:realBlocked = $true }
        $realState = { if ($script:realBlocked) { 'Blocked' } else { 'Deferred' } }
        $realHead = { "$(git -C $repo rev-parse HEAD)".Trim() }
        $realFailSplit = { param($Day)
            $script:splitIds = @('BL-1'); $script:realBlocked = $false
            Invoke-TaskSplit 'BL-1' 'cost cap' -Now ([datetime]$Day) -MarkerDir $markers -RunSplit $realRun -ListTaskIds { $script:splitIds } -StashSha { '' } `
                -TaskState $realState -EnterLock $lockStub -Git $realGit -HeadSha $realHead -BoardCall $realBoard -PullOnRefusal }
        $script:realFailCommit = $true
        $r = & $realFailSplit '2026-11-20'
        $lastFiles = @(git -C $repo show --name-only --format= HEAD) -join ','
        & $check 'a commit failing after add blocks with only the Blocked move in the block commit' 'blocked Tasks/blocked.txt' "$r $lastFiles"
        & $check 'a commit failing after add leaves no split task file' 'False' "$(Test-Path (Join-Path $tasksDir 'BL-2-y.md'))"
        $script:realPull = $true
        $r = & $realFailSplit '2026-11-21'
        $log = @(git -C $repo log --format=%s) -join '|'
        & $check 'a refused push after a pull keeps the pulled commit and drops the split commit' 'blocked True True False' "$r $($log -match 'pulled commit') $(Test-Path (Join-Path $repo 'pulled.txt')) $($log -match 'split BL-1')"        
        # In a lane after a run nothing is pushed, even when the push would be accepted (BL-3100).
        $script:splitPushes = $true
        $null = & $split '2026-11-12' $okRun -Leave
        & $check 'after a run the split commit is not pushed' 'False' ($script:splitSteps -contains 'push').ToString()
        $script:splitState = 'Backlog'
        $null = & $split '2026-11-13' $okRun -Leave
        & $check 'after a run the block commit is not pushed' 'False' ($script:splitSteps -contains 'push').ToString()
        $script:splitState = 'Deferred'
        & $check 'Claim.ps1 and ShiftRunner.ps1 call Invoke-TaskSplit' 'True' "$((Get-Content (Join-Path $PSScriptRoot 'Claim.ps1') -Raw) -match 'Invoke-TaskSplit \$due' -and (Get-Content (Join-Path $PSScriptRoot 'ShiftRunner.ps1') -Raw) -match 'Invoke-TaskSplit \$id')"
        # Real git: one stash for several tasks (BL-3095).
        git -C $repo reset -q --hard HEAD 2>&1 | Out-Null; git -C $repo clean -fdq 2>&1 | Out-Null; git -C $repo stash clear
        $head = & $read 'a.txt'
        Set-Content -Path (Join-Path $repo 'a.txt') -Value 'carried'
        Set-Content -Path (Join-Path $repo 'c.txt') -Value 'added'
        git -C $repo stash push -q --include-untracked -m 'darkfactory BL-1 20260103-000000' 2>&1 | Out-Null
        $orig = "$(git -C $repo rev-parse 'stash@{0}')".Trim()
        & $check 'a stash from stash push is found' $orig (Get-TaskStashSha 'BL-1' $repo)
        # Storing the hash already on top writes no entry, so put another stash above it first.
        Set-Content -Path (Join-Path $repo 'a.txt') -Value 'other'
        git -C $repo stash push -q -m 'darkfactory BL-8 20260103-000000' 2>&1 | Out-Null
        git -C $repo stash store -m 'darkfactory BL-7 20260103-000000' $orig 2>&1 | Out-Null
        & $check 'a stash from stash store is found' $orig (Get-TaskStashSha 'BL-7' $repo)
        & $check 'an id that is a prefix of another is not found' '' (Get-TaskStashSha 'BL-70' $repo)
        git -C $repo stash clear
        git -C $repo stash store -m 'darkfactory BL-1 20260103-000000' $orig 2>&1 | Out-Null
        $two = Copy-TaskStash 'BL-2' $orig '20260103-000001' $repo
        $three = Copy-TaskStash 'BL-3' $orig '20260103-000001' $repo
        & $check 'one stash is stored for each of two new ids' '3' "$(@(git -C $repo stash list).Count)"
        & $check 'each id finds its own copy' 'True' "$((Get-TaskStashSha 'BL-2' $repo) -eq $two -and (Get-TaskStashSha 'BL-3' $repo) -eq $three -and $two -ne $three)"
        $null = Restore-TaskStash -Id 'BL-2' -Repo $repo
        & $check 'the first new id restores the work' 'carried added' "$(& $read 'a.txt') $(& $read 'c.txt')"
        git -C $repo reset -q --hard HEAD 2>&1 | Out-Null; git -C $repo clean -fdq 2>&1 | Out-Null
        $null = Restore-TaskStash -Id 'BL-3' -Repo $repo
        & $check 'the second new id restores the work' 'carried added' "$(& $read 'a.txt') $(& $read 'c.txt')"
        git -C $repo reset -q --hard HEAD 2>&1 | Out-Null; git -C $repo clean -fdq 2>&1 | Out-Null
        # A failed split drops what it stored and nothing else.
        $listBefore = @(git -C $repo stash list --format='%H %gs') -join "`n"
        $realStore = { param($n, $h, $st) Copy-TaskStash $n $h $st $repo }
        $realDrop = { param($h) Remove-TaskStashEntries $h $repo }
        $realSplit = { param($Day)
            $script:splitSteps.Clear(); $script:splitIds = @('BL-1')
            Invoke-TaskSplit 'BL-1' 'cost cap' -Now ([datetime]$Day) -MarkerDir $markers -RunSplit $okRun -ListTaskIds { $script:splitIds } -StashSha { $orig } `
                -ContinuesStash { $true } -StoreStash $realStore -DropStash $realDrop -TaskState $stateStub -EnterLock $lockStub -Git $gitStub -BoardCall $boardStub -PullOnRefusal }
        $script:splitPushes = $false
        & $check 'a failed split is blocked' 'blocked' "$(& $realSplit '2026-11-01')"
        & $check 'a failed split leaves the stash list as it was' $listBefore ((@(git -C $repo stash list --format='%H %gs') -join "`n"))
        $script:splitPushes = $true
        & $check 'a good split adds a stash for each new id' 'split 3' "$(& $realSplit '2026-11-02') $(@(git -C $repo stash list).Count)"
        # A store git cannot make fails the split (BL-3106): the first copy is dropped, the task is blocked.
        $script:splitSteps.Clear(); $script:splitIds = @('BL-1')
        $failingStore = { param($n, $h, $st) if ($n -eq 'BL-3') { throw 'git stash store of x for BL-3 failed: ref locked' }; $script:splitSteps.Add("store $n"); "copy-$n" }
        $r = Invoke-TaskSplit 'BL-1' 'cost cap' -Now ([datetime]'2026-11-03') -MarkerDir $markers -RunSplit $okRun -ListTaskIds { $script:splitIds } -StashSha { 'abc123' } `
            -ContinuesStash { $true } -StoreStash $failingStore -DropStash { param($h) $script:splitSteps.Add("drop $($h -join ',')") } -TaskState $stateStub -EnterLock $lockStub -Git $gitStub -BoardCall $boardStub
        & $check 'a failing store blocks the split, dropping the copies made' 'blocked True True' "$r $(($script:splitSteps -join ' ') -match 'store BL-2 drop copy-BL-2') $(@($script:splitSteps | Where-Object { $_ -match '^board Blocked: .*storing the stash.*ref locked' }).Count -eq 1)"
        # A stash drop or board move that throws (BL-3110): Tasks is restored and the task still ends Blocked.
        $script:splitSteps.Clear(); $script:splitIds = @('BL-1'); $script:boardCalls = 0
        $throwingDrop = { param($h) throw 'drop exploded' }
        $r = Invoke-TaskSplit 'BL-1' 'cost cap' -Now ([datetime]'2026-11-05') -MarkerDir $markers -RunSplit $okRun -ListTaskIds { $script:splitIds } -StashSha { 'abc123' } `
            -ContinuesStash { $true } -StoreStash $failingStore -DropStash $throwingDrop -TaskState $stateStub -EnterLock $lockStub -Git $gitStub -BoardCall $boardStub
        $joined = $script:splitSteps -join ' '
        & $check 'a throwing stash drop restores Tasks and blocks the task with the exception' 'blocked True True True' "$r $($joined -match 'checkout clean') $(@($script:splitSteps | Where-Object { $_ -match '^board Blocked: .*threw: drop exploded' }).Count -eq 1) $($joined -match 'unlock$')"
        $script:splitSteps.Clear(); $script:splitIds = @('BL-1')
        $throwOnceBoard = { param([string[]]$BoardArgs) $script:boardCalls++; if ($script:boardCalls -eq 1) { throw 'board exploded' }; $script:splitSteps.Add("board $($BoardArgs[4]): $($BoardArgs[6])") }
        $r = Invoke-TaskSplit 'BL-1' 'cost cap' -Now ([datetime]'2026-11-06') -MarkerDir $markers -RunSplit $okRun -ListTaskIds { $script:splitIds } -StashSha { 'abc123' } `
            -ContinuesStash { $true } -StoreStash $failingStore -DropStash { param($h) } -TaskState $stateStub -EnterLock $lockStub -Git $gitStub -BoardCall $throwOnceBoard
        & $check 'a throwing board move is retried as a block naming the exception' 'blocked True' "$r $(@($script:splitSteps | Where-Object { $_ -match '^board Blocked: .*threw: board exploded' }).Count -eq 1)"
        # A reset git refuses stops the undo (BL-3113): nothing is committed or pushed on top of the split commit.
        $resetRefusingGit = { param([string[]]$GitArgs) $script:splitSteps.Add($GitArgs[0]); if ($GitArgs[0] -eq 'reset' -or $GitArgs[0] -eq 'push') { $false } else { $true } }
        $script:splitSteps.Clear(); $script:splitIds = @('BL-1'); $script:splitPushes = $false
        $r = Invoke-TaskSplit 'BL-1' 'cost cap' -Now ([datetime]'2026-11-07') -MarkerDir $markers -RunSplit $okRun -ListTaskIds { $script:splitIds } -StashSha { '' } `
            -ContinuesStash { $true } -StoreStash $failingStore -DropStash { param($h) } -TaskState $stateStub -EnterLock $lockStub -Git $resetRefusingGit -BoardCall $boardStub -PullOnRefusal
        $joined = $script:splitSteps -join ' '
        & $check 'a refused reset after a failed push stops: no checkout, block commit or second push' 'reset-failed 2 1 False' "$r $(@($script:splitSteps | Where-Object { $_ -eq 'push' }).Count) $(@($script:splitSteps | Where-Object { $_ -eq 'commit' }).Count) $($joined -match 'checkout|board')"
        $script:splitSteps.Clear(); $script:splitIds = @('BL-1')
        $throwingGitOnPush = { param([string[]]$GitArgs) $script:splitSteps.Add($GitArgs[0]); if ($GitArgs[0] -eq 'reset') { $false } elseif ($GitArgs[0] -eq 'checkout') { $true } elseif ($GitArgs[0] -eq 'push') { throw 'push exploded' } else { $true } }
        $r = Invoke-TaskSplit 'BL-1' 'cost cap' -Now ([datetime]'2026-11-08') -MarkerDir $markers -RunSplit $okRun -ListTaskIds { $script:splitIds } -StashSha { '' } `
            -ContinuesStash { $true } -StoreStash $failingStore -DropStash { param($h) } -TaskState $stateStub -EnterLock $lockStub -Git $throwingGitOnPush -BoardCall $boardStub -PullOnRefusal
        $joined = $script:splitSteps -join ' '
        & $check 'a refused reset after a throw stops: no checkout, block commit or second push' 'reset-failed 1 1 False' "$r $(@($script:splitSteps | Where-Object { $_ -eq 'push' }).Count) $(@($script:splitSteps | Where-Object { $_ -eq 'commit' }).Count) $($joined -match 'checkout|board')"
        # Board moves that all throw leave the task out of Blocked, so the caller blocks it (BL-3113).
        $script:splitSteps.Clear(); $script:splitIds = @('BL-1'); $script:splitPushes = $true
        $allThrowBoard = { param([string[]]$BoardArgs) throw 'board down' }
        $r = Invoke-TaskSplit 'BL-1' 'cost cap' -Now ([datetime]'2026-11-09') -MarkerDir $markers -RunSplit $okRun -ListTaskIds { $script:splitIds } -StashSha { '' } `
            -ContinuesStash { $true } -StoreStash $failingStore -DropStash { param($h) } -TaskState { 'Deferred' } -EnterLock $lockStub -Git { param([string[]]$GitArgs) $script:splitSteps.Add($GitArgs[0]); if ($GitArgs[0] -eq 'push') { $false } else { $true } } -BoardCall $allThrowBoard -PullOnRefusal
        & $check 'when every board move throws the split does not report blocked' 'failed' "$r"
        # The failure path's own state check: the board stub returns, the task stays Deferred (BL-3114).
        $script:splitSteps.Clear(); $script:splitIds = @('BL-1')
        $r = Invoke-TaskSplit 'BL-1' 'cost cap' -Now ([datetime]'2026-11-10') -MarkerDir $markers -RunSplit $okRun -ListTaskIds { $script:splitIds } -StashSha { '' } `
            -ContinuesStash { $true } -StoreStash $failingStore -DropStash { param($h) } -TaskState { 'Deferred' } -EnterLock $lockStub -Git { param([string[]]$GitArgs) $script:splitSteps.Add($GitArgs[0]); if ($GitArgs[0] -eq 'push') { $false } else { $true } } -BoardCall $boardStub -PullOnRefusal
        & $check 'a board move that returns while the task stays Deferred reports failed' 'failed' "$r"
        # The default board call reaches the board script path, not a shadowing parameter (BL-3116).
        $realBoardScript = Join-Path $tasksDir 'record-board.ps1'
        Set-Content -Path $realBoardScript -Value 'Set-Content -Path (Join-Path $PSScriptRoot "board-args.txt") -Value ($args -join " ")'
        $savedBoard = $Board; $Board = $realBoardScript
        $script:splitSteps.Clear(); $script:splitIds = @('BL-1')
        Invoke-TaskSplit 'BL-1' 'cost cap' -Now ([datetime]'2026-11-10') -MarkerDir (Join-Path $markers 'default-board') -RunSplit $okRun -ListTaskIds { $script:splitIds } -StashSha { '' } `
            -ContinuesStash { $true } -StoreStash $failingStore -DropStash { param($h) } -TaskState { 'Deferred' } -EnterLock $lockStub -Git { param([string[]]$GitArgs) if ($GitArgs[0] -eq 'push') { $false } else { $true } } -PullOnRefusal | Out-Null
        $Board = $savedBoard
        $boardArgsFile = Join-Path $tasksDir 'board-args.txt'
        & $check 'the default board call moves the task to Blocked through the board script' 'move -Id BL-1 -To Blocked' $(if (Test-Path $boardArgsFile) { ((Get-Content $boardArgsFile -Raw) -split ' -Reason')[0].Trim() } else { 'no board call' })
        # A failed unstaging reset (the commit itself failed) still restores Tasks (BL-3114).
        $script:splitSteps.Clear(); $script:splitIds = @('BL-1')
        $r = Invoke-TaskSplit 'BL-1' 'cost cap' -Now ([datetime]'2026-11-11') -MarkerDir $markers -RunSplit $okRun -ListTaskIds { $script:splitIds } -StashSha { '' } `
            -ContinuesStash { $true } -StoreStash $failingStore -DropStash { param($h) } -TaskState $stateStub -EnterLock $lockStub -Git { param([string[]]$GitArgs) $script:splitSteps.Add($GitArgs[0]); if ($GitArgs[0] -in 'reset', 'commit') { $false } else { $true } } -BoardCall $boardStub
        & $check 'a failed unstaging reset still runs checkout and clean' 'True' "$(($script:splitSteps -join ' ') -match 'checkout clean')"
        # The caller makes no commit and no push for reset-failed (BL-3114).
        & $check 'the caller blocks only skipped and failed splits, never reset-failed' 'True True False False False' "$(Test-TaskSplitLeavesBlockToCaller 'skipped') $(Test-TaskSplitLeavesBlockToCaller 'failed') $(Test-TaskSplitLeavesBlockToCaller 'reset-failed') $(Test-TaskSplitLeavesBlockToCaller 'split') $(Test-TaskSplitLeavesBlockToCaller 'blocked')"
        & $check 'ShiftRunner.ps1 asks Test-TaskSplitLeavesBlockToCaller before its block commit' 'True' "$((Get-Content (Join-Path $PSScriptRoot 'ShiftRunner.ps1') -Raw) -match 'if \(Test-TaskSplitLeavesBlockToCaller \$split\)')"
        # A split with a stash and no continuing task traces it in red.
        $script:splitSteps.Clear(); $script:splitIds = @('BL-1')
        $traceText = & { Invoke-TaskSplit 'BL-1' 'cost cap' -Now ([datetime]'2026-11-04') -MarkerDir $markers -RunSplit $okRun -ListTaskIds { $script:splitIds } -StashSha { 'abc123' } `
            -ContinuesStash { $false } -StoreStash $failingStore -DropStash { param($h) } -TaskState $stateStub -EnterLock $lockStub -Git $gitStub -BoardCall $boardStub } 6>&1 | Out-String
        & $check 'a stash no new task continues is traced with its hash and the id' 'True' "$($traceText -match 'abc123' -and $traceText -match 'BL-1')"
        # Real git: a stash hash git does not know fails Copy-TaskStash.
        $threw = $false; try { $null = Copy-TaskStash 'BL-4' ('0' * 40) '20260103-000002' $repo } catch { $threw = $true }
        & $check 'Copy-TaskStash throws when git cannot copy the stash' 'True' "$threw"
        # A split's stash copy under BL-8 follows its task when that task is renumbered BL-8 -> BL-9 (BL-3109).
        git -C $repo reset -q --hard HEAD 2>&1 | Out-Null; git -C $repo clean -fdq 2>&1 | Out-Null; git -C $repo stash clear
        Set-Content -Path (Join-Path $repo 'a.txt') -Value 'parent'
        git -C $repo stash push -q -m 'darkfactory BL-7 20260105-000000' 2>&1 | Out-Null
        $parentSha = "$(git -C $repo rev-parse 'stash@{0}')".Trim()
        $null = Copy-TaskStash 'BL-8' $parentSha '20260105-000001' $repo -ParentId 'BL-7'
        Set-Content -Path (Join-Path $repo 'a.txt') -Value 'theirs'
        git -C $repo stash push -q -m 'darkfactory BL-8 20260105-000002' 2>&1 | Out-Null
        $notContinuing = Move-RenumberedTaskStash 'BL-8' 'BL-9' $repo { '' }
        & $check 'a renumbered task that continues no stash moves nothing' '0' "$(@($notContinuing).Count)"
        $traceText = & { $script:movedStash = @(Move-RenumberedTaskStash 'BL-8' 'BL-9' $repo { 'BL-7' }) } 6>&1 | Out-String
        $subjects = @(git -C $repo stash list --format='%gs')
        & $check 'the copy is re-stored under the new ID' '1' "$(@($subjects | Where-Object { $_ -eq 'darkfactory BL-9 20260105-000001' }).Count)"
        & $check 'the old ID keeps no copy' '0' "$(@($subjects | Where-Object { $_ -eq 'darkfactory BL-8 20260105-000001' }).Count)"
        & $check 'the other task''s own stash and the parent stash stay' '2' "$(@($subjects | Where-Object { $_ -match 'darkfactory BL-8 20260105-000002$|darkfactory BL-7 ' }).Count)"
        & $check 'the moved copy is found for the new ID, not offered to the old' 'True False' "$((Get-TaskStashSha 'BL-9' $repo) -eq $script:movedStash[0]) $((Get-TaskStashSha 'BL-8' $repo) -eq $script:movedStash[0])"
        & $check 'the trace says the stash moved to the renumbered ID' 'True' "$($traceText -match 'moved the stash copy' -and $traceText -match 'BL-8 to BL-9')"

        # Two lanes split different parents into tasks that share BL-8: only the renumbered task's copy moves (BL-3112).
        git -C $repo stash clear
        Set-Content -Path (Join-Path $repo 'a.txt') -Value 'parent six'
        git -C $repo stash push -q -m 'darkfactory BL-6 20260106-000000' 2>&1 | Out-Null
        $sixSha = "$(git -C $repo rev-parse 'stash@{0}')".Trim()
        Set-Content -Path (Join-Path $repo 'a.txt') -Value 'parent seven'
        git -C $repo stash push -q -m 'darkfactory BL-7 20260106-000001' 2>&1 | Out-Null
        $sevenSha = "$(git -C $repo rev-parse 'stash@{0}')".Trim()
        $null = Copy-TaskStash 'BL-8' $sixSha '20260106-000002' $repo -ParentId 'BL-6'
        $null = Copy-TaskStash 'BL-8' $sevenSha '20260106-000003' $repo -ParentId 'BL-7'
        $movedSeven = @(Move-RenumberedTaskStash 'BL-8' 'BL-9' $repo { 'BL-7' })
        $subjects = @(git -C $repo stash list --format='%gs')
        & $check 'only the renumbered task''s own copy moves' '1 1 0 1' "$($movedSeven.Count) $(@($subjects | Where-Object { $_ -eq 'darkfactory BL-9 20260106-000003' }).Count) $(@($subjects | Where-Object { $_ -eq 'darkfactory BL-8 20260106-000003' }).Count) $(@($subjects | Where-Object { $_ -eq 'darkfactory BL-8 20260106-000002' }).Count)"
        & $check 'the task that kept the old ID keeps its copy of its own parent' 'True' "$((@(git -C $repo show -s --format=%B (Get-TaskStashSha 'BL-8' $repo)) -join "`n") -match 'stash-continues BL-6')"
        # A refused renumber push moves nothing; a move that throws is traced and the claim goes on (BL-3112).
        $before = @(git -C $repo stash list --format='%H %gs') -join '|'
        $script:moveCalls = 0
        $refused = & {
            function Move-RenumberedTaskStash { $script:moveCalls++ }
            Move-RenumberedStashesAfterPush 'BL-8' @('BL-8 -> BL-9  x') { $false }
        }
        & $check 'a refused renumber push moves no stash and returns false' '0 False' "$script:moveCalls $refused"
        & $check 'a refused renumber push leaves the stash list unchanged' $before ((@(git -C $repo stash list --format='%H %gs')) -join '|')
        $outcome = & {
            function Move-RenumberedTaskStash { $script:moveCalls++; throw 'ref locked' }
            Move-RenumberedStashesAfterPush 'BL-8' @('BL-8 -> BL-9  x') { $true }
        } 6>&1 | Out-String
        & $check 'a throwing move is traced in full and the claim goes on' '1 True True True True' "$script:moveCalls $($outcome -match 'BL-8') $($outcome -match 'BL-9') $($outcome -match 'ref locked') $($outcome -match 'True')"
    } finally {
        Remove-Item -Recurse -Force -Path $repo -ErrorAction SilentlyContinue
    }
    exit $(if ($failed) { 1 } else { 0 })
}

# This file ran to its end; RunDarkFactory.ps1 goes on to the next one.
$AreaFileFinished = $true
