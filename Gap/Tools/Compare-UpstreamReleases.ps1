<#
.SYNOPSIS
    Diffs the upstream inventories of two curl releases, area by area, into a release diff.

.DESCRIPTION
    ADR-0433 decision 6: the baseline moves with upstream. This script builds the newer
    release's upstream inventories, diffs them against the older release's, and writes a
    release diff (Gap/Instructions/Gap-Format.md section 5), which
    Write-GapFindings.ps1 -ReleaseDiff turns into scope newest findings tagged with the
    newer version.

    Inventories. Seven areas, one file each under <UpstreamDirectory>/<version>/:
    options.json (Measure-OptionGap.ps1), protocols.json and features.json
    (Measure-VersionGap.ps1), writeout.json (Measure-WriteOutGap.ps1), exitcodes.json
    (Measure-ExitCodeGap.ps1), environment.json (Measure-EnvironmentGap.ps1), each from
    the tool's -InventoryOnly mode, and behaviour.json, built by this script.

    Rules.
    1. From: the inventories committed under <UpstreamDirectory>/<From>/ are used as they
       are. Only when an area's file is missing is the From release fetched with
       Get-UpstreamRelease.ps1 and that file built; files already there are never
       rewritten.
    2. To: the release is always fetched with Get-UpstreamRelease.ps1 and all seven
       inventories are written to <UpstreamDirectory>/<To>/, replacing any there.
    3. For each area: an item key in To but not in From is added; in From but not in To
       is removed; in both with a different name or different attributes is changed,
       with both inventory entries in before and after. introducedIn is ignored when
       comparing, so an item whose introducedIn alone differs is not listed. Attributes
       are compared by value: property order does not matter.
    4. Behaviour: one item per tests/data/test<number> file, keyed behaviour:test<number>,
       with attributes number, sha256 (the file's SHA-256, lowercase), keywords (the lines
       of <keywords>), tool (the <tool> text, or null) and features (the lines of
       <features>). Behaviour items are compared by sha256 alone: a new file is added, a
       missing one removed, a different hash changed.
    5. The diff names fromVersion and toVersion and lists the changed items sorted by key
       (ordinal). The script prints one line per area, in the order options, protocols,
       features, writeout, exitcodes, environment, behaviour:
           <area>: added <n>, removed <n>, changed <n>
       then "Wrote <OutFile>".

    JSON is written as UTF-8 without a byte order mark, with any non-ASCII character
    escaped as \uXXXX. The script is ASCII only and uses no Windows-only API: the weekly
    release watch runs it under pwsh on ubuntu-latest. It runs under Windows PowerShell
    5.1 and PowerShell 7 too.

.PARAMETER From
    The older version. Default: the version in <BaselinesDirectory>/target.json.

.PARAMETER To
    The newer version. Required unless -SelfTest is given.

.PARAMETER UpstreamDirectory
    The folder holding one inventory folder per version. Default: Gap/Upstream.

.PARAMETER OutFile
    Where to write the release diff. Default: release-<To>.json in the current folder;
    a gap run passes <run>/measurements/release-<To>.json.

.PARAMETER FromArchivePath
    A local curl-<From>.tar.gz passed to Get-UpstreamRelease.ps1 -ArchivePath instead of
    downloading, when From's inventories have to be built. For self-tests.

.PARAMETER ToArchivePath
    A local curl-<To>.tar.gz passed to Get-UpstreamRelease.ps1 -ArchivePath instead of
    downloading. For self-tests.

.PARAMETER CacheRoot
    Passed to Get-UpstreamRelease.ps1 -CacheRoot. Default: that script's default.

.PARAMETER BaselinesDirectory
    The folder holding target.json, passed to Get-UpstreamRelease.ps1
    -BaselinesDirectory, which writes curl-<version>.json there on a first fetch.
    Default: Gap/Baselines.

.PARAMETER SelfTest
    Builds curl-9.0.0.tar.gz and curl-9.1.0.tar.gz from the fake releases in
    Gap/Tools/Fixtures/releases in a temporary folder, runs the script on them with
    temporary -UpstreamDirectory, -CacheRoot and -BaselinesDirectory and no network,
    feeds the diff to Write-GapFindings.ps1 -ReleaseDiff in a temporary findings folder,
    prints a PASS or FAIL line per check, and exits 1 on any FAIL.

.EXAMPLE
    Gap/Tools/Compare-UpstreamReleases.ps1 -From 8.21.0 -To 8.22.0
#>
[CmdletBinding()]
param(
    [string] $From,
    [string] $To,
    [string] $UpstreamDirectory,
    [string] $OutFile,
    [string] $FromArchivePath,
    [string] $ToArchivePath,
    [string] $CacheRoot,
    [string] $BaselinesDirectory,
    [switch] $SelfTest
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
# Windows PowerShell 5.1 has no $PSScriptRoot while it binds parameter defaults.
$script:GapRoot = Split-Path $PSScriptRoot -Parent
if ([string]::IsNullOrEmpty($UpstreamDirectory)) { $UpstreamDirectory = Join-Path $script:GapRoot 'Upstream' }
if ([string]::IsNullOrEmpty($BaselinesDirectory)) { $BaselinesDirectory = Join-Path $script:GapRoot 'Baselines' }

$script:Areas = @('options', 'protocols', 'features', 'writeout', 'exitcodes', 'environment', 'behaviour')

function Write-AsciiJsonFile([string] $Path, $Value) {
    $folder = Split-Path $Path -Parent
    if ($folder -and -not (Test-Path -LiteralPath $folder)) { New-Item -ItemType Directory -Path $folder -Force | Out-Null }
    $json = $Value | ConvertTo-Json -Depth 10
    $json = [regex]::Replace($json, '[^\x00-\x7F]', { param($m) '\u{0:x4}' -f [int][char] $m.Value })
    [System.IO.File]::WriteAllText($Path, $json + "`n", (New-Object System.Text.UTF8Encoding $false))
}

function Read-JsonFile([string] $Path) {
    return [System.IO.File]::ReadAllText($Path) | ConvertFrom-Json
}

function Get-TagLines([string] $Text, [string] $Tag) {
    $match = [regex]::Match($Text, "<$Tag(\s[^>]*)?>(.*?)</$Tag>", [System.Text.RegularExpressions.RegexOptions]::Singleline)
    if (-not $match.Success) { return , @() }
    return , @($match.Groups[2].Value -split "`r?`n" | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne '' })
}

function Get-BehaviourInventory([string] $Root, [string] $ReleaseVersion) {
    $folder = Join-Path (Join-Path $Root 'tests') 'data'
    $sorted = New-Object 'System.Collections.Generic.SortedDictionary[string,object]' ([System.StringComparer]::Ordinal)
    if (Test-Path -LiteralPath $folder) {
        foreach ($file in Get-ChildItem -LiteralPath $folder -File) {
            if ($file.Name -notmatch '^test(\d+)$') { continue }
            $number = [int] $Matches[1]
            $bytes = [System.IO.File]::ReadAllBytes($file.FullName)
            $sha = [System.Security.Cryptography.SHA256]::Create()
            $hash = -join ($sha.ComputeHash($bytes) | ForEach-Object { $_.ToString('x2') })
            # Latin-1 keeps every byte as one character, so a test file in any encoding parses.
            $text = [System.Text.Encoding]::GetEncoding(28591).GetString($bytes)
            $toolLines = Get-TagLines $text 'tool'
            $tool = $null; if ($toolLines.Count -gt 0) { $tool = $toolLines -join ' ' }
            $sorted["behaviour:test$number"] = [pscustomobject] [ordered] @{
                key = "behaviour:test$number"; name = "test$number"; introducedIn = $null
                attributes = [pscustomobject] [ordered] @{ number = $number; sha256 = $hash; keywords = (Get-TagLines $text 'keywords'); tool = $tool; features = (Get-TagLines $text 'features') }
            }
        }
    }
    return [pscustomobject] [ordered] @{ area = 'behaviour'; version = $ReleaseVersion; sources = @('tests/data/test*'); items = [object[]] @($sorted.Values) }
}

function Get-ReleaseRoot([string] $ReleaseVersion, [string] $Archive) {
    $arguments = @{ Version = $ReleaseVersion; BaselinesDirectory = $BaselinesDirectory }
    if ($Archive) { $arguments.ArchivePath = $Archive }
    if ($CacheRoot) { $arguments.CacheRoot = $CacheRoot }
    return @(& (Join-Path $PSScriptRoot 'Get-UpstreamRelease.ps1') @arguments)[-1]
}

# Builds the inventories of the given areas into <UpstreamDirectory>/<version>/, each
# through a scratch folder so a file not asked for is never rewritten.
function Build-Inventories([string] $ReleaseVersion, [string] $Archive, [string[]] $Wanted) {
    $root = Get-ReleaseRoot $ReleaseVersion $Archive
    $scratchRepository = Join-Path ([System.IO.Path]::GetTempPath()) ('gap-compare-' + [guid]::NewGuid().ToString('N'))
    $scratchUpstream = Join-Path (Join-Path $scratchRepository 'Gap') 'Upstream'
    try {
        $common = @{ UpstreamRoot = $root; Version = $ReleaseVersion; InventoryOnly = $true }
        if ($Wanted -contains 'options') { & (Join-Path $PSScriptRoot 'Measure-OptionGap.ps1') @common -InventoryDirectory $scratchUpstream | Out-Null }
        if ($Wanted -contains 'writeout') { & (Join-Path $PSScriptRoot 'Measure-WriteOutGap.ps1') @common -InventoryDirectory $scratchUpstream | Out-Null }
        if ($Wanted -contains 'protocols' -or $Wanted -contains 'features') { & (Join-Path $PSScriptRoot 'Measure-VersionGap.ps1') @common -RepositoryRoot $scratchRepository | Out-Null }
        if ($Wanted -contains 'exitcodes') { & (Join-Path $PSScriptRoot 'Measure-ExitCodeGap.ps1') @common -RepositoryRoot $scratchRepository | Out-Null }
        if ($Wanted -contains 'environment') { & (Join-Path $PSScriptRoot 'Measure-EnvironmentGap.ps1') @common -RepositoryRoot $scratchRepository | Out-Null }
        if ($Wanted -contains 'behaviour') { Write-AsciiJsonFile (Join-Path (Join-Path $scratchUpstream $ReleaseVersion) 'behaviour.json') (Get-BehaviourInventory $root $ReleaseVersion) }
        $target = Join-Path $UpstreamDirectory $ReleaseVersion
        if (-not (Test-Path -LiteralPath $target)) { New-Item -ItemType Directory -Path $target -Force | Out-Null }
        foreach ($area in $Wanted) {
            $built = Join-Path (Join-Path $scratchUpstream $ReleaseVersion) "$area.json"
            if (-not (Test-Path -LiteralPath $built)) { throw "No $area inventory was built for $ReleaseVersion." }
            Copy-Item -LiteralPath $built -Destination (Join-Path $target "$area.json") -Force
        }
    }
    finally { Remove-Item -LiteralPath $scratchRepository -Recurse -Force -ErrorAction SilentlyContinue }
}

function ConvertTo-CanonicalText($Value) {
    if ($null -eq $Value) { return 'null' }
    if ($Value -is [string]) { return ConvertTo-Json -InputObject $Value -Compress }
    if ($Value -is [bool]) { if ($Value) { return 'true' } else { return 'false' } }
    if ($Value -is [System.Management.Automation.PSCustomObject]) {
        $names = [string[]] @($Value.PSObject.Properties | ForEach-Object { $_.Name })
        [Array]::Sort($names, [System.StringComparer]::Ordinal)
        return '{' + ((@($names | ForEach-Object { (ConvertTo-Json -InputObject $_ -Compress) + ':' + (ConvertTo-CanonicalText $Value.$_) })) -join ',') + '}'
    }
    if ($Value -is [System.Collections.IEnumerable]) { return '[' + ((@($Value | ForEach-Object { ConvertTo-CanonicalText $_ })) -join ',') + ']' }
    return [System.Convert]::ToString($Value, [System.Globalization.CultureInfo]::InvariantCulture)
}

function Test-ItemChanged([string] $Area, $Before, $After) {
    if ($Area -ceq 'behaviour') { return $Before.attributes.sha256 -cne $After.attributes.sha256 }
    if ($Before.name -cne $After.name) { return $true }
    return (ConvertTo-CanonicalText $Before.attributes) -cne (ConvertTo-CanonicalText $After.attributes)
}

function Get-ItemsByKey($Inventory) {
    $byKey = @{}
    foreach ($item in @($Inventory.items)) { if ($null -ne $item) { $byKey[[string] $item.key] = $item } }
    return $byKey
}

function Compare-Releases([string] $Older, [string] $Newer) {
    $changes = New-Object 'System.Collections.Generic.SortedDictionary[string,object]' ([System.StringComparer]::Ordinal)
    $lines = New-Object System.Collections.Generic.List[string]
    foreach ($area in $script:Areas) {
        $before = Get-ItemsByKey (Read-JsonFile (Join-Path (Join-Path $UpstreamDirectory $Older) "$area.json"))
        $after = Get-ItemsByKey (Read-JsonFile (Join-Path (Join-Path $UpstreamDirectory $Newer) "$area.json"))
        $added = 0; $removed = 0; $changed = 0
        foreach ($key in $after.Keys) {
            if (-not $before.ContainsKey($key)) {
                $changes[$key] = [pscustomobject] [ordered] @{ key = $key; change = 'added'; before = $null; after = $after[$key] }; $added++
            }
            elseif (Test-ItemChanged $area $before[$key] $after[$key]) {
                $changes[$key] = [pscustomobject] [ordered] @{ key = $key; change = 'changed'; before = $before[$key]; after = $after[$key] }; $changed++
            }
        }
        foreach ($key in $before.Keys) {
            if (-not $after.ContainsKey($key)) {
                $changes[$key] = [pscustomobject] [ordered] @{ key = $key; change = 'removed'; before = $before[$key]; after = $null }; $removed++
            }
        }
        $lines.Add("${area}: added $added, removed $removed, changed $changed")
    }
    $diff = [pscustomobject] [ordered] @{ fromVersion = $Older; toVersion = $Newer; items = [object[]] @($changes.Values) }
    return @{ Diff = $diff; Lines = $lines }
}

function Invoke-CompareUpstreamReleases {
    if ([string]::IsNullOrEmpty($To)) { throw 'Give -To, the newer version.' }
    $older = $From
    if ([string]::IsNullOrEmpty($older)) { $older = (Read-JsonFile (Join-Path $BaselinesDirectory 'target.json')).version }
    $out = $OutFile
    if ([string]::IsNullOrEmpty($out)) { $out = "release-$To.json" }
    if (-not [System.IO.Path]::IsPathRooted($out)) { $out = Join-Path (Get-Location).Path $out }

    $missing = @($script:Areas | Where-Object { -not (Test-Path -LiteralPath (Join-Path (Join-Path $UpstreamDirectory $older) "$_.json")) })
    if ($missing.Count -gt 0) { Build-Inventories $older $FromArchivePath $missing }
    Build-Inventories $To $ToArchivePath $script:Areas

    $result = Compare-Releases $older $To
    Write-AsciiJsonFile $out $result.Diff
    foreach ($line in $result.Lines) { Write-Output $line }
    Write-Output "Wrote $out"
}

function Get-TarPath {
    if ($env:OS -eq 'Windows_NT') {
        $systemTar = Join-Path $env:SystemRoot 'System32\tar.exe'
        if (Test-Path -LiteralPath $systemTar) { return $systemTar }
    }
    return 'tar'
}

function Invoke-SelfTest {
    $lines = New-Object System.Collections.Generic.List[string]
    $report = { param([string] $Name, [bool] $Passed, [string] $Detail) if ($Passed) { $lines.Add("PASS $Name") } else { $lines.Add("FAIL $Name ($Detail)") } }
    $work = Join-Path ([System.IO.Path]::GetTempPath()) ('gap-compare-selftest-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $work | Out-Null
    try {
        $tar = Get-TarPath
        $fixtures = Join-Path (Join-Path $PSScriptRoot 'Fixtures') 'releases'
        $archives = @{}
        foreach ($v in '9.0.0', '9.1.0') {
            $archives[$v] = Join-Path $work "curl-$v.tar.gz"
            & $tar -czf $archives[$v] -C $fixtures "curl-$v" | Out-Null
            if ($LASTEXITCODE -ne 0) { throw "tar could not build $($archives[$v])." }
        }
        $upstream = Join-Path $work 'upstream'
        $baselines = Join-Path $work 'baselines'
        New-Item -ItemType Directory -Path $baselines | Out-Null
        $diffPath = Join-Path $work 'release-9.1.0.json'
        $arguments = @{ From = '9.0.0'; To = '9.1.0'; FromArchivePath = $archives['9.0.0']; ToArchivePath = $archives['9.1.0']
            UpstreamDirectory = $upstream; OutFile = $diffPath; CacheRoot = (Join-Path $work 'cache'); BaselinesDirectory = $baselines }
        $printed = @(& $PSCommandPath @arguments)

        $diff = Read-JsonFile $diffPath
        $byKey = @{}; foreach ($i in @($diff.items)) { $byKey[[string] $i.key] = $i }
        $changeOf = { param([string] $Key) if ($byKey.ContainsKey($Key)) { return $byKey[$Key].change } return 'absent' }
        & $report 'the diff names fromVersion 9.0.0 and toVersion 9.1.0' ($diff.fromVersion -ceq '9.0.0' -and $diff.toVersion -ceq '9.1.0') "$($diff.fromVersion) $($diff.toVersion)"
        & $report 'an option only in the newer release is added, with before null' ((& $changeOf 'options:--ech') -ceq 'added' -and $null -eq $byKey['options:--ech'].before -and $byKey['options:--ech'].after.name -ceq '--ech') (& $changeOf 'options:--ech')
        & $report 'an option only in the older release is removed, with after null' ((& $changeOf 'options:--fail') -ceq 'removed' -and $null -eq $byKey['options:--fail'].after -and $byKey['options:--fail'].before.name -ceq '--fail') (& $changeOf 'options:--fail')
        $maxTime = $byKey['options:--max-time']
        & $report 'an option whose Arg: changed is changed, with both values in before and after' ((& $changeOf 'options:--max-time') -ceq 'changed' -and $maxTime.before.attributes.arg -ceq '<seconds>' -and $maxTime.after.attributes.arg -ceq '<fractional seconds>') (& $changeOf 'options:--max-time')
        & $report 'an option whose introducedIn alone changed is not listed' ((& $changeOf 'options:--verbose') -ceq 'absent') (& $changeOf 'options:--verbose')
        & $report 'a new write-out variable is added' ((& $changeOf 'writeout:time_example') -ceq 'added') (& $changeOf 'writeout:time_example')
        $strerror = $byKey['exitcodes:6:strerror']
        & $report 'a changed strerror text is changed, with both texts' ((& $changeOf 'exitcodes:6:strerror') -ceq 'changed' -and $strerror.before.attributes.strerror -ceq 'Could not resolve hostname' -and $strerror.after.attributes.strerror -ceq 'Could not resolve host name') (& $changeOf 'exitcodes:6:strerror')
        & $report 'test files added, removed and changed by SHA-256; an equal one is not listed' ((& $changeOf 'behaviour:test4') -ceq 'added' -and (& $changeOf 'behaviour:test3') -ceq 'removed' -and (& $changeOf 'behaviour:test2') -ceq 'changed' -and (& $changeOf 'behaviour:test1') -ceq 'absent' -and $byKey['behaviour:test2'].before.attributes.sha256 -cne $byKey['behaviour:test2'].after.attributes.sha256) ((@('test1', 'test2', 'test3', 'test4') | ForEach-Object { & $changeOf "behaviour:$_" }) -join ',')
        $keys = [string[]] @($diff.items | ForEach-Object { $_.key }); $sortedKeys = [string[]] $keys.Clone(); [Array]::Sort($sortedKeys, [System.StringComparer]::Ordinal)
        & $report 'the items are sorted by key' (($keys -join '|') -ceq ($sortedKeys -join '|')) ($keys -join ',')
        $expected = @('options: added 1, removed 1, changed 1', 'protocols: added 0, removed 0, changed 0', 'features: added 0, removed 0, changed 0',
            'writeout: added 1, removed 0, changed 0', 'exitcodes: added 0, removed 0, changed 2', 'environment: added 0, removed 0, changed 0',
            'behaviour: added 1, removed 1, changed 1', "Wrote $diffPath")
        & $report 'one summary line per area, then the file written' (($printed -join '|') -ceq ($expected -join '|')) ($printed -join ' | ')

        $allFiles = @('9.0.0', '9.1.0' | ForEach-Object { $v = $_; $script:Areas | ForEach-Object { Join-Path (Join-Path $upstream $v) "$_.json" } })
        & $report 'both versions get all seven inventories' (@($allFiles | Where-Object { -not (Test-Path -LiteralPath $_) }).Count -eq 0) (($allFiles | Where-Object { -not (Test-Path -LiteralPath $_) }) -join ',')
        $behaviour = Read-JsonFile (Join-Path (Join-Path $upstream '9.1.0') 'behaviour.json')
        $test2 = @($behaviour.items | Where-Object { $_.key -ceq 'behaviour:test2' })[0]
        $test4 = @($behaviour.items | Where-Object { $_.key -ceq 'behaviour:test4' })[0]
        & $report 'the behaviour inventory records number, sha256, keywords, tool and features' ($behaviour.area -ceq 'behaviour' -and $test2.attributes.number -eq 2 -and $test2.attributes.sha256 -cmatch '^[0-9a-f]{64}$' -and (@($test2.attributes.keywords) -join ',') -ceq 'HTTP,globbing' -and (@($test2.attributes.features) -join ',') -ceq 'http' -and $null -eq $test2.attributes.tool -and $test4.attributes.tool -ceq 'unit1234') ($test2 | ConvertTo-Json -Depth 5 -Compress)
        $bytes = [System.IO.File]::ReadAllBytes($diffPath)
        & $report 'the diff is ASCII JSON without a byte order mark' (@($bytes | Where-Object { $_ -gt 127 }).Count -eq 0 -and $bytes[0] -eq 123) "$($bytes[0])"

        $optionsPath = Join-Path (Join-Path $upstream '9.0.0') 'options.json'
        $options = Read-JsonFile $optionsPath
        $sentinel = [pscustomobject] [ordered] @{ key = 'options:--zz-sentinel'; name = '--zz-sentinel'; introducedIn = $null; attributes = [pscustomobject] [ordered] @{ short = $null; arg = $null; protocols = @(); boolean = $true; noForm = $false } }
        $options.items = [object[]] (@($options.items) + $sentinel)
        Write-AsciiJsonFile $optionsPath $options
        $secondPath = Join-Path $work 'second.json'
        $arguments.OutFile = $secondPath; $arguments.Remove('FromArchivePath')
        $second = @(& $PSCommandPath @arguments)
        $secondDiff = Read-JsonFile $secondPath
        $sentinelChange = @($secondDiff.items | Where-Object { $_.key -ceq 'options:--zz-sentinel' } | ForEach-Object { $_.change })
        & $report 'the older release''s committed inventories are used as they are, not rebuilt' (($sentinelChange -join ',') -ceq 'removed' -and $second[0] -ceq 'options: added 1, removed 2, changed 1') (($second -join ' | '))

        $findings = Join-Path $work 'findings'
        $run = Join-Path $work 'run'
        New-Item -ItemType Directory -Path $findings, (Join-Path $run 'measurements') | Out-Null
        & (Join-Path $PSScriptRoot 'Write-GapFindings.ps1') -RunDirectory $run -Stamp '2026-10-08_1200' -FindingsDirectory $findings -ReleaseDiff $diffPath | Out-Null
        $filed = @(Get-ChildItem -LiteralPath $findings -Filter 'GF-*.md')
        $tagged = @($filed | Where-Object { $text = [System.IO.File]::ReadAllText($_.FullName); $text -cmatch '(?m)^scope: newest\s*$' -and $text -cmatch '(?m)^introduced-in: 9\.1\.0\s*$' })
        & $report 'Write-GapFindings.ps1 -ReleaseDiff files scope newest findings with introduced-in 9.1.0' ($filed.Count -gt 0 -and $tagged.Count -eq $filed.Count) "$($tagged.Count) of $($filed.Count)"

        $own = [System.IO.File]::ReadAllBytes($PSCommandPath)
        & $report 'the script is ASCII only' (@($own | Where-Object { $_ -gt 127 }).Count -eq 0) ''
    }
    finally { Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue }
    return $lines
}

if ($SelfTest) {
    $lines = @(Invoke-SelfTest)
    $lines | ForEach-Object { Write-Output $_ }
    if (@($lines | Where-Object { $_ -like 'FAIL *' }).Count -gt 0) { exit 1 }
    exit 0
}
Invoke-CompareUpstreamReleases
