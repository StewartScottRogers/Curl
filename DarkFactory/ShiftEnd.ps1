# The shift-end merge to master through a pull request, and the shift-end review (ADR-0710).
# Dot-sourced by RunDarkFactory.ps1 in its order; see the loader there.

# The body of every commit that changes only the board and is pushed on its own - a claim,
# requeue, park, return or CI-watch filing. CI's concurrency group cancels the run in
# progress at each push, and with lanes claiming every minute or two no run finished for
# two hours after a fix was in (AF-0089); GitHub starts no run for a push whose head
# commit says this, so board pushes no longer cancel the run that tests the code.
$SkipCiNote = '[skip ci] Board change only; the run on the code before it stands.'

# How long the shift-end merge waits for CI on the commit it merges. 30 minutes left shift
# 20261018-103803 unmerged once a run in progress was no longer cancelled (ADR-0693).
$MergeCiWaitMinutes = 60

function Wait-MergeCiRun {
    # Waits for the CI run on -Head, following a newer branch head when a push replaced it, and
    # calls -OnPoll every -PollMinutes meanwhile. Returns Run, Head and, when the wait failed,
    # Failure: the "not merged: ..." line for the trace. -HandOver is the coordinator's hash table
    # whose Done is $true once the next shift has started beside this wait (BL-3142): from then
    # on its lanes push code to -Branch, so a newer head is followed only when every commit
    # since the current one is a [skip ci] board commit (BL-3155).
    param([string]$Branch, [string]$Head, [scriptblock]$OnPoll, [int]$PollMinutes = 5, [hashtable]$HandOver)
    $head = $Head
    $short = $head.Substring(0, 7)
    # Wait for the run on this exact commit. A run on the branch in progress always finishes
    # first (ADR-0693), and each takes 15 to 25 minutes, so allow two runs and some slack.
    $deadline = (Get-Date).AddMinutes($MergeCiWaitMinutes)
    $nextPoll = (Get-Date).AddMinutes($PollMinutes)
    $run = $null
    # Why gh cannot list the runs, traced as the CI watch traces it (BL-4377): shift
    # 20261030-220408 waited out its 60 minutes blind while CI had passed at its 30th.
    $listState = @{}
    $listFailure = ''
    # A board-only head commit started no run (AF-0089), so start one on it by hand.
    if ("$(git -C $Root log -1 --format=%B $head)" -match '\[skip ci\]') { gh workflow run CI --ref $Branch 2>&1 | Out-Null }
    while ($true) {
        $listed = Invoke-GhRunList -Arguments @('--workflow', 'CI', '--branch', $Branch, '--commit', $head, '--limit', '1', '--json', 'status,conclusion')
        $listFailure = Get-GhListFailure $listed
        Write-GhListLine -State $listState -What "for the merge of $short" -Failure $listFailure -Now (Get-Date) -Kind 'shift'
        $run = if ($listFailure -or -not "$listed".Trim()) { @() } else { @($listed | ConvertFrom-Json) }
        if ($run.Count -and $run[0].status -eq 'completed' -and $run[0].conclusion -ne 'cancelled') { break }
        # A newer push replaced this commit's waiting run (ADR-0693): merge the newer commit,
        # which holds all of this one, once CI passes on it (shift 20261019-150735 gave up here).
        if ($run.Count -and $run[0].conclusion -eq 'cancelled') {
            git -C $Root fetch -q origin $Branch 2>&1 | Out-Null
            $newer = (git -C $Root rev-parse "origin/$Branch").Trim()
            if ($newer -eq $head) { return @{ Failure = "not merged: CI cancelled on $short" } }
            if ($HandOver -and $HandOver.Done) {
                $since = [int](git -C $Root rev-list --count "$head..$newer")
                $boardOnly = [int](git -C $Root rev-list --count --grep '\[skip ci\]' "$head..$newer")
                if ($boardOnly -ne $since) { return @{ Failure = "not merged: the next shift was handed over and pushed code to $Branch after $short, so its work is not merged with this shift's" } }
            }
            $head = $newer
            $short = $head.Substring(0, 7)
            if ("$(git -C $Root log -1 --format=%B $head)" -match '\[skip ci\]') { gh workflow run CI --ref $Branch 2>&1 | Out-Null }
        }
        if ((Get-Date) -ge $deadline) {
            $blind = if ($listFailure) { "; gh could not list its runs since $($listState.FailingSince.ToString('HH:mm')) ($listFailure)" } else { '' }
            return @{ Failure = "not merged: CI did not finish on $short within $MergeCiWaitMinutes min$blind" }
        }
        if ($OnPoll -and (Get-Date) -ge $nextPoll) {
            $nextPoll = (Get-Date).AddMinutes($PollMinutes)
            # A poll that throws or writes output must not abort the merge wait or corrupt its result (BL-3150).
            try { & $OnPoll | Out-Null } catch { Write-Trace '-' 'shift' "merge-wait poll failed, waiting on: $($_.Exception.Message)" 'Yellow' }
        }
        Start-Sleep -Seconds 30
    }
    return @{ Run = $run; Head = $head }
}

function Invoke-MergeToMaster {
    # Stewart's standing permission (2026-09-27): at the end of a shift, merge the branch
    # into master through a pull request - only when the CI workflow passed, on every
    # platform, for the exact commit being merged. Returns a line for the trace.
    # -OnPoll, when given, runs every -PollMinutes of the CI wait (BL-3142), so a -Continuous
    # coordinator can start the next shift while the merge waits.
    param([string]$Branch, [scriptblock]$OnPoll, [int]$PollMinutes = 5, [hashtable]$HandOver)
    if (-not (Get-Command gh -ErrorAction SilentlyContinue)) { return 'not merged: gh is not installed' }
    git -C $Root fetch -q origin master $Branch 2>&1 | Out-Null
    if ([int](git -C $Root rev-list --count "origin/master..origin/$Branch") -eq 0) { return "nothing on $Branch to merge" }
    $head = (git -C $Root rev-parse "origin/$Branch").Trim()
    $short = $head.Substring(0, 7)
    $wait = Wait-MergeCiRun -Branch $Branch -Head $head -OnPoll $OnPoll -PollMinutes $PollMinutes -HandOver $HandOver
    if ($wait.Failure) { return $wait.Failure }
    $run = $wait.Run
    $head = $wait.Head
    $short = $head.Substring(0, 7)
    if ($run[0].conclusion -ne 'success') { return "not merged: CI $($run[0].conclusion) on $short" }
    # The pull request comes from a branch pinned at the commit CI passed, not from $Branch:
    # a board push to $Branch while CI ran ([skip ci], so no checks) became the request's head,
    # master's required check had never run on it, and gh pr merge was refused (shift
    # 20261019-061607, pull request #174). factory/** starts no CI run of its own.
    $pin = "factory/merge-$Stamp"
    if (-not (Invoke-Git @('push', '-q', '-f', 'origin', "${head}:refs/heads/$pin"))) { return "not merged: could not push $pin" }
    # --jq, not ConvertFrom-Json: Windows PowerShell turns "[]" into one empty element, so
    # a missing pull request looked like one with no number and the merge was a silent no-op.
    $number = "$(gh pr list --head $pin --base master --state open --json number --limit 1 --jq '.[0].number // empty' 2>$null)".Trim()
    if (-not $number) {
        $bodyFile = Join-Path $LogDir "pr-body-$Stamp.md"
        $robot = [char]::ConvertFromUtf32(0x1F916)
        [IO.File]::WriteAllText($bodyFile, "Dark factory shift ${Stamp}: $Branch at $short, where CI passed on Windows, Linux and macOS.`n`n$robot Generated with [Claude Code](https://claude.com/claude-code)`n", (New-Object Text.UTF8Encoding($false)))
        $url = gh pr create --base master --head $pin --title "Dark factory shift $Stamp" --body-file $bodyFile 2>$null
        if ("$url" -notmatch '/pull/(\d+)') { return 'not merged: could not open the pull request' }
        $number = $Matches[1]
    }
    gh pr merge $number --merge --match-head-commit $head 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) { return "not merged: gh pr merge refused pull request #$number" }
    # The pin has done its job once master holds the commit; it is the factory's own branch.
    Invoke-Git @('push', '-q', 'origin', '--delete', $pin) | Out-Null
    return "merged $short into master (pull request #$number)"
}

# The shift-end review (ADR-0710): how long it may run and what it may cost.
$ShiftReviewMinutes = 45
$ShiftReviewBudgetUsd = 15

# What the review run is told. {BASE}, {HEAD}, {REPORT} and {BRANCH} are filled in.
$ShiftReviewPrompt = @'
You are the dark factory's shift-end reviewer (ADR-0710). This checkout is detached at {HEAD}, the commit the shift just merged into master. Review what the shift merged: git diff {BASE}...{HEAD}.

1. Run /pr-review-toolkit:review-pr errors types code parallel, scoped to that diff (the changed files and lines, not the whole repository). Review only: change no file but the task files and the report below, and never run the code-simplifier.
2. Keep only issues that are real and that the diff introduced, checked against the code, CLAUDE.md and the project's own CLAUDE.md files. Drop style preferences and anything the repository's rules already decide another way.
3. For each Critical or Important issue left, file one task with powershell (pwsh on Linux and macOS) -NoProfile -ExecutionPolicy Bypass -File .claude/skills/task-board/task-board.ps1 new -Title "<what to fix>" -Priority <High for Critical, Normal for Important> -Assignee Claude -Pipeline direct -Touches "<the projects or files>", then fill its Goal, Context (the file and line, the reviewer that found it, what is wrong and why) and checkable Acceptance criteria. File none for Suggestions. Never file one that touches an audit path or a guard file.
4. Commit only the new task files, with the message "chore(tasks): file shift-end review findings" and [skip ci] in the body, run task-board.ps1 dedupe -Since origin/{BRANCH}, and stop there: do not push. The coordinator pushes the commit once the run ends, holding the lock every lane takes to push (BL-3060).
5. Write the review summary (Critical, Important and Suggestions, each with the task ID filed for it) to {REPORT}. Its last line is exactly "FILED: <number of tasks filed>".
'@

function Invoke-ReviewDedupe {
    # The review numbers its tasks from the board at the reviewed commit, which lanes have
    # moved past: on 2026-10-23 it filed BL-3263 twelve minutes after a lane had, and two
    # lanes then claimed the one ID (AF-0386, BL-3363). Once its commits sit on the fresh
    # branch, every task the branch already had keeps its ID and the review's take the next
    # free ones; the renumbering is committed for the push that follows.
    # Returns '' when the IDs are settled (nothing to renumber, or the renumbering committed),
    # or why they are not: the caller must not push then (BL-3491).
    param([string]$Dir, [string]$Branch)
    $saved = $env:CLAUDE_PROJECT_DIR
    try {
        $env:CLAUDE_PROJECT_DIR = $Dir
        $output = @(& (Get-FactoryPowerShellExe) -NoProfile -ExecutionPolicy Bypass -File $Board dedupe -Since "origin/$Branch" 2>&1 | ForEach-Object { "$_" })
        $dedupeExit = $LASTEXITCODE
    } finally { $env:CLAUDE_PROJECT_DIR = $saved }
    if ($dedupeExit -ne 0) {
        Write-Trace '-' 'renum' "review: task-board dedupe failed (exit $dedupeExit): $(($output | Select-Object -First 5) -join ' | ')" 'Red'
        return 'its task IDs could not be checked against the branch (task-board dedupe failed)'
    }
    $renumbered = @($output | Where-Object { $_ -match '->' })
    if (-not $renumbered.Count) { return '' }
    $added = git -C $Dir add -A Tasks 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Trace '-' 'renum' "review: git add of the renumbered tasks failed: $("$added".Trim())" 'Red'
        return 'its renumbered task IDs could not be staged (git add failed)'
    }
    $committed = git -C $Dir commit -q -m "chore(tasks): renumber the shift-end review's task IDs other tasks took first`n`n$($renumbered -join "`n")" -m $SkipCiNote 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Trace '-' 'renum' "review: git commit of the renumbered tasks failed: $("$committed".Trim())" 'Red'
        return 'its renumbered task IDs could not be committed (git commit failed)'
    }
    foreach ($line in $renumbered) { Write-Trace '-' 'renum' "review: $($line -replace '\s{2,}.*$', '')" 'DarkYellow' }
    return ''
}

function Push-ReviewCommitsUnderLock {
    # Pushes the commits the review run made in its worktree, holding the integrate lock every
    # lane takes to push, so the push cannot land in the middle of a lane's verify and cost it
    # its own push (BL-3060). Up to five tries: fetch, rebase onto the branch, push. Returns
    # '' when nothing needed pushing or the push worked, or why it did not.
    param([string]$Dir, [string]$Head, [string]$Branch)
    if ("$(git -C $Dir rev-parse HEAD 2>$null)".Trim() -eq $Head) { return '' }
    $lock = Enter-Lock
    try {
        for ($try = 0; $try -lt 5; $try++) {
            git -C $Dir fetch -q origin $Branch 2>&1 | Out-Null
            if ($LASTEXITCODE -ne 0) { continue }
            git -C $Dir rebase -q "origin/$Branch" 2>&1 | Out-Null
            if ($LASTEXITCODE -ne 0) { git -C $Dir rebase --abort 2>&1 | Out-Null; return 'its commit would not rebase onto the branch' }
            $unsettled = Invoke-ReviewDedupe -Dir $Dir -Branch $Branch
            if ($unsettled) { return $unsettled }
            git -C $Dir push -q origin "HEAD:$Branch" 2>&1 | Out-Null
            if ($LASTEXITCODE -eq 0) { return '' }
        }
        return 'its push was refused 5 times'
    } finally { $lock.Dispose() }
}

function Invoke-ShiftReview {
    # After the shift-end merge: one headless run of three of the PR Review Toolkit's
    # reviewers over what the shift merged, filing a task for each Critical or Important
    # issue (ADR-0710). It never holds or undoes the merge. Returns a line for the trace.
    param([string]$Base, [string]$Head, [string]$Branch)
    if (-not "$(& claude plugin list 2>$null)".Contains('pr-review-toolkit')) { return 'review skipped: the pr-review-toolkit plugin is not installed' }
    $short = $Head.Substring(0, 7)
    $dir = Join-Path $LanesDir "review-$Stamp"
    if (-not (Invoke-Git @('worktree', 'add', '-q', '--detach', $dir, $Head))) { return "review skipped: no worktree at $short" }
    $report = Join-Path $LogDir "review-$Stamp.md"
    $raw = Join-Path $LogDir "review-$Stamp.jsonl"
    try {
        $text = $ShiftReviewPrompt.Replace('{BASE}', $Base).Replace('{HEAD}', $Head).Replace('{REPORT}', $report).Replace('{BRANCH}', $Branch)
        $budget = $ShiftReviewBudgetUsd.ToString([System.Globalization.CultureInfo]::InvariantCulture)
        $denied = ($Forbidden | ForEach-Object { "`"Bash($_`:*)`" `"PowerShell($_`:*)`"" }) -join ' '
        $psi = New-ClaudeRunStartInfo "claude -p --model opus --dangerously-skip-permissions --output-format stream-json --verbose --max-budget-usd $budget --disallowedTools $denied 2>$(Get-FactoryNullDevice)"
        $psi.WorkingDirectory = $dir
        $p = [System.Diagnostics.Process]::Start($psi)
        $p.StandardInput.Write($text)
        $p.StandardInput.Close()
        $deadline = (Get-Date).AddMinutes($ShiftReviewMinutes)
        $pending = $p.StandardOutput.ReadLineAsync()
        # No heartbeat here: the review can run beside the next shift, whose heartbeats the
        # board page shows.
        while ($true) {
            if ($pending.Wait(1000)) {
                if ($null -eq $pending.Result) { break }
                Add-Content -Path $raw -Value $pending.Result -Encoding UTF8
                $pending = $p.StandardOutput.ReadLineAsync()
            } elseif ((Get-Date) -gt $deadline) {
                Stop-FactoryProcessTree -ProcessId $p.Id
                return "review of $short killed after $ShiftReviewMinutes min; see $raw"
            }
        }
        $p.WaitForExit()
        $pushed = Push-ReviewCommitsUnderLock -Dir $dir -Head $Head -Branch $Branch
        if ($pushed) { Write-Trace '-' 'review' "review of $short filed tasks but $pushed" 'Yellow' }
        $filed = if (Test-Path $report) { [regex]::Match((Get-Content $report -Raw), 'FILED:\s*(\d+)\s*$').Groups[1].Value } else { '' }
        if (-not $filed) { return "review of $short ended (exit $($p.ExitCode)) without a report; see $raw" }
        return "reviewed $short; $filed task(s) filed; report $report"
    } finally {
        Invoke-Git @('worktree', 'remove', '--force', $dir) | Out-Null
    }
}

# This file ran to its end; RunDarkFactory.ps1 goes on to the next one.
$AreaFileFinished = $true
