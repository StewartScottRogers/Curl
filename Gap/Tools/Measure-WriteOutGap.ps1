<#
.SYNOPSIS
    Builds the writeout upstream inventory from a curl release's
    docs/cmdline-opts/write-out.md and measures every --write-out variable on
    Curl.Console against the matched reference curl.
.DESCRIPTION
    ADR-0433 decision 2, area writeout. Formats are in Gap/Instructions/Gap-Format.md.

    Inventory: write-out.md lists the variables as "## `<name>`" headings after the line
    "The variables available are:", and ends the list with a bare "##" heading, after
    which the TIME OUTPUT FORMAT section's "## `%a`" headings are strftime conversions,
    not variables. A heading whose name holds braces, such as "## `header{name}`", is a
    function-style form: its item is %<form>{} (key writeout:%header{}, attribute name
    header). introducedIn is the version in the section's first "(Added in X)", or null.
    The inventory is written to Gap/Upstream/<version>/writeout.json.

    Probing: every item runs
        -s -o <null device> -w <format> <file URL of a small temporary file>
    through both binaries with Invoke-GapProbe, where <format> is %{<name>} for a plain
    variable and, for a function-style form, %header{content-type}, %output{<scratch>/output.txt},
    %time{%Y}, or %<form>{x} for any other. The file URL is built for the platform the
    tool runs on (file:///C:/... on Windows), percent-encoded.

    Facets, one item key each:
      writeout:<name>        recognition: match when both binaries agree on whether stderr
                             carries the reference's unknown-variable warning, gap
                             otherwise. The warning was measured on curl 8.21.0
                             (Schannel, Git for Windows) and is pinned in
                             Gap/Tools/Fixtures/writeout/reference-unknown-variable.txt;
                             its prefix up to the quoted name is what is looked for.
      writeout:<name>:value  match when stdout is byte-equal, gap otherwise. Not measured
                             for a volatile variable.

    Volatile variables, whose value differs between any two runs, have no value facet.
    The list is Gap/Tools/Fixtures/writeout/volatile-variables.txt, decided from
    write-out.md's own descriptions: %time{} (the current time), json (holds the
    times and speeds), local_port (an ephemeral port), speed_download and speed_upload
    (average speeds), and time_appconnect, time_connect, time_namelookup,
    time_posttransfer, time_pretransfer, time_queue, time_redirect, time_starttransfer
    and time_total (elapsed seconds).

    With no matching reference, recognition falls back to the documents: every documented
    variable is expected to be recognised, so the facet is gap when Curl's stderr carries
    the warning and match otherwise, and the value facet is unmeasured with reason
    no-reference. The measurement records referenceFallback "docs".

    ASCII only; runs under Windows PowerShell 5.1 and PowerShell 7. -InventoryOnly uses
    no Windows-only API and runs under pwsh on Linux with neither curl present.
.PARAMETER UpstreamRoot
    The release folder holding docs/cmdline-opts/write-out.md. Default: the folder
    Gap/Tools/Get-UpstreamRelease.ps1 prints for -Version.
.PARAMETER Version
    The release version. Default: the target in Gap/Baselines/target.json.
.PARAMETER Candidate
    The Curl.Console binary to measure. Default: Get-GapCandidateCurl.
.PARAMETER OutFile
    Where to write the measurement. Default: standard output.
.PARAMETER InventoryOnly
    Write only the inventory to Gap/Upstream/<version>/writeout.json and probe nothing.
.PARAMETER ProbeResults
    A JSON file of canned answers used instead of running any binary: { "reference":
    <version line or null>, "answers": { "<format>": { "reference": { "exitCode",
    "stdout", "stderr" } or null, "candidate": { ... } } } }, keyed by the -w format.
.PARAMETER InventoryDirectory
    The folder the inventory is written under, as <InventoryDirectory>/<version>/writeout.json.
    Default: Gap/Upstream.
.PARAMETER SelfTest
    Runs the inventory and measurement against the fake write-out.md under
    Gap/Tools/Fixtures/writeout with canned probe results, prints a PASS or FAIL line per
    check and exits 1 on any FAIL.
.EXAMPLE
    Gap/Tools/Measure-WriteOutGap.ps1 -InventoryOnly
.EXAMPLE
    Gap/Tools/Measure-WriteOutGap.ps1 -OutFile $env:TEMP\writeout.json
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
$script:WriteOutToolsDirectory = $PSScriptRoot
$script:WriteOutRepositoryRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$script:WriteOutFixtures = Join-Path $PSScriptRoot 'Fixtures/writeout'
$script:RunSelfTest = $SelfTest.IsPresent

function Write-WriteOutJsonFile([string] $Path, $Value) {
    $directory = Split-Path $Path -Parent
    if ($directory -and -not (Test-Path -LiteralPath $directory)) { New-Item -ItemType Directory -Path $directory -Force | Out-Null }
    $json = ($Value | ConvertTo-Json -Depth 8) + "`n"
    [System.IO.File]::WriteAllText($Path, $json, (New-Object System.Text.UTF8Encoding($false)))
}

function Get-WriteOutInventory([string] $Root, [string] $ReleaseVersion) {
    $path = Join-Path (Join-Path (Join-Path $Root 'docs') 'cmdline-opts') 'write-out.md'
    $lines = [System.IO.File]::ReadAllLines($path)
    $sorted = New-Object 'System.Collections.Generic.SortedDictionary[string,object]' ([System.StringComparer]::Ordinal)
    $inList = $false
    $current = $null
    foreach ($line in $lines) {
        if (-not $inList) {
            if ($line.Trim() -ceq 'The variables available are:') { $inList = $true }
            continue
        }
        if ($line.Trim() -ceq '##') { break }
        if ($line -match '^## `([^`]+)`\s*$') {
            $heading = $Matches[1]
            if ($heading -match '^([A-Za-z_]+)\{[^}]*\}$') { $form = $Matches[1]; $name = "%$form{}"; $variable = $form }
            else { $name = $heading; $variable = $heading }
            $current = [pscustomobject] [ordered] @{ key = "writeout:$name"; name = $name; introducedIn = $null; attributes = [pscustomobject] [ordered] @{ name = $variable } }
            $sorted[$current.key] = $current
            continue
        }
        if ($null -ne $current -and $null -eq $current.introducedIn -and $line -match '\(Added in ([0-9][0-9.]*[0-9])\)') { $current.introducedIn = $Matches[1] }
    }
    return [pscustomobject] [ordered] @{ area = 'writeout'; version = $ReleaseVersion; sources = @('docs/cmdline-opts/write-out.md'); items = @($sorted.Values) }
}

function Get-WriteOutFormat($Item) {
    if (-not $Item.name.StartsWith('%')) { return '%{' + $Item.name + '}' }
    switch ($Item.attributes.name) {
        'header' { return '%header{content-type}' }
        'output' { return '%output{<scratch>/output.txt}' }
        'time' { return '%time{%Y}' }
        default { return '%' + $Item.attributes.name + '{x}' }
    }
}

function Get-WriteOutVolatileNames {
    return @([System.IO.File]::ReadAllLines((Join-Path $script:WriteOutFixtures 'volatile-variables.txt')) | Where-Object { $_.Trim() } | ForEach-Object { $_.Trim() })
}

function Get-WriteOutUnknownPrefix {
    $pinned = [System.IO.File]::ReadAllLines((Join-Path $script:WriteOutFixtures 'reference-unknown-variable.txt'))[0]
    return $pinned.Substring(0, $pinned.IndexOf("'"))
}

function Format-WriteOutText([string] $Text) {
    $builder = New-Object System.Text.StringBuilder
    foreach ($ch in $Text.ToCharArray()) {
        $code = [int] $ch
        if ($ch -eq "`n") { [void] $builder.Append('\n') }
        elseif ($ch -eq "`r") { [void] $builder.Append('\r') }
        elseif ($code -lt 32 -or $code -gt 126) { [void] $builder.Append(('\x{0:X2}' -f $code)) }
        else { [void] $builder.Append($ch) }
    }
    return $builder.ToString()
}

function Test-WriteOutUnknown($Answer) {
    return ([string] $Answer.stderr).Contains((Get-WriteOutUnknownPrefix))
}

function Format-WriteOutRecognition($Answer) {
    if (Test-WriteOutUnknown $Answer) { return 'unknown: ' + (([string] $Answer.stderr -replace "`r`n", "`n").TrimEnd() -replace "`n", ' | ') }
    return 'recognised'
}

function Measure-WriteOutItem($Item, [string] $Format, $Answer, [bool] $HasReference, [bool] $IsVolatile, [string] $Evidence) {
    $results = @()
    $actual = Format-WriteOutRecognition $Answer.candidate
    if ($HasReference) {
        $expected = Format-WriteOutRecognition $Answer.reference
        $state = if ((Test-WriteOutUnknown $Answer.reference) -eq (Test-WriteOutUnknown $Answer.candidate)) { 'match' } else { 'gap' }
    }
    else {
        $expected = 'recognised'
        $state = if (Test-WriteOutUnknown $Answer.candidate) { 'gap' } else { 'match' }
    }
    $results += [pscustomobject] [ordered] @{ key = $Item.key; state = $state; reason = $null; expected = $expected; actual = $actual; evidence = $Evidence; introducedIn = $Item.introducedIn }
    if ($IsVolatile) { return $results }
    $valueKey = "$($Item.key):value"
    $candidateOut = [string] $Answer.candidate.stdout
    if ($HasReference) {
        $referenceOut = [string] $Answer.reference.stdout
        $valueState = if ($referenceOut -ceq $candidateOut) { 'match' } else { 'gap' }
        $results += [pscustomobject] [ordered] @{ key = $valueKey; state = $valueState; reason = $null; expected = (Format-WriteOutText $referenceOut); actual = (Format-WriteOutText $candidateOut); evidence = $Evidence; introducedIn = $Item.introducedIn }
    }
    else {
        $results += [pscustomobject] [ordered] @{ key = $valueKey; state = 'unmeasured'; reason = 'no-reference'; expected = $null; actual = (Format-WriteOutText $candidateOut); evidence = $Evidence; introducedIn = $Item.introducedIn }
    }
    return $results
}

function Get-WriteOutCounts($Items) {
    $c = [ordered] @{ match = 0; gap = 0; unmeasured = 0; excluded = 0; x = 0; y = 0 }
    foreach ($item in $Items) { $c[$item.state]++ }
    $c.x = $c.match
    $c.y = $c.match + $c.gap + $c.unmeasured
    return [pscustomobject] $c
}

function ConvertTo-WriteOutAnswer($Run) {
    if ($null -eq $Run) { return $null }
    # ISO-8859-1 maps every byte to one character, so string equality is byte equality.
    $stdout = [System.Text.Encoding]::GetEncoding(28591).GetString($Run.Stdout)
    return [pscustomobject] @{ exitCode = $Run.ExitCode; stdout = $stdout; stderr = $Run.Stderr }
}

function Measure-WriteOutGap($Inventory, [string] $TargetVersion, [string] $CandidatePath, [string] $ProbeResultsPath, [string] $CandidateCommit) {
    $canned = $null
    $referenceLine = $null
    $onWindows = [System.IO.Path]::DirectorySeparatorChar -eq '\'
    $nullDevice = if ($onWindows) { 'NUL' } else { '/dev/null' }
    $url = '<url>'
    if ($ProbeResultsPath) {
        $canned = Get-Content -LiteralPath $ProbeResultsPath -Raw | ConvertFrom-Json
        $referenceLine = $canned.reference
    }
    else {
        . (Join-Path $script:WriteOutToolsDirectory 'Invoke-GapProbe.ps1')
        $reference = Get-GapReferenceCurl -TargetVersion $TargetVersion
        if ($null -ne $reference -and $reference.Matches) { $referenceLine = $reference.VersionLine.TrimEnd() }
        $CandidatePath = Get-GapCandidateCurl -Path $CandidatePath
        $scratch = Join-Path ([System.IO.Path]::GetTempPath()) ('gap-writeout-' + [guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $scratch -Force | Out-Null
        $source = Join-Path $scratch 'source.txt'
        [System.IO.File]::WriteAllText($source, "x`n")
        $url = (New-Object System.Uri($source)).AbsoluteUri
    }
    $hasReference = $null -ne $referenceLine
    $volatile = Get-WriteOutVolatileNames
    $items = New-Object 'System.Collections.Generic.SortedDictionary[string,object]' ([System.StringComparer]::Ordinal)
    try {
        foreach ($item in $Inventory.items) {
            $format = Get-WriteOutFormat $item
            if ($null -ne $canned) {
                $answer = $canned.answers.$format
                if ($null -eq $answer) { throw "No canned probe result for: $format" }
                $evidence = "curl -s -o $nullDevice -w $format $url"
            }
            else {
                $actualFormat = $format.Replace('<scratch>', ($scratch -replace '\\', '/'))
                $run = Invoke-GapProbe -Arguments @('-s', '-o', $nullDevice, '-w', $actualFormat, $url) -CandidatePath $CandidatePath -TargetVersion $TargetVersion -WorkingDirectory $scratch -TimeoutSeconds 20
                $answer = [pscustomobject] @{ reference = (ConvertTo-WriteOutAnswer $run.Reference); candidate = (ConvertTo-WriteOutAnswer $run.Candidate) }
                $evidence = "curl -s -o $nullDevice -w $format <file URL of a temporary file>"
            }
            foreach ($result in Measure-WriteOutItem $item $format $answer $hasReference ($volatile -ccontains $item.name) $evidence) { $items[$result.key] = $result }
        }
    }
    finally {
        if ($null -eq $canned) { Remove-Item -LiteralPath $scratch -Recurse -Force -ErrorAction SilentlyContinue }
    }
    $list = @($items.Values)
    $platform = if ($onWindows) { 'windows' } elseif (Test-Path '/System/Library') { 'macos' } else { 'linux' }
    $measurement = [ordered] @{ area = 'writeout'; targetVersion = $TargetVersion; candidateCommit = $CandidateCommit; platform = $platform; reference = $referenceLine }
    if (-not $hasReference) { $measurement.referenceFallback = 'docs' }
    $measurement.measuredAt = [DateTime]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ssZ')
    $measurement.items = $list
    $measurement.counts = Get-WriteOutCounts $list
    return [pscustomobject] $measurement
}

function Invoke-WriteOutGapSelfTest {
    $release = Join-Path $script:WriteOutFixtures 'upstream'
    $lines = New-Object System.Collections.Generic.List[string]
    $report = { param([string] $Name, [bool] $Passed, [string] $Detail) if ($Passed) { $lines.Add("PASS $Name") } else { $lines.Add("FAIL $Name ($Detail)") } }

    $inventory = Get-WriteOutInventory $release '9.9.9'
    $keys = @($inventory.items | ForEach-Object { $_.key }) -join ','
    & $report 'plain and function-style variables are both inventoried, and nothing outside the list' ($keys -ceq 'writeout:%header{},writeout:content_type,writeout:http_code,writeout:time_total,writeout:url') $keys
    $byName = @{}; foreach ($i in $inventory.items) { $byName[$i.name] = $i }
    $attributesOk = $byName['%header{}'].attributes.name -ceq 'header' -and $byName['%header{}'].introducedIn -ceq '7.84.0' -and
        $byName['time_total'].introducedIn -ceq '7.9.7' -and $null -eq $byName['content_type'].introducedIn -and $byName['url'].attributes.name -ceq 'url'
    & $report 'introducedIn comes from (Added in X) and the name attribute is the variable inside %{...}' $attributesOk ($inventory | ConvertTo-Json -Depth 6 -Compress)

    $measured = Measure-WriteOutGap $inventory '9.9.9' 'unused' (Join-Path $script:WriteOutFixtures 'probe-results.json') '0000000000000000000000000000000000000000'
    $byKey = @{}; foreach ($i in $measured.items) { $byKey[$i.key] = $i }
    & $report 'agreement on recognition gives match' ($byKey['writeout:content_type'].state -ceq 'match' -and $byKey['writeout:%header{}'].state -ceq 'match' -and $byKey['writeout:http_code'].state -ceq 'match') $byKey['writeout:http_code'].state
    $url = $byKey['writeout:url']
    & $report 'Curl reporting a variable unknown that the reference knows gives gap' ($url.state -ceq 'gap' -and $url.expected -ceq 'recognised' -and $url.actual -like 'unknown: *') "$($url.state) $($url.actual)"
    & $report 'a volatile variable has no value facet' (-not $byKey.ContainsKey('writeout:time_total:value') -and $byKey['writeout:time_total'].state -ceq 'match') (@($byKey.Keys) -join ',')
    $code = $byKey['writeout:http_code:value']
    & $report 'differing stdout on a non-volatile variable gives a value gap' ($code.state -ceq 'gap' -and $code.expected -ceq '000' -and $code.actual -ceq '0' -and $byKey['writeout:content_type:value'].state -ceq 'match') "$($code.state) $($code.expected)/$($code.actual)"
    $hasFallback = @($measured.PSObject.Properties.Name) -contains 'referenceFallback'
    $c = $measured.counts
    & $report 'a matching reference is recorded, no referenceFallback, counts follow the formula' ($measured.reference -ceq 'curl 9.9.9 (fixture) libcurl/9.9.9' -and -not $hasFallback -and $c.match -eq 6 -and $c.gap -eq 3 -and $c.x -eq 6 -and $c.y -eq 9) ($c | ConvertTo-Json -Compress)

    $docs = Measure-WriteOutGap $inventory '9.9.9' 'unused' (Join-Path $script:WriteOutFixtures 'probe-results-docs.json') '0000000000000000000000000000000000000000'
    $docsByKey = @{}; foreach ($i in $docs.items) { $docsByKey[$i.key] = $i }
    $value = $docsByKey['writeout:http_code:value']
    & $report 'with no reference the value facet is unmeasured with no-reference' ($null -eq $docs.reference -and $docs.referenceFallback -ceq 'docs' -and $value.state -ceq 'unmeasured' -and $value.reason -ceq 'no-reference') ($value | ConvertTo-Json -Compress)
    & $report 'with no reference a documented variable Curl reports unknown is a gap' ($docsByKey['writeout:url'].state -ceq 'gap' -and $docsByKey['writeout:content_type'].state -ceq 'match') $docsByKey['writeout:url'].state

    $scratch = Join-Path ([System.IO.Path]::GetTempPath()) ('gap-writeout-test-' + [guid]::NewGuid().ToString('N'))
    try {
        $path = Join-Path $scratch '9.9.9/writeout.json'
        Write-WriteOutJsonFile $path $inventory
        $bytes = [System.IO.File]::ReadAllBytes($path)
        $back = [System.Text.Encoding]::UTF8.GetString($bytes) | ConvertFrom-Json
        $ascii = @($bytes | Where-Object { $_ -gt 127 }).Count -eq 0
        & $report 'the inventory file is ASCII JSON without a byte order mark and parses back' ($ascii -and $bytes[0] -eq 123 -and @($back.items).Count -eq 5 -and $back.area -ceq 'writeout') "$($bytes[0])"
    }
    finally { Remove-Item -LiteralPath $scratch -Recurse -Force -ErrorAction SilentlyContinue }
    return $lines
}

if ($script:RunSelfTest) {
    $lines = @(Invoke-WriteOutGapSelfTest)
    $lines | ForEach-Object { Write-Output $_ }
    if (@($lines | Where-Object { $_ -like 'FAIL *' }).Count -gt 0) { exit 1 }
    exit 0
}
if (-not $Version) { $Version = (Get-Content -LiteralPath (Join-Path $script:WriteOutRepositoryRoot 'Gap/Baselines/target.json') -Raw | ConvertFrom-Json).version }
if (-not $UpstreamRoot) { $UpstreamRoot = (& (Join-Path $script:WriteOutToolsDirectory 'Get-UpstreamRelease.ps1') -Version $Version | Select-Object -Last 1) }
if (-not $InventoryDirectory) { $InventoryDirectory = Join-Path $script:WriteOutRepositoryRoot 'Gap/Upstream' }
$inventory = Get-WriteOutInventory $UpstreamRoot $Version
Write-WriteOutJsonFile (Join-Path (Join-Path $InventoryDirectory $Version) 'writeout.json') $inventory
if ($InventoryOnly) { exit 0 }
$commit = (& git -C $script:WriteOutRepositoryRoot rev-parse HEAD).Trim()
$measurement = Measure-WriteOutGap $inventory $Version $Candidate $ProbeResults $commit
if ($OutFile) { Write-WriteOutJsonFile $OutFile $measurement } else { $measurement | ConvertTo-Json -Depth 8 }
