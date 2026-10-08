<#
.SYNOPSIS
    Measures Curl against an upstream curl release, area by area, in a detached tree of the
    commit to measure (ADR-0433 decision 3). This is the measurement half of a gap run.
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
    7. A summary: each area's measured X of Y, and the run folder. The tree worktree is left
       in place for the analysts' steps (BL-1741), which remove it.
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
.PARAMETER SelfTest
    Checks the refusals on a faked process list and the area-to-tool mapping, printing a
    PASS or FAIL line per check, and exits 1 on any FAIL.
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
    [switch] $SelfTest
)

$ErrorActionPreference = 'Stop'
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
    # where it only prints the command). The output is kept in $script:LastOutput.
    param([string] $Name, [string] $Exe, [string[]] $Arguments, [string] $WorkingDirectory)
    $command = Format-GapCommand $Exe $Arguments
    if ($DryRun) { Write-Host "[$Name] $command"; $script:LastOutput = @(); return 0 }
    Write-GapLog "[$Name] $command" 'Cyan'
    $previous = Get-Location
    if ($WorkingDirectory) { Set-Location -LiteralPath $WorkingDirectory }
    $ErrorActionPreference = 'Continue'
    try { $script:LastOutput = @(& $Exe @Arguments 2>&1 | ForEach-Object { [string]$_ }) ; $code = $LASTEXITCODE }
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
    return Invoke-GapStep $Name $script:Shell @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-Command', $command)
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
    return $results
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

if ((Invoke-GapStep 'fetch' 'git' @('-C', $script:RepositoryRoot, 'fetch', 'origin')) -ne 0) { Stop-GapRun 'git fetch origin failed.' }
# Step 2: the tree.
if ((Invoke-GapStep 'tree' 'git' @('-C', $script:RepositoryRoot, 'worktree', 'add', '--detach', $tree, $Ref)) -ne 0) { Stop-GapRun "could not create the tree at $Ref." }
$commit = if ($DryRun) { '<commit of ' + $Ref + '>' } else { (& git -C $tree rev-parse HEAD).Trim() }
# Step 3: the candidate.
if ((Invoke-GapStep 'build' 'dotnet' @('build', (Join-Path $tree 'Curl.Console'), '-c', 'Release')) -ne 0) { Stop-GapRun 'the Release build of Curl.Console failed.' }
# Step 4: the release.
if ((Invoke-GapTool 'release' 'Get-UpstreamRelease.ps1' @('-Version', $CurlVersion)) -ne 0) { Stop-GapRun "could not fetch curl $CurlVersion." }
$release = if ($DryRun) { '<release folder>' } else { @($script:LastOutput | Where-Object { $_ })[-1].Trim() }
# Step 5: the reference and run.json.
$reference = $null
if ($DryRun) { Write-Host "[reference] . $(Join-Path $script:ToolsDirectory 'Invoke-GapProbe.ps1'); Get-GapReferenceCurl -TargetVersion $CurlVersion" }
else {
    . (Join-Path $script:ToolsDirectory 'Invoke-GapProbe.ps1')
    $ErrorActionPreference = 'Stop'
    $reference = Get-GapReferenceCurl -TargetVersion $CurlVersion
}
$referenceLine = if ($null -ne $reference -and $reference.Matches) { $reference.VersionLine } else { 'docs' }
$referenceNames = @()
if ($referenceLine -ne 'docs') {
    $versionText = @(& $reference.Path -V 2>$null)
    foreach ($line in $versionText) { if ($line -match '^(Protocols|Features): (.*)$') { $referenceNames += @($Matches[2].Trim() -split '\s+') } }
}
$run = [ordered]@{
    stamp = $stamp; ref = $Ref; commit = $commit; platform = $(if ($onWindows) { 'Windows' } else { 'Unix' })
    target = $CurlVersion; areas = $selection.Areas; reference = $referenceLine
}
if ($DryRun) { Write-Host "[run.json] $(Join-Path $runDirectory 'run.json')" }
else { [System.IO.File]::WriteAllText((Join-Path $runDirectory 'run.json'), ($run | ConvertTo-Json -Depth 4)); Write-GapLog "Reference: $referenceLine" }

# Step 6: the areas.
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

# Step 7: the summary.
Write-GapLog 'Summary:'
foreach ($area in $status.Keys) { Write-GapLog ("  {0,-12} {1}" -f $area, $status[$area]) }
Write-GapLog "Run folder: $runDirectory$(if ($DryRun) { ' (dry run: not created)' })"
