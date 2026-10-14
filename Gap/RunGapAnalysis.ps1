<#
.SYNOPSIS
    Measures Curl against an upstream curl release, area by area, in a detached tree of the
    commit to measure, then turns the measurements into findings, a scorecard, a pull
    request and Curl tasks (ADR-0433 decisions 3, 4, 5 and 7).
.DESCRIPTION
    Steps, each traced to <repo>.gap\<stamp>\gap.log, where <repo>.gap sits beside the
    checkout and <stamp> is yyyy-MM-dd_HHmm:

    1. Refusals: inside a dark factory process (CURL_DARK_FACTORY_LANE set), and while a
       shift runs (a process whose command line holds RunDarkFactory.ps1 without -NewTab or
       -Restart) unless -AlongsideShift is given. Then git fetch origin.
    2. git worktree add --detach <stamp>\tree <Ref>. Every measurement runs against that
       tree, never against the checkout this script was started from.
    3. dotnet build <tree>\Curl.Console -c Release.
    4. Gap/Tools/Get-UpstreamRelease.ps1 -Version <CurlVersion>; its last output line is the
       cached release folder.
    5. Get-GapReferenceCurl of Invoke-GapProbe.ps1 finds the reference curl. Its version
       line (or "docs" when there is no matching reference) is written to <stamp>\run.json
       with the commit, platform, target version and areas.
    6. Each selected area's tool runs from the copy of Gap/Tools beside this script, never
       from the measured tree, with -Candidate set to the tree's Release binary and its
       output in <stamp>\measurements\:
         options      Measure-OptionGap.ps1 -OutFile options.json
         protocols,
         features     Measure-VersionGap.ps1 -OutDirectory (one run writes both)
         writeout     Measure-WriteOutGap.ps1 -OutFile writeout.json
         exitcodes    Measure-ExitCodeGap.ps1 -RepositoryRoot <tree> -OutFile exitcodes.json
         environment  Measure-EnvironmentGap.ps1 -RepositoryRoot <tree> -OutFile environment.json
         behaviour    dotnet run --file <tree>\Gap\Tools\Measure-UpstreamCases.cs --
                      <release>\tests\data <stamp>\raw.json, then
                      ConvertTo-BehaviourMeasurement.ps1 (with the reference's feature and
                      protocol names), then Measure-ReferenceCrossCheck.ps1.
       Measure-UpstreamCases.cs is the one exception to "tools beside this script": its
       #:project line builds the Curl.Console next to it into the harness, so it is run
       from the tree to measure the tree. A tool that fails is logged, its area is reported
       as not measured, and the run goes on with the next area.
    7. First the tree is checked: a measurement step that changed it is logged with the files
       and the tree reset, so no analyst is blamed for it. Each measured area's analyst runs
       headless, the way the dark factory runs agents:
       claude -p --agent gap-<area> --model <model: of .claude/agents/gap-<area>.md>
       --dangerously-skip-permissions --output-format stream-json --verbose, with a prompt
       on standard input naming the run folder, measurement file, tree, release folder, run
       stamp and the area's existing findings (id, key, status, items) from the gap
       worktree's Gap/Findings. The result event's text is saved to
       <stamp>\reports\gap-<area>.md and its total_cost_usd logged. An analyst that leaves
       the tree changed (git status --porcelain not empty; the files are logged) is logged, the tree is reset
       (git reset --hard, git clean -fd) and its report dropped, so its gaps fall to the
       ungrouped finding.
    8. In the gap worktree: Write-GapFindings.ps1 -RunDirectory <stamp> -Stamp <stamp>, then
       Write-GapScorecard.ps1 (scorecard and Gap/Scorecards/history.json); the measured
       inventories Gap/Upstream/<version>/*.json are copied in when they differ (when the
       tools ran from the gap worktree they are already there); then
       Export-GapDashboardData.ps1 -OutFile <stamp>\data.json.
    9. Unless -NoCommit: git add -A Gap and commit "gap: scorecard <stamp>" in the gap
       worktree, git push origin gap, and, unless -NoPullRequest or one is already open
       (gh pr list --head gap --base master), gh pr create --base master --head gap with the
       scorecard's Scores table as the body. The pull request is never merged here.
   10. Unless -NoCommit or -NoTasks: git fetch origin work/dark-factory, then a detached
       worktree <stamp>\board of it, so task IDs are allocated from the board as it is now,
       not as it was when the run started; New-TasksFromGaps.ps1 -BoardRoot <that worktree>;
       commit "chore(tasks): file gap tasks from <stamp>"; then, up to three tries: fetch,
       rebase onto origin/work/dark-factory, task-board.ps1 dedupe -Since <the commit
       rebased onto> (so a task another filer pushed meanwhile keeps its ID and this run's
       duplicate takes the next free one), amend the commit and push HEAD:work/dark-factory;
       remove that worktree. Every ID dedupe renumbered is then rewritten in the findings
       (Update-GapFindingTaskIds), and their new task fields are committed ("gap: task links
       <stamp>") and pushed to gap again (BL-2013).
   11. git worktree remove --force <stamp>\tree, then a summary: each analyst's outcome,
       each area's measured X of Y, and the run folder, which is kept for reading.

    The gap worktree (step 1, after the fetch) is the worktree that has the gap branch
    checked out; otherwise <repo>.gap\gap-branch is created on the local gap branch, else
    from origin/gap, else from origin/master. origin/master is merged into it, never
    rebased (with -NoCommit, fast-forward only, so nothing is committed). The measurement
    and gap tools run from its Gap/Tools; when it lacks any of them, from the Gap/Tools
    beside this script.
.PARAMETER Ref
    The commit measured. Default: origin/work/dark-factory.
.PARAMETER Areas
    Any of options, protocols, features, writeout, exitcodes, environment, behaviour,
    comma-separated or as an array. Default: all seven. protocols and features come from
    one tool, so asking for either runs it once.
.PARAMETER CurlVersion
    The upstream release measured against. Default: Gap/Baselines/target.json's version.
.PARAMETER AlongsideShift
    Runs even while a dark factory shift runs, sharing the machine with it.
.PARAMETER DryRun
    Does the refusals, then prints every step's command and changes nothing: no fetch, no
    run folder, no worktree.
.PARAMETER NewTab
    Starts the run in a new herdr tab when HERDR_ENV=1, otherwise in a new console window,
    and returns at once.
.PARAMETER NoTasks
    Skips step 10: no tasks are filed on work/dark-factory.
.PARAMETER NoPullRequest
    Skips the gh pr list and gh pr create calls of step 9.
.PARAMETER NoCommit
    Writes everything into the gap worktree but skips steps 9 and 10: no commit, no push,
    no pull request, no tasks.
.PARAMETER SelfTest
    Checks the refusals on a faked process list, the area-to-tool mapping, the choice or
    creation of the gap worktree, an analyst that changes the tree, and -NoTasks and
    -NoPullRequest, all against faked git, gh and claude commands (no network), then step
    10's task renumbering: the dedupe map, its rewrite of the findings, and two filers
    started from the same board, with real git on a throwaway local origin and the real
    task-board.ps1 (BL-2013). Prints a PASS or FAIL line per check, and exits 1 on any FAIL.
.EXAMPLE
    Gap\RunGapAnalysis.cmd -NewTab
.EXAMPLE
    Gap\RunGapAnalysis.cmd -Areas exitcodes,writeout -AlongsideShift
#>
param(
    [string] $Ref = 'origin/work/dark-factory',
    [string[]] $Areas = @(),
    [string] $CurlVersion,
    [switch] $AlongsideShift,
    [switch] $DryRun,
    [switch] $NewTab,
    [switch] $NoTasks,
    [switch] $NoPullRequest,
    [switch] $NoCommit,
    [switch] $SelfTest
)

$ErrorActionPreference = 'Stop'
$script:Commands = @{ git = 'git'; gh = 'gh'; claude = 'claude'; shell = (Get-Process -Id $PID).Path }
$script:AllAreas = @('options', 'protocols', 'features', 'writeout', 'exitcodes', 'environment', 'behaviour')
$script:ToolsDirectory = Join-Path $PSScriptRoot 'Tools'
$script:RepositoryRoot = Split-Path $PSScriptRoot -Parent
$script:Shell = (Get-Process -Id $PID).Path
$script:LogFile = $null

function Get-GapRunRefusal {
    # The reason this run must not start, or $null. $CommandLines is the command line of
    # every running process.
    param([string] $LaneVariable, [string[]] $CommandLines, [bool] $Alongside)
    if ($LaneVariable) { return "refused: this is a dark factory process (CURL_DARK_FACTORY_LANE=$LaneVariable)." }
    if ($Alongside) { return $null }
    $shift = @($CommandLines | Where-Object { $_ -match 'RunDarkFactory\.ps1' -and $_ -notmatch '-NewTab\b' -and $_ -notmatch '-Restart\b' })
    if ($shift.Count -gt 0) { return 'refused: a dark factory shift is running; pass -AlongsideShift to run anyway.' }
    return $null
}

function Get-ProcessCommandLines {
    # Every running process's command line; empty where the platform cannot tell.
    try { return @(Get-CimInstance Win32_Process -ErrorAction Stop | ForEach-Object { [string]$_.CommandLine }) }
    catch { return @() }
}

function Resolve-GapAreas {
    # The selected areas in run order, with protocols and features folded into one
    # 'version' tool run. Throws on an unknown area.
    param([string[]] $Requested)
    $names = @($Requested | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim().ToLowerInvariant() } | Where-Object { $_ })
    if ($names.Count -eq 0) { $names = $script:AllAreas }
    foreach ($name in $names) { if ($script:AllAreas -notcontains $name) { throw "Unknown area '$name'; use any of $($script:AllAreas -join ', ')." } }
    $tools = New-Object System.Collections.Generic.List[string]
    foreach ($area in $script:AllAreas) {
        if ($names -notcontains $area) { continue }
        $tool = if ($area -eq 'protocols' -or $area -eq 'features') { 'version' } else { $area }
        if (-not $tools.Contains($tool)) { $tools.Add($tool) }
    }
    return [pscustomobject]@{ Areas = @($script:AllAreas | Where-Object { $names -contains $_ }); Tools = $tools.ToArray() }
}

function Write-GapLog {
    param([string] $Text, [string] $Color = 'Gray')
    $line = (Get-Date).ToString('HH:mm:ss') + ' ' + $Text
    Write-Host $line -ForegroundColor $Color
    if ($script:LogFile) { Add-Content -LiteralPath $script:LogFile -Value $line -Encoding UTF8 }
}

function Format-GapCommand {
    param([string] $Exe, [string[]] $Arguments)
    $quoted = @($Arguments | ForEach-Object { if ($_ -match '\s' -or $_ -eq '') { '"' + $_ + '"' } else { $_ } })
    return (@($Exe) + $quoted) -join ' '
}

function Invoke-GapStep {
    # Runs one step, logging its command and output; returns its exit code (0 in -DryRun,
    # where it only prints the command). The output is kept in $script:LastOutput. An $Exe
    # named in $script:Commands (git, gh, claude, shell) runs through it, so -SelfTest can
    # fake it; $InputText, when given, is piped to its standard input.
    param([string] $Name, [string] $Exe, [string[]] $Arguments, [string] $WorkingDirectory, [string] $InputText)
    $shown = if ($script:Commands[$Exe] -is [string]) { $script:Commands[$Exe] } else { $Exe }
    $command = Format-GapCommand $shown $Arguments
    if ($InputText) { $command += ' < prompt' }
    if ($DryRun) { Write-Host "[$Name] $command"; $script:LastOutput = @(); return 0 }
    Write-GapLog "[$Name] $command" 'Cyan'
    $runner = if ($script:Commands.Contains($Exe)) { $script:Commands[$Exe] } else { $Exe }
    $previous = Get-Location
    if ($WorkingDirectory) { Set-Location -LiteralPath $WorkingDirectory }
    $ErrorActionPreference = 'Continue'
    $global:LASTEXITCODE = 0
    try {
        if ($InputText) { $script:LastOutput = @($InputText | & $runner @Arguments 2>&1 | ForEach-Object { [string]$_ }) }
        else { $script:LastOutput = @(& $runner @Arguments 2>&1 | ForEach-Object { [string]$_ }) }
        $code = $global:LASTEXITCODE
    }
    catch { $script:LastOutput = @([string]$_); $code = 1 }
    finally { Set-Location $previous }
    if ($null -eq $code) { $code = 0 }
    if ($script:LogFile) { $script:LastOutput | Add-Content -LiteralPath $script:LogFile -Encoding UTF8 }
    Write-GapLog "[$Name] exit $code" $(if ($code -eq 0) { 'Green' } else { 'Red' })
    return $code
}

function Invoke-GapTool {
    # Runs a Gap/Tools PowerShell script beside this one in its own process. -Command, not
    # -File, so an argument written as @('a','b') reaches a [string[]] parameter as an array.
    param([string] $Name, [string] $Tool, [string[]] $Arguments)
    $quoted = @($Arguments | ForEach-Object { if ($_ -match '^-[A-Za-z]' -or $_ -like '@(*') { $_ } else { "'" + $_.Replace("'", "''") + "'" } })
    $command = "& '" + (Join-Path $script:ToolsDirectory $Tool).Replace("'", "''") + "' " + ($quoted -join ' ') + '; exit $LASTEXITCODE'
    return Invoke-GapStep $Name 'shell' @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-Command', $command)
}

function Invoke-GapQuery {
    # Runs a read-only command through $script:Commands and returns its output lines; runs
    # in -DryRun too, since it changes nothing.
    param([string] $Exe, [string[]] $Arguments)
    $ErrorActionPreference = 'Continue'
    try { return @(& $script:Commands[$Exe] @Arguments 2>$null | ForEach-Object { [string]$_ }) }
    catch { return @() }
}

function Resolve-GapWorktree {
    # The gap worktree: the worktree that has the gap branch checked out, or else how to
    # create one at $Path - on the local gap branch, else from origin/gap, else from
    # origin/master. Reads git only; creates nothing.
    param([string] $Path)
    $current = $null
    foreach ($line in (Invoke-GapQuery 'git' @('-C', $script:RepositoryRoot, 'worktree', 'list', '--porcelain'))) {
        if ($line -like 'worktree *') { $current = $line.Substring(9).Trim() }
        elseif ($line.Trim() -eq 'branch refs/heads/gap' -and $current) { return [pscustomobject]@{ Path = $current; Create = $null; Source = 'existing worktree' } }
    }
    $hasLocal = @(Invoke-GapQuery 'git' @('-C', $script:RepositoryRoot, 'branch', '--list', 'gap') | Where-Object { $_.Trim() }).Count -gt 0
    $hasOrigin = @(Invoke-GapQuery 'git' @('-C', $script:RepositoryRoot, 'branch', '-r', '--list', 'origin/gap') | Where-Object { $_.Trim() }).Count -gt 0
    if ($hasLocal) { return [pscustomobject]@{ Path = $Path; Create = @('worktree', 'add', $Path, 'gap'); Source = 'gap' } }
    if ($hasOrigin) { return [pscustomobject]@{ Path = $Path; Create = @('worktree', 'add', '-b', 'gap', $Path, 'origin/gap'); Source = 'origin/gap' } }
    return [pscustomobject]@{ Path = $Path; Create = @('worktree', 'add', '-b', 'gap', $Path, 'origin/master'); Source = 'origin/master' }
}

function Get-GapAnalystModel {
    # The model: line of .claude/agents/gap-<area>.md; sonnet when it has none.
    param([string] $Area)
    $file = Join-Path $script:RepositoryRoot ".claude/agents/gap-$Area.md"
    if (Test-Path -LiteralPath $file) {
        foreach ($line in (Get-Content -LiteralPath $file -TotalCount 12)) { if ($line -match '^model:\s*(\S+)') { return $Matches[1] } }
    }
    return 'sonnet'
}

function Get-GapAreaFindings {
    # One line per finding of the area in the gap branch's Gap/Findings: id, key, status, items.
    param([string] $FindingsDirectory, [string] $Area)
    $lines = @()
    if (-not (Test-Path -LiteralPath $FindingsDirectory)) { return $lines }
    foreach ($file in (Get-ChildItem -LiteralPath $FindingsDirectory -Filter 'GF-*.md')) {
        $fields = @{}
        foreach ($line in (Get-Content -LiteralPath $file.FullName -TotalCount 30 -Encoding UTF8)) { if ($line -match '^(id|area|key|status|items):\s*(.*)$') { $fields[$Matches[1]] = $Matches[2].Trim() } }
        if ($fields['area'] -eq $Area) { $lines += "- $($fields['id']) key $($fields['key']) status $($fields['status']) items $($fields['items'])" }
    }
    return $lines
}

function Get-GapTreeChanges {
    # The lines of git status --porcelain of the measured tree; empty when it is untouched.
    param([string] $Tree)
    return @(Invoke-GapQuery 'git' @('-C', $Tree, 'status', '--porcelain') | Where-Object { $_.Trim() } | ForEach-Object { $_.Trim() })
}

function Reset-GapTree {
    # Puts the measured tree back to its commit: git reset --hard, then git clean -fd.
    param([string] $ResetName, [string] $CleanName, [string] $Tree)
    Invoke-GapStep $ResetName 'git' @('-C', $Tree, 'reset', '--hard') | Out-Null
    Invoke-GapStep $CleanName 'git' @('-C', $Tree, 'clean', '-fd') | Out-Null
}

function Invoke-GapAnalyst {    # Step 7 for one area: runs gap-<area> headless with the prompt on standard input, saves
    # its reply to $ReportFile and logs its cost. An analyst that leaves the measured tree
    # changed is logged, the tree is reset and its report dropped. Returns what happened.
    param([string] $Area, [string] $Prompt, [string] $Tree, [string] $ReportFile)
    $arguments = @('-p', '--agent', "gap-$Area", '--model', (Get-GapAnalystModel $Area), '--dangerously-skip-permissions', '--output-format', 'stream-json', '--verbose')
    $code = Invoke-GapStep "analyst-$Area" 'claude' $arguments -InputText $Prompt
    if ($DryRun) { return 'dry run' }
    $result = $null
    foreach ($line in $script:LastOutput) {
        if ($line -notmatch '"type"\s*:\s*"result"') { continue }
        try { $result = $line | ConvertFrom-Json } catch { }
    }
    if ($result) { Write-GapLog ("Analyst gap-{0} cost {1:N4} USD." -f $Area, [double]$result.total_cost_usd) }
    $changes = @(Get-GapTreeChanges $Tree)
    if ($changes.Count -gt 0) {
        Write-GapLog "Analyst gap-$Area wrote to the measured tree ($($changes -join '; ')); tree reset and its report dropped." 'Red'
        Reset-GapTree "reset-$Area" "clean-$Area" $Tree
        if (Test-Path -LiteralPath $ReportFile) { Remove-Item -LiteralPath $ReportFile -Force }
        return 'changed the tree'
    }
    if ($code -ne 0 -or -not $result -or -not $result.result) { Write-GapLog "Analyst gap-$Area gave no report (exit $code)." 'Red'; return 'no report' }
    [System.IO.File]::WriteAllText($ReportFile, [string]$result.result)
    return 'report written'
}

function Get-GapScoresTable {
    # The Scores section of a scorecard, for the pull request body.
    param([string] $ScorecardFile)
    if (-not (Test-Path -LiteralPath $ScorecardFile)) { return '(scorecard not found)' }
    $lines = @(Get-Content -LiteralPath $ScorecardFile -Encoding UTF8)
    $start = [Array]::IndexOf($lines, '## Scores')
    if ($start -lt 0) { return '(no Scores section)' }
    $section = @($lines[$start])
    for ($i = $start + 1; $i -lt $lines.Count -and $lines[$i] -notlike '## *'; $i++) { $section += $lines[$i] }
    return ($section -join "`n").Trim()
}

function Publish-GapRun {
    # Steps 9 and 10: commit and push the gap worktree and open the gap to master pull
    # request, then file the tasks on work/dark-factory and push the findings' task fields.
    param([string] $GapWorktree, [string] $Stamp, [string] $RunDirectory, [string] $FindingsDirectory, [string] $ScorecardFile,
          [bool] $SkipTasks, [bool] $SkipPullRequest)
    Write-Host '== Step 9: commit, push gap and open the pull request'
    Invoke-GapStep 'gap-add' 'git' @('-C', $GapWorktree, 'add', '-A', 'Gap') | Out-Null
    if ((Invoke-GapStep 'gap-commit' 'git' @('-C', $GapWorktree, 'commit', '-m', "gap: scorecard $Stamp")) -ne 0) { Write-GapLog 'Nothing committed on gap.' 'DarkYellow' }
    if ((Invoke-GapStep 'gap-push' 'git' @('-C', $GapWorktree, 'push', 'origin', 'gap')) -ne 0) { Write-GapLog 'git push origin gap failed.' 'Red' }
    if ($SkipPullRequest) { Write-GapLog 'Pull request skipped (-NoPullRequest).' }
    else {
        Invoke-GapStep 'pr-list' 'gh' @('pr', 'list', '--head', 'gap', '--base', 'master', '--state', 'open', '--json', 'number', '--jq', 'length') $GapWorktree | Out-Null
        $open = (@($script:LastOutput) -join '').Trim()
        if ($open -and $open -ne '0') { Write-GapLog 'A gap to master pull request is already open.' }
        else {
            $robot = [char]::ConvertFromUtf32(0x1F916)
            $body = "Gap analysis run $Stamp.`n`n" + (Get-GapScoresTable $ScorecardFile) + "`n`n$robot Generated with [Claude Code](https://claude.com/claude-code)"
            if ((Invoke-GapStep 'pr-create' 'gh' @('pr', 'create', '--base', 'master', '--head', 'gap', '--title', "gap: scorecard $Stamp", '--body', $body) $GapWorktree) -ne 0) { Write-GapLog 'gh pr create failed.' 'Red' }
        }
    }
    Write-Host '== Step 10: file the tasks on work/dark-factory'
    if ($SkipTasks) { Write-GapLog 'Tasks skipped (-NoTasks).'; return }
    $board = Join-Path $RunDirectory 'board'
    # Fetch right before allocating: the run started an hour or more ago, and the audit office
    # may have filed tasks since (BL-2013).
    Invoke-GapStep 'board-fetch' 'git' @('-C', $script:RepositoryRoot, 'fetch', 'origin', 'work/dark-factory') | Out-Null
    if ((Invoke-GapStep 'board-tree' 'git' @('-C', $script:RepositoryRoot, 'worktree', 'add', '--detach', $board, 'origin/work/dark-factory')) -ne 0) { Write-GapLog 'Could not create the board worktree; no tasks filed.' 'Red'; return }
    $renumbered = [ordered]@{}
    if ((Invoke-GapTool 'tasks' 'New-TasksFromGaps.ps1' @('-FindingsDirectory', $FindingsDirectory, '-BoardRoot', $board, '-MeasurementsDirectory', (Join-Path $RunDirectory 'measurements'))) -eq 0) {
        Invoke-GapStep 'tasks-add' 'git' @('-C', $board, 'add', '-A', 'Tasks') | Out-Null
        if ((Invoke-GapStep 'tasks-commit' 'git' @('-C', $board, 'commit', '-m', "chore(tasks): file gap tasks from $Stamp")) -eq 0) {
            $pushed = $false
            for ($try = 1; $try -le 3 -and -not $pushed; $try++) {
                $renumbered = Join-GapRenumbering $renumbered (Sync-GapBoard $board $try)
                $pushed = (Invoke-GapStep "tasks-push-$try" 'git' @('-C', $board, 'push', 'origin', 'HEAD:work/dark-factory')) -eq 0
            }
            if (-not $pushed) { Write-GapLog 'Could not push the gap tasks to work/dark-factory after three tries.' 'Red' }
        } else { Write-GapLog 'No new gap tasks to commit.' }
    } else { Write-GapLog 'New-TasksFromGaps.ps1 failed; no tasks filed.' 'Red' }
    Invoke-GapStep 'board-remove' 'git' @('-C', $script:RepositoryRoot, 'worktree', 'remove', '--force', $board) | Out-Null
    if ($renumbered.Count -gt 0) {
        Write-GapLog ('Renumbered: ' + (@($renumbered.Keys | ForEach-Object { "$_ -> $($renumbered[$_])" }) -join ', '))
        Update-GapFindingTaskIds $FindingsDirectory $renumbered
    }
    Invoke-GapStep 'gap-add-tasks' 'git' @('-C', $GapWorktree, 'add', '-A', 'Gap') | Out-Null
    if ((Invoke-GapStep 'gap-commit-tasks' 'git' @('-C', $GapWorktree, 'commit', '-m', "gap: task links $Stamp")) -eq 0) {
        Invoke-GapStep 'gap-push-tasks' 'git' @('-C', $GapWorktree, 'push', 'origin', 'gap') | Out-Null
    }
}

function Sync-GapBoard {
    # One push try's preparation in the board worktree: fetch, rebase the gap tasks onto
    # origin/work/dark-factory, then task-board.ps1 dedupe against the commit rebased onto, so
    # a task another filer pushed meanwhile keeps its ID and this run's takes the next free one,
    # and amend the commit when dedupe renumbered anything. Returns dedupe's renumbering
    # (BL-2013). CLAUDE_PROJECT_DIR names the board, or task-board.ps1 would work on the
    # session's own checkout.
    param([string] $Board, [int] $Try)
    Invoke-GapStep "tasks-fetch-$Try" 'git' @('-C', $Board, 'fetch', 'origin', 'work/dark-factory') | Out-Null
    if ((Invoke-GapStep "tasks-rebase-$Try" 'git' @('-C', $Board, 'rebase', 'origin/work/dark-factory')) -ne 0) {
        Invoke-GapStep "tasks-rebase-abort-$Try" 'git' @('-C', $Board, 'rebase', '--abort') | Out-Null
        Write-GapLog 'Rebasing the gap tasks onto work/dark-factory conflicted.' 'Red'
        return [ordered]@{}
    }
    $upstream = @(Invoke-GapQuery 'git' @('-C', $Board, 'rev-parse', 'origin/work/dark-factory')) | Select-Object -First 1
    if (-not $upstream) { $upstream = 'origin/work/dark-factory' }
    $taskBoard = Join-Path $Board '.claude\skills\task-board\task-board.ps1'
    # dedupe leaves git cat-file's exit code 1 (a file not in -Since) in $LASTEXITCODE, so the
    # outcome is whether it threw, not that code.
    $command = "`$env:CLAUDE_PROJECT_DIR = '" + $Board.Replace("'", "''") + "'; try { & '" + $taskBoard.Replace("'", "''") + "' dedupe -Since '" + $upstream + "'; exit 0 } catch { Write-Output `$_; exit 1 }"
    if ((Invoke-GapStep "tasks-dedupe-$Try" 'shell' @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-Command', $command)) -ne 0) {
        Write-GapLog 'task-board.ps1 dedupe failed.' 'Red'
        return [ordered]@{}
    }
    $renumbered = Get-GapRenumbering $script:LastOutput
    if ($renumbered.Count -gt 0) {
        Invoke-GapStep "tasks-dedupe-add-$Try" 'git' @('-C', $Board, 'add', '-A', 'Tasks') | Out-Null
        Invoke-GapStep "tasks-dedupe-amend-$Try" 'git' @('-C', $Board, 'commit', '--amend', '--no-edit') | Out-Null
    }
    return $renumbered
}

function Get-GapRenumbering {
    # dedupe's "BL-1 -> BL-2  <title>" lines as an ordered old -> new map.
    param([string[]] $Lines)
    $map = [ordered]@{}
    foreach ($line in @($Lines)) {
        if ([string]$line -match '^(BL-\d+) -> (BL-\d+)\b') { $map[$Matches[1]] = $Matches[2] }
    }
    return $map
}

function Join-GapRenumbering {
    # One map from the IDs New-TasksFromGaps.ps1 wrote to their final IDs, across tries: an ID
    # renumbered twice maps to its last number.
    param([System.Collections.IDictionary] $First, [System.Collections.IDictionary] $Then)
    $joined = [ordered]@{}
    foreach ($old in @($First.Keys)) {
        $joined[$old] = if ($Then.Contains($First[$old])) { $Then[$First[$old]] } else { $First[$old] }
    }
    $values = @($First.Values)
    foreach ($old in @($Then.Keys)) {
        if ($values -notcontains $old -and -not $joined.Contains($old)) { $joined[$old] = $Then[$old] }
    }
    return $joined
}

function Update-GapFindingTaskIds {
    # Rewrites each renumbered task ID in the findings, all at once, so A -> B and B -> C never
    # chain; each file keeps its byte-order mark, or its lack of one.
    param([string] $FindingsDirectory, [System.Collections.IDictionary] $Renumbered)
    if ($DryRun -or $Renumbered.Count -eq 0) { return }
    foreach ($file in @(Get-ChildItem -LiteralPath $FindingsDirectory -Filter 'GF-*.md' -File)) {
        $bytes = [IO.File]::ReadAllBytes($file.FullName)
        $bom = $bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF
        $text = [IO.File]::ReadAllText($file.FullName)
        $updated = [regex]::Replace($text, '\bBL-\d+\b', { param($match) if ($Renumbered.Contains($match.Value)) { $Renumbered[$match.Value] } else { $match.Value } })
        if ($updated -ne $text) { [IO.File]::WriteAllText($file.FullName, $updated, (New-Object Text.UTF8Encoding($bom))) }
    }
}

function Get-MeasurementCount {
    # "X of Y" from a measurement's counts (Gap/Instructions/Gap-Format.md).
    param([string] $Path)
    try {
        $counts = (Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json).counts
        return "$($counts.x) of $($counts.y)"
    } catch { return 'measured (counts unreadable)' }
}

function Invoke-GapSelfTest {
    $results = New-Object System.Collections.Generic.List[string]
    $report = { param([string] $Name, [bool] $Passed) $results.Add($(if ($Passed) { "PASS $Name" } else { "FAIL $Name" })) }
    $shift = @('powershell -File Z:\repos\Curl\RunDarkFactory.ps1 -Lanes Auto -Continuous')
    & $report 'refuses under CURL_DARK_FACTORY_LANE' ($null -ne (Get-GapRunRefusal '2' @() $false))
    & $report 'refuses with a faked running shift' ($null -ne (Get-GapRunRefusal '' $shift $false))
    & $report 'does not refuse with -AlongsideShift' ($null -eq (Get-GapRunRefusal '' $shift $true))
    & $report 'does not take a -NewTab or -Restart launcher for a shift' ($null -eq (Get-GapRunRefusal '' @('x RunDarkFactory.ps1 -NewTab', 'x RunDarkFactory.ps1 -Restart') $false))
    $protocols = Resolve-GapAreas @('protocols')
    & $report '-Areas protocols runs the version tool once' (($protocols.Tools -join ',') -eq 'version')
    $both = Resolve-GapAreas @('protocols,features')
    & $report '-Areas protocols,features runs the version tool once' (($both.Tools -join ',') -eq 'version')
    $all = Resolve-GapAreas @()
    & $report 'no -Areas runs all six tools' (($all.Tools -join ',') -eq 'options,version,writeout,exitcodes,environment,behaviour')
    $threw = $false; try { Resolve-GapAreas @('nope') | Out-Null } catch { $threw = $true }
    & $report 'an unknown area is refused' $threw

    # Faked git, gh, claude and shell: each records its call in $script:FakeCalls.
    $saved = $script:Commands
    $script:FakeCalls = New-Object System.Collections.Generic.List[string]
    $script:FakeListing = @(); $script:FakeRemote = @(); $script:FakeStatus = @()
    $script:Commands = @{
        git = { $script:FakeCalls.Add('git ' + ($args -join ' '))
            if ($args -contains 'worktree' -and $args -contains 'list') { return $script:FakeListing }
            if ($args -contains '-r') { return $script:FakeRemote }
            if ($args -contains 'status') { return $script:FakeStatus } }
        gh = { $script:FakeCalls.Add('gh ' + ($args -join ' ')); if ($args -contains 'list') { '0' } }
        claude = { $null = @($input); $script:FakeCalls.Add('claude ' + ($args -join ' ')); '{"type":"result","result":"# report","total_cost_usd":0.01}' }
        shell = { $script:FakeCalls.Add('shell ' + ($args -join ' ')) }
    }
    $scratch = Join-Path ([System.IO.Path]::GetTempPath()) ('gap-selftest-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Force -Path $scratch | Out-Null
    try {
        $script:FakeListing = @('worktree Z:/repos/Curl', 'HEAD 1', 'branch refs/heads/master', '', 'worktree Z:/repos/Curl.gap/gap-branch', 'HEAD 2', 'branch refs/heads/gap')
        $chosen = Resolve-GapWorktree 'X:\new'
        & $report 'the worktree that has gap checked out is chosen' ($chosen.Path -eq 'Z:/repos/Curl.gap/gap-branch' -and $null -eq $chosen.Create)
        $script:FakeListing = @('worktree Z:/repos/Curl', 'branch refs/heads/master'); $script:FakeRemote = @('  origin/gap')
        $fromGap = Resolve-GapWorktree 'X:\new'
        & $report 'the gap worktree is created from origin/gap' ($fromGap.Path -eq 'X:\new' -and $fromGap.Create[-1] -eq 'origin/gap')
        $script:FakeRemote = @()
        $fromMaster = Resolve-GapWorktree 'X:\new'
        & $report 'the gap worktree is created from origin/master without origin/gap' ($fromMaster.Create[-1] -eq 'origin/master')

        $reportFile = Join-Path $scratch 'gap-exitcodes.md'
        $script:FakeStatus = @(' M Curl.Console/Program.cs')
        $outcome = Invoke-GapAnalyst 'exitcodes' 'prompt' $scratch $reportFile
        & $report 'an analyst that changes the tree is logged, the tree reset and its report dropped' ($outcome -eq 'changed the tree' -and -not (Test-Path $reportFile) -and @($script:FakeCalls | Where-Object { $_ -like '*reset --hard*' }).Count -eq 1)
        & $report 'a changed tree is reported with its files' ((Get-GapTreeChanges $scratch) -contains 'M Curl.Console/Program.cs')
        $script:FakeStatus = @()
        $outcome = Invoke-GapAnalyst 'exitcodes' 'prompt' $scratch $reportFile
        & $report 'an analyst that leaves the tree alone has its report saved' ($outcome -eq 'report written' -and (Test-Path $reportFile))

        $script:FakeCalls.Clear()
        Publish-GapRun $scratch 'stamp' $scratch $scratch (Join-Path $scratch 'none.md') $false $false | Out-Null
        $all = $script:FakeCalls -join "`n"
        & $report 'without switches the pull request is opened and the tasks filed' ($all -match 'gh pr create' -and $all -match 'New-TasksFromGaps' -and $all -match 'HEAD:work/dark-factory')
        $script:FakeCalls.Clear()
        Publish-GapRun $scratch 'stamp' $scratch $scratch (Join-Path $scratch 'none.md') $true $false | Out-Null
        $all = $script:FakeCalls -join "`n"
        & $report '-NoTasks skips step 10' ($all -match 'gh pr create' -and $all -notmatch 'New-TasksFromGaps' -and $all -notmatch 'work/dark-factory')
        $script:FakeCalls.Clear()
        Publish-GapRun $scratch 'stamp' $scratch $scratch (Join-Path $scratch 'none.md') $false $true | Out-Null
        $all = $script:FakeCalls -join "`n"
        & $report '-NoPullRequest skips the gh pr create call' ($all -notmatch 'gh pr' -and $all -match 'push origin gap')
        $fetchAt = [array]::FindIndex([string[]]$script:FakeCalls.ToArray(), [Predicate[string]] { param($call) $call -like '*fetch origin work/dark-factory' })
        $treeAt = [array]::FindIndex([string[]]$script:FakeCalls.ToArray(), [Predicate[string]] { param($call) $call -like '*worktree add --detach*' })
        & $report 'step 10 fetches work/dark-factory right before it makes the board worktree' ($fetchAt -ge 0 -and $fetchAt -lt $treeAt)
        & $report 'step 10 dedupes against the commit it rebased onto before each push' ($all -match 'rebase origin/work/dark-factory' -and $all -match 'dedupe -Since')

        $map = Get-GapRenumbering @('BL-10 -> BL-20  a task', 'No duplicate IDs.', 'BL-11 -> BL-21  another')
        & $report 'dedupe output reads as an old -> new map' ($map.Count -eq 2 -and $map['BL-10'] -eq 'BL-20' -and $map['BL-11'] -eq 'BL-21')
        $joined = Join-GapRenumbering ([ordered]@{ 'BL-10' = 'BL-20' }) ([ordered]@{ 'BL-20' = 'BL-30'; 'BL-11' = 'BL-31' })
        & $report 'an ID renumbered on two tries maps to its last number' ($joined.Count -eq 2 -and $joined['BL-10'] -eq 'BL-30' -and $joined['BL-11'] -eq 'BL-31')
        $findings = Join-Path $scratch 'findings'
        New-Item -ItemType Directory -Force -Path $findings | Out-Null
        [IO.File]::WriteAllText((Join-Path $findings 'GF-0001-x.md'), "---`ntask: BL-10`ntasks: [BL-9, BL-10, BL-11]`n---`n", (New-Object Text.UTF8Encoding($false)))
        Update-GapFindingTaskIds $findings ([ordered]@{ 'BL-10' = 'BL-11'; 'BL-11' = 'BL-12' })
        $rewritten = [IO.File]::ReadAllText((Join-Path $findings 'GF-0001-x.md'))
        & $report 'renumbered IDs are rewritten in the findings at once, never chained' ($rewritten -eq "---`ntask: BL-11`ntasks: [BL-9, BL-11, BL-12]`n---`n")

        # Two filers from the same board, with real git and the real task-board.ps1: the audit
        # office pushes BL-002 first, then the gap run's own BL-002 is renumbered on its way in.
        & $report 'two filers started from the same board push distinct IDs and the findings follow' (Test-GapTwoFilers (Join-Path $scratch 'filers'))
    } finally {
        $script:Commands = $saved
        Remove-Item -LiteralPath $scratch -Recurse -Force -ErrorAction SilentlyContinue
    }
    return $results
}

function Test-GapTwoFilers {
    # Builds a bare origin with a one-task board on work/dark-factory, clones it twice, files a
    # BL-002 in each, pushes the first, then runs Sync-GapBoard on the second and pushes it.
    # True when origin then holds the first's BL-002 and the second's task as BL-003, and a
    # finding naming the second's BL-002 is rewritten to BL-003.
    param([string] $Root)
    $saved = $script:Commands
    $script:Commands = @{ git = 'git'; gh = 'gh'; claude = 'claude'; shell = (Get-Process -Id $PID).Path }
    $ErrorActionPreference = 'Continue'
    try {
        $origin = Join-Path $Root 'origin.git'; $seed = Join-Path $Root 'seed'
        git init -q --bare $origin 2>$null | Out-Null
        git init -q $seed 2>$null | Out-Null
        $skill = Join-Path $seed '.claude\skills\task-board'
        New-Item -ItemType Directory -Force -Path $skill, (Join-Path $seed 'Tasks\Backlog') | Out-Null
        $source = Join-Path $script:RepositoryRoot '.claude\skills\task-board'
        foreach ($name in 'task-board.ps1', 'TASK-TEMPLATE.md') { Copy-Item -LiteralPath (Join-Path $source $name) -Destination $skill }
        $task = { param([string] $Folder, [string] $Id, [string] $Slug) [IO.File]::WriteAllText((Join-Path $Folder "Tasks\Backlog\$Id-$Slug.md"), "---`nid: $Id`ntitle: $Slug`n---`n# $Id - $Slug`n", (New-Object Text.UTF8Encoding($false))) }
        & $task $seed 'BL-001' 'seed'
        foreach ($command in @(@('add', '-A'), @('-c', 'user.name=t', '-c', 'user.email=t@t', 'commit', '-q', '-m', 'seed'), @('branch', '-M', 'work/dark-factory'), @('remote', 'add', 'origin', $origin), @('push', '-q', 'origin', 'work/dark-factory'))) { git -C $seed @command 2>$null | Out-Null }
        $audit = Join-Path $Root 'audit'; $gap = Join-Path $Root 'gap'
        foreach ($clone in $audit, $gap) {
            git clone -q -b work/dark-factory $origin $clone 2>$null | Out-Null
            git -C $clone config user.name t; git -C $clone config user.email t@t
        }
        & $task $audit 'BL-002' 'audit-task'
        git -C $audit add -A 2>$null | Out-Null; git -C $audit commit -q -m audit 2>$null | Out-Null; git -C $audit push -q origin HEAD:work/dark-factory 2>$null | Out-Null
        & $task $gap 'BL-002' 'gap-task'
        git -C $gap add -A 2>$null | Out-Null; git -C $gap commit -q -m gap 2>$null | Out-Null
        $renumbered = Sync-GapBoard $gap 1
        git -C $gap push -q origin HEAD:work/dark-factory 2>$null | Out-Null
        $pushed = @(git -C $origin ls-tree --name-only work/dark-factory Tasks/Backlog/ 2>$null | ForEach-Object { Split-Path $_ -Leaf })
        $findings = Join-Path $Root 'findings'
        New-Item -ItemType Directory -Force -Path $findings | Out-Null
        [IO.File]::WriteAllText((Join-Path $findings 'GF-0001-x.md'), "task: BL-002`n", (New-Object Text.UTF8Encoding($false)))
        Update-GapFindingTaskIds $findings $renumbered
        return ($pushed -contains 'BL-002-audit-task.md') -and ($pushed -contains 'BL-003-gap-task.md') -and
            ($renumbered['BL-002'] -eq 'BL-003') -and ([IO.File]::ReadAllText((Join-Path $findings 'GF-0001-x.md')) -eq "task: BL-003`n")
    } catch {
        return $false
    } finally {
        $script:Commands = $saved
    }
}

if ($SelfTest) {
    $lines = @(Invoke-GapSelfTest)
    $lines | ForEach-Object { Write-Output $_ }
    if (@($lines | Where-Object { $_ -like 'FAIL *' }).Count -gt 0) { exit 1 }
    exit 0
}

# Step 1: refusals.
$refusal = Get-GapRunRefusal $env:CURL_DARK_FACTORY_LANE (Get-ProcessCommandLines) $AlongsideShift.IsPresent
Write-Host "Refusal check: $(if ($refusal) { $refusal } else { 'none; not a factory process and no shift in the way.' })"
if ($refusal) { exit 2 }
$selection = Resolve-GapAreas $Areas

if ($NewTab) {
    $forward = @('-Ref', $Ref, '-Areas', ($selection.Areas -join ','))
    if ($CurlVersion) { $forward += @('-CurlVersion', $CurlVersion) }
    if ($AlongsideShift) { $forward += '-AlongsideShift' }
    if ($DryRun) { $forward += '-DryRun' }
    if ($NoTasks) { $forward += '-NoTasks' }
    if ($NoPullRequest) { $forward += '-NoPullRequest' }
    if ($NoCommit) { $forward += '-NoCommit' }
    $label = 'Gap ' + (Get-Date).ToString('HH:mm')
    $herdr = $null
    if ($env:HERDR_ENV -eq '1') {
        if ($env:HERDR_BIN_PATH -and (Test-Path $env:HERDR_BIN_PATH)) { $herdr = $env:HERDR_BIN_PATH }
        elseif (Get-Command herdr -ErrorAction SilentlyContinue) { $herdr = (Get-Command herdr).Source }
    }
    if ($herdr) {
        $create = @('tab', 'create', '--cwd', $script:RepositoryRoot, '--label', $label, '--no-focus')
        if ($env:HERDR_WORKSPACE_ID) { $create += @('--workspace', $env:HERDR_WORKSPACE_ID) }
        $created = (& $herdr @create) -join "`n" | ConvertFrom-Json
        $pane = $created.result.root_pane.pane_id
        if ($pane) {
            & $herdr pane wait-output $pane --match '>' --timeout 15000 2>&1 | Out-Null
            $line = "powershell -NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`" " + ($forward -join ' ')
            # herdr's argument parser strips bare inner quotes; \" survives as a literal quote.
            & $herdr pane run $pane $line.Replace('"', '\"') 2>&1 | Out-Null
            Write-Host "Gap run started in herdr tab '$label'."
            exit 0
        }
        Write-Host 'Could not create a herdr tab; using a console window.' -ForegroundColor DarkYellow
    }
    $argList = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-NoExit', '-File', "`"$PSCommandPath`"") + $forward
    Start-Process -FilePath 'powershell.exe' -ArgumentList $argList -WorkingDirectory $script:RepositoryRoot | Out-Null
    Write-Host 'Gap run started in a new console window.'
    exit 0
}

if (-not $CurlVersion) {
    $CurlVersion = (Get-Content -LiteralPath (Join-Path $PSScriptRoot 'Baselines/target.json') -Raw | ConvertFrom-Json).version
}
$stamp = (Get-Date).ToString('yyyy-MM-dd_HHmm')
$repoName = Split-Path $script:RepositoryRoot -Leaf
$runDirectory = Join-Path (Split-Path $script:RepositoryRoot -Parent) "$repoName.gap\$stamp"
$tree = Join-Path $runDirectory 'tree'
$measurements = Join-Path $runDirectory 'measurements'
$exeName = if ($env:OS -eq 'Windows_NT') { 'curl.exe' } else { 'curl' }
$candidate = Join-Path $tree "Curl.Console/bin/Release/net10.0/$exeName"
$onWindows = $env:OS -eq 'Windows_NT'
if (-not $DryRun) {
    New-Item -ItemType Directory -Force -Path $measurements | Out-Null
    $script:LogFile = Join-Path $runDirectory 'gap.log'
}
Write-GapLog "Gap run $stamp of $Ref against curl $CurlVersion; areas: $($selection.Areas -join ', ')"
$status = [ordered]@{}
foreach ($area in $selection.Areas) { $status[$area] = 'not measured' }

function Stop-GapRun {
    param([string] $Why)
    Write-GapLog "Stopped: $Why" 'Red'
    exit 1
}

Write-Host '== Step 1: refusals, fetch and the gap worktree'
if ((Invoke-GapStep 'fetch' 'git' @('-C', $script:RepositoryRoot, 'fetch', 'origin')) -ne 0) { Stop-GapRun 'git fetch origin failed.' }
$gap = Resolve-GapWorktree (Join-Path (Split-Path $runDirectory -Parent) 'gap-branch')
$gapWorktree = $gap.Path
if ($gap.Create) {
    if ((Invoke-GapStep 'gap-worktree' 'git' (@('-C', $script:RepositoryRoot) + $gap.Create)) -ne 0) { Stop-GapRun "could not create the gap worktree from $($gap.Source)." }
} else { Write-GapLog "Gap worktree: $gapWorktree (already has gap checked out)." }
# Merge, never rebase (a rebase of the audit branch replayed hundreds of commits). -NoCommit
# takes only a fast-forward, so the run commits nothing.
$merge = if ($NoCommit) { @('merge', '--ff-only', 'origin/master') } else { @('merge', '--no-edit', 'origin/master') }
if ((Invoke-GapStep 'gap-merge' 'git' (@('-C', $gapWorktree) + $merge)) -ne 0) { Write-GapLog 'origin/master not merged into the gap worktree; going on with it as it is.' 'DarkYellow' }
# The measurement tools run from the gap worktree's Gap/Tools, so their inventories land in
# its Gap/Upstream; the tools beside this script stand in when it lacks one.
$gapTools = Join-Path $gapWorktree 'Gap/Tools'
$needed = @('Get-UpstreamRelease.ps1', 'GapProbeFunctions.ps1', 'Measure-OptionGap.ps1', 'Measure-VersionGap.ps1', 'Measure-WriteOutGap.ps1', 'Measure-ExitCodeGap.ps1', 'Measure-EnvironmentGap.ps1', 'ConvertTo-BehaviourMeasurement.ps1', 'Measure-ReferenceCrossCheck.ps1', 'Write-GapFindings.ps1', 'Write-GapScorecard.ps1', 'Export-GapDashboardData.ps1', 'New-TasksFromGaps.ps1')
if ($DryRun -or @($needed | Where-Object { -not (Test-Path -LiteralPath (Join-Path $gapTools $_)) }).Count -eq 0) { $script:ToolsDirectory = $gapTools }
else { Write-GapLog "The gap worktree lacks some Gap/Tools; using $script:ToolsDirectory." 'DarkYellow' }
$gapFindings = Join-Path $gapWorktree 'Gap/Findings'
$gapScorecards = Join-Path $gapWorktree 'Gap/Scorecards'
Write-Host '== Step 2: the measured tree'
if ((Invoke-GapStep 'tree' 'git' @('-C', $script:RepositoryRoot, 'worktree', 'add', '--detach', $tree, $Ref)) -ne 0) { Stop-GapRun "could not create the tree at $Ref." }
$commit = if ($DryRun) { '<commit of ' + $Ref + '>' } else { (& git -C $tree rev-parse HEAD).Trim() }
Write-Host '== Step 3: build the candidate'
if ((Invoke-GapStep 'build' 'dotnet' @('build', (Join-Path $tree 'Curl.Console'), '-c', 'Release')) -ne 0) { Stop-GapRun 'the Release build of Curl.Console failed.' }
Write-Host '== Step 4: the upstream release'
if ((Invoke-GapTool 'release' 'Get-UpstreamRelease.ps1' @('-Version', $CurlVersion)) -ne 0) { Stop-GapRun "could not fetch curl $CurlVersion." }
$release = if ($DryRun) { '<release folder>' } else { @($script:LastOutput | Where-Object { $_ })[-1].Trim() }
Write-Host '== Step 5: the reference curl and run.json'
$reference = $null
if ($DryRun) { Write-Host "[reference] . $(Join-Path $script:ToolsDirectory 'GapProbeFunctions.ps1'); Get-GapReferenceCurl -TargetVersion $CurlVersion" }
else {
    . (Join-Path $script:ToolsDirectory 'GapProbeFunctions.ps1')
    $ErrorActionPreference = 'Stop'
    $reference = Get-GapReferenceCurl -TargetVersion $CurlVersion
}
$referenceLine = if ($null -ne $reference -and $reference.Matches) { $reference.VersionLine } else { 'docs' }
$referenceNames = @()
if ($referenceLine -ne 'docs') {
    # Invoke-GapRun reads stdout alone. Under Windows PowerShell 5.1 with Stop, "& curl -V
    # 2>$null" throws on any stderr line, and the debug-enabled 8.22.0 reference writes a
    # warning there on -V, which ended the run with no log line (BL-2128).
    $versionRun = Invoke-GapRun -Path $reference.Path -Arguments @('-V') -Environment @{} -TimeoutSeconds 20
    $versionText = @([System.Text.Encoding]::UTF8.GetString($versionRun.Stdout) -split "`r?`n")
    foreach ($line in $versionText) { if ($line -match '^(Protocols|Features): (.*)$') { $referenceNames += @($Matches[2].Trim() -split '\s+') } }
}
$run = [ordered]@{
    stamp = $stamp; ref = $Ref; commit = $commit; platform = $(if ($onWindows) { 'Windows' } else { 'Unix' })
    target = $CurlVersion; areas = $selection.Areas; reference = $referenceLine
}
if ($DryRun) { Write-Host "[run.json] $(Join-Path $runDirectory 'run.json')" }
else { [System.IO.File]::WriteAllText((Join-Path $runDirectory 'run.json'), ($run | ConvertTo-Json -Depth 4)); Write-GapLog "Reference: $referenceLine" }

Write-Host '== Step 6: measure the areas'
foreach ($tool in $selection.Tools) {
    $out = { param([string] $Name) Join-Path $measurements "$Name.json" }
    $common = @('-Version', $CurlVersion, '-UpstreamRoot', $release)
    switch ($tool) {
        'options' { $code = Invoke-GapTool 'options' 'Measure-OptionGap.ps1' ($common + @('-Candidate', $candidate, '-OutFile', (& $out 'options'))) ; $files = @{ options = 'options' } }
        'version' { $code = Invoke-GapTool 'version' 'Measure-VersionGap.ps1' ($common + @('-RepositoryRoot', $tree, '-Candidate', $candidate, '-OutDirectory', $measurements)) ; $files = @{ protocols = 'protocols'; features = 'features' } }
        'writeout' { $code = Invoke-GapTool 'writeout' 'Measure-WriteOutGap.ps1' ($common + @('-Candidate', $candidate, '-OutFile', (& $out 'writeout'))) ; $files = @{ writeout = 'writeout' } }
        'exitcodes' { $code = Invoke-GapTool 'exitcodes' 'Measure-ExitCodeGap.ps1' ($common + @('-RepositoryRoot', $tree, '-OutFile', (& $out 'exitcodes'))) ; $files = @{ exitcodes = 'exitcodes' } }
        'environment' { $code = Invoke-GapTool 'environment' 'Measure-EnvironmentGap.ps1' ($common + @('-RepositoryRoot', $tree, '-Candidate', $candidate, '-OutFile', (& $out 'environment'))) ; $files = @{ environment = 'environment' } }
        'behaviour' {
            $testsData = Join-Path $release 'tests/data'
            $raw = Join-Path $runDirectory 'raw.json'
            $converted = Join-Path $runDirectory 'behaviour-unchecked.json'
            $code = Invoke-GapStep 'behaviour-run' 'dotnet' @('run', '--file', (Join-Path $tree 'Gap/Tools/Measure-UpstreamCases.cs'), '--', $testsData, $raw) $tree
            if ($code -eq 0) {
                $convert = @('-RawFile', $raw, '-TestsData', $testsData, '-Version', $CurlVersion, '-OutFile', $converted)
                if ($referenceNames.Count -gt 0) { $convert += @('-Reference', $referenceLine, '-ReferenceFeatures', ('@(' + (@($referenceNames | ForEach-Object { "'" + $_.Replace("'", "''") + "'" }) -join ',') + ')')) }
                $code = Invoke-GapTool 'behaviour-convert' 'ConvertTo-BehaviourMeasurement.ps1' $convert
            }
            if ($code -eq 0) { $code = Invoke-GapTool 'behaviour-crosscheck' 'Measure-ReferenceCrossCheck.ps1' @('-Measurement', $converted, '-TestsData', $testsData, '-Candidate', $candidate, '-OutFile', (& $out 'behaviour')) }
            $files = @{ behaviour = 'behaviour' }
        }
    }
    foreach ($area in $files.Keys) {
        if (-not $status.Contains($area)) { continue }
        $path = & $out $files[$area]
        if ($DryRun) { $status[$area] = 'dry run' }
        elseif ($code -eq 0 -and (Test-Path -LiteralPath $path)) { $status[$area] = Get-MeasurementCount $path }
        else { Write-GapLog "Area $area not measured: $tool exited $code." 'Red' }
    }
}

Write-Host '== Step 7: the analysts'
# A measurement tool that changed the tree must not be blamed on the first analyst: check the
# tree before any analyst runs, say which files changed, and reset it.
if (-not $DryRun) {
    $beforeAnalysts = @(Get-GapTreeChanges $tree)
    if ($beforeAnalysts.Count -gt 0) {
        Write-GapLog "A measurement step changed the measured tree before any analyst ran ($($beforeAnalysts -join '; ')); tree reset." 'Red'
        Reset-GapTree 'reset-measured' 'clean-measured' $tree
    }
}
$reports = Join-Path $runDirectory 'reports'
if (-not $DryRun) { New-Item -ItemType Directory -Force -Path $reports | Out-Null }
$analysts = [ordered]@{}
foreach ($area in $status.Keys) {
    if ($status[$area] -eq 'not measured') { continue }
    $prompt = @(
        "Gap analysis run $stamp, area $area.",
        "Run folder: $runDirectory",
        "Measurement file: $(Join-Path $measurements "$area.json")",
        "Measured tree: $tree",
        "Release folder: $release",
        "Run stamp: $stamp",
        'Existing findings of this area (id, key, status, items), from the gap branch''s Gap/Findings:'
    ) + @(Get-GapAreaFindings $gapFindings $area) + @('', 'Read only; never change the measured tree. End with your one report block.')
    $analysts[$area] = Invoke-GapAnalyst $area ($prompt -join "`n") $tree (Join-Path $reports "gap-$area.md")
}

Write-Host '== Step 8: findings, scorecard, inventories and dashboard data'
if ((Invoke-GapTool 'findings' 'Write-GapFindings.ps1' @('-RunDirectory', $runDirectory, '-Stamp', $stamp, '-FindingsDirectory', $gapFindings)) -ne 0) { Write-GapLog 'Write-GapFindings.ps1 failed.' 'Red' }
if ((Invoke-GapTool 'scorecard' 'Write-GapScorecard.ps1' @('-RunDirectory', $runDirectory, '-Stamp', $stamp, '-FindingsDirectory', $gapFindings, '-ScorecardsDirectory', $gapScorecards)) -ne 0) { Write-GapLog 'Write-GapScorecard.ps1 failed.' 'Red' }
$inventorySource = Join-Path (Split-Path $script:ToolsDirectory -Parent) "Upstream/$CurlVersion"
$inventoryTarget = Join-Path $gapWorktree "Gap/Upstream/$CurlVersion"
if ($DryRun) { Write-Host "[inventories] copy $inventorySource\*.json to $inventoryTarget when they differ" }
elseif ((Test-Path -LiteralPath $inventorySource) -and ([System.IO.Path]::GetFullPath($inventorySource) -ne [System.IO.Path]::GetFullPath($inventoryTarget))) {
    New-Item -ItemType Directory -Force -Path $inventoryTarget | Out-Null
    foreach ($file in (Get-ChildItem -LiteralPath $inventorySource -Filter '*.json')) {
        $target = Join-Path $inventoryTarget $file.Name
        if (-not (Test-Path -LiteralPath $target) -or (Get-FileHash -LiteralPath $target).Hash -ne (Get-FileHash -LiteralPath $file.FullName).Hash) {
            Copy-Item -LiteralPath $file.FullName -Destination $target -Force
            Write-GapLog "Inventory $($file.Name) copied to the gap worktree."
        }
    }
} else { Write-GapLog 'Inventories already written in the gap worktree.' }
if ((Invoke-GapTool 'dashboard' 'Export-GapDashboardData.ps1' @('-FindingsDirectory', $gapFindings, '-ScorecardsDirectory', $gapScorecards, '-BaselinesDirectory', (Join-Path $gapWorktree 'Gap/Baselines'), '-OutFile', (Join-Path $runDirectory 'data.json'))) -ne 0) { Write-GapLog 'Export-GapDashboardData.ps1 failed.' 'Red' }

if ($NoCommit) {
    Write-Host '== Step 9: skipped (-NoCommit)'
    Write-Host '== Step 10: skipped (-NoCommit)'
    Write-GapLog "Nothing committed; the run's changes are in $gapWorktree."
} else {
    Publish-GapRun $gapWorktree $stamp $runDirectory $gapFindings (Join-Path $gapScorecards "$stamp.md") $NoTasks.IsPresent $NoPullRequest.IsPresent
}

Write-Host '== Step 11: remove the measured tree and summarise'
Invoke-GapStep 'tree-remove' 'git' @('-C', $script:RepositoryRoot, 'worktree', 'remove', '--force', $tree) | Out-Null
foreach ($area in $analysts.Keys) { Write-GapLog ("  analyst {0,-12} {1}" -f $area, $analysts[$area]) }
Write-GapLog 'Summary:'
foreach ($area in $status.Keys) { Write-GapLog ("  {0,-12} {1}" -f $area, $status[$area]) }
Write-GapLog "Run folder: $runDirectory$(if ($DryRun) { ' (dry run: not created)' })"
