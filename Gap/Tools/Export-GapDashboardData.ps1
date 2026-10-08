<#
.SYNOPSIS
    Writes the gap dashboard's data.json from the gap office's history, findings and baselines.

.DESCRIPTION
    Reads Gap/Scorecards/history.json, every Gap/Findings/GF-*.md and
    Gap/Baselines/target.json and newest.json, and writes the one JSON file the gap dashboard
    page reads, in the shape Gap/Instructions/Gap-Format.md section 9 ("Dashboard data")
    defines (ADR-0433 decision 8, BL-1733).

    - open: every "status: open" finding, sorted by severity (Critical, High, Medium, Low),
      then scope (target before newest), then ID.
    - closed: the findings whose "closed" run stamp is one of the last five runs in
      history.json, in the same order.
    - regressions: the open findings with "regression: true", in the same order.
    - release: { version, newGaps } when newest.json names a version above target.json's,
      newGaps counting the open "scope: newest" findings introduced in that version;
      otherwise null.
    - With no history.json, or an empty one, latest is null and history is [].

    Rejected findings appear in no list. The output is written by this script's own JSON
    writer, not ConvertTo-Json, so Windows PowerShell 5.1 and PowerShell 7 write the same
    bytes: UTF-8 without a byte order mark, LF line endings, two-space indents, ASCII only.
    The same inputs give byte-identical output apart from "generated".

    Runs under Windows PowerShell 5.1 and PowerShell 7 on Windows, Linux and macOS; it uses
    no Windows-only API.

.PARAMETER FindingsDirectory
    The folder holding the GF-*.md findings. Default: Gap/Findings beside this script's
    folder.

.PARAMETER ScorecardsDirectory
    The folder holding history.json. Default: Gap/Scorecards. A missing folder or file means
    no run has happened yet.

.PARAMETER BaselinesDirectory
    The folder holding target.json and newest.json. Default: Gap/Baselines. When either file
    is missing, its version comes from the last history entry, or is null.

.PARAMETER RepositoryUrl
    The GitHub repository each finding's url points into. Default:
    https://github.com/StewartScottRogers/Curl.

.PARAMETER Branch
    The branch each finding's url names, <RepositoryUrl>/blob/<Branch>/Gap/Findings/<file>.
    Default: gap.

.PARAMETER OutFile
    The data.json to write. Its folder is created when missing. Required unless -SelfTest.

.PARAMETER SelfTest
    Runs the checks against the fixtures in Gap/Tools/Fixtures/dashboard and prints one
    PASS or FAIL line per check. Exits 1 when any check fails.

.EXAMPLE
    powershell -NoProfile -File Gap/Tools/Export-GapDashboardData.ps1 -OutFile site/gaps/data.json

.EXAMPLE
    pwsh -NoProfile -File Gap/Tools/Export-GapDashboardData.ps1 -SelfTest
#>
[CmdletBinding()]
param(
    [string]$FindingsDirectory,
    [string]$ScorecardsDirectory,
    [string]$BaselinesDirectory,
    [string]$RepositoryUrl = 'https://github.com/StewartScottRogers/Curl',
    [string]$Branch = 'gap',
    [string]$OutFile,
    [switch]$SelfTest
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

$gapRoot = Split-Path -Parent $PSScriptRoot
if (-not $FindingsDirectory) { $FindingsDirectory = Join-Path $gapRoot 'Findings' }
if (-not $ScorecardsDirectory) { $ScorecardsDirectory = Join-Path $gapRoot 'Scorecards' }
if (-not $BaselinesDirectory) { $BaselinesDirectory = Join-Path $gapRoot 'Baselines' }

$ClosedRunWindow = 5
$SeverityRank = @{ 'Critical' = 0; 'High' = 1; 'Medium' = 2; 'Low' = 3 }
$ScopeRank = @{ 'target' = 0; 'newest' = 1 }

function Read-TextFile([string]$Path) {
    $text = [System.IO.File]::ReadAllText($Path, [System.Text.Encoding]::UTF8)
    return $text.Replace("`r`n", "`n").Replace("`r", "`n")
}

function Read-JsonFile([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { return $null }
    $text = Read-TextFile $Path
    if ($text.Trim().Length -eq 0) { return $null }
    return ConvertFrom-Json -InputObject $text
}

function ConvertFrom-FrontMatterList([string]$Value) {
    $inner = $Value.Trim()
    if ($inner.StartsWith('[') -and $inner.EndsWith(']')) { $inner = $inner.Substring(1, $inner.Length - 2) }
    $list = New-Object System.Collections.Generic.List[string]
    foreach ($part in $inner.Split(',')) {
        $item = $part.Trim()
        if ($item.Length -gt 0) { $list.Add($item) }
    }
    return , $list.ToArray()
}

function Read-GapFinding([System.IO.FileInfo]$File) {
    $lines = (Read-TextFile $File.FullName).Split("`n")
    if ($lines.Length -lt 2 -or $lines[0].Trim() -ne '---') { throw "$($File.Name): no front matter." }
    $front = @{}
    $index = 1
    while ($index -lt $lines.Length -and $lines[$index].Trim() -ne '---') {
        $line = $lines[$index]
        $colon = $line.IndexOf(':')
        if ($colon -gt 0) { $front[$line.Substring(0, $colon).Trim()] = $line.Substring($colon + 1).Trim() }
        $index++
    }
    foreach ($field in 'id', 'title', 'area', 'severity', 'status', 'scope') {
        if (-not $front.ContainsKey($field) -or $front[$field].Length -eq 0) { throw "$($File.Name): front matter lacks '$field'." }
    }
    if (-not $SeverityRank.ContainsKey($front['severity'])) { throw "$($File.Name): unknown severity '$($front['severity'])'." }
    if (-not $ScopeRank.ContainsKey($front['scope'])) { throw "$($File.Name): unknown scope '$($front['scope'])'." }

    $suggestion = New-Object System.Collections.Generic.List[string]
    $inSuggestion = $false
    for ($index++; $index -lt $lines.Length; $index++) {
        $line = $lines[$index]
        if ($line.StartsWith('## ')) { $inSuggestion = ($line.Trim() -eq '## Suggestion'); continue }
        if ($inSuggestion) { $suggestion.Add($line.TrimEnd()) }
    }

    $introducedIn = $null
    if ($front.ContainsKey('introduced-in') -and $front['introduced-in'].Length -gt 0) { $introducedIn = $front['introduced-in'] }
    $items = @()
    if ($front.ContainsKey('items')) { $items = ConvertFrom-FrontMatterList $front['items'] }
    $tasks = @()
    if ($front.ContainsKey('tasks')) { $tasks = ConvertFrom-FrontMatterList $front['tasks'] }
    $closedStamp = ''
    if ($front.ContainsKey('closed')) { $closedStamp = $front['closed'] }
    $regression = $front.ContainsKey('regression') -and $front['regression'] -eq 'true'

    $entry = [ordered]@{
        id           = $front['id']
        title        = $front['title']
        area         = $front['area']
        severity     = $front['severity']
        scope        = $front['scope']
        introducedIn = $introducedIn
        suggestion   = ([string]::Join("`n", $suggestion.ToArray())).Trim()
        items        = $items.Length
        tasks        = $tasks
        url          = "$($RepositoryUrl.TrimEnd('/'))/blob/$Branch/Gap/Findings/$($File.Name)"
    }
    return [pscustomobject]@{
        Status     = $front['status']
        Closed     = $closedStamp
        Regression = $regression
        SortKey    = '{0}{1}{2}' -f $SeverityRank[$front['severity']], $ScopeRank[$front['scope']], $front['id']
        Entry      = $entry
    }
}

function Select-SortedEntries($Findings) {
    $list = @($Findings)
    if ($list.Length -eq 0) { return , @() }
    $byKey = @{}
    $keys = New-Object System.Collections.Generic.List[string]
    foreach ($finding in $list) { $byKey[$finding.SortKey] = $finding.Entry; $keys.Add($finding.SortKey) }
    $keys.Sort([System.StringComparer]::Ordinal)
    return , [object[]]@(foreach ($key in $keys) { $byKey[$key] })
}

function Compare-Version([string]$Left, [string]$Right) {
    $a = $Left.Split('.')
    $b = $Right.Split('.')
    for ($i = 0; $i -lt [Math]::Max($a.Length, $b.Length); $i++) {
        $x = 0; $y = 0
        if ($i -lt $a.Length) { $x = [long]$a[$i] }
        if ($i -lt $b.Length) { $y = [long]$b[$i] }
        if ($x -ne $y) { if ($x -lt $y) { return -1 } else { return 1 } }
    }
    return 0
}

function ConvertTo-JsonString([string]$Text) {
    $builder = New-Object System.Text.StringBuilder
    [void]$builder.Append('"')
    foreach ($c in $Text.ToCharArray()) {
        $code = [int]$c
        if ($c -eq '"') { [void]$builder.Append('\"') }
        elseif ($c -eq '\') { [void]$builder.Append('\\') }
        elseif ($code -eq 10) { [void]$builder.Append('\n') }
        elseif ($code -eq 13) { [void]$builder.Append('\r') }
        elseif ($code -eq 9) { [void]$builder.Append('\t') }
        elseif ($code -lt 32 -or $code -gt 126) { [void]$builder.Append(('\u{0:x4}' -f $code)) }
        else { [void]$builder.Append($c) }
    }
    [void]$builder.Append('"')
    return $builder.ToString()
}

function ConvertTo-StableJson($Value, [string]$Indent) {
    if ($null -eq $Value) { return 'null' }
    if ($Value -is [bool]) { if ($Value) { return 'true' } else { return 'false' } }
    if ($Value -is [string]) { return ConvertTo-JsonString $Value }
    if ($Value -is [int] -or $Value -is [long] -or $Value -is [decimal] -or $Value -is [double] -or $Value -is [single]) {
        return ([System.IFormattable]$Value).ToString($null, [System.Globalization.CultureInfo]::InvariantCulture)
    }
    $inner = $Indent + '  '
    $pairs = $null
    if ($Value -is [System.Collections.IDictionary]) {
        $pairs = @(foreach ($key in $Value.Keys) { , @([string]$key, $Value[$key]) })
    }
    elseif ($Value -is [System.Management.Automation.PSCustomObject]) {
        $pairs = @(foreach ($property in $Value.PSObject.Properties) { , @($property.Name, $property.Value) })
    }
    if ($null -ne $pairs) {
        if ($pairs.Length -eq 0) { return '{}' }
        $parts = foreach ($pair in $pairs) { $inner + (ConvertTo-JsonString $pair[0]) + ': ' + (ConvertTo-StableJson $pair[1] $inner) }
        return "{`n" + ([string]::Join(",`n", [string[]]@($parts))) + "`n$Indent}"
    }
    if ($Value -is [System.Collections.IEnumerable]) {
        $elements = @($Value)
        if ($elements.Length -eq 0) { return '[]' }
        $parts = foreach ($element in $elements) { $inner + (ConvertTo-StableJson $element $inner) }
        return "[`n" + ([string]::Join(",`n", [string[]]@($parts))) + "`n$Indent]"
    }
    throw "Cannot write a value of type $($Value.GetType().FullName) as JSON."
}

function New-GapDashboardData([string]$Generated) {
    $history = @()
    $historyFile = Join-Path $ScorecardsDirectory 'history.json'
    $historyJson = Read-JsonFile $historyFile
    if ($null -ne $historyJson) { $history = @($historyJson) }
    $latest = $null
    if ($history.Length -gt 0) { $latest = $history[$history.Length - 1] }

    $target = $null
    $targetJson = Read-JsonFile (Join-Path $BaselinesDirectory 'target.json')
    if ($null -ne $targetJson) { $target = [string]$targetJson.version }
    elseif ($null -ne $latest) { $target = [string]$latest.targetVersion }
    $newest = $null
    $newestJson = Read-JsonFile (Join-Path $BaselinesDirectory 'newest.json')
    if ($null -ne $newestJson) { $newest = [string]$newestJson.version }
    elseif ($null -ne $latest) { $newest = [string]$latest.newestVersion }

    $findings = @()
    if (Test-Path -LiteralPath $FindingsDirectory -PathType Container) {
        $files = @(Get-ChildItem -LiteralPath $FindingsDirectory -Filter 'GF-*.md' -File)
        $findings = @(foreach ($file in $files) { Read-GapFinding $file })
    }
    $open = @($findings | Where-Object { $_.Status -eq 'open' })

    $recentStamps = @{}
    $first = [Math]::Max(0, $history.Length - $ClosedRunWindow)
    for ($i = $first; $i -lt $history.Length; $i++) { $recentStamps[[string]$history[$i].stamp] = $true }
    $closed = @($findings | Where-Object { $_.Status -eq 'closed' -and $_.Closed.Length -gt 0 -and $recentStamps.ContainsKey($_.Closed) })
    $regressions = @($open | Where-Object { $_.Regression })

    $release = $null
    if ($null -ne $target -and $null -ne $newest -and (Compare-Version $newest $target) -gt 0) {
        $newGaps = @($open | Where-Object { $_.Entry.scope -eq 'newest' -and $_.Entry.introducedIn -eq $newest }).Length
        $release = [ordered]@{ version = $newest; newGaps = $newGaps }
    }

    $data = [ordered]@{
        generated   = $Generated
        target      = $target
        newest      = $newest
        latest      = $latest
        history     = $history
        open        = (Select-SortedEntries $open)
        closed      = (Select-SortedEntries $closed)
        regressions = (Select-SortedEntries $regressions)
        release     = $release
    }
    return (ConvertTo-StableJson $data '') + "`n"
}

function Write-GapDashboardData([string]$Path, [string]$Json) {
    $full = [System.IO.Path]::GetFullPath($Path)
    $folder = Split-Path -Parent $full
    if ($folder -and -not (Test-Path -LiteralPath $folder)) { [void](New-Item -ItemType Directory -Path $folder -Force) }
    [System.IO.File]::WriteAllText($full, $Json, (New-Object System.Text.UTF8Encoding($false)))
}

function Get-UtcNow { return [DateTime]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ssZ', [System.Globalization.CultureInfo]::InvariantCulture) }

function Invoke-SelfTest {
    $fixtures = Join-Path $PSScriptRoot (Join-Path 'Fixtures' 'dashboard')
    $failures = 0
    function Test-Check([string]$Name, [bool]$Passed) {
        if ($Passed) { Write-Output "PASS $Name" } else { Write-Output "FAIL $Name"; $script:failures++ }
    }
    $script:failures = 0
    $fixedTime = '2026-10-09T15:00:00Z'

    $script:FindingsDirectory = Join-Path $fixtures 'Findings'
    $script:ScorecardsDirectory = Join-Path $fixtures 'Scorecards'
    $script:BaselinesDirectory = Join-Path $fixtures 'Baselines'
    $json = New-GapDashboardData $fixedTime
    $data = ConvertFrom-Json -InputObject $json

    $openIds = [string]::Join(',', [string[]]@($data.open | ForEach-Object { $_.id }))
    Test-Check "open list is ordered by severity, scope, then ID ($openIds)" ($openIds -eq 'GF-0002,GF-0001,GF-0005,GF-0003,GF-0004,GF-0006')
    $allIds = @($data.open + $data.closed + $data.regressions | ForEach-Object { $_.id })
    Test-Check 'rejected finding GF-0009 is absent' (-not ($allIds -contains 'GF-0009'))
    $closedIds = [string]::Join(',', [string[]]@($data.closed | ForEach-Object { $_.id }))
    Test-Check "closed holds only findings closed in the last five runs ($closedIds)" ($closedIds -eq 'GF-0007')
    $regressionIds = [string]::Join(',', [string[]]@($data.regressions | ForEach-Object { $_.id }))
    Test-Check "regressions lists the open regression GF-0002 ($regressionIds)" ($regressionIds -eq 'GF-0002')
    $first = @($data.open)[0]
    Test-Check 'an open finding carries its suggestion, item count, tasks and url' (
        $first.suggestion.StartsWith('Send the request body') -and $first.items -eq 3 -and
        [string]::Join(',', [string[]]@($first.tasks)) -eq 'BL-1801,BL-1805' -and
        $first.url -eq 'https://github.com/StewartScottRogers/Curl/blob/gap/Gap/Findings/GF-0002-post-body-is-sent-twice-after-a-redirect.md')
    $low = @($data.open | Where-Object { $_.id -eq 'GF-0006' })[0]
    Test-Check 'an empty introduced-in is null and an empty tasks list is []' ($null -eq $low.introducedIn -and @($low.tasks).Length -eq 0 -and $json.Contains("""tasks"": []"))
    Test-Check 'release is set when newest 8.22.0 is above target 8.21.0, counting 2 new gaps' (
        $null -ne $data.release -and $data.release.version -eq '8.22.0' -and $data.release.newGaps -eq 2)
    Test-Check 'latest is the last history entry and history holds all six runs' ($data.latest.stamp -eq '2026-10-06_0900' -and @($data.history).Length -eq 6)
    $expected = Read-TextFile (Join-Path $fixtures 'expected-data.json')
    Test-Check 'the fixture output equals expected-data.json' ($json -ceq $expected)
    Test-Check 'the output is ASCII only' (-not ($json -match '[^\x00-\x7F]'))

    $temp = Join-Path ([System.IO.Path]::GetTempPath()) ('gap-dashboard-' + [Guid]::NewGuid().ToString('N'))
    try {
        $sameBaselines = Join-Path $temp 'Baselines'
        [void](New-Item -ItemType Directory -Path $sameBaselines -Force)
        [System.IO.File]::WriteAllText((Join-Path $sameBaselines 'target.json'), '{ "version": "8.22.0", "decidedBy": "ADR-0433" }')
        [System.IO.File]::WriteAllText((Join-Path $sameBaselines 'newest.json'), '{ "version": "8.22.0", "tag": "curl-8_22_0", "published": "2026-11-05", "checked": "2026-11-09" }')
        $script:BaselinesDirectory = $sameBaselines
        $sameData = ConvertFrom-Json -InputObject (New-GapDashboardData $fixedTime)
        Test-Check 'release is null when newest equals the target' ($null -eq $sameData.release)
        [System.IO.File]::WriteAllText((Join-Path $sameBaselines 'target.json'), '{ "version": "8.22.1", "decidedBy": "ADR-0433" }')
        $olderData = ConvertFrom-Json -InputObject (New-GapDashboardData $fixedTime)
        Test-Check 'release is null when newest is below the target' ($null -eq $olderData.release)

        $script:BaselinesDirectory = Join-Path $fixtures 'Baselines'
        $script:ScorecardsDirectory = Join-Path $temp 'Scorecards'
        $noHistoryJson = New-GapDashboardData $fixedTime
        $noHistory = ConvertFrom-Json -InputObject $noHistoryJson
        Test-Check 'with no history, latest is null, history is [] and the findings are still listed' (
            $null -eq $noHistory.latest -and $noHistoryJson.Contains("""history"": []") -and @($noHistory.open).Length -eq 6 -and @($noHistory.closed).Length -eq 0)

        $script:ScorecardsDirectory = Join-Path $fixtures 'Scorecards'
        $firstFile = Join-Path $temp 'first.json'
        $secondFile = Join-Path $temp 'second.json'
        Write-GapDashboardData $firstFile (New-GapDashboardData (Get-UtcNow))
        Start-Sleep -Milliseconds 1100
        Write-GapDashboardData $secondFile (New-GapDashboardData (Get-UtcNow))
        $pattern = '"generated": "[^"]*"'
        $one = [regex]::Replace((Read-TextFile $firstFile), $pattern, '"generated": ""')
        $two = [regex]::Replace((Read-TextFile $secondFile), $pattern, '"generated": ""')
        Test-Check 'two runs give identical output apart from generated' ($one -ceq $two)
        $bytes = [System.IO.File]::ReadAllBytes($firstFile)
        Test-Check 'the written file has no byte order mark and no carriage return' ($bytes[0] -eq 0x7B -and -not ($bytes -contains 13))
    }
    finally {
        if (Test-Path -LiteralPath $temp) { Remove-Item -LiteralPath $temp -Recurse -Force }
    }

    if ($script:failures -gt 0) { Write-Output "$($script:failures) check(s) failed."; exit 1 }
    Write-Output 'All checks passed.'
}

if ($SelfTest) { Invoke-SelfTest; return }
if (-not $OutFile) { throw 'Pass -OutFile <path>/data.json, or -SelfTest.' }
Write-GapDashboardData $OutFile (New-GapDashboardData (Get-UtcNow))
Write-Output "Wrote $OutFile"
