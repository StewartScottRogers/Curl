<#
.SYNOPSIS
    Builds the exitcodes upstream inventory from a curl release and measures whether Curl
    has every exit code under the same number with the same curl_easy_strerror text.

.DESCRIPTION
    Area exitcodes of the gap analysis office (ADR-0433 decision 2). Formats are in
    Gap/Instructions/Gap-Format.md.

    Upstream sources, read as text from the release (never compiled):
    - docs/libcurl/libcurl-errors.md: the "# CURLcode" section's "## CURLE_<NAME> (<n>)"
      headings give each code's name and number. "## Obsolete error (<n>)" and
      "## Obsolete errors (<a>-<b>)" headings give retired codes; each gets the name
      CURLE_OBSOLETE<n>, the name curl.h gives it.
    - docs/cmdline-opts/_EXITCODES.md: each "## <n>" heading and the text under it, joined
      into one line, is the code's exitText. "## XX" is skipped.
    - lib/strerror.c: in curl_easy_strerror's switch, every "case CURLE_<NAME>:" up to its
      "return" gives the returned text. Adjacent C string literals are concatenated and
      the escapes \" and \\ are decoded. The switch ends at "default:".

    Inventory, <RepositoryRoot>/Gap/Upstream/<Version>/exitcodes.json, keys per code:
    - exitcodes:<n>           the code exists (every code in libcurl-errors.md).
    - exitcodes:<n>:strerror  curl_easy_strerror's text (only codes with a case in
                              strerror.c).
    - exitcodes:<n>:man       the code is listed in _EXITCODES.md. Inventory only: it has
                              no candidate facet of its own.
    Each item's attributes are number, curleName, strerror and exitText. introducedIn is
    null: the error table does not say.

    Curl's side is read as source text from <RepositoryRoot>:
    - Curl.Protocol.Abstractions.UnitLibrary/CurlExitCode.cs: "<Member> = <n>," lines.
    - Curl.Console/CurlEasyErrorText.cs: "[CurlExitCode.<Member>] = "<text>"" entries
      (the escapes \" and \\ decoded) and its UnknownError constant, the text of a
      member with no entry.

    Scoring:
    - exitcodes:<n> is match when a CurlExitCode member has the number n, else gap.
    - exitcodes:<n>:strerror is match when Curl's text for that member equals
      strerror.c's byte for byte, else gap (also gap when no member has the number).
    - exitcodes:<n>:man has the state of exitcodes:<n>: the curl tool documents exit
      code n, and Curl has it or not.
    - An obsolete code (CURLE_OBSOLETE<n>) is excluded with reason obsolete-code, on
      every key it has, because upstream never returns it.
    - When either Curl source file cannot be read, every key that is not excluded is
      unmeasured with reason source-not-found.
    The measurement's reference is null and referenceFallback is "docs": the comparison
    is with the release's text, not with a binary. evidence names the Curl source file
    and the commit measured. Codes Curl has that upstream lacks are not scored.

    Uses no Windows-only API. Runs under Windows PowerShell 5.1, PowerShell 7 and pwsh on
    Linux. The script is ASCII only.

.PARAMETER UpstreamRoot
    The extracted release folder holding docs/ and lib/strerror.c. Default: the folder
    Gap/Tools/Get-UpstreamRelease.ps1 -Version <Version> prints.

.PARAMETER Version
    The release version. Default: the version in Gap/Baselines/target.json.

.PARAMETER RepositoryRoot
    The Curl tree read and written. Default: the repository this script is in.

.PARAMETER OutFile
    Where to write the area measurement, normally <run>/measurements/exitcodes.json.
    Required unless -InventoryOnly or -SelfTest is given.

.PARAMETER InventoryOnly
    Writes the inventory and stops; Curl's sources are not read and nothing is measured.

.PARAMETER SelfTest
    Copies Gap/Tools/Fixtures/exitcodes/repo to a temporary folder, runs against the fake
    release in Gap/Tools/Fixtures/exitcodes/upstream as version 9.9.9, prints a PASS or
    FAIL line per check, and exits 1 on any FAIL.

.EXAMPLE
    Gap/Tools/Measure-ExitCodeGap.ps1 -OutFile ..\Curl.gap\2026-10-09_1430\measurements\exitcodes.json

.EXAMPLE
    Gap/Tools/Measure-ExitCodeGap.ps1 -Version 8.21.0 -InventoryOnly
#>
[CmdletBinding()]
param(
    [string] $UpstreamRoot,
    [string] $Version,
    [string] $RepositoryRoot,
    [string] $OutFile,
    [switch] $InventoryOnly,
    [switch] $SelfTest
)

$ErrorActionPreference = 'Stop'

$ExitCodeSource = 'Curl.Protocol.Abstractions.UnitLibrary/CurlExitCode.cs'
$ErrorTextSource = 'Curl.Console/CurlEasyErrorText.cs'

function Write-Utf8File([string] $Path, [string] $Text) {
    $folder = Split-Path $Path -Parent
    if ($folder -and -not (Test-Path -LiteralPath $folder)) { New-Item -ItemType Directory -Path $folder -Force | Out-Null }
    [System.IO.File]::WriteAllText($Path, $Text, (New-Object System.Text.UTF8Encoding($false)))
}

function ConvertFrom-EscapedText([string] $Text) {
    return [regex]::Replace($Text, '\\(.)', { param($m) $m.Groups[1].Value })
}

function Read-ErrorTable([string] $Path) {
    $codes = New-Object System.Collections.Generic.List[object]
    $inSection = $false
    foreach ($line in [System.IO.File]::ReadAllLines($Path)) {
        if ($line -match '^# (\S+)') { $inSection = ($Matches[1] -eq 'CURLcode'); continue }
        if (-not $inSection) { continue }
        if ($line -match '^## (CURLE_[A-Z0-9_]+) \((\d+)\)\s*$') {
            $codes.Add([pscustomobject]@{ Number = [int]$Matches[2]; Name = $Matches[1]; Obsolete = $false })
        } elseif ($line -match '^## Obsolete errors? \((\d+)(?:-(\d+))?\)\s*$') {
            $first = [int]$Matches[1]
            $last = if ($Matches[2]) { [int]$Matches[2] } else { $first }
            for ($n = $first; $n -le $last; $n++) {
                $codes.Add([pscustomobject]@{ Number = $n; Name = "CURLE_OBSOLETE$n"; Obsolete = $true })
            }
        }
    }
    return $codes
}

function Read-ExitCodeTexts([string] $Path) {
    $texts = @{}
    $current = $null
    $lines = New-Object System.Collections.Generic.List[string]
    foreach ($line in ([System.IO.File]::ReadAllLines($Path) + '## END')) {
        if ($line -match '^#') {
            if ($null -ne $current) { $texts[$current] = (($lines | Where-Object { $_.Trim() }) | ForEach-Object { $_.Trim() }) -join ' ' }
            $current = if ($line -match '^## (\d+)\s*$') { [int]$Matches[1] } else { $null }
            $lines.Clear()
        } elseif ($null -ne $current) {
            $lines.Add($line)
        }
    }
    return $texts
}

function Read-StrErrorTexts([string] $Path) {
    $source = [System.IO.File]::ReadAllText($Path)
    $start = $source.IndexOf('curl_easy_strerror(')
    if ($start -lt 0) { throw "curl_easy_strerror not found in $Path" }
    $end = $source.IndexOf('default:', $start)
    $body = $source.Substring($start, $end - $start)
    $texts = @{}
    $pending = New-Object System.Collections.Generic.List[string]
    $tokens = [regex]::Matches($body, 'case\s+(CURLE_[A-Z0-9_]+)\s*:|return\s+((?:"(?:[^"\\]|\\.)*"\s*)+);')
    foreach ($token in $tokens) {
        if ($token.Groups[1].Success) { $pending.Add($token.Groups[1].Value); continue }
        $literals = [regex]::Matches($token.Groups[2].Value, '"((?:[^"\\]|\\.)*)"')
        $text = (($literals | ForEach-Object { ConvertFrom-EscapedText $_.Groups[1].Value }) -join '')
        foreach ($name in $pending) { $texts[$name] = $text }
        $pending.Clear()
    }
    return $texts
}

function Read-CurlSide([string] $Root) {
    $enumPath = Join-Path $Root $ExitCodeSource
    $textPath = Join-Path $Root $ErrorTextSource
    if (-not (Test-Path -LiteralPath $enumPath) -or -not (Test-Path -LiteralPath $textPath)) { return $null }
    $members = @{}
    foreach ($m in [regex]::Matches([System.IO.File]::ReadAllText($enumPath), '(?m)^\s*([A-Za-z]\w*)\s*=\s*(\d+)\s*,?\s*$')) {
        $members[[int]$m.Groups[2].Value] = $m.Groups[1].Value
    }
    $textSource = [System.IO.File]::ReadAllText($textPath)
    $texts = @{}
    foreach ($m in [regex]::Matches($textSource, '\[CurlExitCode\.(\w+)\]\s*=\s*"((?:[^"\\]|\\.)*)"')) {
        $texts[$m.Groups[1].Value] = ConvertFrom-EscapedText $m.Groups[2].Value
    }
    $unknown = [regex]::Match($textSource, 'UnknownError\s*=\s*"((?:[^"\\]|\\.)*)"')
    if ($members.Count -eq 0 -or -not $unknown.Success) { return $null }
    return [pscustomobject]@{ Members = $members; Texts = $texts; Unknown = (ConvertFrom-EscapedText $unknown.Groups[1].Value) }
}

function Get-Inventory([string] $Root, [string] $ReleaseVersion) {
    $table = Read-ErrorTable (Join-Path $Root 'docs/libcurl/libcurl-errors.md')
    $exitTexts = Read-ExitCodeTexts (Join-Path $Root 'docs/cmdline-opts/_EXITCODES.md')
    $strerrors = Read-StrErrorTexts (Join-Path $Root 'lib/strerror.c')
    $items = New-Object System.Collections.Generic.List[object]
    foreach ($code in $table) {
        $strerror = if ($strerrors.ContainsKey($code.Name)) { $strerrors[$code.Name] } else { $null }
        $exitText = if ($exitTexts.ContainsKey($code.Number)) { $exitTexts[$code.Number] } else { $null }
        $attributes = [ordered]@{ number = $code.Number; curleName = $code.Name; strerror = $strerror; exitText = $exitText }
        $keys = @("exitcodes:$($code.Number)")
        if ($null -ne $strerror) { $keys += "exitcodes:$($code.Number):strerror" }
        if ($null -ne $exitText) { $keys += "exitcodes:$($code.Number):man" }
        foreach ($key in $keys) {
            $items.Add([ordered]@{ key = $key; name = $code.Name; introducedIn = $null; attributes = $attributes })
        }
    }
    $sorted = [string[]]($items | ForEach-Object { $_.key })
    [Array]::Sort($sorted, [StringComparer]::Ordinal)
    $byKey = @{}; foreach ($i in $items) { $byKey[$i.key] = $i }
    return [ordered]@{
        area = 'exitcodes'
        version = $ReleaseVersion
        sources = @('docs/libcurl/libcurl-errors.md', 'docs/cmdline-opts/_EXITCODES.md', 'lib/strerror.c')
        items = @($sorted | ForEach-Object { $byKey[$_] })
    }
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

function Measure-Item($Item, $Curl, [string] $Commit) {
    $a = $Item.attributes
    $facet = ($Item.key -split ':')[2]
    $result = [ordered]@{ key = $Item.key; state = $null; reason = $null; expected = $null; actual = $null; evidence = $null; introducedIn = $Item.introducedIn }
    if ($a.curleName -like 'CURLE_OBSOLETE*') { $result.state = 'excluded'; $result.reason = 'obsolete-code'; return $result }
    if ($null -eq $Curl) { $result.state = 'unmeasured'; $result.reason = 'source-not-found'; return $result }
    $member = if ($Curl.Members.ContainsKey([int]$a.number)) { $Curl.Members[[int]$a.number] } else { $null }
    if ($facet -eq 'strerror') {
        $result.expected = $a.strerror
        $result.evidence = "$ErrorTextSource at $Commit"
        if ($null -ne $member) {
            $result.actual = if ($Curl.Texts.ContainsKey($member)) { $Curl.Texts[$member] } else { $Curl.Unknown }
        }
        $result.state = if ($null -ne $result.actual -and [string]::Equals($result.actual, $result.expected, [StringComparison]::Ordinal)) { 'match' } else { 'gap' }
    } else {
        $result.expected = "$($a.curleName) = $($a.number)"
        $result.actual = if ($null -ne $member) { "CurlExitCode.$member = $($a.number)" } else { $null }
        $result.evidence = "$ExitCodeSource at $Commit"
        $result.state = if ($null -ne $member) { 'match' } else { 'gap' }
    }
    return $result
}

function Get-Measurement($Inventory, [string] $Root) {
    $curl = Read-CurlSide $Root
    $commit = Get-CandidateCommit $Root
    $items = @($Inventory.items | ForEach-Object { Measure-Item $_ $curl $commit })
    $counts = [ordered]@{}
    foreach ($state in 'match', 'gap', 'unmeasured', 'excluded') { $counts[$state] = @($items | Where-Object { $_.state -eq $state }).Count }
    $counts.x = $counts.match
    $counts.y = $counts.match + $counts.gap + $counts.unmeasured
    return [ordered]@{
        area = 'exitcodes'
        targetVersion = $Inventory.version
        candidateCommit = $commit
        platform = Get-Platform
        reference = $null
        referenceFallback = 'docs'
        measuredAt = [DateTime]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ssZ')
        items = $items
        counts = $counts
    }
}

function Invoke-ExitCodeGap([string] $Upstream, [string] $ReleaseVersion, [string] $Root, [string] $Out, [bool] $OnlyInventory) {
    $inventory = Get-Inventory $Upstream $ReleaseVersion
    Write-Utf8File (Join-Path $Root "Gap/Upstream/$ReleaseVersion/exitcodes.json") ($inventory | ConvertTo-Json -Depth 8)
    if ($OnlyInventory) { return $null }
    $measurement = Get-Measurement $inventory $Root
    Write-Utf8File $Out ($measurement | ConvertTo-Json -Depth 8)
    return $measurement
}

function Invoke-SelfTest {
    $script:failed = $false
    function Report([bool] $Passed, [string] $Check) {
        if ($Passed) { Write-Output "PASS $Check" } else { Write-Output "FAIL $Check"; $script:failed = $true }
    }
    $fixtures = Join-Path $PSScriptRoot 'Fixtures/exitcodes'
    $temp = Join-Path ([System.IO.Path]::GetTempPath()) ("exitcodes-selftest-" + [guid]::NewGuid().ToString('N'))
    try {
        Copy-Item -Path (Join-Path $fixtures 'repo') -Destination $temp -Recurse
        $out = Join-Path $temp 'measurements/exitcodes.json'
        Invoke-ExitCodeGap (Join-Path $fixtures 'upstream') '9.9.9' $temp $out $false | Out-Null
        $inventory = [System.IO.File]::ReadAllText((Join-Path $temp 'Gap/Upstream/9.9.9/exitcodes.json')) | ConvertFrom-Json
        $measurement = [System.IO.File]::ReadAllText($out) | ConvertFrom-Json
        $item = @{}; foreach ($i in $inventory.items) { $item[$i.key] = $i }
        $state = @{}; foreach ($i in $measurement.items) { $state[$i.key] = $i }

        Report ($item['exitcodes:1'].name -eq 'CURLE_UNSUPPORTED_PROTOCOL' -and $item['exitcodes:1'].attributes.number -eq 1) 'number and name parse from the error table'
        Report ($item.ContainsKey('exitcodes:50') -and $item.ContainsKey('exitcodes:51') -and $item['exitcodes:51'].name -eq 'CURLE_OBSOLETE51') 'an obsolete range gives one item per code'
        Report (-not $item.ContainsKey('exitcodes:-1') -and -not ($inventory.items | Where-Object { $_.name -like 'CURLM_*' })) 'only the CURLcode section is read'
        Report ($item['exitcodes:4:strerror'].attributes.strerror -eq 'A feature was not built-in in this libcurl.') 'a text split across C string literals is joined'
        Report ($item['exitcodes:3:strerror'].attributes.strerror -eq 'Quote "x" and \ here') 'C escapes in strerror.c are decoded'
        Report ($item['exitcodes:2:man'].attributes.exitText -eq 'Failed to initialize the thing.') 'exit text parses from _EXITCODES.md'
        Report ($state['exitcodes:6'].state -eq 'gap' -and $state['exitcodes:6:strerror'].state -eq 'gap') 'a missing enum member gives gap'
        Report ($state['exitcodes:1'].state -eq 'match' -and $state['exitcodes:1:strerror'].state -eq 'match') 'an equal text gives match'
        Report ($state['exitcodes:2:strerror'].state -eq 'gap') 'a one-character difference gives gap'
        Report ($state['exitcodes:5:strerror'].state -eq 'gap' -and $state['exitcodes:5:strerror'].actual -eq 'Unknown error') 'a member with no text entry is measured as UnknownError'
        Report ($state['exitcodes:20'].state -eq 'excluded' -and $state['exitcodes:20'].reason -eq 'obsolete-code') 'an obsolete code is excluded with reason obsolete-code'
        Report ($measurement.reference -eq $null -and $measurement.referenceFallback -eq 'docs') 'reference is null with the docs fallback'
        $c = $measurement.counts
        Report ($c.y -eq ($c.match + $c.gap + $c.unmeasured) -and $c.x -eq $c.match -and ($c.match + $c.gap + $c.unmeasured + $c.excluded) -eq @($measurement.items).Count) 'counts add up'

        Remove-Item -LiteralPath (Join-Path $temp $ErrorTextSource)
        Invoke-ExitCodeGap (Join-Path $fixtures 'upstream') '9.9.9' $temp $out $false | Out-Null
        $missing = [System.IO.File]::ReadAllText($out) | ConvertFrom-Json
        Report (@($missing.items | Where-Object { $_.state -eq 'unmeasured' -and $_.reason -eq 'source-not-found' }).Count -eq ($missing.counts.y)) 'a missing Curl source gives source-not-found'
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
if (-not $InventoryOnly -and [string]::IsNullOrEmpty($OutFile)) { throw 'Give -OutFile, or -InventoryOnly to write the inventory alone.' }

$result = Invoke-ExitCodeGap $UpstreamRoot $Version $RepositoryRoot $OutFile $InventoryOnly.IsPresent
if ($null -ne $result) {
    $c = $result.counts
    Write-Output "exitcodes: match $($c.match), gap $($c.gap), unmeasured $($c.unmeasured), excluded $($c.excluded), X/Y $($c.x)/$($c.y)"
}
