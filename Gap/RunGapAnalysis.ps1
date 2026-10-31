<#
.SYNOPSIS
    Measures Curl against an upstream curl release, area by area, on Windows here and on
    Linux and macOS through the gap-platforms workflow, then turns the measurements into
    findings, a scorecard, a pull request and Curl tasks (ADR-0433 decisions 3, 4, 5 and 7;
    ADR-0505).
.DESCRIPTION
    Steps, each traced to <repo>.gap\<stamp>\gap.log, where <repo>.gap sits beside the
    checkout and <stamp> is yyyy-MM-dd_HHmm:

    1. Refusals: inside a dark factory process (CURL_DARK_FACTORY_LANE set), and while a
       shift runs (a process whose command line holds RunDarkFactory.ps1 without -NewTab or
       -Restart) unless -AlongsideShift is given. Then git fetch origin.
    2. git worktree add --detach <stamp>\tree <Ref>. Every measurement runs against that
       tree's commit, never against the checkout this script was started from.
    The platform measured here is the one this script runs on (BL-3928): windows from
    Gap\RunGapAnalysis.cmd, linux or macos from Gap/RunGapAnalysis.sh (PowerShell 7). The
    others selected come from gap-platforms.yml, which measures only linux and macos, so off
    Windows a selected windows is missing, with the reason. "Windows" in steps 3 to 5 below
    means the platform measured here, and "linux and macos" the platforms measured by CI.

    3. When linux or macos is selected: gh workflow run gap-platforms.yml --ref master
       -f ref=<commit> -f request=<stamp>, then the run is found in gh run list by its title
       "Gap platforms <stamp> <commit>" (up to two minutes). A dispatch that fails leaves
       linux and macos missing, with the reason, and the run goes on.
    4. Gap/Tools/Get-UpstreamRelease.ps1 -Version <CurlVersion>; its last output line is
       the release folder, named in every analyst's prompt. Then, when windows is selected:
       Gap/Tools/Measure-GapAreas.ps1 -Tree <tree> -Release <folder> -OutDirectory
       <stamp>\measurements\windows -StatusFile <stamp>\status-windows.json, the same
       measurement phase gap-platforms.yml runs on Linux and macOS: it builds the candidate,
       finds the reference curl (the Schannel curl of Git for Windows) and runs each area's
       Measure-* tool (its help lists them). A measurement step that
       changed the tree is logged with the files and the tree reset, so no analyst is
       blamed for it.
    5. gh run view <run> once a minute until it completes or -PlatformTimeoutMinutes pass
       from the dispatch; then for linux and macos, gh run download <run> -n
       gap-measurements-<platform> into <stamp>\downloads\<platform>, and its
       measurements are copied to <stamp>\measurements\<platform>\. A platform whose run
       did not finish in time, whose job left no measurement, or that was never started is
       missing, with the reason, and the run goes on with the others; nothing stands in for
       it. <stamp>\platforms.json records each platform's state and reason (the scorecard
       names a missing platform from it), and <stamp>\run.json the commit, platforms,
       target, areas, each platform's reference and the gap-platforms run. With no platform
       measured the run stops.
    6. Write-GapFindings.ps1 -ListUncovered names each platform's own gaps (its rule 0: a
       linux or macos gap that windows, or linux for macos, shows too is left to that
       platform's finding).
    7. The analysts, headless, the way the dark factory runs agents:
       claude -p --agent gap-<area> --model <model: of .claude/agents/gap-<area>.md>
       --dangerously-skip-permissions --output-format stream-json --verbose, with a prompt
       on standard input naming the platform, run folder, measurement file, tree, release
       folder and run stamp and the area's existing findings of that platform (id, key, status, items).
       Every measured windows area gets its analyst; a linux or macos area gets one only
       when it has gaps of its own, and its prompt lists them. The result event's text is
       saved to <stamp>\reports\<platform>\gap-<area>.md and its total_cost_usd logged. An
       analyst that leaves the tree changed (git status --porcelain not empty; the files
       are logged) is logged, the tree is reset (git reset --hard, git clean -fd) and its
       report dropped, so its gaps fall to the ungrouped finding.
    8. In the gap worktree: Write-GapFindings.ps1 -RunDirectory <stamp> -Stamp <stamp>, then
       Write-GapScorecard.ps1 (scorecard and Gap/Scorecards/history.json, one entry per
       measured platform); the measured inventories Gap/Upstream/<version>/*.json are
       copied in when they differ (when the tools ran from the gap worktree they are
       already there); then Export-GapDashboardData.ps1 -OutFile <stamp>\data.json.
    9. Unless -NoCommit: git add -A Gap and commit "gap: scorecard <stamp>" in the gap
       worktree, git push origin gap, and, unless -NoPullRequest or one is already open
       (gh pr list --head gap --base master), gh pr create --base master --head gap with the
       scorecard's Scores section as the body. The pull request is never merged here.
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
       each platform's areas as X of Y (or why the platform is missing), and the run
       folder, which is kept for reading.

    The gap worktree (step 1, after the fetch) is the worktree that has the gap branch
    checked out; otherwise <repo>.gap\gap-branch is created on the local gap branch, else
    from origin/gap, else from origin/master. origin/master is merged into it, never
    rebased (with -NoCommit, fast-forward only, so nothing is committed). The measurement
    and gap tools run from its Gap/Tools; when it lacks any of them, from the Gap/Tools
    beside this script. gap-platforms.yml runs master's Gap/Tools.
.PARAMETER Ref
    The commit measured. Default: origin/work/dark-factory. It must be on origin for the
    gap-platforms workflow to check it out.
.PARAMETER Areas
    Any of options, protocols, features, writeout, exitcodes, environment, behaviour,
    comma-separated or as an array. Default: all seven. The gap-platforms workflow measures
    all seven on Linux and macOS whatever is asked; only the selected areas' measurements
    are taken from its artifacts.
.PARAMETER Platforms
    Any of windows, linux, macos, comma-separated or as an array. Default: all three.
.PARAMETER PlatformTimeoutMinutes
    How long after the dispatch step 5 waits for the gap-platforms run. Default: 180.
.PARAMETER CurlVersion
    The upstream release measured against. Default: Gap/Baselines/target.json's version.
.PARAMETER AlongsideShift
    Runs even while a dark factory shift runs, sharing the machine with it.
.PARAMETER DryRun
    Does the refusals, then prints every step's command and changes nothing: no fetch, no
    dispatch, no run folder, no worktree.
.PARAMETER NewTab
    Starts the run in a new herdr tab when HERDR_ENV=1, otherwise in a new console window
    (off Windows, a detached pwsh writing <repo>.gap/RunGapAnalysis-<stamp>.log), and returns
    at once.
.PARAMETER NoTasks
    Skips step 10: no tasks are filed on work/dark-factory.
.PARAMETER NoPullRequest
    Skips the gh pr list and gh pr create calls of step 9.
.PARAMETER NoCommit
    Writes everything into the gap worktree but skips steps 9 and 10: no commit, no push,
    no pull request, no tasks.
.PARAMETER SelfTest
    Checks the refusals on a faked process list, the area and platform selection, the
    choice or creation of the gap worktree, an analyst that changes the tree, -NoTasks and
    -NoPullRequest, the gap-platforms dispatch, wait and download with a missing platform
    and a timeout, platforms.json and each platform's findings in the analyst prompt, all
    against faked git, gh and claude commands (no network), then step 10's task
    renumbering: the dedupe map, its rewrite of the findings, and two filers started from
    the same board, with real git on a throwaway local origin and the real task-board.ps1
    (BL-2013). Prints a PASS or FAIL line per check, and exits 1 on any FAIL.
.EXAMPLE
    Gap\RunGapAnalysis.cmd -NewTab
.EXAMPLE
    Gap\RunGapAnalysis.cmd -Areas exitcodes,writeout -Platforms windows -AlongsideShift
.EXAMPLE
    Gap/RunGapAnalysis.sh -NewTab -Platforms linux,macos
#>
param(
    [string] $Ref = 'origin/work/dark-factory',
    [string[]] $Areas = @(),
    [string[]] $Platforms = @(),
    [int] $PlatformTimeoutMinutes = 180,
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
# The PowerShell running this script; pwsh installed as a .NET tool runs as dotnet hosting
# pwsh.dll, so its pwsh launcher on PATH stands in for the process.
$script:Shell = (Get-Process -Id $PID).Path
if ([System.IO.Path]::GetFileNameWithoutExtension($script:Shell) -eq 'dotnet') { $script:Shell = (Get-Command pwsh -CommandType Application | Select-Object -First 1).Source }
$script:Commands = @{ git = 'git'; gh = 'gh'; claude = 'claude'; shell = $script:Shell }
# The platform this run measures itself (BL-3928): windows from Gap\RunGapAnalysis.cmd, linux
# or macos from Gap/RunGapAnalysis.sh. The others come from gap-platforms.yml, which measures
# only linux and macos, so windows is measured only from a Windows checkout.
$script:OnWindows = $PSVersionTable.PSVersion.Major -lt 6 -or $IsWindows
$script:LocalPlatform = if ($script:OnWindows) { 'windows' } elseif ($IsMacOS) { 'macos' } else { 'linux' }
$script:PowerShellExe = if ($script:OnWindows) { 'powershell' } else { 'pwsh' }
$script:AllAreas = @('options', 'protocols', 'features', 'writeout', 'exitcodes', 'environment', 'behaviour')
$script:AllPlatforms = @('windows', 'linux', 'macos')
$script:Sleep = { param([int] $Seconds) Start-Sleep -Seconds $Seconds }
$script:PlatformWorkflow = 'gap-platforms.yml'
$script:ToolsDirectory = Join-Path $PSScriptRoot 'Tools'
$script:RepositoryRoot = Split-Path $PSScriptRoot -Parent
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
    try {
        if ($script:OnWindows) { return @(Get-CimInstance Win32_Process -ErrorAction Stop | ForEach-Object { [string]$_.CommandLine }) }
        return @(& ps -axo 'args=' | ForEach-Object { "$_".Trim() })
    } catch { return @() }
}

function Resolve-GapAreas {
    # The selected areas in run order. Throws on an unknown area. Which tool measures which
    # area is Measure-GapAreas.ps1's business.
    param([string[]] $Requested)
    $names = @($Requested | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim().ToLowerInvariant() } | Where-Object { $_ })
    if ($names.Count -eq 0) { $names = $script:AllAreas }
    foreach ($name in $names) { if ($script:AllAreas -notcontains $name) { throw "Unknown area '$name'; use any of $($script:AllAreas -join ', ')." } }
    return [pscustomobject]@{ Areas = @($script:AllAreas | Where-Object { $names -contains $_ }) }
}

function Resolve-GapPlatforms {
    # The selected platforms in the order windows, linux, macos. Throws on an unknown one.
    param([string[]] $Requested)
    $names = @($Requested | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim().ToLowerInvariant() } | Where-Object { $_ })
    if ($names.Count -eq 0) { $names = $script:AllPlatforms }
    foreach ($name in $names) { if ($script:AllPlatforms -notcontains $name) { throw "Unknown platform '$name'; use any of $($script:AllPlatforms -join ', ')." } }
    return @($script:AllPlatforms | Where-Object { $names -contains $_ })
}

function Start-GapPlatformRun {
    # Step 3: dispatches gap-platforms.yml on master for the measured commit, named after the
    # run stamp, then finds the run by its title ("Gap platforms <stamp> <commit>"). Returns
    # { Id, Url, Failure }: Id and Url $null and Failure saying why when either step fails.
    param([string] $Stamp, [string] $Commit)
    $dispatch = @('workflow', 'run', $script:PlatformWorkflow, '--ref', 'master', '-f', "ref=$Commit", '-f', "request=$Stamp")
    $code = Invoke-GapStep 'platforms-dispatch' 'gh' $dispatch $script:RepositoryRoot
    if ($DryRun) { return [pscustomobject]@{ Id = $null; Url = $null; Failure = 'dry run' } }
    if ($code -ne 0) { return [pscustomobject]@{ Id = $null; Url = $null; Failure = "gh workflow run $($script:PlatformWorkflow) failed (exit $code): $(@($script:LastOutput) | Select-Object -Last 1)" } }
    for ($try = 1; $try -le 12; $try++) {
        $listing = (Invoke-GapQuery 'gh' @('run', 'list', '--workflow', $script:PlatformWorkflow, '--event', 'workflow_dispatch', '--limit', '30', '--json', 'databaseId,displayTitle,url') $script:RepositoryRoot) -join "`n"
        $runs = @()
        try { if ($listing.Trim()) { $runs = @($listing | ConvertFrom-Json | ForEach-Object { $_ }) } } catch { $runs = @() }
        $found = @($runs | Where-Object { [string]$_.displayTitle -like "Gap platforms $Stamp *" }) | Select-Object -First 1
        if ($null -ne $found) {
            Write-GapLog "gap-platforms run $($found.databaseId): $($found.url)"
            return [pscustomobject]@{ Id = [string]$found.databaseId; Url = [string]$found.url; Failure = $null }
        }
        & $script:Sleep 10
    }
    return [pscustomobject]@{ Id = $null; Url = $null; Failure = "the dispatched $($script:PlatformWorkflow) run named $Stamp did not appear in gh run list within two minutes" }
}

function Wait-GapPlatformRun {
    # Step 5: polls the gap-platforms run once a minute until it completes or $Deadline
    # passes. Returns { Completed, Conclusion, Jobs } (Jobs: name, conclusion).
    param([pscustomobject] $Run, [datetime] $Deadline)
    while ($true) {
        $text = (Invoke-GapQuery 'gh' @('run', 'view', $Run.Id, '--json', 'status,conclusion,jobs') $script:RepositoryRoot) -join "`n"
        $view = $null
        try { if ($text.Trim()) { $view = $text | ConvertFrom-Json } } catch { $view = $null }
        if ($null -ne $view -and [string]$view.status -eq 'completed') {
            return [pscustomobject]@{ Completed = $true; Conclusion = [string]$view.conclusion; Jobs = @($view.jobs | ForEach-Object { [pscustomobject]@{ Name = [string]$_.name; Conclusion = [string]$_.conclusion } }) }
        }
        if ((Get-Date) -ge $Deadline) { return [pscustomobject]@{ Completed = $false; Conclusion = 'timed out'; Jobs = @() } }
        & $script:Sleep 60
    }
}

function Receive-GapPlatformMeasurements {
    # Step 5 for one remote platform: gh run download of the gap-measurements-<platform>
    # artifact into <run>/downloads/<platform>, then its measurements into
    # <run>/measurements/<platform>/. Returns { State = measured | missing, Reason, Status }:
    # missing when the run never started, did not finish in time, or left no measurement,
    # with the reason; measured when any area's measurement arrived, with a reason naming
    # the job's conclusion when it did not succeed.
    param([string] $Platform, [pscustomobject] $Run, [pscustomobject] $Outcome, [string] $RunDirectory, [string[]] $SelectedAreas = $script:AllAreas)
    if ($null -eq $Run.Id) { return [pscustomobject]@{ State = 'missing'; Reason = $Run.Failure; Status = $null } }
    if (-not $Outcome.Completed) { return [pscustomobject]@{ State = 'missing'; Reason = "gap-platforms run $($Run.Id) did not finish within $PlatformTimeoutMinutes minutes ($($Run.Url))"; Status = $null } }
    $job = @($Outcome.Jobs | Where-Object { $_.Name -like "*$Platform)*" }) | Select-Object -First 1
    $jobConclusion = if ($null -ne $job) { $job.Conclusion } else { 'not found' }
    $download = Join-Path (Join-Path $RunDirectory 'downloads') $Platform
    $code = Invoke-GapStep "download-$Platform" 'gh' @('run', 'download', $Run.Id, '-n', "gap-measurements-$Platform", '-D', $download) $script:RepositoryRoot
    $status = $null
    $statusFile = Join-Path $download 'status.json'
    if (Test-Path -LiteralPath $statusFile) { try { $status = Get-Content -LiteralPath $statusFile -Raw | ConvertFrom-Json } catch { $status = $null } }
    $files = @()
    $source = Join-Path $download 'measurements'
    if ($code -eq 0 -and (Test-Path -LiteralPath $source)) { $files = @(Get-ChildItem -LiteralPath $source -Filter '*.json' -File | Where-Object { $SelectedAreas -contains $_.BaseName }) }
    if ($files.Count -eq 0) {
        $why = "gap-platforms run $($Run.Id): the $Platform job ended $jobConclusion and left no measurement"
        if ($null -ne $status -and $status.failure) { $why += " ($($status.failure))" }
        return [pscustomobject]@{ State = 'missing'; Reason = "$why; $($Run.Url)"; Status = $status }
    }
    $target = Join-Path (Join-Path $RunDirectory 'measurements') $Platform
    New-Item -ItemType Directory -Force -Path $target | Out-Null
    foreach ($file in $files) { Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $target $file.Name) -Force }
    $reason = if ($jobConclusion -eq 'success') { $null } else { "the $Platform job ended $jobConclusion; areas it did not measure are carried or missing" }
    return [pscustomobject]@{ State = 'measured'; Reason = $reason; Status = $status }
}

function Write-GapPlatformsFile {
    # <run>/platforms.json: one property per selected platform, { state, reason, reference,
    # areas }, read by Write-GapScorecard.ps1 to name a missing platform and why.
    param([string] $Path, [System.Collections.IDictionary] $States)
    $json = [ordered]@{}
    foreach ($platform in $States.Keys) {
        $state = $States[$platform]
        $reference = $null; $areas = $null
        if ($null -ne $state.Status) { $reference = $state.Status.reference; $areas = $state.Status.areas }
        $json[$platform] = [ordered]@{ state = $state.State; reason = $state.Reason; reference = $reference; areas = $areas }
    }
    if ($DryRun) { Write-Host "[platforms.json] $Path"; return }
    [System.IO.File]::WriteAllText($Path, ($json | ConvertTo-Json -Depth 5), (New-Object System.Text.UTF8Encoding $false))
}

function Get-GapUncovered {
    # The linux and macos gap items that are their platform's own (Write-GapFindings.ps1
    # -ListUncovered, rule 0) as "<platform>|<area>" -> keys.
    param([string[]] $Lines)
    $map = @{}
    foreach ($line in @($Lines)) {
        if ([string]$line -notmatch '^(windows|linux|macos) (\S+) (\S+)$') { continue }
        $slot = "$($Matches[1])|$($Matches[2])"
        if (-not $map.ContainsKey($slot)) { $map[$slot] = New-Object System.Collections.Generic.List[string] }
        $map[$slot].Add($Matches[3])
    }
    return $map
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
    param([string] $Exe, [string[]] $Arguments, [string] $WorkingDirectory)
    $ErrorActionPreference = 'Continue'
    $previous = Get-Location
    if ($WorkingDirectory -and (Test-Path -LiteralPath $WorkingDirectory)) { Set-Location -LiteralPath $WorkingDirectory }
    try { return @(& $script:Commands[$Exe] @Arguments 2>$null | ForEach-Object { [string]$_ }) }
    catch { return @() }
    finally { Set-Location $previous }
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
    # One line per finding of the area and platform in the gap branch's Gap/Findings: id,
    # key, status, items. A finding without a platform field is a windows one.
    param([string] $FindingsDirectory, [string] $Area, [string] $Platform = 'windows')
    $lines = @()
    if (-not (Test-Path -LiteralPath $FindingsDirectory)) { return $lines }
    foreach ($file in (Get-ChildItem -LiteralPath $FindingsDirectory -Filter 'GF-*.md')) {
        $fields = @{ platform = 'windows' }
        foreach ($line in (Get-Content -LiteralPath $file.FullName -TotalCount 30 -Encoding UTF8)) { if ($line -match '^(id|area|platform|key|status|items):\s*(.*)$' -and $Matches[2].Trim()) { $fields[$Matches[1]] = $Matches[2].Trim() } }
        if ($fields['area'] -eq $Area -and $fields['platform'] -eq $Platform) { $lines += "- $($fields['id']) key $($fields['key']) status $($fields['status']) items $($fields['items'])" }
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
    $taskBoard = Join-Path $Board '.claude/skills/task-board/task-board.ps1'
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

function Test-GapPlatformRuns {
    # Steps 3 and 5 against a faked gh: the dispatch names the commit and the stamp, the run
    # is found by its title, a finished run's artifact lands in measurements/<platform>/, a
    # failed job with no artifact and a run past its deadline are reported missing with why.
    param([string] $Root, [scriptblock] $Report)
    $script:FakeRunState = 'completed'
    $script:FakeDownloads = @{ linux = $true; macos = $false }
    $script:Commands.gh = {
        $script:FakeCalls.Add('gh ' + ($args -join ' '))
        if ($args -contains 'list' -and $args -contains 'run') { return '[{"databaseId":7,"displayTitle":"Gap platforms 2026-10-15_1200 abc","url":"https://example/runs/7"},{"databaseId":6,"displayTitle":"Gap platforms 2026-10-14_0900 def","url":"https://example/runs/6"}]' }
        if ($args -contains 'view') { return ('{"status":"' + $script:FakeRunState + '","conclusion":"failure","jobs":[{"name":"measure (ubuntu-latest, linux)","conclusion":"success"},{"name":"measure (macos-latest, macos)","conclusion":"failure"}]}') }
        if ($args -contains 'download') {
            $platform = ([string]$args[4]) -replace '^gap-measurements-', ''
            if (-not $script:FakeDownloads[$platform]) { $global:LASTEXITCODE = 1; return 'no artifact' }
            $folder = Join-Path ([string]$args[6]) 'measurements'
            New-Item -ItemType Directory -Force -Path $folder | Out-Null
            [IO.File]::WriteAllText((Join-Path $folder 'options.json'), '{ "area": "options", "platform": "' + $platform + '" }')
            [IO.File]::WriteAllText((Join-Path ([string]$args[6]) 'status.json'), '{ "platform": "' + $platform + '", "reference": "curl 9.9.9 (x86_64-pc-linux-gnu)", "areas": { "options": "1 of 2" }, "failure": null }')
        }
    }
    $script:FakeCalls.Clear()
    $run = Start-GapPlatformRun '2026-10-15_1200' 'abc'
    $calls = $script:FakeCalls -join "`n"
    & $Report 'step 3 dispatches gap-platforms.yml on master for the commit, named after the stamp' ($calls -match 'gh workflow run gap-platforms\.yml --ref master -f ref=abc -f request=2026-10-15_1200')
    & $Report 'step 3 finds the dispatched run by its title' ($run.Id -eq '7' -and $run.Url -eq 'https://example/runs/7' -and $null -eq $run.Failure)
    $outcome = Wait-GapPlatformRun $run (Get-Date).AddMinutes(5)
    $linux = Receive-GapPlatformMeasurements 'linux' $run $outcome $Root
    & $Report 'step 5 puts a finished platform''s measurements in measurements/<platform>/' ($linux.State -eq 'measured' -and $null -eq $linux.Reason -and (Test-Path -LiteralPath (Join-Path $Root 'measurements/linux/options.json')) -and $linux.Status.reference -like 'curl 9.9.9*')
    $macos = Receive-GapPlatformMeasurements 'macos' $run $outcome $Root
    & $Report 'step 5 reports a platform whose job failed with no artifact as missing, saying why' ($macos.State -eq 'missing' -and $macos.Reason -match 'macos job ended failure and left no measurement' -and $macos.Reason -match 'https://example/runs/7')
    $script:FakeRunState = 'in_progress'
    $late = Wait-GapPlatformRun $run (Get-Date).AddMinutes(-1)
    $timedOut = Receive-GapPlatformMeasurements 'linux' $run $late $Root
    & $Report 'step 5 reports a run past its deadline as missing, naming the timeout' (-not $late.Completed -and $timedOut.State -eq 'missing' -and $timedOut.Reason -match 'did not finish within')
    $never = Receive-GapPlatformMeasurements 'linux' ([pscustomobject]@{ Id = $null; Url = $null; Failure = 'gh workflow run gap-platforms.yml failed (exit 1): x' }) $late $Root
    & $Report 'a run that never started leaves its platforms missing with the dispatch failure' ($never.State -eq 'missing' -and $never.Reason -match 'failed \(exit 1\)')
    $states = [ordered]@{ windows = [pscustomobject]@{ State = 'measured'; Reason = $null; Status = $null }; macos = $macos }
    Write-GapPlatformsFile (Join-Path $Root 'platforms.json') $states
    $written = Get-Content -LiteralPath (Join-Path $Root 'platforms.json') -Raw | ConvertFrom-Json
    & $Report 'platforms.json names each platform''s state and a missing one''s reason' ($written.windows.state -eq 'measured' -and $written.macos.state -eq 'missing' -and $written.macos.reason -eq $macos.Reason)
    $map = Get-GapUncovered @('windows options options:--a', 'linux options options:--b', 'linux options options:--c', 'macos behaviour behaviour:test1', 'noise')
    & $Report 'the -ListUncovered lines read as platform and area to keys' ($map['linux|options'].Count -eq 2 -and $map['macos|behaviour'][0] -eq 'behaviour:test1' -and -not $map.ContainsKey('noise'))
}

function Invoke-GapSelfTest {
    $results = New-Object System.Collections.Generic.List[string]
    $report = { param([string] $Name, [bool] $Passed) $results.Add($(if ($Passed) { "PASS $Name" } else { "FAIL $Name" })) }
    $shift = @('powershell -File Z:\repos\Curl\RunDarkFactory.ps1 -Lanes Auto -Continuous')
    & $report 'refuses under CURL_DARK_FACTORY_LANE' ($null -ne (Get-GapRunRefusal '2' @() $false))
    & $report 'refuses with a faked running shift' ($null -ne (Get-GapRunRefusal '' $shift $false))
    & $report 'does not refuse with -AlongsideShift' ($null -eq (Get-GapRunRefusal '' $shift $true))
    & $report 'does not take a -NewTab or -Restart launcher for a shift' ($null -eq (Get-GapRunRefusal '' @('x RunDarkFactory.ps1 -NewTab', 'x RunDarkFactory.ps1 -Restart') $false))
    & $report 'no -Areas selects all seven areas in run order' (((Resolve-GapAreas @()).Areas -join ',') -eq 'options,protocols,features,writeout,exitcodes,environment,behaviour')
    & $report '-Areas features,protocols selects them in run order' (((Resolve-GapAreas @('features,protocols')).Areas -join ',') -eq 'protocols,features')
    $threw = $false; try { Resolve-GapAreas @('nope') | Out-Null } catch { $threw = $true }
    & $report 'an unknown area is refused' $threw
    $expectedLocal = if ($PSVersionTable.PSVersion.Major -lt 6 -or $IsWindows) { 'windows' } elseif ($IsMacOS) { 'macos' } else { 'linux' }
    & $report "the platform measured here is this machine's ($expectedLocal)" ($script:LocalPlatform -eq $expectedLocal)
    & $report 'the process table shows this run' (@(Get-ProcessCommandLines | Where-Object { $_ -match 'RunGapAnalysis\.ps1' }).Count -gt 0)
    & $report 'no -Platforms selects windows, linux and macos' (((Resolve-GapPlatforms @()) -join ',') -eq 'windows,linux,macos')
    & $report '-Platforms macos,windows selects them in platform order' (((Resolve-GapPlatforms @('macos,windows')) -join ',') -eq 'windows,macos')
    $threw = $false; try { Resolve-GapPlatforms @('beos') | Out-Null } catch { $threw = $true }
    & $report 'an unknown platform is refused' $threw

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

        $savedSleep = $script:Sleep
        $script:Sleep = { param([int] $Seconds) }
        try { Test-GapPlatformRuns (Join-Path $scratch 'platforms') $report }
        finally { $script:Sleep = $savedSleep }
        $platformFindings = Join-Path $scratch 'platform-findings'
        New-Item -ItemType Directory -Force -Path $platformFindings | Out-Null
        [IO.File]::WriteAllText((Join-Path $platformFindings 'GF-0001-a.md'), "---`nid: GF-0001`narea: options`nkey: options:a`nstatus: open`nitems: [options:--a]`n---`n")
        [IO.File]::WriteAllText((Join-Path $platformFindings 'GF-0002-b.md'), "---`nid: GF-0002`narea: options`nplatform: linux`nkey: options:b`nstatus: open`nitems: [options:--b]`n---`n")
        $windowsLines = @(Get-GapAreaFindings $platformFindings 'options' 'windows')
        $linuxLines = @(Get-GapAreaFindings $platformFindings 'options' 'linux')
        & $report 'an analyst is shown only its own platform''s findings; one without a platform field is windows''' ($windowsLines.Count -eq 1 -and $windowsLines[0] -like '- GF-0001 *' -and $linuxLines.Count -eq 1 -and $linuxLines[0] -like '- GF-0002 *')

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
    $script:Commands = @{ git = 'git'; gh = 'gh'; claude = 'claude'; shell = $script:Shell }
    $ErrorActionPreference = 'Continue'
    try {
        $origin = Join-Path $Root 'origin.git'; $seed = Join-Path $Root 'seed'
        git init -q --bare $origin 2>$null | Out-Null
        git init -q $seed 2>$null | Out-Null
        $skill = Join-Path $seed '.claude/skills/task-board'
        New-Item -ItemType Directory -Force -Path $skill, (Join-Path $seed 'Tasks/Backlog') | Out-Null
        $source = Join-Path $script:RepositoryRoot '.claude/skills/task-board'
        foreach ($name in 'task-board.ps1', 'TASK-TEMPLATE.md') { Copy-Item -LiteralPath (Join-Path $source $name) -Destination $skill }
        $task = { param([string] $Folder, [string] $Id, [string] $Slug) [IO.File]::WriteAllText((Join-Path $Folder "Tasks/Backlog/$Id-$Slug.md"), "---`nid: $Id`ntitle: $Slug`n---`n# $Id - $Slug`n", (New-Object Text.UTF8Encoding($false))) }
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
$platformSelection = @(Resolve-GapPlatforms $Platforms)

if ($NewTab) {
    $forward = @('-Ref', $Ref, '-Areas', ($selection.Areas -join ','), '-Platforms', ($platformSelection -join ','), '-PlatformTimeoutMinutes', [string]$PlatformTimeoutMinutes)
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
            $line = "$script:PowerShellExe -NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`" " + ($forward -join ' ')
            # herdr's argument parser strips bare inner quotes; \" survives as a literal quote.
            & $herdr pane run $pane $line.Replace('"', '\"') 2>&1 | Out-Null
            Write-Host "Gap run started in herdr tab '$label'."
            exit 0
        }
        Write-Host 'Could not create a herdr tab; using a console window.' -ForegroundColor DarkYellow
    }
    if ($script:OnWindows) {
        $argList = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-NoExit', '-File', "`"$PSCommandPath`"") + $forward
        Start-Process -FilePath 'powershell.exe' -ArgumentList $argList -WorkingDirectory $script:RepositoryRoot | Out-Null
        Write-Host 'Gap run started in a new console window.'
        exit 0
    }
    # No console windows off Windows: a detached pwsh writing its output beside the run folders.
    $gapRoot = Join-Path (Split-Path $script:RepositoryRoot -Parent) "$(Split-Path $script:RepositoryRoot -Leaf).gap"
    New-Item -ItemType Directory -Force -Path $gapRoot | Out-Null
    $out = Join-Path $gapRoot ('RunGapAnalysis-' + (Get-Date).ToString('yyyy-MM-dd_HHmm') + '.log')
    $argList = @('-NoProfile', '-File', "`"$PSCommandPath`"") + $forward
    $p = Start-Process -FilePath 'pwsh' -ArgumentList $argList -WorkingDirectory $script:RepositoryRoot -RedirectStandardOutput $out -RedirectStandardError "$out.err" -PassThru
    Write-Host "Gap run started as pid $($p.Id), writing $out."
    exit 0
}

if (-not $CurlVersion) {
    $CurlVersion = (Get-Content -LiteralPath (Join-Path $PSScriptRoot 'Baselines/target.json') -Raw | ConvertFrom-Json).version
}
$stamp = (Get-Date).ToString('yyyy-MM-dd_HHmm')
$repoName = Split-Path $script:RepositoryRoot -Leaf
$runDirectory = Join-Path (Join-Path (Split-Path $script:RepositoryRoot -Parent) "$repoName.gap") $stamp
$tree = Join-Path $runDirectory 'tree'
$measurements = Join-Path $runDirectory 'measurements'
$local = $script:LocalPlatform
$remotePlatforms = @($platformSelection | Where-Object { $_ -ne 'windows' -and $_ -ne $local })
if (-not $DryRun) {
    New-Item -ItemType Directory -Force -Path $measurements | Out-Null
    $script:LogFile = Join-Path $runDirectory 'gap.log'
}
Write-GapLog "Gap run $stamp of $Ref against curl $CurlVersion; areas: $($selection.Areas -join ', '); platforms: $($platformSelection -join ', ')"

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
$needed = @('Measure-GapAreas.ps1', 'Get-UpstreamRelease.ps1', 'GapProbeFunctions.ps1', 'Measure-OptionGap.ps1', 'Measure-VersionGap.ps1', 'Measure-WriteOutGap.ps1', 'Measure-ExitCodeGap.ps1', 'Measure-EnvironmentGap.ps1', 'ConvertTo-BehaviourMeasurement.ps1', 'Measure-ReferenceCrossCheck.ps1', 'Write-GapFindings.ps1', 'Write-GapScorecard.ps1', 'Export-GapDashboardData.ps1', 'New-TasksFromGaps.ps1')
if ($DryRun -or @($needed | Where-Object { -not (Test-Path -LiteralPath (Join-Path $gapTools $_)) }).Count -eq 0) { $script:ToolsDirectory = $gapTools }
else { Write-GapLog "The gap worktree lacks some Gap/Tools; using $script:ToolsDirectory." 'DarkYellow' }
$gapFindings = Join-Path $gapWorktree 'Gap/Findings'
$gapScorecards = Join-Path $gapWorktree 'Gap/Scorecards'
Write-Host '== Step 2: the measured tree'
if ((Invoke-GapStep 'tree' 'git' @('-C', $script:RepositoryRoot, 'worktree', 'add', '--detach', $tree, $Ref)) -ne 0) { Stop-GapRun "could not create the tree at $Ref." }
$commit = if ($DryRun) { '<commit of ' + $Ref + '>' } else { (& git -C $tree rev-parse HEAD).Trim() }

Write-Host "== Step 3: ask gap-platforms.yml to measure $(if ($remotePlatforms.Count) { $remotePlatforms -join ' and ' } else { 'nothing' })"
$platformRun = $null
$dispatchedAt = Get-Date
if ($remotePlatforms.Count -gt 0) { $platformRun = Start-GapPlatformRun $stamp $commit }
else { Write-GapLog 'No remote platform selected; gap-platforms.yml not dispatched.' }

Write-Host "== Step 4: measure $local here"
$states = [ordered]@{}
if ($platformSelection -contains 'windows' -and $local -ne 'windows') {
    $states['windows'] = [pscustomobject]@{ State = 'missing'; Reason = "windows is measured only from a Windows checkout (Gap\RunGapAnalysis.cmd); this run is on $local"; Status = $null }
}
# The release folder, for the local measurement and for every analyst's prompt.
if ((Invoke-GapTool 'release' 'Get-UpstreamRelease.ps1' @('-Version', $CurlVersion)) -ne 0) { Stop-GapRun "could not fetch curl $CurlVersion." }
$release = if ($DryRun) { '<release folder>' } else { @($script:LastOutput | Where-Object { $_ })[-1].Trim() }
if ($platformSelection -contains $local) {
    $statusFile = Join-Path $runDirectory "status-$local.json"
    $measure = @('-Tree', $tree, '-OutDirectory', (Join-Path $measurements $local), '-WorkDirectory', (Join-Path $runDirectory "work-$local"), '-StatusFile', $statusFile, '-Version', $CurlVersion, '-Release', $release, '-Areas', ($selection.Areas -join ','))
    if ($DryRun) { $measure += '-DryRun' }
    $code = Invoke-GapTool "measure-$local" 'Measure-GapAreas.ps1' $measure
    $measureScript = Join-Path $script:ToolsDirectory 'Measure-GapAreas.ps1'
    if ($DryRun -and (Test-Path -LiteralPath $measureScript)) { & $script:Commands.shell -NoProfile -ExecutionPolicy Bypass -File $measureScript @measure }
    $status = $null
    if (-not $DryRun -and (Test-Path -LiteralPath $statusFile)) { try { $status = Get-Content -LiteralPath $statusFile -Raw | ConvertFrom-Json } catch { $status = $null } }
    $measuredHere = -not $DryRun -and (Test-Path -LiteralPath (Join-Path $measurements $local)) -and @(Get-ChildItem -LiteralPath (Join-Path $measurements $local) -Filter '*.json' -File).Count -gt 0
    if ($DryRun) { $states[$local] = [pscustomobject]@{ State = 'measured'; Reason = 'dry run'; Status = $null } }
    elseif ($measuredHere) { $states[$local] = [pscustomobject]@{ State = 'measured'; Reason = $(if ($code -eq 0) { $null } else { "Measure-GapAreas.ps1 exited $code; areas it did not measure are carried or missing" }); Status = $status } }
    else {
        $why = "Measure-GapAreas.ps1 exited $code and measured nothing"
        if ($null -ne $status -and $status.failure) { $why += " ($($status.failure))" }
        $states[$local] = [pscustomobject]@{ State = 'missing'; Reason = $why; Status = $status }
    }
    # A measurement step that changed the tree must not be blamed on the first analyst.
    if (-not $DryRun) {
        $afterMeasure = @(Get-GapTreeChanges $tree)
        if ($afterMeasure.Count -gt 0) {
            Write-GapLog "A measurement step changed the measured tree ($($afterMeasure -join '; ')); tree reset." 'Red'
            Reset-GapTree 'reset-measured' 'clean-measured' $tree
        }
    }
}

Write-Host '== Step 5: wait for gap-platforms.yml and take its measurements'
if ($remotePlatforms.Count -gt 0) {
    $outcome = [pscustomobject]@{ Completed = $false; Conclusion = 'not started'; Jobs = @() }
    if ($DryRun) { Write-Host "[platforms-wait] gh run view <run> until it completes or $PlatformTimeoutMinutes minutes pass, then gh run download -n gap-measurements-<platform>" }
    elseif ($null -ne $platformRun.Id) { $outcome = Wait-GapPlatformRun $platformRun $dispatchedAt.AddMinutes($PlatformTimeoutMinutes) }
    foreach ($platform in $remotePlatforms) {
        if ($DryRun) { $states[$platform] = [pscustomobject]@{ State = 'measured'; Reason = 'dry run'; Status = $null }; continue }
        $states[$platform] = Receive-GapPlatformMeasurements $platform $platformRun $outcome $runDirectory $selection.Areas
    }
}
foreach ($platform in $states.Keys) { Write-GapLog ("Platform {0}: {1}{2}" -f $platform, $states[$platform].State, $(if ($states[$platform].Reason) { " - $($states[$platform].Reason)" } else { '' })) }
Write-GapPlatformsFile (Join-Path $runDirectory 'platforms.json') $states
$references = [ordered]@{}
foreach ($platform in $states.Keys) { if ($null -ne $states[$platform].Status) { $references[$platform] = $states[$platform].Status.reference } }
$run = [ordered]@{
    stamp = $stamp; ref = $Ref; commit = $commit; platforms = $platformSelection
    target = $CurlVersion; areas = $selection.Areas; references = $references
    platformRun = $(if ($null -ne $platformRun) { $platformRun.Url } else { $null })
}
if ($DryRun) { Write-Host "[run.json] $(Join-Path $runDirectory 'run.json')" }
else { [System.IO.File]::WriteAllText((Join-Path $runDirectory 'run.json'), ($run | ConvertTo-Json -Depth 4)) }
$measuredPlatforms = @($states.Keys | Where-Object { $states[$_].State -eq 'measured' })
if ($measuredPlatforms.Count -eq 0) { Stop-GapRun 'no platform was measured.' }

Write-Host '== Step 6: each platform''s own gaps'
$uncovered = @{}
if (@($measuredPlatforms | Where-Object { $_ -ne 'windows' }).Count -gt 0) {
    if ((Invoke-GapTool 'uncovered' 'Write-GapFindings.ps1' @('-RunDirectory', $runDirectory, '-Stamp', $stamp, '-FindingsDirectory', $gapFindings, '-ListUncovered')) -eq 0) { $uncovered = Get-GapUncovered $script:LastOutput }
    else { Write-GapLog 'Write-GapFindings.ps1 -ListUncovered failed; no linux or macos analyst runs.' 'Red' }
}

Write-Host '== Step 7: the analysts'
$reports = Join-Path $runDirectory 'reports'
$analysts = [ordered]@{}
foreach ($platform in $measuredPlatforms) {
    $folder = Join-Path $measurements $platform
    $platformReports = Join-Path $reports $platform
    if (-not $DryRun) { New-Item -ItemType Directory -Force -Path $platformReports | Out-Null }
    foreach ($area in $selection.Areas) {
        $file = Join-Path $folder "$area.json"
        if (-not $DryRun -and -not (Test-Path -LiteralPath $file)) { continue }
        $own = @()
        if ($platform -ne 'windows') {
            if ($uncovered.ContainsKey("$platform|$area")) { $own = @($uncovered["$platform|$area"]) }
            # A linux or macos analyst runs only for gaps that are its platform's own.
            if (-not $DryRun -and $own.Count -eq 0) { continue }
        }
        $prompt = @(
            "Gap analysis run $stamp, area $area, platform $platform.",
            "Run folder: $runDirectory",
            "Measurement file: $file",
            "Measured tree: $tree",
            "Release folder: $release",
            "Run stamp: $stamp"
        )
        if ($platform -ne 'windows') {
            $prompt += @("This measurement ran on $platform against curl $CurlVersion built with OpenSSL. Group only these gap items, which are $platform's own; every other gap is covered by a finding of another platform:") + @($own | ForEach-Object { "- $_" })
        }
        $prompt += @("Existing $platform findings of this area (id, key, status, items), from the gap branch's Gap/Findings:") + @(Get-GapAreaFindings $gapFindings $area $platform) + @('', 'Read only; never change the measured tree. End with your one report block.')
        $analysts["$platform $area"] = Invoke-GapAnalyst $area ($prompt -join "`n") $tree (Join-Path $platformReports "gap-$area.md")
    }
}

Write-Host '== Step 8: findings, scorecard, inventories and dashboard data'
if ((Invoke-GapTool 'findings' 'Write-GapFindings.ps1' @('-RunDirectory', $runDirectory, '-Stamp', $stamp, '-FindingsDirectory', $gapFindings)) -ne 0) { Write-GapLog 'Write-GapFindings.ps1 failed.' 'Red' }
if ((Invoke-GapTool 'scorecard' 'Write-GapScorecard.ps1' @('-RunDirectory', $runDirectory, '-Stamp', $stamp, '-FindingsDirectory', $gapFindings, '-ScorecardsDirectory', $gapScorecards)) -ne 0) { Write-GapLog 'Write-GapScorecard.ps1 failed.' 'Red' }
$inventorySource = Join-Path (Split-Path $script:ToolsDirectory -Parent) "Upstream/$CurlVersion"
$inventoryTarget = Join-Path $gapWorktree "Gap/Upstream/$CurlVersion"
if ($DryRun) { Write-Host "[inventories] copy $(Join-Path $inventorySource '*.json') to $inventoryTarget when they differ" }
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
foreach ($key in $analysts.Keys) { Write-GapLog ("  analyst {0,-20} {1}" -f $key, $analysts[$key]) }
Write-GapLog 'Summary:'
foreach ($platform in $states.Keys) {
    $state = $states[$platform]
    if ($state.State -ne 'measured') { Write-GapLog ("  {0,-8} missing: {1}" -f $platform, $state.Reason); continue }
    foreach ($area in $selection.Areas) {
        $text = 'not measured'
        if ($null -ne $state.Status -and $null -ne $state.Status.areas -and $null -ne $state.Status.areas.PSObject.Properties[$area]) { $text = [string]$state.Status.areas.$area }
        elseif ($DryRun) { $text = 'dry run' }
        Write-GapLog ("  {0,-8} {1,-12} {2}" -f $platform, $area, $text)
    }
}
Write-GapLog "Run folder: $runDirectory$(if ($DryRun) { ' (dry run: not created)' })"
