<#
.SYNOPSIS
    The measurement phase of a gap run on one platform: builds the candidate, fetches the
    upstream release, finds the reference curl, runs each selected area's Measure-* tool and
    writes <OutDirectory>/<area>.json and a status file. RunGapAnalysis.ps1 runs it on
    Windows and .github/workflows/gap-platforms.yml runs it on Linux and macOS, so every
    platform is measured by the same code (ADR-0505).

.DESCRIPTION
    Steps, each printed with its command and exit code:

    1. Unless -Candidate is given: dotnet build <Tree>/Curl.Console -c Release; the
       candidate is <Tree>/Curl.Console/bin/Release/net10.0/curl (curl.exe on Windows).
       A failed build ends the script with exit 2.
    2. Unless -Release is given: Get-UpstreamRelease.ps1 -Version <Version> beside this
       script; its last output line is the release folder. A failure ends with exit 2.
    3. The reference curl: Get-GapReferenceCurl of GapProbeFunctions.ps1, which takes
       CURL_GAP_REFERENCE_CURL when it is set (-ReferencePath sets it for this script and
       every tool it starts), else the platform's lookup. Its version line, or "docs" when
       no reference names <Version>, goes in the status file; the names on its Protocols:
       and Features: lines go to ConvertTo-BehaviourMeasurement.ps1.
    4. Each selected area's tool runs in its own process of the PowerShell running this
       script, from the folder this script is in, with -Candidate the candidate:
         options      Measure-OptionGap.ps1 -OutFile <OutDirectory>/options.json
         protocols,
         features     Measure-VersionGap.ps1 -OutDirectory <OutDirectory> (one run writes both)
         writeout     Measure-WriteOutGap.ps1 -OutFile writeout.json
         exitcodes    Measure-ExitCodeGap.ps1 -RepositoryRoot <Tree> -OutFile exitcodes.json
         environment  Measure-EnvironmentGap.ps1 -RepositoryRoot <Tree> -OutFile environment.json
         behaviour    dotnet run --file <Tree>/Gap/Tools/Measure-UpstreamCases.cs --
                      <release>/tests/data <WorkDirectory>/raw.json (run from <Tree>: its
                      #:project line builds the tree's Curl.Console into the harness), then
                      ConvertTo-BehaviourMeasurement.ps1, then Measure-ReferenceCrossCheck.ps1.
       A tool that fails is reported, its areas are "not measured: <tool> exited <n>", and
       the next tool runs. After a tool succeeds, every item of its areas that
       Gap/Baselines/platform-exclusions.json lists for this platform ({ key, platform,
       reason }, a decision that holds on one platform only, such as ADR-0786's HTTP/2 on
       Windows) is set excluded with that reason, keeping its expected and actual answers,
       and the measurement's counts (and behaviour's reasons) are recounted.
    5. The status file, JSON: platform (windows, linux or macos), commit (git -C <Tree>
       rev-parse HEAD), target, reference, areas (one property per selected area: "X of Y"
       or "not measured: ...") and failure (null, or why the script stopped early).

    Exit code: 0 when every selected area was measured, 1 when any was not, 2 when step 1
    or 2 failed (the status file says which). Runs under Windows PowerShell 5.1 and
    PowerShell 7 on Windows, Linux and macOS; ASCII only.
.PARAMETER Tree
    The Curl tree measured: a checkout of the commit to measure. Required unless -SelfTest.
.PARAMETER OutDirectory
    The folder the area measurements are written to. Created when missing. Required unless
    -SelfTest.
.PARAMETER WorkDirectory
    The folder for the behaviour area's raw.json and behaviour-unchecked.json and the
    harness logs; it must hold no blank. Default: <OutDirectory>-work.
.PARAMETER StatusFile
    The status file. Default: <WorkDirectory>/status.json.
.PARAMETER Version
    The upstream release measured against. Default: Gap/Baselines/target.json's version.
.PARAMETER Areas
    Any of options, protocols, features, writeout, exitcodes, environment, behaviour,
    comma-separated or as an array. Default: all seven.
.PARAMETER Candidate
    The Curl binary to measure; skips step 1.
.PARAMETER Release
    The extracted upstream release folder; skips step 2.
.PARAMETER ReferencePath
    The reference curl; sets CURL_GAP_REFERENCE_CURL for this script and its tools.
.PARAMETER DryRun
    Prints every step's command and runs nothing.
.PARAMETER SelfTest
    Checks the area-to-tool mapping, the "X of Y" status reading, the status file and the
    exit codes against faked tools in a temporary folder. Prints a PASS or FAIL line per
    check and exits 1 on any FAIL.
.EXAMPLE
    pwsh -NoProfile -File Gap/Tools/Measure-GapAreas.ps1 -Tree tree -OutDirectory out/measurements -StatusFile out/status.json
#>
param(
    [string] $Tree,
    [string] $OutDirectory,
    [string] $WorkDirectory,
    [string] $StatusFile,
    [string] $Version,
    [string[]] $Areas = @(),
    [string] $Candidate,
    [string] $Release,
    [string] $ReferencePath,
    [switch] $DryRun,
    [switch] $SelfTest
)

$ErrorActionPreference = 'Stop'
$script:AllAreas = @('options', 'protocols', 'features', 'writeout', 'exitcodes', 'environment', 'behaviour')
$script:ToolsDirectory = $PSScriptRoot
$script:Shell = (Get-Process -Id $PID).Path
# pwsh installed as a .NET tool runs as dotnet hosting pwsh.dll.
$script:ShellPrefix = @()
if ([System.IO.Path]::GetFileNameWithoutExtension($script:Shell) -eq 'dotnet') { $script:ShellPrefix = @(Join-Path $PSHOME 'pwsh.dll') }
$script:OnWindows = [System.IO.Path]::DirectorySeparatorChar -eq '\'

function Get-GapPlatform {
    # windows, linux or macos: the platform this script runs on.
    if ($script:OnWindows) { return 'windows' }
    if (Test-Path -LiteralPath '/System/Library/CoreServices') { return 'macos' }
    return 'linux'
}

function Resolve-GapAreaTools {
    # The selected areas in run order, and the tools that measure them, with protocols and
    # features folded into one 'version' tool run. Throws on an unknown area.
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

function Get-GapToolAreas {
    # The areas one tool's run writes.
    param([string] $Tool)
    if ($Tool -eq 'version') { return @('protocols', 'features') }
    return @($Tool)
}

function Get-MeasurementCount {
    # "X of Y" from a measurement's counts (Gap/Instructions/Gap-Format.md section 4).
    param([string] $Path)
    try {
        $counts = (Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json).counts
        return "$($counts.x) of $($counts.y)"
    } catch { return 'measured (counts unreadable)' }
}

function Get-GapPlatformExclusions {
    # The items platform-exclusions.json excludes on one platform, as key -> reason. No file
    # means no exclusions.
    param([string] $Path, [string] $Platform)
    $table = @{}
    if (-not $Path -or -not (Test-Path -LiteralPath $Path)) { return $table }
    foreach ($entry in @((Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json).items)) {
        if (-not $entry.key -or -not $entry.platform -or -not $entry.reason) { throw "An exclusion in $Path needs a key, a platform and a reason." }
        if ($entry.platform -eq $Platform) { $table[[string] $entry.key] = [string] $entry.reason }
    }
    return $table
}

function Set-GapPlatformExclusions {
    # Sets each listed item of one measurement file excluded with its reason, keeping its
    # answers, and recounts counts (and behaviour's reasons). Rewrites the file only when an
    # item changed; returns the number changed.
    param([string] $Path, [hashtable] $Exclusions)
    if ($Exclusions.Count -eq 0) { return 0 }
    $read = @{}
    # PowerShell 7 would turn measuredAt into a DateTime and write it back reformatted.
    if ((Get-Command ConvertFrom-Json).Parameters.ContainsKey('DateKind')) { $read.DateKind = 'String' }
    $measurement = Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json @read
    $items = @($measurement.items)
    $changed = 0
    foreach ($item in $items) {
        $key = [string] $item.key
        if (-not $Exclusions.ContainsKey($key) -or ($item.state -eq 'excluded' -and $item.reason -eq $Exclusions[$key])) { continue }
        $item.state = 'excluded'
        if (@($item.PSObject.Properties.Name) -contains 'reason') { $item.reason = $Exclusions[$key] } else { $item | Add-Member -NotePropertyName reason -NotePropertyValue $Exclusions[$key] }
        $changed++
    }
    if ($changed -eq 0) { return 0 }
    foreach ($state in 'match', 'gap', 'unmeasured', 'excluded') { $measurement.counts.$state = @($items | Where-Object { $_.state -eq $state }).Count }
    $measurement.counts.x = $measurement.counts.match
    $measurement.counts.y = $measurement.counts.match + $measurement.counts.gap + $measurement.counts.unmeasured
    if (@($measurement.PSObject.Properties.Name) -contains 'reasons') {
        $sorted = New-Object 'System.Collections.Generic.SortedDictionary[string,int]' ([System.StringComparer]::Ordinal)
        foreach ($item in $items) { if ($item.state -eq 'unmeasured' -or $item.state -eq 'excluded') { $sorted[[string] $item.reason] = 1 + $(if ($sorted.ContainsKey([string] $item.reason)) { $sorted[[string] $item.reason] } else { 0 }) } }
        $reasons = [ordered] @{}
        foreach ($pair in $sorted.GetEnumerator()) { $reasons[$pair.Key] = $pair.Value }
        $measurement.reasons = $reasons
    }
    $write = @{ Depth = 32 }
    if ((Get-Command ConvertTo-Json).Parameters.ContainsKey('EscapeHandling')) { $write.EscapeHandling = 'EscapeNonAscii' }
    [System.IO.File]::WriteAllText([System.IO.Path]::GetFullPath($Path), ($measurement | ConvertTo-Json @write), (New-Object System.Text.UTF8Encoding $false))
    return $changed
}

function Format-GapCommand {
    param([string] $Exe, [string[]] $Arguments)
    $quoted = @($Arguments | ForEach-Object { if ($_ -match '\s' -or $_ -eq '') { '"' + $_ + '"' } else { $_ } })
    return (@($Exe) + $quoted) -join ' '
}

function Invoke-GapMeasureStep {
    # Runs one step, echoing its command, output and exit code; returns the exit code (0 in
    # -DryRun, where it only prints the command).
    param([string] $Name, [string] $Exe, [string[]] $Arguments, [string] $WorkingDirectory)
    Write-Host "[$Name] $(Format-GapCommand $Exe $Arguments)"
    $script:LastOutput = @()
    if ($DryRun) { return 0 }
    $previous = Get-Location
    if ($WorkingDirectory) { Set-Location -LiteralPath $WorkingDirectory }
    $ErrorActionPreference = 'Continue'
    $global:LASTEXITCODE = 0
    try {
        & $Exe @Arguments 2>&1 | ForEach-Object { $line = [string]$_; $script:LastOutput += $line; Write-Host $line }
        $code = $global:LASTEXITCODE
    }
    catch { Write-Host ([string]$_); $code = 1 }
    finally { Set-Location $previous }
    if ($null -eq $code) { $code = 0 }
    Write-Host "[$Name] exit $code"
    return $code
}

function Invoke-GapMeasureTool {
    # Runs a Gap/Tools PowerShell script beside this one in its own process. -Command, not
    # -File, so an argument written as @('a','b') reaches a [string[]] parameter as an array.
    param([string] $Name, [string] $Tool, [string[]] $Arguments)
    $quoted = @($Arguments | ForEach-Object { if ($_ -match '^-[A-Za-z]' -or $_ -like '@(*') { $_ } else { "'" + $_.Replace("'", "''") + "'" } })
    $command = "& '" + (Join-Path $script:ToolsDirectory $Tool).Replace("'", "''") + "' " + ($quoted -join ' ') + '; exit $LASTEXITCODE'
    return Invoke-GapMeasureStep $Name $script:Shell ($script:ShellPrefix + @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-Command', $command))
}

function Write-GapMeasureStatus {
    param([string] $Path, [string] $Platform, [string] $Commit, [string] $Target, [string] $Reference, [System.Collections.IDictionary] $AreaStatus, [string] $Failure)
    $status = [ordered]@{ platform = $Platform; commit = $Commit; target = $Target; reference = $Reference; areas = $AreaStatus; failure = $(if ($Failure) { $Failure } else { $null }) }
    if ($DryRun) { Write-Host "[status] $Path"; return }
    $folder = Split-Path -Parent $Path
    if ($folder -and -not (Test-Path -LiteralPath $folder)) { New-Item -ItemType Directory -Force -Path $folder | Out-Null }
    [System.IO.File]::WriteAllText([System.IO.Path]::GetFullPath($Path), ($status | ConvertTo-Json -Depth 4), (New-Object System.Text.UTF8Encoding $false))
}

function Invoke-GapMeasurement {
    # Steps 1 to 5. Returns the exit code.
    $selection = Resolve-GapAreaTools $Areas
    $platform = Get-GapPlatform
    $areaStatus = [ordered]@{}
    foreach ($area in $selection.Areas) { $areaStatus[$area] = 'not measured' }
    if (-not $Version) { $Version = (Get-Content -LiteralPath (Join-Path (Split-Path $script:ToolsDirectory -Parent) 'Baselines/target.json') -Raw | ConvertFrom-Json).version }
    $commit = '<commit of the tree>'
    if (-not $DryRun) {
        # Windows PowerShell 5.1 with Stop throws on git's stderr outside a repository.
        $ErrorActionPreference = 'Continue'
        $commit = "$(& git -C $Tree rev-parse HEAD 2>$null)".Trim()
        $ErrorActionPreference = 'Stop'
    }
    if ($ReferencePath) { $env:CURL_GAP_REFERENCE_CURL = $ReferencePath }
    if (-not $DryRun) { New-Item -ItemType Directory -Force -Path $OutDirectory, $WorkDirectory | Out-Null }
    Write-Host "Measuring $Tree ($commit) on $platform against curl $Version; areas: $($selection.Areas -join ', ')"

    $candidatePath = $Candidate
    if (-not $candidatePath) {
        $exeName = if ($script:OnWindows) { 'curl.exe' } else { 'curl' }
        $candidatePath = Join-Path $Tree "Curl.Console/bin/Release/net10.0/$exeName"
        if ((Invoke-GapMeasureStep 'build' 'dotnet' @('build', (Join-Path $Tree 'Curl.Console'), '-c', 'Release')) -ne 0) {
            Write-GapMeasureStatus $StatusFile $platform $commit $Version 'docs' $areaStatus 'the Release build of Curl.Console failed.'
            return 2
        }
    }
    $releaseFolder = $Release
    if (-not $releaseFolder) {
        if ((Invoke-GapMeasureTool 'release' 'Get-UpstreamRelease.ps1' @('-Version', $Version)) -ne 0) {
            Write-GapMeasureStatus $StatusFile $platform $commit $Version 'docs' $areaStatus "could not fetch curl $Version."
            return 2
        }
        $releaseFolder = if ($DryRun) { '<release folder>' } else { @($script:LastOutput | Where-Object { $_.Trim() })[-1].Trim() }
    }

    $referenceLine = 'docs'
    $referenceNames = @()
    if (-not $DryRun) {
        . (Join-Path $script:ToolsDirectory 'GapProbeFunctions.ps1')
        Set-StrictMode -Off
        $ErrorActionPreference = 'Stop'
        $reference = Get-GapReferenceCurl -TargetVersion $Version
        if ($null -ne $reference -and $reference.Matches) {
            $referenceLine = $reference.VersionLine
            # Invoke-GapRun reads stdout alone: under Windows PowerShell 5.1 with Stop, a
            # debug-enabled reference's warning on stderr would throw (BL-2128).
            $versionRun = Invoke-GapRun -Path $reference.Path -Arguments @('-V') -Environment @{} -TimeoutSeconds 20
            foreach ($line in @([System.Text.Encoding]::UTF8.GetString($versionRun.Stdout) -split "`r?`n")) {
                if ($line -match '^(Protocols|Features): (.*)$') { $referenceNames += @($Matches[2].Trim() -split '\s+') }
            }
        }
    }
    Write-Host "Reference: $referenceLine"

    $platformExclusions = Get-GapPlatformExclusions (Join-Path (Split-Path $script:ToolsDirectory -Parent) 'Baselines/platform-exclusions.json') $platform
    $common = @('-Version', $Version, '-UpstreamRoot', $releaseFolder)
    foreach ($tool in $selection.Tools) {
        $out = { param([string] $Name) Join-Path $OutDirectory "$Name.json" }
        switch ($tool) {
            'options' { $code = Invoke-GapMeasureTool 'options' 'Measure-OptionGap.ps1' ($common + @('-Candidate', $candidatePath, '-OutFile', (& $out 'options'))) }
            'version' { $code = Invoke-GapMeasureTool 'version' 'Measure-VersionGap.ps1' ($common + @('-RepositoryRoot', $Tree, '-Candidate', $candidatePath, '-OutDirectory', $OutDirectory)) }
            'writeout' { $code = Invoke-GapMeasureTool 'writeout' 'Measure-WriteOutGap.ps1' ($common + @('-Candidate', $candidatePath, '-OutFile', (& $out 'writeout'))) }
            'exitcodes' { $code = Invoke-GapMeasureTool 'exitcodes' 'Measure-ExitCodeGap.ps1' ($common + @('-RepositoryRoot', $Tree, '-OutFile', (& $out 'exitcodes'))) }
            'environment' { $code = Invoke-GapMeasureTool 'environment' 'Measure-EnvironmentGap.ps1' ($common + @('-RepositoryRoot', $Tree, '-Candidate', $candidatePath, '-OutFile', (& $out 'environment'))) }
            'behaviour' {
                $testsData = Join-Path $releaseFolder 'tests/data'
                $raw = Join-Path $WorkDirectory 'raw.json'
                $converted = Join-Path $WorkDirectory 'behaviour-unchecked.json'
                $code = Invoke-GapMeasureStep 'behaviour-run' 'dotnet' @('run', '--file', (Join-Path $Tree 'Gap/Tools/Measure-UpstreamCases.cs'), '--', $testsData, $raw) $Tree
                if ($code -eq 0) {
                    $convert = @('-RawFile', $raw, '-TestsData', $testsData, '-Version', $Version, '-OutFile', $converted)
                    if ($referenceNames.Count -gt 0) { $convert += @('-Reference', $referenceLine, '-ReferenceFeatures', ('@(' + (@($referenceNames | ForEach-Object { "'" + $_.Replace("'", "''") + "'" }) -join ',') + ')')) }
                    $code = Invoke-GapMeasureTool 'behaviour-convert' 'ConvertTo-BehaviourMeasurement.ps1' $convert
                }
                if ($code -eq 0) { $code = Invoke-GapMeasureTool 'behaviour-crosscheck' 'Measure-ReferenceCrossCheck.ps1' @('-Measurement', $converted, '-TestsData', $testsData, '-Candidate', $candidatePath, '-OutFile', (& $out 'behaviour')) }
            }
        }
        foreach ($area in (Get-GapToolAreas $tool)) {
            if (-not $areaStatus.Contains($area)) { continue }
            $path = & $out $area
            if ($DryRun) { $areaStatus[$area] = 'dry run' }
            elseif ($code -eq 0 -and (Test-Path -LiteralPath $path)) {
                $excluded = Set-GapPlatformExclusions $path $platformExclusions
                if ($excluded -gt 0) { Write-Host "[exclusions] $area`: $excluded item(s) excluded by platform-exclusions.json on $platform" }
                $areaStatus[$area] = Get-MeasurementCount $path
            }
            else { $areaStatus[$area] = "not measured: $tool exited $code"; Write-Host "Area $area not measured: $tool exited $code." }
        }
    }
    Write-GapMeasureStatus $StatusFile $platform $commit $Version $referenceLine $areaStatus $null
    foreach ($area in $areaStatus.Keys) { Write-Host ("  {0,-12} {1}" -f $area, $areaStatus[$area]) }
    if (@($areaStatus.Values | Where-Object { $_ -like 'not measured*' }).Count -gt 0) { return 1 }
    return 0
}

function Invoke-GapMeasureSelfTest {
    $results = New-Object System.Collections.Generic.List[string]
    $report = { param([string] $Name, [bool] $Passed) $results.Add($(if ($Passed) { "PASS $Name" } else { "FAIL $Name" })) }
    $all = Resolve-GapAreaTools @()
    & $report 'no -Areas runs all six tools in order' (($all.Tools -join ',') -eq 'options,version,writeout,exitcodes,environment,behaviour')
    & $report '-Areas protocols,features runs the version tool once' (((Resolve-GapAreaTools @('protocols,features')).Tools -join ',') -eq 'version')
    & $report 'the version tool writes protocols and features' (((Get-GapToolAreas 'version') -join ',') -eq 'protocols,features')
    $threw = $false; try { Resolve-GapAreaTools @('nope') | Out-Null } catch { $threw = $true }
    & $report 'an unknown area is refused' $threw
    & $report 'the platform is windows, linux or macos' (@('windows', 'linux', 'macos') -contains (Get-GapPlatform))

    $scratch = Join-Path ([System.IO.Path]::GetTempPath()) ('gap-measure-selftest-' + [guid]::NewGuid().ToString('N'))
    $tools = Join-Path $scratch 'Tools'
    New-Item -ItemType Directory -Force -Path $tools | Out-Null
    $savedTools = $script:ToolsDirectory
    $savedReference = $env:CURL_GAP_REFERENCE_CURL
    try {
        $measurement = '{ "area": "AREA", "counts": { "match": 3, "gap": 1, "unmeasured": 1, "excluded": 2, "x": 3, "y": 5 } }'
        $counted = Join-Path $scratch 'counted.json'
        [System.IO.File]::WriteAllText($counted, $measurement.Replace('AREA', 'options'))
        & $report 'a measurement reads as "X of Y"' ((Get-MeasurementCount $counted) -eq '3 of 5')
        & $report 'an unreadable measurement says so' ((Get-MeasurementCount (Join-Path $scratch 'none.json')) -eq 'measured (counts unreadable)')

        $shipped = Join-Path (Split-Path $savedTools -Parent) 'Baselines/platform-exclusions.json'
        $onWindows = Get-GapPlatformExclusions $shipped 'windows'
        $http2Keys = 'behaviour:test1704,options:--http2,options:--http2-prior-knowledge,options:--proxy-http2,options:--proxy-http2:no-form'
        & $report 'the shipped platform exclusions are the five ADR-0786 HTTP/2 items, on Windows only' ((($onWindows.Keys | Sort-Object) -join ',') -eq $http2Keys -and @($onWindows.Values | Where-Object { $_ -ne 'decided:ADR-0786' }).Count -eq 0 -and (Get-GapPlatformExclusions $shipped 'linux').Count -eq 0 -and (Get-GapPlatformExclusions $shipped 'macos').Count -eq 0)
        & $report 'no platform exclusions file means no exclusions' ((Get-GapPlatformExclusions (Join-Path $scratch 'none.json') 'windows').Count -eq 0)
        $exclusionsFile = Join-Path $scratch 'platform-exclusions.json'
        [System.IO.File]::WriteAllText($exclusionsFile, '{ "items": [ { "key": "behaviour:test2", "platform": "windows", "reason": "decided:ADR-9999" }, { "key": "behaviour:test1", "platform": "linux", "reason": "decided:ADR-9998" } ] }')
        $table = Get-GapPlatformExclusions $exclusionsFile 'windows'
        & $report 'an exclusion applies only on its own platform' ($table.Count -eq 1 -and $table['behaviour:test2'] -eq 'decided:ADR-9999')
        [System.IO.File]::WriteAllText($exclusionsFile, '{ "items": [ { "key": "behaviour:test2", "reason": "decided:ADR-9999" } ] }')
        $threw = $false; try { Get-GapPlatformExclusions $exclusionsFile 'windows' | Out-Null } catch { $threw = $true }
        & $report 'an exclusion without a platform is refused' $threw
        $behaviour = Join-Path $scratch 'behaviour.json'
        [System.IO.File]::WriteAllText($behaviour, '{ "area": "behaviour", "measuredAt": "2026-10-23T16:37:00Z", "items": [ { "key": "behaviour:test1", "state": "match", "reason": null, "expected": "e1", "actual": "a1" }, { "key": "behaviour:test2", "state": "gap", "reason": null, "expected": "exit 2", "actual": "exit 8" }, { "key": "behaviour:test3", "state": "unmeasured", "reason": "needs-server:smtp", "expected": null, "actual": null } ], "counts": { "match": 1, "gap": 1, "unmeasured": 1, "excluded": 0, "x": 1, "y": 3 }, "reasons": { "needs-server:smtp": 1 } }')
        $changed = Set-GapPlatformExclusions $behaviour $table
        $after = Get-Content -LiteralPath $behaviour -Raw | ConvertFrom-Json
        $test2 = @($after.items | Where-Object { $_.key -eq 'behaviour:test2' })[0]
        & $report 'a listed item becomes excluded with its reason and keeps its answers' ($changed -eq 1 -and $test2.state -eq 'excluded' -and $test2.reason -eq 'decided:ADR-9999' -and $test2.expected -eq 'exit 2' -and $test2.actual -eq 'exit 8')
        & $report 'counts and reasons are recounted after an exclusion' ($after.counts.gap -eq 0 -and $after.counts.excluded -eq 1 -and $after.counts.x -eq 1 -and $after.counts.y -eq 2 -and $after.reasons.'decided:ADR-9999' -eq 1 -and $after.reasons.'needs-server:smtp' -eq 1 -and (Get-Content -LiteralPath $behaviour -Raw) -match '"measuredAt":\s*"2026-10-23T16:37:00Z"')
        $before = [System.IO.File]::ReadAllBytes($behaviour)
        & $report 'a measurement already excluded is left unwritten' ((Set-GapPlatformExclusions $behaviour $table) -eq 0 -and [Convert]::ToBase64String($before) -eq [Convert]::ToBase64String([System.IO.File]::ReadAllBytes($behaviour)))

        # Faked tools: options writes its file, writeout fails, the probe functions find no reference.
        $writer = 'param([string] $Version, [string] $UpstreamRoot, [string] $Candidate, [string] $OutFile) [System.IO.File]::WriteAllText($OutFile, ''' + $measurement.Replace('AREA', 'options').Replace("'", "''") + '''); exit 0'
        [System.IO.File]::WriteAllText((Join-Path $tools 'Measure-OptionGap.ps1'), $writer)
        [System.IO.File]::WriteAllText((Join-Path $tools 'Measure-WriteOutGap.ps1'), 'exit 3')
        [System.IO.File]::WriteAllText((Join-Path $tools 'GapProbeFunctions.ps1'), 'function Get-GapReferenceCurl { param([string] $TargetVersion) return $null }')
        $script:ToolsDirectory = $tools
        $out = Join-Path $scratch 'measurements'
        $status = Join-Path $scratch 'status.json'
        $code = & {
            $Tree = $scratch; $OutDirectory = $out; $WorkDirectory = (Join-Path $scratch 'work'); $StatusFile = $status
            $Version = '9.9.9'; $Areas = @('options', 'writeout'); $Candidate = (Join-Path $scratch 'curl'); $Release = $scratch; $ReferencePath = ''
            Invoke-GapMeasurement
        } 6>$null
        $read = Get-Content -LiteralPath $status -Raw | ConvertFrom-Json
        & $report 'a failed tool exits 1 and its area is not measured, naming the tool and code' ($code -eq 1 -and $read.areas.writeout -eq 'not measured: writeout exited 3')
        & $report 'a measured area reads as "X of Y" in the status file' ($read.areas.options -eq '3 of 5' -and (Test-Path -LiteralPath (Join-Path $out 'options.json')))
        & $report 'the status file names platform, target and the docs fallback, with no failure' ($read.platform -eq (Get-GapPlatform) -and $read.target -eq '9.9.9' -and $read.reference -eq 'docs' -and $null -eq $read.failure)
        $code = & {
            $Tree = $scratch; $OutDirectory = $out; $WorkDirectory = (Join-Path $scratch 'work'); $StatusFile = $status
            $Version = '9.9.9'; $Areas = @('options'); $Candidate = (Join-Path $scratch 'curl'); $Release = $scratch; $ReferencePath = 'x-reference'
            Invoke-GapMeasurement
        } 6>$null
        & $report 'every area measured exits 0, and -ReferencePath sets CURL_GAP_REFERENCE_CURL' ($code -eq 0 -and $env:CURL_GAP_REFERENCE_CURL -eq 'x-reference')
        $env:CURL_GAP_REFERENCE_CURL = $savedReference
        [System.IO.File]::WriteAllText((Join-Path $tools 'Get-UpstreamRelease.ps1'), 'exit 1')
        $code = & {
            $Tree = $scratch; $OutDirectory = $out; $WorkDirectory = (Join-Path $scratch 'work'); $StatusFile = $status
            $Version = '9.9.9'; $Areas = @('options'); $Candidate = (Join-Path $scratch 'curl'); $Release = ''; $ReferencePath = ''
            Invoke-GapMeasurement
        } 6>$null
        $read = Get-Content -LiteralPath $status -Raw | ConvertFrom-Json
        & $report 'a release that cannot be fetched exits 2 and the status file says why' ($code -eq 2 -and $read.failure -eq 'could not fetch curl 9.9.9.' -and $read.areas.options -eq 'not measured')
    } finally {
        $script:ToolsDirectory = $savedTools
        $env:CURL_GAP_REFERENCE_CURL = $savedReference
        Remove-Item -LiteralPath $scratch -Recurse -Force -ErrorAction SilentlyContinue
    }
    return $results
}

if ($SelfTest) {
    $lines = @(Invoke-GapMeasureSelfTest)
    $lines | ForEach-Object { Write-Output $_ }
    if (@($lines | Where-Object { $_ -like 'FAIL *' }).Count -gt 0) { exit 1 }
    exit 0
}
if (-not $Tree -or -not $OutDirectory) { throw 'Pass -Tree and -OutDirectory, or -SelfTest.' }
$Tree = [System.IO.Path]::GetFullPath($Tree)
$OutDirectory = [System.IO.Path]::GetFullPath($OutDirectory)
if (-not $WorkDirectory) { $WorkDirectory = $OutDirectory.TrimEnd('\', '/') + '-work' }
$WorkDirectory = [System.IO.Path]::GetFullPath($WorkDirectory)
if (-not $StatusFile) { $StatusFile = Join-Path $WorkDirectory 'status.json' }
$StatusFile = [System.IO.Path]::GetFullPath($StatusFile)
exit (Invoke-GapMeasurement)
