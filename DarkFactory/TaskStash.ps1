# Stray changes a run left behind: saving them, restoring them for the task they belong to,
# and -TestTaskStash. Dot-sourced by RunDarkFactory.ps1 in its order; see the loader there.

function Save-StrayChanges {
    param([string]$Id)
    if (-not (Get-Dirty)) { return }
    git -C $Root stash push --include-untracked -m "darkfactory $Id $Stamp" | Out-Null
    Write-Trace $Id 'stash' "uncommitted work kept: git stash list" 'Yellow'
}

function Restore-TaskStash {
    # A task that went back to Backlog with work in progress left it in the shared stash
    # list as "darkfactory <id> <stamp>" (Save-StrayChanges). A lane's run may not run
    # git stash, not even git stash list (AF-0091: BL-1609's fourth claim was spent finding
    # the third's stash; AF-0099: BL-1683's second claim began by hunting for the first's),
    # so the shift applies the newest such stash itself when the task
    # is claimed again. Returns the note put in front of the run's prompt, '' for none.
    param([string]$Id, [string]$Repo = $Root)
    $pattern = "^(\S+) .*: darkfactory $([regex]::Escape($Id)) "
    $entry = @(git -C $Repo stash list --format='%H %gs' 2>$null) | Where-Object { $_ -match $pattern } | Select-Object -First 1
    if (-not $entry) { return '' }
    $sha = ($entry -split ' ', 2)[0]
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
        $index = 0
        foreach ($line in @(git -C $Repo stash list --format='%H' 2>$null)) {
            if ($line -eq $sha) { git -C $Repo stash drop -q "stash@{$index}" 2>&1 | Out-Null; break }
            $index++
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
    } finally {
        Remove-Item -Recurse -Force -Path $repo -ErrorAction SilentlyContinue
    }
    exit $(if ($failed) { 1 } else { 0 })
}

# This file ran to its end; RunDarkFactory.ps1 goes on to the next one.
$AreaFileFinished = $true
