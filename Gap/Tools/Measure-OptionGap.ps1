<#
.SYNOPSIS
    Builds the options upstream inventory from a curl release's docs/cmdline-opts and
    measures every option's facets on Curl.Console against the matched reference curl.
.DESCRIPTION
    ADR-0433 decision 2, area options. Formats are in Gap/Instructions/Gap-Format.md.

    Inventory: one item per docs/cmdline-opts/*.md file that has a Long: field, skipping
    the man page's sections (_*.md) and MANPAGE.md, which documents the front-matter
    format. Fields, as MANPAGE.md describes them: Long (name, no dashes), Short (one
    letter, no dash), Arg (the argument placeholder), Protocols (space separated),
    Added (introducedIn), Multi (boolean marks a boolean switch). An option has a --no-
    form when its Multi is boolean and its long name does not itself start with no-
    (curl's tool_getparam accepts --no-<name> for every boolean option).
    The inventory is written to Gap/Upstream/<version>/options.json.

    Facets, one item key each:
      options:--<long>           the option is recognised: --<long> [placeholder], no URL;
      options:--<long>:alias     -<short> [placeholder] answers the same; only with Short:;
      options:--<long>:argument  --<long> given last with nothing after it answers the
                                 same; only with Arg:;
      options:--<long>:no-form   --no-<long> answers the same; only when it has a --no- form.
    The placeholder is chosen from the Arg: text: 1 for a number (<seconds>, <num>,
    <speed>, <offset>, <priority>, <time>, <ms>, <bytes>, <size>), an existing temporary
    file for <file>, <filename> or <path>, an existing temporary folder for <dir>, x:x for
    <user:password> (a bare user name makes curl prompt for the password), and x
    otherwise.

    Probing: each command line runs through both binaries with Invoke-GapProbe. A facet is
    match when the exit code and the stderr text (line endings normalised, trailing white
    space trimmed) are equal, and gap otherwise, with both answers in expected and actual.
    With no matching reference the facet falls back to the documents: match when Curl's
    stderr does not say the option is unknown, gap when it does, and the measurement
    records referenceFallback "docs". The unknown-option text is the reference's own,
    measured on curl 8.21.0 and pinned in Gap/Tools/Fixtures/options/reference-unknown-option.txt.

    ASCII only; runs under Windows PowerShell 5.1 and PowerShell 7. -InventoryOnly uses
    no Windows-only API and runs under pwsh on Linux with neither curl present.
.PARAMETER UpstreamRoot
    The release folder holding docs/cmdline-opts. Default: the folder
    Gap/Tools/Get-UpstreamRelease.ps1 prints for -Version.
.PARAMETER Version
    The release version. Default: the target in Gap/Baselines/target.json.
.PARAMETER Candidate
    The Curl.Console binary to measure. Default: Get-GapCandidateCurl.
.PARAMETER OutFile
    Where to write the measurement. Default: standard output.
.PARAMETER InventoryOnly
    Write only the inventory to Gap/Upstream/<version>/options.json and probe nothing.
.PARAMETER ProbeResults
    A JSON file of canned answers used instead of running any binary: { "reference":
    <version line or null>, "answers": { "<command line>": { "reference": { "exitCode",
    "stderr" }, "candidate": { ... } } } }, keyed by the arguments joined with spaces.
.PARAMETER InventoryDirectory
    The folder the inventory is written under, as <InventoryDirectory>/<version>/options.json.
    Default: Gap/Upstream.
.PARAMETER SelfTest
    Runs the inventory and measurement against Gap/Tools/Fixtures/options with canned
    probe results, prints a PASS or FAIL line per check and exits 1 on any FAIL.
.EXAMPLE
    Gap/Tools/Measure-OptionGap.ps1 -InventoryOnly
.EXAMPLE
    Gap/Tools/Measure-OptionGap.ps1 -OutFile $env:TEMP\options.json
#>
[CmdletBinding()]
param(
    [string] $UpstreamRoot,
    [string] $Version,
    [string] $Candidate,
    [string] $OutFile,
    [switch] $InventoryOnly,
    [string] $ProbeResults,
    [string] $InventoryDirectory,
    [switch] $SelfTest
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$script:OptionToolsDirectory = $PSScriptRoot
$script:OptionRepositoryRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$script:RunSelfTest = $SelfTest.IsPresent

function Write-OptionJsonFile([string] $Path, $Value) {
    $directory = Split-Path $Path -Parent
    if ($directory -and -not (Test-Path -LiteralPath $directory)) { New-Item -ItemType Directory -Path $directory -Force | Out-Null }
    $json = ($Value | ConvertTo-Json -Depth 8) + "`n"
    [System.IO.File]::WriteAllText($Path, $json, (New-Object System.Text.UTF8Encoding($false)))
}

function Read-OptionFrontMatter([string] $Path) {
    $fields = @{}
    $lines = [System.IO.File]::ReadAllLines($Path)
    if ($lines.Count -eq 0 -or $lines[0].Trim() -ne '---') { return $fields }
    for ($i = 1; $i -lt $lines.Count; $i++) {
        if ($lines[$i].Trim() -eq '---') { break }
        if ($lines[$i] -match '^([A-Za-z][A-Za-z-]*):\s*(.*)$') { $fields[$Matches[1]] = $Matches[2].Trim() }
    }
    return $fields
}

function Get-OptionInventory([string] $Root, [string] $ReleaseVersion) {
    $folder = Join-Path (Join-Path $Root 'docs') 'cmdline-opts'
    $sorted = New-Object 'System.Collections.Generic.SortedDictionary[string,object]' ([System.StringComparer]::Ordinal)
    foreach ($file in Get-ChildItem -LiteralPath $folder -File -Filter '*.md') {
        if ($file.Name.StartsWith('_') -or $file.Name -ceq 'MANPAGE.md') { continue }
        $fields = Read-OptionFrontMatter $file.FullName
        if (-not $fields.ContainsKey('Long')) { continue }
        $long = $fields['Long']
        $isBoolean = $fields.ContainsKey('Multi') -and $fields['Multi'] -eq 'boolean'
        $short = $null; if ($fields.ContainsKey('Short') -and $fields['Short']) { $short = '-' + $fields['Short'] }
        $arg = $null; if ($fields.ContainsKey('Arg') -and $fields['Arg']) { $arg = $fields['Arg'] }
        $added = $null; if ($fields.ContainsKey('Added') -and $fields['Added']) { $added = $fields['Added'] }
        [string[]] $protocols = @()
        if ($fields.ContainsKey('Protocols') -and $fields['Protocols']) { $protocols = @($fields['Protocols'] -split '\s+' | Where-Object { $_ }) }
        $sorted["options:--$long"] = [pscustomobject] [ordered] @{
            key = "options:--$long"; name = "--$long"; introducedIn = $added
            attributes = [pscustomobject] [ordered] @{ short = $short; arg = $arg; protocols = $protocols; boolean = $isBoolean; noForm = ($isBoolean -and -not $long.StartsWith('no-')) }
        }
    }
    return [pscustomobject] [ordered] @{ area = 'options'; version = $ReleaseVersion; sources = @('docs/cmdline-opts/*.md'); items = @($sorted.Values) }
}

function Get-OptionPlaceholder([string] $Arg) {
    if ($Arg -match '<(seconds|num|number|speed|offset|priority|time|ms|bytes|size)>') { return 'number' }
    if ($Arg -match '<(file|filename|path)>') { return 'file' }
    if ($Arg -match '<dir>') { return 'dir' }
    if ($Arg -match ':password>') { return 'credentials' }
    return 'text'
}

function Get-OptionFacetProbes($Item) {
    $long = $Item.name
    $a = $Item.attributes
    $value = @()
    if ($null -ne $a.arg) {
        $kind = Get-OptionPlaceholder $a.arg
        $value = @(switch ($kind) { 'number' { '1' } 'file' { '<tempfile>' } 'dir' { '<tempdir>' } 'credentials' { 'x:x' } default { 'x' } })
    }
    $probes = @([pscustomobject] @{ key = $Item.key; arguments = @(@($long) + $value); typed = $long })
    if ($null -ne $a.short) { $probes += [pscustomobject] @{ key = "$($Item.key):alias"; arguments = @(@($a.short) + $value); typed = $a.short } }
    if ($null -ne $a.arg) { $probes += [pscustomobject] @{ key = "$($Item.key):argument"; arguments = @($long); typed = $long } }
    if ($a.noForm) { $noForm = '--no-' + $long.Substring(2); $probes += [pscustomobject] @{ key = "$($Item.key):no-form"; arguments = @($noForm); typed = $noForm } }
    return $probes
}

function Format-OptionAnswer($Answer) {
    if ($null -eq $Answer) { return $null }
    $stderr = ([string] $Answer.stderr -replace "`r`n", "`n").TrimEnd()
    $code = if ($null -eq $Answer.exitCode) { 'timeout' } else { [string] $Answer.exitCode }
    if ($stderr) { return "exit ${code}: " + ($stderr -replace "`n", ' | ') }
    return "exit $code"
}

function Get-OptionUnknownLine([string] $Typed) {
    $pinned = [System.IO.File]::ReadAllLines((Join-Path $script:OptionToolsDirectory 'Fixtures/options/reference-unknown-option.txt'))[0]
    return $pinned.Replace('--zzz-nonexistent', $Typed)
}

function Measure-OptionFacet($Probe, $Answer, [bool] $HasReference, [string] $IntroducedIn) {
    $display = ($Probe.arguments | ForEach-Object { $_ }) -join ' '
    $actual = Format-OptionAnswer $Answer.candidate
    if ($HasReference) {
        $expected = Format-OptionAnswer $Answer.reference
        $state = if ($expected -ceq $actual) { 'match' } else { 'gap' }
    }
    else {
        $unknown = Get-OptionUnknownLine $Probe.typed
        $expected = "not: $unknown"
        $said = ([string] $Answer.candidate.stderr -split "`r?`n") -contains $unknown
        $state = if ($said) { 'gap' } else { 'match' }
    }
    return [pscustomobject] [ordered] @{ key = $Probe.key; state = $state; reason = $null; expected = $expected; actual = $actual; evidence = "curl $display"; introducedIn = $IntroducedIn }
}

function Get-OptionCounts($Items) {
    $c = [ordered] @{ match = 0; gap = 0; unmeasured = 0; excluded = 0; x = 0; y = 0 }
    foreach ($item in $Items) { $c[$item.state]++ }
    $c.x = $c.match
    $c.y = $c.match + $c.gap + $c.unmeasured
    return [pscustomobject] $c
}

function Measure-OptionGap($Inventory, [string] $TargetVersion, [string] $CandidatePath, [string] $ProbeResultsPath, [string] $CandidateCommit) {
    $canned = $null
    $referenceLine = $null
    if ($ProbeResultsPath) {
        $canned = Get-Content -LiteralPath $ProbeResultsPath -Raw | ConvertFrom-Json
        $referenceLine = $canned.reference
    }
    else {
        . (Join-Path $script:OptionToolsDirectory 'Invoke-GapProbe.ps1')
        $reference = Get-GapReferenceCurl -TargetVersion $TargetVersion
        if ($null -ne $reference -and $reference.Matches) { $referenceLine = $reference.VersionLine.TrimEnd() }
        $CandidatePath = Get-GapCandidateCurl -Path $CandidatePath
        $scratch = Join-Path ([System.IO.Path]::GetTempPath()) ('gap-options-' + [guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path (Join-Path $scratch 'dir') -Force | Out-Null
        $tempFile = Join-Path $scratch 'placeholder.txt'
        [System.IO.File]::WriteAllText($tempFile, "x`n")
    }
    $hasReference = $null -ne $referenceLine
    $items = New-Object 'System.Collections.Generic.SortedDictionary[string,object]' ([System.StringComparer]::Ordinal)
    try {
        foreach ($item in $Inventory.items) {
            foreach ($probe in Get-OptionFacetProbes $item) {
                $command = $probe.arguments -join ' '
                if ($null -ne $canned) {
                    $answer = $canned.answers.$command
                    if ($null -eq $answer) { throw "No canned probe result for: $command" }
                }
                else {
                    $arguments = @($probe.arguments | ForEach-Object { if ($_ -ceq '<tempfile>') { $tempFile } elseif ($_ -ceq '<tempdir>') { Join-Path $scratch 'dir' } else { $_ } })
                    $run = Invoke-GapProbe -Arguments $arguments -CandidatePath $CandidatePath -TargetVersion $TargetVersion -WorkingDirectory $scratch -TimeoutSeconds 20
                    $toAnswer = { param($r) if ($null -eq $r) { $null } else { [pscustomobject] @{ exitCode = $r.ExitCode; stderr = $r.Stderr } } }
                    $answer = [pscustomobject] @{ reference = (& $toAnswer $run.Reference); candidate = (& $toAnswer $run.Candidate) }
                }
                $items[$probe.key] = Measure-OptionFacet $probe $answer $hasReference $item.introducedIn
            }
        }
    }
    finally {
        if ($null -eq $canned) { Remove-Item -LiteralPath $scratch -Recurse -Force -ErrorAction SilentlyContinue }
    }
    $list = @($items.Values)
    $platform = if ([System.IO.Path]::DirectorySeparatorChar -eq '\') { 'windows' } elseif (Test-Path '/System/Library') { 'macos' } else { 'linux' }
    $measurement = [ordered] @{ area = 'options'; targetVersion = $TargetVersion; candidateCommit = $CandidateCommit; platform = $platform; reference = $referenceLine }
    if (-not $hasReference) { $measurement.referenceFallback = 'docs' }
    $measurement.measuredAt = [DateTime]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ssZ')
    $measurement.items = $list
    $measurement.counts = Get-OptionCounts $list
    return [pscustomobject] $measurement
}

function Invoke-OptionGapSelfTest {
    $fixtures = Join-Path $script:OptionToolsDirectory 'Fixtures/options'
    $release = Join-Path $fixtures 'upstream'
    $lines = New-Object System.Collections.Generic.List[string]
    $report = { param([string] $Name, [bool] $Passed, [string] $Detail) if ($Passed) { $lines.Add("PASS $Name") } else { $lines.Add("FAIL $Name ($Detail)") } }

    $inventory = Get-OptionInventory $release '9.9.9'
    $names = @($inventory.items | ForEach-Object { $_.name }) -join ','
    & $report '_*.md and MANPAGE.md are skipped; one item per option file' ($names -ceq '--fail,--max-time,--verbose') $names
    $verbose = $inventory.items | Where-Object { $_.name -ceq '--verbose' }
    $maxTime = $inventory.items | Where-Object { $_.name -ceq '--max-time' }
    $fail = $inventory.items | Where-Object { $_.name -ceq '--fail' }
    $attributesOk = $verbose.attributes.short -ceq '-v' -and $verbose.attributes.boolean -and $verbose.attributes.noForm -and $null -eq $verbose.attributes.arg -and
        $maxTime.attributes.arg -ceq '<seconds>' -and -not $maxTime.attributes.noForm -and $null -eq $fail.attributes.short -and (@($fail.attributes.protocols) -join ',') -ceq 'HTTP' -and $verbose.introducedIn -ceq '4.0'
    & $report 'front matter gives short, arg, protocols, boolean, noForm and introducedIn' $attributesOk ($inventory | ConvertTo-Json -Depth 6 -Compress)

    $measured = Measure-OptionGap $inventory '9.9.9' 'unused' (Join-Path $fixtures 'probe-results.json') '0000000000000000000000000000000000000000'
    $keys = @($measured.items | ForEach-Object { $_.key }) -join ','
    $expectedKeys = 'options:--fail,options:--fail:no-form,options:--max-time,options:--max-time:alias,options:--max-time:argument,options:--verbose,options:--verbose:alias,options:--verbose:no-form'
    & $report 'each facet appears only when its condition holds' ($keys -ceq $expectedKeys) $keys
    $byKey = @{}; foreach ($i in $measured.items) { $byKey[$i.key] = $i }
    & $report 'equal answers give match' ($byKey['options:--verbose'].state -ceq 'match' -and $byKey['options:--max-time:alias'].state -ceq 'match') $byKey['options:--verbose'].state
    $argument = $byKey['options:--max-time:argument']
    & $report 'a different exit code gives gap with both answers recorded' ($argument.state -ceq 'gap' -and $argument.expected -like 'exit 2: *requires parameter' -and $argument.actual -ceq 'exit 0') "$($argument.expected) / $($argument.actual)"
    $hasFallback = @($measured.PSObject.Properties.Name) -contains 'referenceFallback'
    & $report 'a matching reference is recorded and no referenceFallback is written' ($measured.reference -ceq 'curl 9.9.9 (fixture) libcurl/9.9.9' -and -not $hasFallback) $measured.reference

    $docs = Measure-OptionGap $inventory '9.9.9' 'unused' (Join-Path $fixtures 'probe-results-docs.json') '0000000000000000000000000000000000000000'
    $docsByKey = @{}; foreach ($i in $docs.items) { $docsByKey[$i.key] = $i }
    $docsOk = $null -eq $docs.reference -and $docs.referenceFallback -ceq 'docs' -and $docsByKey['options:--fail'].state -ceq 'gap' -and $docsByKey['options:--verbose'].state -ceq 'match' -and $docsByKey['options:--fail:no-form'].state -ceq 'match'
    & $report 'the docs fallback marks an unknown-option answer as gap' $docsOk ($docs | ConvertTo-Json -Depth 6 -Compress)

    $c = $measured.counts
    & $report 'counts.x is match and counts.y is match + gap + unmeasured' ($c.match -eq 6 -and $c.gap -eq 2 -and $c.x -eq 6 -and $c.y -eq 8 -and $c.excluded -eq 0) ($c | ConvertTo-Json -Compress)
    $d = $docs.counts
    & $report 'docs fallback counts follow the same formula' ($d.x -eq $d.match -and $d.y -eq ($d.match + $d.gap + $d.unmeasured) -and $d.gap -eq 1) ($d | ConvertTo-Json -Compress)

    $scratch = Join-Path ([System.IO.Path]::GetTempPath()) ('gap-options-test-' + [guid]::NewGuid().ToString('N'))
    try {
        $path = Join-Path $scratch '9.9.9/options.json'
        Write-OptionJsonFile $path $inventory
        $bytes = [System.IO.File]::ReadAllBytes($path)
        $back = [System.Text.Encoding]::UTF8.GetString($bytes) | ConvertFrom-Json
        $ascii = @($bytes | Where-Object { $_ -gt 127 }).Count -eq 0
        & $report 'the inventory file is ASCII JSON without a byte order mark and parses back' ($ascii -and $bytes[0] -eq 123 -and @($back.items).Count -eq 3 -and $back.area -ceq 'options') "$($bytes[0])"
    }
    finally { Remove-Item -LiteralPath $scratch -Recurse -Force -ErrorAction SilentlyContinue }
    return $lines
}

if ($script:RunSelfTest) {
    $lines = @(Invoke-OptionGapSelfTest)
    $lines | ForEach-Object { Write-Output $_ }
    if (@($lines | Where-Object { $_ -like 'FAIL *' }).Count -gt 0) { exit 1 }
    exit 0
}
if (-not $Version) { $Version = (Get-Content -LiteralPath (Join-Path $script:OptionRepositoryRoot 'Gap/Baselines/target.json') -Raw | ConvertFrom-Json).version }
if (-not $UpstreamRoot) { $UpstreamRoot = (& (Join-Path $script:OptionToolsDirectory 'Get-UpstreamRelease.ps1') -Version $Version | Select-Object -Last 1) }
if (-not $InventoryDirectory) { $InventoryDirectory = Join-Path $script:OptionRepositoryRoot 'Gap/Upstream' }
$inventory = Get-OptionInventory $UpstreamRoot $Version
Write-OptionJsonFile (Join-Path (Join-Path $InventoryDirectory $Version) 'options.json') $inventory
if ($InventoryOnly) { exit 0 }
$commit = (& git -C $script:OptionRepositoryRoot rev-parse HEAD).Trim()
$measurement = Measure-OptionGap $inventory $Version $Candidate $ProbeResults $commit
if ($OutFile) { Write-OptionJsonFile $OutFile $measurement } else { $measurement | ConvertTo-Json -Depth 8 }
