<#
.SYNOPSIS
    Builds the protocols and features upstream inventories from a curl release and
    measures whether Curl's curl -V and URL schemes agree with the matched reference build.

.DESCRIPTION
    Areas protocols and features of the gap analysis office (ADR-0433 decision 2). Formats
    are in Gap/Instructions/Gap-Format.md.

    Upstream sources, read as text from the release:
    - docs/cmdline-opts/_PROTOCOLS.md: each "## <NAME>" heading is one protocol; a
      "(S)" suffix, as in "## FTP(S)", gives the plain scheme and its TLS variant.
      Key protocols:<scheme>, lower case; attributes scheme and tls.
    - docs/cmdline-opts/version.md: each "## `<name>`" heading is one feature.
      Key features:<name> as upstream spells it; attribute name.
    introducedIn is null: neither document says.

    Inventories, <RepositoryRoot>/Gap/Upstream/<Version>/protocols.json and features.json,
    hold only what the documents name, so they do not depend on the platform.

    Measurement, with a matched reference (Get-GapReferenceCurl names the version):
    - Both binaries' curl -V are read. Every scheme on the reference's Protocols: line and
      every name on its Features: line that the documents lack joins the measurement
      (not the inventory) as an extra item.
    - A scheme is match when both Protocols: lines agree on listing it and, when both list
      it, "<scheme>://127.0.0.1:1/" (nothing listening on port 1) exits with the same code
      through both binaries; otherwise gap. A scheme the reference lacks is excluded with
      reason reference-lacks:<scheme> when Curl lacks it too, and gap when Curl lists it:
      Curl matches the build it targets (ADR-0021, ADR-0397).
    - A feature is match when both Features: lines agree on it, else gap. One both lack
      is excluded: debug-build-only for Debug and TrackMemory, else
      reference-lacks:<name>. Feature names compare case-insensitively.
    Without a matched reference, reference is null, referenceFallback is "docs" and every
    documented item is expected: match when Curl's curl -V lists it, else gap. No probe runs.

    -InventoryOnly uses no Windows-only API and no binary. Runs under Windows PowerShell
    5.1, PowerShell 7 and pwsh on Linux. The script is ASCII only.

.PARAMETER UpstreamRoot
    The extracted release folder holding docs/. Default: the folder
    Gap/Tools/Get-UpstreamRelease.ps1 -Version <Version> prints.

.PARAMETER Version
    The release version. Default: the version in Gap/Baselines/target.json.

.PARAMETER RepositoryRoot
    The Curl tree written to (its Gap/Upstream folder) and whose commit is recorded.
    Default: the repository this script is in.

.PARAMETER Candidate
    The Curl.Console binary to measure. Default: Get-GapCandidateCurl, the newest
    Curl.Console/bin/Release build.

.PARAMETER Reference
    The reference curl to use instead of Get-GapReferenceCurl's lookup.

.PARAMETER OutDirectory
    The folder protocols.json and features.json are written to, normally
    <run>/measurements. Required unless -InventoryOnly or -SelfTest is given.

.PARAMETER ProbeResults
    A JSON file of canned results used instead of running either binary:
    { "reference": null | { "version": [lines], "exitCodes": { "<scheme>": n } },
      "candidate": { "version": [lines], "exitCodes": { "<scheme>": n } } }.
    A null reference means no matched reference. The self-test uses it.

.PARAMETER InventoryOnly
    Writes both inventories to Gap/Upstream/<Version>/ and stops; nothing is run.

.PARAMETER SelfTest
    Runs against the fake release and canned probe results in
    Gap/Tools/Fixtures/version as version 9.9.9 in a temporary folder, prints a PASS or
    FAIL line per check, and exits 1 on any FAIL.

.EXAMPLE
    Gap/Tools/Measure-VersionGap.ps1 -OutDirectory ..\Curl.gap\2026-10-09_1430\measurements

.EXAMPLE
    Gap/Tools/Measure-VersionGap.ps1 -Version 8.21.0 -InventoryOnly
#>
[CmdletBinding()]
param(
    [string] $UpstreamRoot,
    [string] $Version,
    [string] $RepositoryRoot,
    [string] $Candidate,
    [string] $Reference,
    [string] $OutDirectory,
    [string] $ProbeResults,
    [switch] $InventoryOnly,
    [switch] $SelfTest
)

$ErrorActionPreference = 'Stop'

$DebugBuildFeatures = @('Debug', 'TrackMemory')

function Write-Utf8File([string] $Path, [string] $Text) {
    $folder = Split-Path $Path -Parent
    if ($folder -and -not (Test-Path -LiteralPath $folder)) { New-Item -ItemType Directory -Path $folder -Force | Out-Null }
    [System.IO.File]::WriteAllText($Path, $Text, (New-Object System.Text.UTF8Encoding($false)))
}

function Get-SortedItems($Items) {
    $byKey = @{}; foreach ($i in $Items) { $byKey[$i.key] = $i }
    $keys = [string[]]@($byKey.Keys)
    [Array]::Sort($keys, [StringComparer]::Ordinal)
    return @($keys | ForEach-Object { $byKey[$_] })
}

function Get-Inventories([string] $Root, [string] $ReleaseVersion) {
    $protocols = New-Object System.Collections.Generic.List[object]
    foreach ($line in [System.IO.File]::ReadAllLines((Join-Path $Root 'docs/cmdline-opts/_PROTOCOLS.md'))) {
        if ($line -notmatch '^## ([A-Za-z0-9]+)(\(S\))?\s*$') { continue }
        $base = $Matches[1].ToLowerInvariant()
        $schemes = @(@{ Scheme = $base; Tls = $false })
        if ($Matches[2]) { $schemes += @{ Scheme = "${base}s"; Tls = $true } }
        foreach ($s in $schemes) {
            $protocols.Add([ordered]@{ key = "protocols:$($s.Scheme)"; name = $s.Scheme; introducedIn = $null; attributes = [ordered]@{ scheme = $s.Scheme; tls = $s.Tls } })
        }
    }
    $features = New-Object System.Collections.Generic.List[object]
    foreach ($line in [System.IO.File]::ReadAllLines((Join-Path $Root 'docs/cmdline-opts/version.md'))) {
        if ($line -notmatch '^## `([^`]+)`\s*$') { continue }
        $features.Add([ordered]@{ key = "features:$($Matches[1])"; name = $Matches[1]; introducedIn = $null; attributes = [ordered]@{ name = $Matches[1] } })
    }
    return [ordered]@{
        protocols = [ordered]@{ area = 'protocols'; version = $ReleaseVersion; sources = @('docs/cmdline-opts/_PROTOCOLS.md'); items = (Get-SortedItems $protocols) }
        features = [ordered]@{ area = 'features'; version = $ReleaseVersion; sources = @('docs/cmdline-opts/version.md'); items = (Get-SortedItems $features) }
    }
}

function Read-VersionList([string[]] $Lines, [string] $Label) {
    foreach ($line in $Lines) {
        if ($line -match "^${Label}:\s*(.*)$") { return @(($Matches[1].Trim() -split '\s+') | Where-Object { $_ }) }
    }
    return @()
}

function ConvertTo-BinaryAnswer($Canned) {
    if ($null -eq $Canned) { return $null }
    $codes = @{}
    if ($null -ne $Canned.exitCodes) { foreach ($p in $Canned.exitCodes.PSObject.Properties) { $codes[$p.Name] = [int]$p.Value } }
    $lines = [string[]]@($Canned.version)
    return [pscustomobject]@{ VersionLine = $lines[0]; Protocols = (Read-VersionList $lines 'Protocols'); Features = (Read-VersionList $lines 'Features'); ExitCodes = $codes }
}

function Get-CannedAnswers([string] $Path) {
    $canned = [System.IO.File]::ReadAllText($Path) | ConvertFrom-Json
    return [pscustomobject]@{ Reference = (ConvertTo-BinaryAnswer $canned.reference); Candidate = (ConvertTo-BinaryAnswer $canned.candidate) }
}

function Get-LiveAnswers([string] $CandidatePath, [string] $ReferencePath, [string] $ReleaseVersion) {
    . (Join-Path $PSScriptRoot 'Invoke-GapProbe.ps1')
    $candidatePath = Get-GapCandidateCurl -Path $CandidatePath
    $found = Get-GapReferenceCurl -TargetVersion $ReleaseVersion -Path $ReferencePath
    $read = {
        param([string] $Path)
        $run = Invoke-GapRun -Path $Path -Arguments @('-V') -Environment @{} -TimeoutSeconds 20
        $lines = [string[]]([System.Text.Encoding]::UTF8.GetString($run.Stdout) -split "`r?`n")
        return [pscustomobject]@{ VersionLine = $lines[0]; Protocols = (Read-VersionList $lines 'Protocols'); Features = (Read-VersionList $lines 'Features'); ExitCodes = @{} }
    }
    $referenceAnswer = $null
    if ($null -ne $found -and $found.Matches) { $referenceAnswer = & $read $found.Path }
    $candidateAnswer = & $read $candidatePath
    if ($null -ne $referenceAnswer) {
        foreach ($scheme in $referenceAnswer.Protocols) {
            if (-not (Test-Listed $candidateAnswer.Protocols $scheme)) { continue }
            $result = Invoke-GapProbe -Arguments @("${scheme}://127.0.0.1:1/") -CandidatePath $candidatePath -ReferencePath $found.Path -TargetVersion $ReleaseVersion -TimeoutSeconds 20
            $referenceAnswer.ExitCodes[$scheme.ToLowerInvariant()] = $result.Reference.ExitCode
            $candidateAnswer.ExitCodes[$scheme.ToLowerInvariant()] = $result.Candidate.ExitCode
        }
    }
    return [pscustomobject]@{ Reference = $referenceAnswer; Candidate = $candidateAnswer }
}

function Test-Listed([string[]] $List, [string] $Name) {
    foreach ($entry in $List) { if ([string]::Equals($entry, $Name, [StringComparison]::OrdinalIgnoreCase)) { return $true } }
    return $false
}

function Get-ProbeExitCodes($Answers, [string] $Scheme) {
    $r =if ($Answers.Reference.ExitCodes.ContainsKey($Scheme)) { $Answers.Reference.ExitCodes[$Scheme] } else { $null }
    $c = if ($Answers.Candidate.ExitCodes.ContainsKey($Scheme)) { $Answers.Candidate.ExitCodes[$Scheme] } else { $null }
    return @($r, $c)
}

function Measure-Item([string] $Area, [string] $Key, [string] $Name, $IntroducedIn, $Answers) {
    $label = if ($Area -eq 'protocols') { 'Protocols' } else { 'Features' }
    $result = [ordered]@{ key = $Key; state = $null; reason = $null; expected = $null; actual = $null; evidence = "curl -V $($label): line"; introducedIn = $IntroducedIn }
    $curlLists = Test-Listed $Answers.Candidate.$label $Name
    $result.actual = if ($curlLists) { 'listed' } else { 'not listed' }
    if ($null -eq $Answers.Reference) {
        $result.expected = 'listed'
        $result.state = if ($curlLists) { 'match' } else { 'gap' }
        return $result
    }
    $referenceLists = Test-Listed $Answers.Reference.$label $Name
    $result.expected = if ($referenceLists) { 'listed' } else { 'not listed' }
    if (-not $referenceLists -and -not $curlLists) {
        $result.state = 'excluded'
        $result.reason = if ($Area -eq 'features' -and (Test-Listed $DebugBuildFeatures $Name)) { 'debug-build-only' } else { "reference-lacks:$Name" }
        return $result
    }
    $result.state = if ($referenceLists -eq $curlLists) { 'match' } else { 'gap' }
    if ($Area -eq 'protocols' -and $referenceLists -and $curlLists) {
        $codes = Get-ProbeExitCodes $Answers $Name
        $result.expected = "listed, exit $($codes[0])"
        $result.actual = "listed, exit $($codes[1])"
        $result.evidence = "curl ${Name}://127.0.0.1:1/"
        if ($null -eq $codes[0] -or $codes[0] -ne $codes[1]) { $result.state = 'gap' }
    }
    return $result
}

function Get-CandidateCommit([string] $Root) {
    try {
        $commit = (& git -C $Root rev-parse HEAD 2>$null)
        if ($LASTEXITCODE -eq 0 -and $commit -match '^[0-9a-f]{40}$') { return $commit }
    } catch { }
    return ('0' * 40)
}

function Get-Platform {
    if ($env:OS -eq 'Windows_NT') { return 'windows' }
    if ((& uname) -eq 'Darwin') { return 'macos' }
    return 'linux'
}

function Get-Measurement($Inventory, $Answers, [string] $Commit) {
    $area = $Inventory.area
    $items = New-Object System.Collections.Generic.List[object]
    foreach ($i in $Inventory.items) { $items.Add((Measure-Item $area $i.key $i.name $i.introducedIn $Answers)) }
    if ($null -ne $Answers.Reference) {
        $label = if ($area -eq 'protocols') { 'Protocols' } else { 'Features' }
        $documented = @($Inventory.items | ForEach-Object { $_.name })
        foreach ($name in $Answers.Reference.$label) {
            if (Test-Listed $documented $name) { continue }
            $spelled = if ($area -eq 'protocols') { $name.ToLowerInvariant() } else { $name }
            $items.Add((Measure-Item $area "${area}:$spelled" $spelled $null $Answers))
            $documented += $spelled
        }
    }
    $sorted = Get-SortedItems $items
    $counts = [ordered]@{}
    foreach ($state in 'match', 'gap', 'unmeasured', 'excluded') { $counts[$state] = @($sorted | Where-Object { $_.state -eq $state }).Count }
    $counts.x = $counts.match
    $counts.y = $counts.match + $counts.gap + $counts.unmeasured
    $measurement = [ordered]@{
        area = $area
        targetVersion = $Inventory.version
        candidateCommit = $Commit
        platform = Get-Platform
        reference = $null
    }
    if ($null -ne $Answers.Reference) { $measurement.reference = $Answers.Reference.VersionLine } else { $measurement.referenceFallback = 'docs' }
    $measurement.measuredAt = [DateTime]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ssZ')
    $measurement.items = $sorted
    $measurement.counts = $counts
    return $measurement
}

function Invoke-VersionGap([string] $Upstream, [string] $ReleaseVersion, [string] $Root, [string] $Out, [bool] $OnlyInventory, [string] $Canned, [string] $CandidatePath, [string] $ReferencePath) {
    $inventories = Get-Inventories $Upstream $ReleaseVersion
    foreach ($area in 'protocols', 'features') {
        Write-Utf8File (Join-Path $Root "Gap/Upstream/$ReleaseVersion/$area.json") ($inventories[$area] | ConvertTo-Json -Depth 8)
    }
    if ($OnlyInventory) { return $null }
    $answers = if ($Canned) { Get-CannedAnswers $Canned } else { Get-LiveAnswers $CandidatePath $ReferencePath $ReleaseVersion }
    $commit = Get-CandidateCommit $Root
    $results = [ordered]@{}
    foreach ($area in 'protocols', 'features') {
        $results[$area] = Get-Measurement $inventories[$area] $answers $commit
        Write-Utf8File (Join-Path $Out "$area.json") ($results[$area] | ConvertTo-Json -Depth 8)
    }
    return $results
}

function Invoke-SelfTest {
    $script:failed = $false
    function Report([bool] $Passed, [string] $Check) {
        if ($Passed) { Write-Output "PASS $Check" } else { Write-Output "FAIL $Check"; $script:failed = $true }
    }
    $fixtures = Join-Path $PSScriptRoot 'Fixtures/version'
    $temp = Join-Path ([System.IO.Path]::GetTempPath()) ("version-selftest-" + [guid]::NewGuid().ToString('N'))
    try {
        New-Item -ItemType Directory -Path $temp | Out-Null
        $upstream = Join-Path $fixtures 'upstream'
        $out = Join-Path $temp 'measurements'
        Invoke-VersionGap $upstream '9.9.9' $temp $out $false (Join-Path $fixtures 'probe-results.json') $null $null | Out-Null
        $read = { param([string] $Path) $state = @{}; foreach ($i in ([System.IO.File]::ReadAllText($Path) | ConvertFrom-Json).items) { $state[$i.key] = $i }; $state }
        $protocolInventory = & $read (Join-Path $temp 'Gap/Upstream/9.9.9/protocols.json')
        $featureInventory = & $read (Join-Path $temp 'Gap/Upstream/9.9.9/features.json')
        $protocols = & $read (Join-Path $out 'protocols.json')
        $features = & $read (Join-Path $out 'features.json')
        $measurement = [System.IO.File]::ReadAllText((Join-Path $out 'protocols.json')) | ConvertFrom-Json

        Report ($protocolInventory.ContainsKey('protocols:http') -and $protocolInventory['protocols:https'].attributes.tls -eq $true -and $protocolInventory['protocols:http'].attributes.tls -eq $false -and $protocolInventory.Count -eq 7) '(S) headings give both schemes'
        Report ($featureInventory.ContainsKey('features:alt-svc') -and $featureInventory['features:HTTP3'].attributes.name -eq 'HTTP3' -and $featureInventory.Count -eq 4) 'feature headings parse from version.md'
        Report ($protocols.ContainsKey('protocols:ipfs') -and -not $protocolInventory.ContainsKey('protocols:ipfs') -and $features.ContainsKey('features:Largefile') -and -not $featureInventory.ContainsKey('features:Largefile')) 'a reference-only item joins the measurement but not the inventory'
        Report ($protocols['protocols:http'].state -eq 'match' -and $protocols['protocols:dict'].state -eq 'match' -and $features['features:alt-svc'].state -eq 'match' -and $features['features:Largefile'].state -eq 'match') 'agreement gives match'
        Report ($protocols['protocols:mqtt'].state -eq 'gap' -and $protocols['protocols:mqtt'].actual -eq 'listed, exit 1') 'a different probe exit code gives gap'
        Report ($protocols['protocols:gopher'].state -eq 'gap' -and $features['features:HTTP3'].state -eq 'gap') 'an item Curl lists that the reference lacks gives gap'
        Report ($protocols['protocols:ipfs'].state -eq 'gap' -and $features['features:zstd'].state -eq 'gap') 'an item the reference lists that Curl lacks gives gap'
        Report ($protocols['protocols:tftp'].state -eq 'excluded' -and $protocols['protocols:tftp'].reason -eq 'reference-lacks:tftp' -and $protocols['protocols:gophers'].reason -eq 'reference-lacks:gophers') 'a scheme both lack gives excluded with reference-lacks:'
        Report ($features['features:Debug'].state -eq 'excluded' -and $features['features:Debug'].reason -eq 'debug-build-only') 'a debug-build feature both lack is excluded with debug-build-only'
        Report ($measurement.reference -like 'curl 9.9.9 *' -and $null -eq $measurement.PSObject.Properties['referenceFallback']) 'a matched reference is named with no fallback'
        $c = $measurement.counts
        Report ($c.y -eq ($c.match + $c.gap + $c.unmeasured) -and $c.x -eq $c.match -and ($c.match + $c.gap + $c.unmeasured + $c.excluded) -eq @($measurement.items).Count) 'counts add up'

        Invoke-VersionGap $upstream '9.9.9' $temp $out $false (Join-Path $fixtures 'probe-results-docs.json') $null $null | Out-Null
        $docs = [System.IO.File]::ReadAllText((Join-Path $out 'protocols.json')) | ConvertFrom-Json
        $docProtocols = & $read (Join-Path $out 'protocols.json')
        $docFeatures = & $read (Join-Path $out 'features.json')
        Report ($null -eq $docs.reference -and $docs.referenceFallback -eq 'docs') 'without a reference the fallback is docs'
        Report ($docProtocols.Count -eq 7 -and $docFeatures.Count -eq 4 -and $docProtocols['protocols:tftp'].state -eq 'gap' -and $docProtocols['protocols:http'].state -eq 'match' -and $docFeatures['features:Debug'].state -eq 'gap' -and $docs.counts.excluded -eq 0) 'the docs fallback expects every documented item'
    } finally {
        if (Test-Path -LiteralPath $temp) { Remove-Item -LiteralPath $temp -Recurse -Force }
    }
    if ($script:failed) { exit 1 }
}

if ($SelfTest) { Invoke-SelfTest; return }

if ([string]::IsNullOrEmpty($RepositoryRoot)) { $RepositoryRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent }
if ([string]::IsNullOrEmpty($Version)) {
    $Version = ([System.IO.File]::ReadAllText((Join-Path $RepositoryRoot 'Gap/Baselines/target.json')) | ConvertFrom-Json).version
}
if ([string]::IsNullOrEmpty($UpstreamRoot)) {
    $UpstreamRoot = @(& (Join-Path $PSScriptRoot 'Get-UpstreamRelease.ps1') -Version $Version)[-1]
}
if (-not $InventoryOnly -and [string]::IsNullOrEmpty($OutDirectory)) { throw 'Give -OutDirectory, or -InventoryOnly to write the inventories alone.' }

$results = Invoke-VersionGap $UpstreamRoot $Version $RepositoryRoot $OutDirectory $InventoryOnly.IsPresent $ProbeResults $Candidate $Reference
if ($null -ne $results) {
    foreach ($area in $results.Keys) {
        $c = $results[$area].counts
        Write-Output "${area}: match $($c.match), gap $($c.gap), unmeasured $($c.unmeasured), excluded $($c.excluded), X/Y $($c.x)/$($c.y)"
    }
}
