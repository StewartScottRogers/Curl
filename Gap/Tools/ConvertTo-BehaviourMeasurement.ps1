<#
.SYNOPSIS
    Turns the raw case outcomes of Gap/Tools/Measure-UpstreamCases.cs into the behaviour
    area measurement: every upstream tests/data case gets one state and, where the state
    needs one, a reason.

.DESCRIPTION
    Area behaviour of the gap analysis office (ADR-0433 decision 2). Formats are in
    Gap/Instructions/Gap-Format.md. The item key is behaviour:test<N>.

    A Passed case is match. A Failed case is gap: actual is the harness's first
    difference, expected is "upstream test<N> passes", and attributes.keywords holds the
    case's <keywords> so the analyst can group gaps by cause.

    A Skipped case is classified from the case file's <client><tool>, <client><server>,
    <client><features>, <client><command type="perl">, <client><precheck> and
    <info><keywords> parts (upstream's docs/tests/FILEFORMAT.md;
    Curl.Conformance.UnitLibrary/UpstreamTestCaseParser.cs reads them the same way). The
    first rule that fits wins, in this order:
      0. the harness's reason is "disabled
         upstream" (the release's
         tests/data/DISABLED lists the case,
         BL-2027)                             -> excluded, disabled-upstream
      1. <tool> names a lib... program        -> excluded, libcurl-api
      2. <tool> names a unit... program       -> excluded, libcurl-unit-test
     2a. <command type="perl"> never names
         %CURL: a script over upstream's own
         source, documents or test scripts
         (BL-2160, ADR-0490)                  -> excluded, upstream-source-check
      3. <features> needs Debug, TrackMemory
         or unittest                          -> excluded, debug-build-only
     3a. measured on Windows, <features>
         lists !win32 or the <precheck>
         refuses MSWin32                      -> excluded, platform:unix
         measured elsewhere, <features>
         lists win32                          -> excluded, platform:windows
      4. <features> needs a name absent from
         -ReferenceFeatures (when given)      -> excluded, reference-lacks:<name>
      5. the harness's reason says it does
         not emulate the <name> server        -> unmeasured, needs-server:<name>
         or has no value for a %<NAME>PORT
         variable (the first one it names,
         %NOLISTENPORT aside): no server
         listens there (BL-2233)              -> unmeasured, needs-server:<name>,
                                                 lower case
      6. the harness's reason says it has no
         value for %PERL alone (no perl was
         found, BL-1839)                      -> unmeasured, no-perl
         for any other variable (or %PERL
         with others)                         -> unmeasured, unknown-variable
      7. anything else                        -> unmeasured, harness-unsupported
    A feature written !<name> asks for its absence and never fits rules 3 or 4 (rule 3a reads
    !win32 as "not on Windows"). A perl command that names %CURL runs curl and is not rule
    2a's: it falls through to the later rules. Feature
    names compare case-insensitively. Rules 5 to 7 read only the harness's own reason, never
    the case's <server> tag: the harness runs most of upstream's servers, so a case that names
    one and is skipped is skipped for something else, which its reason says (BL-2233). Every
    skipped item's evidence is the harness's reason.

    The measurement adds "reasons": the count of items per reason, sorted by reason, for the
    scorecard's "Unmeasured by reason" table. Its values add up to unmeasured + excluded.

    Uses no Windows-only API. Runs under Windows PowerShell 5.1, PowerShell 7 and pwsh on
    Linux. The script is ASCII only.

.PARAMETER RawFile
    The JSON Gap/Tools/Measure-UpstreamCases.cs wrote. Its commit is the measurement's
    candidateCommit (all zeros when it is "unknown"), its platform Windows or Unix.

.PARAMETER TestsData
    The release's tests/data folder, read for each case's tags. A case whose file is
    missing has no tags.

.PARAMETER ReferenceFeatures
    The names on the matched reference build's curl -V Features: and Protocols: lines,
    passed by the gap run's orchestrator. Empty means no reference: rule 4 is skipped,
    reference is null and referenceFallback is "docs" (the release's own test files
    are what is compared against).

.PARAMETER Reference
    The first line of the reference build's curl --version, written as the measurement's
    reference when -ReferenceFeatures is given. Default: "reference curl".

.PARAMETER Version
    The targeted release version. Default: the name of the folder two levels above
    -TestsData (<cache>/<version>/tests/data).

.PARAMETER OutFile
    Where to write the area measurement, normally <run>/measurements/behaviour.json.
    Required unless -SelfTest is given.

.PARAMETER SelfTest
    Converts Gap/Tools/Fixtures/behaviour/raw.json against the fake cases in
    Gap/Tools/Fixtures/behaviour/tests/data, prints a PASS or FAIL line per check, and
    exits 1 on any FAIL.

.EXAMPLE
    Gap/Tools/ConvertTo-BehaviourMeasurement.ps1 -RawFile C:\Temp\raw.json -TestsData $cache\8.21.0\tests\data -OutFile ..\Curl.gap\2026-10-09_1430\measurements\behaviour.json

.EXAMPLE
    Gap/Tools/ConvertTo-BehaviourMeasurement.ps1 -SelfTest
#>
[CmdletBinding()]
param(
    [string] $RawFile,
    [string] $TestsData,
    [string[]] $ReferenceFeatures = @(),
    [string] $Reference,
    [string] $Version,
    [string] $OutFile,
    [switch] $SelfTest
)

$ErrorActionPreference = 'Stop'

$DebugFeatures = @('Debug', 'TrackMemory', 'unittest')

function Write-Utf8File([string] $Path, [string] $Text) {
    $folder = Split-Path $Path -Parent
    if ($folder -and -not (Test-Path -LiteralPath $folder)) { New-Item -ItemType Directory -Path $folder -Force | Out-Null }
    [System.IO.File]::WriteAllText($Path, $Text, (New-Object System.Text.UTF8Encoding($false)))
}

# The non-blank, non-comment lines inside the first <Name ...>...</Name> of Text.
function Get-PartLines([string] $Text, [string] $Name) {
    $m = [regex]::Match($Text, "<$Name(?:\s[^>]*)?>(.*?)</$Name>", [System.Text.RegularExpressions.RegexOptions]::Singleline)
    if (-not $m.Success) { return @() }
    return @($m.Groups[1].Value -split "`r?`n" | ForEach-Object { $_.Trim() } | Where-Object { $_ -and -not $_.StartsWith('#') })
}

function Read-CaseTags([string] $Folder, [int] $Number) {
    $path = Join-Path $Folder "test$Number"
    $tags = [pscustomobject]@{ Tool = $null; Features = @(); Keywords = @(); PerlCommand = $null; Precheck = '' }
    if (-not (Test-Path -LiteralPath $path)) { return $tags }
    # Latin-1 keeps every byte as one character, so binary test data cannot break the read.
    $text = [System.IO.File]::ReadAllText($path, [System.Text.Encoding]::GetEncoding(28591))
    $client = [regex]::Match($text, '<client>(.*?)</client>', [System.Text.RegularExpressions.RegexOptions]::Singleline)
    $clientText = if ($client.Success) { $client.Groups[1].Value } else { '' }
    $tool = @(Get-PartLines $clientText 'tool')
    $tags.Tool = if ($tool.Count -gt 0) { $tool[0] } else { $null }
    $tags.Features = @(Get-PartLines $clientText 'features')
    $tags.Keywords = @(Get-PartLines $text 'keywords')
    $perl = [regex]::Match($clientText, '<command\s[^>]*type="perl"[^>]*>(.*?)</command>', [System.Text.RegularExpressions.RegexOptions]::Singleline)
    if ($perl.Success) { $tags.PerlCommand = $perl.Groups[1].Value.Trim() }
    $tags.Precheck = (@(Get-PartLines $clientText 'precheck') -join "`n")
    return $tags
}

# The server rule 5 reads from the harness's reason, or $null: the <name> of "the harness does
# not emulate the <name> server", else the first %<NAME>PORT of "the harness has no value for
# ..." in lower case, %NOLISTENPORT aside (a port nothing listens on by design).
function Get-MissingServer([string] $Detail) {
    $emulate = [regex]::Match($Detail, '^the harness does not emulate the (\S+) server')
    if ($emulate.Success) { return $emulate.Groups[1].Value }
    if ($Detail -notlike 'the harness has no value for *') { return $null }
    foreach ($m in [regex]::Matches($Detail, '%([A-Z0-9_]+)PORT\b')) {
        if ($m.Groups[1].Value -ne 'NOLISTEN') { return $m.Groups[1].Value.ToLowerInvariant() }
    }
    return $null
}

# The state and reason of a skipped case, by rules 0 to 7 in order.
function Get-SkippedVerdict($Tags, [string] $Detail, [string[]] $Available, [bool] $OnWindows) {
    if ($Detail -eq 'disabled upstream') { return @('excluded', 'disabled-upstream') }
    if ($Tags.Tool -like 'lib*') { return @('excluded', 'libcurl-api') }
    if ($Tags.Tool -like 'unit*') { return @('excluded', 'libcurl-unit-test') }
    if ($null -ne $Tags.PerlCommand -and $Tags.PerlCommand -notmatch '%CURL\b') { return @('excluded', 'upstream-source-check') }
    $needed = @($Tags.Features | Where-Object { -not $_.StartsWith('!') })
    foreach ($feature in $needed) {
        if ($DebugFeatures -contains $feature) { return @('excluded', 'debug-build-only') }
    }
    if ($OnWindows) {
        if ($Tags.Features -contains '!win32' -or $Tags.Precheck -match "\`$\^O eq 'MSWin32'") { return @('excluded', 'platform:unix') }
    } elseif ($needed -contains 'win32') {
        return @('excluded', 'platform:windows')
    }
    if ($Available.Count -gt 0) {
        foreach ($feature in $needed) {
            if (-not ($Available -contains $feature)) { return @('excluded', "reference-lacks:$feature") }
        }
    }
    $server = Get-MissingServer $Detail
    if ($server) { return @('unmeasured', "needs-server:$server") }
    if ($Detail -eq 'the harness has no value for %PERL') { return @('unmeasured', 'no-perl') }
    if ($Detail -like 'the harness has no value for *') { return @('unmeasured', 'unknown-variable') }
    return @('unmeasured', 'harness-unsupported')
}

function Convert-Case($Case, [string] $Folder, [string[]] $Available, [bool] $OnWindows) {
    $number = [int]$Case.number
    $tags = Read-CaseTags $Folder $number
    $item = [ordered]@{
        key = "behaviour:test$number"; state = $null; reason = $null; expected = $null; actual = $null
        evidence = "tests/data/test$number"; introducedIn = $null; attributes = [ordered]@{ keywords = @($tags.Keywords) }
    }
    switch ($Case.kind) {
        'Passed' { $item.state = 'match'; $item.expected = "upstream test$number passes"; $item.actual = 'passes' }
        'Failed' { $item.state = 'gap'; $item.expected = "upstream test$number passes"; $item.actual = [string]$Case.detail }
        'Skipped' {
            $verdict = Get-SkippedVerdict $tags ([string]$Case.detail) $Available $OnWindows
            $item.state = $verdict[0]; $item.reason = $verdict[1]; $item.evidence = [string]$Case.detail
        }
        default { throw "Case $number has the unknown kind '$($Case.kind)'." }
    }
    return $item
}

function Get-Platform([string] $RawPlatform) {
    if ($RawPlatform -eq 'Windows') { return 'windows' }
    if ((Get-Command uname -ErrorAction SilentlyContinue) -and (& uname) -eq 'Darwin') { return 'macos' }
    return 'linux'
}

function ConvertTo-BehaviourMeasurement($Raw, [string] $Folder, [string[]] $Available, [string] $ReferenceLine, [string] $ReleaseVersion) {
    $available = @($Available | Where-Object { $_ })
    $onWindows = [string]$Raw.platform -eq 'Windows'
    $items = @($Raw.cases | ForEach-Object { Convert-Case $_ $Folder $available $onWindows })
    $sortedKeys = [string[]]($items | ForEach-Object { $_.key })
    [Array]::Sort($sortedKeys, [StringComparer]::Ordinal)
    $byKey = @{}; foreach ($i in $items) { $byKey[$i.key] = $i }
    $items = @($sortedKeys | ForEach-Object { $byKey[$_] })
    $counts = [ordered]@{}
    foreach ($state in 'match', 'gap', 'unmeasured', 'excluded') { $counts[$state] = @($items | Where-Object { $_.state -eq $state }).Count }
    $counts.x = $counts.match
    $counts.y = $counts.match + $counts.gap + $counts.unmeasured
    $reasonNames = [string[]]@($items | Where-Object { $_.reason } | ForEach-Object { $_.reason } | Select-Object -Unique)
    [Array]::Sort($reasonNames, [StringComparer]::Ordinal)
    $reasons = [ordered]@{}
    foreach ($name in $reasonNames) { $reasons[$name] = @($items | Where-Object { $_.reason -eq $name }).Count }
    $commit = if ([string]$Raw.commit -match '^[0-9a-f]{40}$') { [string]$Raw.commit } else { '0' * 40 }
    $measurement = [ordered]@{
        area = 'behaviour'
        targetVersion = $ReleaseVersion
        candidateCommit = $commit
        platform = Get-Platform ([string]$Raw.platform)
        reference = $null
    }
    if ($available.Count -gt 0) {
        $measurement.reference = if ($ReferenceLine) { $ReferenceLine } else { 'reference curl' }
    } else {
        $measurement.referenceFallback = 'docs'
    }
    $measurement.measuredAt = [DateTime]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ssZ')
    $measurement.items = $items
    $measurement.counts = $counts
    $measurement.reasons = $reasons
    return $measurement
}

function Invoke-Conversion([string] $Raw, [string] $Folder, [string[]] $Available, [string] $ReferenceLine, [string] $ReleaseVersion, [string] $Out) {
    $rawObject = [System.IO.File]::ReadAllText($Raw) | ConvertFrom-Json
    $measurement = ConvertTo-BehaviourMeasurement $rawObject $Folder $Available $ReferenceLine $ReleaseVersion
    Write-Utf8File $Out ($measurement | ConvertTo-Json -Depth 8)
    return $measurement
}

function Invoke-SelfTest {
    $script:failed = $false
    function Report([bool] $Passed, [string] $Check) {
        if ($Passed) { Write-Output "PASS $Check" } else { Write-Output "FAIL $Check"; $script:failed = $true }
    }
    $fixtures = Join-Path $PSScriptRoot 'Fixtures/behaviour'
    $raw = Join-Path $fixtures 'raw.json'
    $data = Join-Path $fixtures 'tests/data'
    $temp = Join-Path ([System.IO.Path]::GetTempPath()) ("behaviour-selftest-" + [guid]::NewGuid().ToString('N'))
    try {
        $out = Join-Path $temp 'behaviour.json'
        Invoke-Conversion $raw $data @() $null '9.9.9' $out | Out-Null
        $m = [System.IO.File]::ReadAllText($out) | ConvertFrom-Json
        $s = @{}; foreach ($i in $m.items) { $s[$i.key] = $i }
        function Is($Key, $State, $Reason) { return ($s[$Key].state -eq $State -and $s[$Key].reason -eq $Reason) }

        Report ((Is 'behaviour:test15' 'excluded' 'disabled-upstream') -and $s['behaviour:test15'].evidence -eq 'disabled upstream') 'rule 0: a case upstream lists in DISABLED gives excluded disabled-upstream'
        Report (Is 'behaviour:test1' 'excluded' 'libcurl-api') 'rule 1: a lib... tool gives excluded libcurl-api'
        Report (Is 'behaviour:test2' 'excluded' 'libcurl-unit-test') 'rule 2: a unit... tool gives excluded libcurl-unit-test'
        Report (Is 'behaviour:test3' 'excluded' 'debug-build-only') 'rule 3: a Debug, TrackMemory or unittest feature gives excluded debug-build-only'
        Report (Is 'behaviour:test5' 'unmeasured' 'needs-server:ftp') 'rule 5: no value for %<NAME>PORT gives unmeasured needs-server:<name>'
        Report (Is 'behaviour:test12' 'unmeasured' 'needs-server:https') 'rule 5: the port variable''s name, lower case, is the server'
        Report (Is 'behaviour:test21' 'unmeasured' 'needs-server:http/2') 'rule 5: the harness not emulating the <name> server gives needs-server:<name>'
        Report ((Is 'behaviour:test22' 'unmeasured' 'harness-unsupported') -and $s['behaviour:test22'].evidence -eq 'the harness does not act on <client><stdout>') 'rule 5: a case naming a server but skipped for something else carries the harness''s reason, not needs-server'
        Report (Is 'behaviour:test6' 'unmeasured' 'unknown-variable') 'rule 6: a harness reason naming a variable gives unmeasured unknown-variable'
        Report (Is 'behaviour:test13' 'unmeasured' 'no-perl') 'rule 6: no value for %PERL alone gives unmeasured no-perl'
        Report (Is 'behaviour:test14' 'unmeasured' 'unknown-variable') 'rule 6: no value for %PERL with another variable stays unknown-variable'
        Report ((Is 'behaviour:test7' 'unmeasured' 'harness-unsupported') -and $s['behaviour:test7'].evidence -eq 'the harness does not act on <client><setenv>') 'rule 7: anything else gives unmeasured harness-unsupported with the harness reason as evidence'
        Report (Is 'behaviour:test8' 'match' $null) 'a passed case gives match'
        Report ((Is 'behaviour:test9' 'gap' $null) -and $s['behaviour:test9'].actual -eq 'stdout differs at byte 4' -and (@($s['behaviour:test9'].attributes.keywords) -join ',') -eq 'HTTP,HTTP GET') 'a failed case gives gap with its first difference and keywords'
        Report (Is 'behaviour:test10' 'excluded' 'libcurl-api') 'the rules apply in order: a lib tool on an ftp server is libcurl-api'
        Report (Is 'behaviour:test11' 'unmeasured' 'unknown-variable') 'a !Debug feature asks for its absence and is not debug-build-only'
        Report (Is 'behaviour:test4' 'unmeasured' 'needs-server:http3') 'with no reference, rule 4 is skipped'
        Report (Is 'behaviour:test16' 'excluded' 'upstream-source-check') 'rule 2a: a perl command that never names %CURL gives excluded upstream-source-check'
        Report (Is 'behaviour:test17' 'unmeasured' 'harness-unsupported') 'rule 2a: a perl command that runs %CURL is not a source check'
        Report (Is 'behaviour:test18' 'excluded' 'platform:unix') 'rule 3a: on Windows, a !win32 feature gives excluded platform:unix'
        Report (Is 'behaviour:test19' 'excluded' 'platform:unix') 'rule 3a: on Windows, a precheck refusing MSWin32 gives excluded platform:unix'
        Report (Is 'behaviour:test20' 'unmeasured' 'harness-unsupported') 'rule 3a: on Windows, a win32 feature is not platform-excluded'
        Report ($null -eq $m.reference -and $m.referenceFallback -eq 'docs' -and $m.targetVersion -eq '9.9.9') 'with no reference, reference is null with the docs fallback'
        $c = $m.counts
        $reasonTotal = 0; foreach ($p in $m.reasons.PSObject.Properties) { $reasonTotal += [int]$p.Value }
        Report ($c.x -eq $c.match -and $c.y -eq ($c.match + $c.gap + $c.unmeasured) -and ($c.match + $c.gap + $c.unmeasured + $c.excluded) -eq @($m.items).Count -and @($m.items).Count -eq 22) 'counts add up to the case count'
        Report ($reasonTotal -eq ($c.unmeasured + $c.excluded) -and $m.reasons.'libcurl-api' -eq 2 -and $m.reasons.'unknown-variable' -eq 3 -and $m.reasons.'no-perl' -eq 1) 'the per-reason summary adds up to unmeasured plus excluded'

        Invoke-Conversion $raw $data @('http', 'IPv6') 'curl 9.9.9 (fake) libcurl/9.9.9' '9.9.9' $out | Out-Null
        $r = [System.IO.File]::ReadAllText($out) | ConvertFrom-Json
        $t = @{}; foreach ($i in $r.items) { $t[$i.key] = $i }
        Report ($t['behaviour:test4'].state -eq 'excluded' -and $t['behaviour:test4'].reason -eq 'reference-lacks:HTTP3') 'rule 4: a feature the reference lacks gives excluded reference-lacks:<name>'
        Report ($r.reference -eq 'curl 9.9.9 (fake) libcurl/9.9.9' -and $null -eq $r.PSObject.Properties['referenceFallback']) 'with a reference, reference is its version line'

        $unixRaw = [System.IO.File]::ReadAllText($raw) | ConvertFrom-Json
        $unixRaw.platform = 'Unix'
        $u = @{}; foreach ($i in (ConvertTo-BehaviourMeasurement $unixRaw $data @() $null '9.9.9').items) { $u[$i.key] = $i }
        Report ($u['behaviour:test20'].state -eq 'excluded' -and $u['behaviour:test20'].reason -eq 'platform:windows') 'rule 3a: off Windows, a win32 feature gives excluded platform:windows'
        Report ($u['behaviour:test18'].reason -eq 'harness-unsupported' -and $u['behaviour:test19'].reason -eq 'harness-unsupported') 'rule 3a: off Windows, !win32 and a precheck refusing MSWin32 are not platform-excluded'
    } finally {
        if (Test-Path -LiteralPath $temp) { Remove-Item -LiteralPath $temp -Recurse -Force }
    }
    if ($script:failed) { exit 1 }
}

if ($SelfTest) { Invoke-SelfTest; return }

if ([string]::IsNullOrEmpty($RawFile) -or [string]::IsNullOrEmpty($TestsData) -or [string]::IsNullOrEmpty($OutFile)) {
    throw 'Give -RawFile, -TestsData and -OutFile, or -SelfTest.'
}
if ([string]::IsNullOrEmpty($Version)) { $Version = Split-Path (Split-Path (Split-Path ([System.IO.Path]::GetFullPath($TestsData)) -Parent) -Parent) -Leaf }

$result = Invoke-Conversion $RawFile $TestsData $ReferenceFeatures $Reference $Version $OutFile
$c = $result.counts
Write-Output "behaviour: match $($c.match), gap $($c.gap), unmeasured $($c.unmeasured), excluded $($c.excluded), X/Y $($c.x)/$($c.y)"
foreach ($name in $result.reasons.Keys) { Write-Output "  $name $($result.reasons[$name])" }
