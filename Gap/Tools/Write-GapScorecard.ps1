<#
.SYNOPSIS
    Writes one gap run's scorecard, Gap/Scorecards/<stamp>.md, and appends the run to
    Gap/Scorecards/history.json.

.DESCRIPTION
    Reads a gap run's area measurements (<run>/measurements/<area>.json), the optional
    release diff (<run>/measurements/release-<newest>.json), the gap findings and the
    history of earlier runs, and writes the scorecard in the shape
    Gap/Instructions/Gap-Format.md section 8 defines (ADR-0433 decisions 2 and 3, BL-1732).

    The scorecard has exactly these sections, in this order, each saying "None." when it
    has nothing to list: Run, Scores, Against the newest version, New gaps, Closed,
    Regressions, Unmeasured by reason.

    Rules:

    - Per area: X = counts.x and Y = counts.y from its measurement, and % = 100 * X / Y,
      rounded to one decimal place (midpoints away from zero). An area with Y = 0 prints
      "n/a" for its percentage and its change, and adds nothing to the overall score.
    - Overall = sum of X over sum of Y. An area this run did not measure, but an earlier run
      on the same platform did, is shown with its X and Y from the newest such history
      entry, marked "(carried from <stamp>)", and is included in the overall score, so a
      partial run does not jump the trend. A carried area's unmeasured, excluded and change
      columns print "-", because history.json does not record them.
    - Change since the last run: the percentage now minus the percentage in the newest
      history.json entry for the same platform, in percentage points, "+n.n" or "-n.n".
      The overall row says "first run" when no entry has this platform; an area row says
      "first run" when no entry for this platform measured that area.
    - Against the newest version (Gap-Format.md section 10): for each measured area, X is
      the items measured "match" whose key the release diff neither removes nor changes; Y
      is the items not measured "excluded" whose key the diff does not remove, plus the keys
      the diff adds to the area. That one rule gives both section 10's inventory-area rule
      and its behaviour rule (added and changed cases count against X, removed cases leave
      both). A carried area keeps its target figures. When the newest version equals the
      target, the section says so and repeats the target figures. When it does not and the
      run holds no release-<newest>.json, the section says so and repeats the target
      figures, since nothing measured the difference.
    - New gaps: findings whose "opened" is this run's stamp. Closed: findings with status
      "closed" whose "closed" is this run's stamp. Regressions: open findings with
      "regression: true" whose Log has a line "- <stamp>: ..." that mentions "reopen".
      Each lists "- <id> <title> (<severity>)", by ID. Rejected findings are never listed.
    - Unmeasured by reason: one row per measured area and reason for its "unmeasured" and
      "excluded" items, with the count; areas in the order below, reasons sorted ordinally.
    - Areas are listed in the order options, protocols, features, writeout, exitcodes,
      environment, behaviour, then any other name ordinally; the overall row is last.
    - history.json gains exactly one entry, at its end, written as text so every earlier
      byte stays as it was; the file is created as "[]" first when missing. Its "areas"
      holds only the areas this run measured; "overall" and "newest" include carried areas.
      A run whose stamp history.json already holds, or whose scorecard already exists, is
      refused, so a rerun never appends twice.

    The run's commit, platform, target version and reference come from its measurements,
    which must all agree on commit, platform and target. Files are written UTF-8 without a
    byte order mark, LF line endings, ASCII only. Runs under Windows PowerShell 5.1 and
    PowerShell 7 on Windows, Linux and macOS.

.PARAMETER RunDirectory
    The gap run folder, <repo>.gap\<stamp>. Its measurements/*.json are the area
    measurements, apart from release-*.json, which are release diffs. Required unless
    -SelfTest.

.PARAMETER Stamp
    The run stamp, yyyy-MM-dd_HHmm. Names the scorecard and the history entry. Default: the
    run folder's name.

.PARAMETER FindingsDirectory
    The folder holding the GF-*.md findings, read after the findings step (BL-1731) has updated them
    for this run. Default: Gap/Findings. A missing folder lists no findings.

.PARAMETER ScorecardsDirectory
    The folder the scorecard and history.json are written to. Default: Gap/Scorecards.
    Created when missing.

.PARAMETER NewestVersion
    The newest upstream version. Default: the version in Gap/Baselines/newest.json, else
    the run's target version.

.PARAMETER SelfTest
    Copies the fixtures in Gap/Tools/Fixtures/scorecard to a temporary folder, writes two
    runs' scorecards there (a first run and a later partial run) and prints one PASS or
    FAIL line per check. Exits 1 when any check fails.

.EXAMPLE
    powershell -NoProfile -File Gap/Tools/Write-GapScorecard.ps1 -RunDirectory ..\Curl.gap\2026-10-09_1430

.EXAMPLE
    pwsh -NoProfile -File Gap/Tools/Write-GapScorecard.ps1 -SelfTest
#>
[CmdletBinding()]
param(
    [string]$RunDirectory,
    [string]$Stamp,
    [string]$FindingsDirectory,
    [string]$ScorecardsDirectory,
    [string]$NewestVersion,
    [switch]$SelfTest
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

$gapRoot = Split-Path -Parent $PSScriptRoot
if (-not $FindingsDirectory) { $FindingsDirectory = Join-Path $gapRoot 'Findings' }
if (-not $ScorecardsDirectory) { $ScorecardsDirectory = Join-Path $gapRoot 'Scorecards' }

$AreaOrder = @('options', 'protocols', 'features', 'writeout', 'exitcodes', 'environment', 'behaviour')
$Invariant = [System.Globalization.CultureInfo]::InvariantCulture

function Read-TextFile([string]$Path) {
    $text = [System.IO.File]::ReadAllText($Path, [System.Text.Encoding]::UTF8)
    return $text.Replace("`r`n", "`n").Replace("`r", "`n")
}

function Read-JsonFile([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { return $null }
    $text = Read-TextFile $Path
    if ($text.Trim().Length -eq 0) { return $null }
    $value = ConvertFrom-Json -InputObject $text
    return $value
}

function Write-TextFile([string]$Path, [string]$Text) {
    [System.IO.File]::WriteAllText($Path, $Text, (New-Object System.Text.UTF8Encoding($false)))
}

function Get-SortedAreas($Names) {
    $known = @($AreaOrder | Where-Object { $Names -contains $_ })
    $other = New-Object System.Collections.Generic.List[string]
    foreach ($name in $Names) { if (-not ($AreaOrder -contains $name) -and -not $other.Contains($name)) { $other.Add($name) } }
    $other.Sort([System.StringComparer]::Ordinal)
    return , [string[]]@($known + $other.ToArray())
}

function Get-Percent([long]$X, [long]$Y) {
    if ($Y -eq 0) { return $null }
    return 100.0 * $X / $Y
}

function Format-Percent($Percent) {
    if ($null -eq $Percent) { return 'n/a' }
    return ([Math]::Round([double]$Percent, 1, [MidpointRounding]::AwayFromZero)).ToString('0.0', $Invariant)
}

function Format-Change($Now, $Before) {
    if ($null -eq $Now -or $null -eq $Before) { return 'n/a' }
    $change = [Math]::Round([double]$Now - [double]$Before, 1, [MidpointRounding]::AwayFromZero)
    $text = ([Math]::Abs($change)).ToString('0.0', $Invariant)
    if ($change -lt 0) { return "-$text" }
    return "+$text"
}

function Read-Measurements([string]$Directory) {
    $folder = Join-Path $Directory 'measurements'
    if (-not (Test-Path -LiteralPath $folder -PathType Container)) { throw "No measurements folder in $Directory." }
    $areas = @{}
    foreach ($file in @(Get-ChildItem -LiteralPath $folder -Filter '*.json' -File)) {
        if ($file.Name.StartsWith('release-')) { continue }
        $measurement = Read-JsonFile $file.FullName
        $areas[[string]$measurement.area] = $measurement
    }
    if ($areas.Count -eq 0) { throw "No area measurements in $folder." }
    return $areas
}

function Read-ReleaseDiff([string]$Directory, [string]$Version) {
    foreach ($candidate in @((Join-Path (Join-Path $Directory 'measurements') "release-$Version.json"), (Join-Path $Directory "release-$Version.json"))) {
        $diff = Read-JsonFile $candidate
        if ($null -ne $diff) { return $diff }
    }
    return $null
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
    foreach ($field in 'id', 'title', 'severity', 'status') {
        if (-not $front.ContainsKey($field) -or $front[$field].Length -eq 0) { throw "$($File.Name): front matter lacks '$field'." }
    }
    $log = New-Object System.Collections.Generic.List[string]
    $inLog = $false
    for ($index++; $index -lt $lines.Length; $index++) {
        $line = $lines[$index]
        if ($line.StartsWith('## ')) { $inLog = ($line.Trim() -eq '## Log'); continue }
        if ($inLog) { $log.Add($line.Trim()) }
    }
    $opened = ''; if ($front.ContainsKey('opened')) { $opened = $front['opened'] }
    $closed = ''; if ($front.ContainsKey('closed')) { $closed = $front['closed'] }
    return [pscustomobject]@{
        Id         = $front['id']
        Line       = "- $($front['id']) $($front['title']) ($($front['severity']))"
        Status     = $front['status']
        Opened     = $opened
        Closed     = $closed
        Regression = ($front.ContainsKey('regression') -and $front['regression'] -eq 'true')
        Log        = $log.ToArray()
    }
}

function Get-FindingLines([string]$Kind) {
    if (-not (Test-Path -LiteralPath $FindingsDirectory -PathType Container)) { return , @() }
    $findings = @(foreach ($file in @(Get-ChildItem -LiteralPath $FindingsDirectory -Filter 'GF-*.md' -File)) { Read-GapFinding $file })
    $selected = switch ($Kind) {
        'new' { @($findings | Where-Object { $_.Status -ne 'rejected' -and $_.Opened -eq $Stamp }) }
        'closed' { @($findings | Where-Object { $_.Status -eq 'closed' -and $_.Closed -eq $Stamp }) }
        'regressions' {
            @($findings | Where-Object {
                    $finding = $_
                    $finding.Status -eq 'open' -and $finding.Regression -and
                    @($finding.Log | Where-Object { $_.StartsWith("- ${Stamp}:") -and $_.ToLowerInvariant().Contains('reopen') }).Length -gt 0
                })
        }
    }
    $ids = New-Object System.Collections.Generic.List[string]
    $byId = @{}
    foreach ($finding in @($selected)) { $ids.Add($finding.Id); $byId[$finding.Id] = $finding.Line }
    $ids.Sort([System.StringComparer]::Ordinal)
    return , [string[]]@(foreach ($id in $ids) { $byId[$id] })
}

function Get-NewestScore($Measurement, $Diff) {
    $area = [string]$Measurement.area
    $removedOrChanged = @{}
    $added = 0
    if ($null -ne $Diff) {
        foreach ($item in @($Diff.items)) {
            if (-not ([string]$item.key).StartsWith("${area}:")) { continue }
            if ($item.change -eq 'added') { $added++ } else { $removedOrChanged[[string]$item.key] = [string]$item.change }
        }
    }
    $x = 0; $y = $added
    foreach ($item in @($Measurement.items)) {
        $change = $removedOrChanged[[string]$item.key]
        if ($item.state -eq 'match' -and $null -eq $change) { $x++ }
        if ($item.state -ne 'excluded' -and $change -ne 'removed') { $y++ }
    }
    return @{ x = $x; y = $y }
}

function Format-Lines($Lines) {
    $list = @($Lines)
    if ($list.Length -eq 0) { return 'None.' }
    return [string]::Join("`n", [string[]]$list)
}

function New-GapScorecard {
    $measurements = Read-Measurements $RunDirectory
    $first = $null
    foreach ($measurement in $measurements.Values) {
        if ($null -eq $first) { $first = $measurement; continue }
        foreach ($field in 'candidateCommit', 'platform', 'targetVersion') {
            if ([string]$measurement.$field -ne [string]$first.$field) { throw "Measurements disagree on ${field}: '$($first.$field)' and '$($measurement.$field)'." }
        }
    }
    $commit = [string]$first.candidateCommit
    $platform = [string]$first.platform
    $target = [string]$first.targetVersion
    $reference = 'documents'
    if ($null -ne $first.reference) { $reference = [string]$first.reference }
    $newest = $NewestVersion
    if (-not $newest) {
        $newestJson = Read-JsonFile (Join-Path (Join-Path $gapRoot 'Baselines') 'newest.json')
        if ($null -ne $newestJson) { $newest = [string]$newestJson.version } else { $newest = $target }
    }

    if (-not (Test-Path -LiteralPath $ScorecardsDirectory -PathType Container)) { [void](New-Item -ItemType Directory -Path $ScorecardsDirectory -Force) }
    $historyFile = Join-Path $ScorecardsDirectory 'history.json'
    $scorecardFile = Join-Path $ScorecardsDirectory "$Stamp.md"
    if (Test-Path -LiteralPath $scorecardFile) { throw "Scorecard $scorecardFile already exists." }
    $history = @()
    $historyJson = Read-JsonFile $historyFile
    if ($null -ne $historyJson) { $history = @($historyJson) }
    if (@($history | Where-Object { $_.stamp -eq $Stamp }).Length -gt 0) { throw "history.json already holds run $Stamp." }
    $samePlatform = @($history | Where-Object { $_.platform -eq $platform })
    $previous = $null
    if ($samePlatform.Length -gt 0) { $previous = $samePlatform[$samePlatform.Length - 1] }

    $carried = @{}
    foreach ($entry in $samePlatform) {
        foreach ($property in $entry.areas.PSObject.Properties) {
            if (-not $measurements.ContainsKey($property.Name)) { $carried[$property.Name] = [pscustomobject]@{ Stamp = [string]$entry.stamp; X = [long]$property.Value.x; Y = [long]$property.Value.y } }
        }
    }

    $diff = $null
    if ($newest -ne $target) { $diff = Read-ReleaseDiff $RunDirectory $newest }

    $scoreRows = New-Object System.Collections.Generic.List[string]
    $newestRows = New-Object System.Collections.Generic.List[string]
    $reasonRows = New-Object System.Collections.Generic.List[string]
    $areasJson = New-Object System.Collections.Generic.List[string]
    $sumX = 0; $sumY = 0; $sumUnmeasured = 0; $sumExcluded = 0; $newestX = 0; $newestY = 0
    $allAreas = @($measurements.Keys) + @($carried.Keys)
    foreach ($area in (Get-SortedAreas $allAreas)) {
        if ($measurements.ContainsKey($area)) {
            $measurement = $measurements[$area]
            $x = [long]$measurement.counts.x; $y = [long]$measurement.counts.y
            $unmeasured = [long]$measurement.counts.unmeasured; $excluded = [long]$measurement.counts.excluded
            $percent = Get-Percent $x $y
            $before = $null
            foreach ($entry in $samePlatform) { if ($null -ne $entry.areas.PSObject.Properties[$area]) { $before = $entry.areas.$area } }
            if ($null -eq $before) { $change = 'first run' }
            else { $change = Format-Change $percent (Get-Percent ([long]$before.x) ([long]$before.y)) }
            $scoreRows.Add("| $area | $x | $y | $(Format-Percent $percent) | $unmeasured | $excluded | $change |")
            $sumUnmeasured += $unmeasured; $sumExcluded += $excluded
            $areasJson.Add("""$area"": { ""x"": $x, ""y"": $y }")
            if ($null -ne $diff) { $score = Get-NewestScore $measurement $diff } else { $score = @{ x = $x; y = $y } }

            $reasons = @{}
            foreach ($item in @($measurement.items)) {
                if ($item.state -ne 'unmeasured' -and $item.state -ne 'excluded') { continue }
                $reason = '(none)'; if ($null -ne $item.reason) { $reason = [string]$item.reason }
                if ($reasons.ContainsKey($reason)) { $reasons[$reason]++ } else { $reasons[$reason] = 1 }
            }
            $reasonNames = New-Object System.Collections.Generic.List[string]
            foreach ($name in $reasons.Keys) { $reasonNames.Add($name) }
            $reasonNames.Sort([System.StringComparer]::Ordinal)
            foreach ($name in $reasonNames) { $reasonRows.Add("| $area | $name | $($reasons[$name]) |") }
            $label = $area
        }
        else {
            $carry = $carried[$area]
            $x = $carry.X; $y = $carry.Y
            $label = "$area (carried from $($carry.Stamp))"
            $scoreRows.Add("| $label | $x | $y | $(Format-Percent (Get-Percent $x $y)) | - | - | - |")
            $score = @{ x = $x; y = $y }
        }
        $sumX += $x; $sumY += $y
        $newestX += $score.x; $newestY += $score.y
        $newestRows.Add("| $label | $($score.x) | $($score.y) | $(Format-Percent (Get-Percent $score.x $score.y)) |")
    }
    $overallPercent = Get-Percent $sumX $sumY
    if ($null -eq $previous) { $overallChange = 'first run' }
    else { $overallChange = Format-Change $overallPercent (Get-Percent ([long]$previous.overall.x) ([long]$previous.overall.y)) }
    $scoreRows.Add("| overall | $sumX | $sumY | $(Format-Percent $overallPercent) | $sumUnmeasured | $sumExcluded | $overallChange |")
    $newestRows.Add("| overall | $newestX | $newestY | $(Format-Percent (Get-Percent $newestX $newestY)) |")

    if ($newest -eq $target) { $newestIntro = "Newest version $newest is the target; the target figures are repeated." }
    elseif ($null -eq $diff) { $newestIntro = "Release $newest. This run has no release diff for it; the target figures are repeated." }
    else { $newestIntro = "Release $newest." }

    $reasonText = 'None.'
    if ($reasonRows.Count -gt 0) { $reasonText = "| Area | Reason | Count |`n| --- | --- | --- |`n" + [string]::Join("`n", $reasonRows.ToArray()) }

    $markdown = @(
        "# Gap scorecard $Stamp", '',
        '## Run', '',
        "- Commit: $commit", "- Platform: $platform", "- Target: $target", "- Newest: $newest", "- Reference: $reference", '',
        '## Scores', '',
        '| Area | X | Y | % | Unmeasured | Excluded | Change |', '| --- | --- | --- | --- | --- | --- | --- |',
        [string]::Join("`n", $scoreRows.ToArray()), '',
        '## Against the newest version', '',
        $newestIntro, '',
        '| Area | X | Y | % |', '| --- | --- | --- | --- |',
        [string]::Join("`n", $newestRows.ToArray()), '',
        '## New gaps', '', (Format-Lines (Get-FindingLines 'new')), '',
        '## Closed', '', (Format-Lines (Get-FindingLines 'closed')), '',
        '## Regressions', '', (Format-Lines (Get-FindingLines 'regressions')), '',
        '## Unmeasured by reason', '', $reasonText
    )
    $scorecard = [string]::Join("`n", [string[]]$markdown) + "`n"
    if ($scorecard -match '[^\x00-\x7F]') { throw 'The scorecard would hold a non-ASCII character.' }

    $entry = @(
        '  {',
        "    ""stamp"": ""$Stamp"",",
        "    ""commit"": ""$commit"",",
        "    ""platform"": ""$platform"",",
        "    ""targetVersion"": ""$target"",",
        "    ""newestVersion"": ""$newest"",",
        "    ""areas"": { $([string]::Join(', ', $areasJson.ToArray())) },",
        "    ""overall"": { ""x"": $sumX, ""y"": $sumY },",
        "    ""newest"": { ""x"": $newestX, ""y"": $newestY }",
        '  }'
    )
    $entryText = [string]::Join("`n", [string[]]$entry)
    if (-not (Test-Path -LiteralPath $historyFile -PathType Leaf)) { Write-TextFile $historyFile "[]`n" }
    $historyText = [System.IO.File]::ReadAllText($historyFile, [System.Text.Encoding]::UTF8)
    $close = $historyText.LastIndexOf(']')
    if ($close -lt 0) { throw "$historyFile is not a JSON array." }
    $head = $historyText.Substring(0, $close).TrimEnd()
    if ($head.EndsWith('[')) { $historyText = "$head`n$entryText`n]`n" }
    else { $historyText = "$head,`n$entryText`n]`n" }

    Write-TextFile $scorecardFile $scorecard
    Write-TextFile $historyFile $historyText
    return $scorecardFile
}

function Invoke-SelfTest {
    $fixtures = Join-Path $PSScriptRoot (Join-Path 'Fixtures' 'scorecard')
    function Test-Check([string]$Name, [bool]$Passed) {
        if ($Passed) { Write-Output "PASS $Name" } else { Write-Output "FAIL $Name"; $script:failures++ }
    }
    function Get-Section([string]$Text, [string]$Heading) {
        $start = $Text.IndexOf("## $Heading`n")
        if ($start -lt 0) { return '' }
        $body = $Text.Substring($start + $Heading.Length + 4)
        $next = $body.IndexOf("`n## ")
        if ($next -ge 0) { $body = $body.Substring(0, $next) }
        return $body.Trim()
    }
    $script:failures = 0
    $temp = Join-Path ([System.IO.Path]::GetTempPath()) ('gap-scorecard-' + [Guid]::NewGuid().ToString('N'))
    try {
        Copy-Item -LiteralPath $fixtures -Destination $temp -Recurse
        $script:FindingsDirectory = Join-Path $temp 'Findings'
        $script:ScorecardsDirectory = Join-Path $temp 'Scorecards'
        $historyFile = Join-Path $ScorecardsDirectory 'history.json'
        $originalHistory = [System.IO.File]::ReadAllBytes($historyFile)

        $script:RunDirectory = Join-Path $temp 'first'
        $script:Stamp = '2026-10-09_1430'
        $script:NewestVersion = '8.21.0'
        $firstText = Read-TextFile (New-GapScorecard)
        $scores = Get-Section $firstText 'Scores'
        Test-Check 'per-area X, Y and % for the first run' ($scores.Contains('| options | 3 | 5 | 60.0 | 1 | 1 | first run |') -and $scores.Contains('| writeout | 2 | 2 | 100.0 | 0 | 0 | first run |'))
        Test-Check 'an area with Y = 0 prints n/a' ($scores.Contains('| exitcodes | 0 | 0 | n/a | 0 | 1 | first run |'))
        Test-Check 'overall is the sum of X over the sum of Y (5 of 7)' ($scores.Contains('| overall | 5 | 7 | 71.4 | 1 | 2 | first run |'))
        Test-Check 'first run when only another platform has history' (-not $scores.Contains('environment') -and $scores.Contains('| overall | 5 | 7 | 71.4 | 1 | 2 | first run |'))
        $newestFirst = Get-Section $firstText 'Against the newest version'
        Test-Check 'newest equal to the target says so and repeats the target figures' ($newestFirst.StartsWith('Newest version 8.21.0 is the target') -and $newestFirst.Contains('| overall | 5 | 7 | 71.4 |'))
        $headings = [string]::Join('|', [string[]]@([regex]::Matches($firstText, '(?m)^## (.+)$') | ForEach-Object { $_.Groups[1].Value }))
        Test-Check "the scorecard has exactly the fixed sections in order ($headings)" ($headings -ceq 'Run|Scores|Against the newest version|New gaps|Closed|Regressions|Unmeasured by reason')
        Test-Check 'the Run section names commit, platform, target, newest and reference' ((Get-Section $firstText 'Run') -ceq "- Commit: d15d115db0a1b2c3d4e5f60718293a4b5c6d7e01`n- Platform: windows`n- Target: 8.21.0`n- Newest: 8.21.0`n- Reference: curl 8.21.0 (x86_64-w64-mingw32) libcurl/8.21.0 Schannel")
        Test-Check 'unmeasured by reason sums each area per reason' ((Get-Section $firstText 'Unmeasured by reason') -ceq "| Area | Reason | Count |`n| --- | --- | --- |`n| options | needs-server:smtp | 1 |`n| options | platform:linux | 1 |`n| exitcodes | obsolete-code | 1 |")
        Test-Check 'the first run lists the findings it opened' ((Get-Section $firstText 'New gaps') -ceq "- GF-0002 The --d option differs (Medium)`n- GF-0004 The --b help line differs (Low)" -and (Get-Section $firstText 'Closed') -ceq 'None.')

        $afterFirst = [System.IO.File]::ReadAllBytes($historyFile)
        $history = @(Read-JsonFile $historyFile)
        $prefix = [Array]::LastIndexOf($originalHistory, [byte]0x7D) + 1
        $samePrefix = $true
        for ($i = 0; $i -lt $prefix; $i++) { if ($afterFirst[$i] -ne $originalHistory[$i]) { $samePrefix = $false; break } }
        Test-Check 'history gains exactly one entry and keeps the earlier one byte-identical' ($history.Length -eq 2 -and $samePrefix -and $history[1].stamp -eq '2026-10-09_1430')
        Test-Check 'the history entry records the measured areas, overall and newest' (
            $history[1].areas.options.x -eq 3 -and $history[1].areas.exitcodes.y -eq 0 -and $history[1].overall.x -eq 5 -and $history[1].overall.y -eq 7 -and $history[1].newest.y -eq 7)

        $script:RunDirectory = Join-Path $temp 'partial'
        $script:Stamp = '2026-10-10_0900'
        $script:NewestVersion = '8.22.0'
        $secondText = Read-TextFile (New-GapScorecard)
        $scores = Get-Section $secondText 'Scores'
        Test-Check 'the change against the previous same-platform entry (+20.0 and +14.3)' ($scores.Contains('| options | 4 | 5 | 80.0 | 0 | 1 | +20.0 |') -and $scores.Contains('| overall | 6 | 7 | 85.7 | 0 | 1 | +14.3 |'))
        Test-Check 'an unmeasured area is carried with its stamp and counted in overall' ($scores.Contains('| writeout (carried from 2026-10-09_1430) | 2 | 2 | 100.0 | - | - | - |') -and $scores.Contains('| exitcodes (carried from 2026-10-09_1430) | 0 | 0 | n/a | - | - | - |'))
        $newestSecond = Get-Section $secondText 'Against the newest version'
        Test-Check 'the newest-version section follows Gap-Format.md section 10' (
            $newestSecond.StartsWith('Release 8.22.0.') -and $newestSecond.Contains('| options | 2 | 6 | 33.3 |') -and
            $newestSecond.Contains('| writeout (carried from 2026-10-09_1430) | 2 | 2 | 100.0 |') -and $newestSecond.Contains('| overall | 4 | 8 | 50.0 |'))
        Test-Check 'new gaps, closed and regressions list this run''s findings only' (
            (Get-Section $secondText 'New gaps') -ceq '- GF-0001 The --e option is missing (High)' -and
            (Get-Section $secondText 'Closed') -ceq '- GF-0002 The --d option differs (Medium)' -and
            (Get-Section $secondText 'Regressions') -ceq '- GF-0003 The --c alias differs (Low)')
        Test-Check 'a section with nothing to list says None.' ((Get-Section $secondText 'Unmeasured by reason') -ceq "| Area | Reason | Count |`n| --- | --- | --- |`n| options | platform:linux | 1 |" -and (Get-Section $firstText 'Regressions') -ceq 'None.')

        $afterSecond = [System.IO.File]::ReadAllBytes($historyFile)
        $history = @(Read-JsonFile $historyFile)
        $prefix = [Array]::LastIndexOf($afterFirst, [byte]0x7D) + 1
        $samePrefix = $true
        for ($i = 0; $i -lt $prefix; $i++) { if ($afterSecond[$i] -ne $afterFirst[$i]) { $samePrefix = $false; break } }
        Test-Check 'the partial run appends one entry with only its measured area' (
            $history.Length -eq 3 -and $samePrefix -and @($history[2].areas.PSObject.Properties).Length -eq 1 -and $history[2].overall.x -eq 6 -and $history[2].newest.x -eq 4 -and $history[2].newest.y -eq 8)

        $refused = $false
        try { [void](New-GapScorecard) } catch { $refused = $true }
        Test-Check 'a second write of the same run is refused' ($refused -and @(Read-JsonFile $historyFile).Length -eq 3)

        Remove-Item -LiteralPath $historyFile
        Remove-Item -LiteralPath (Join-Path $ScorecardsDirectory '2026-10-10_0900.md')
        $script:NewestVersion = '8.23.0'
        $thirdText = Read-TextFile (New-GapScorecard)
        Test-Check 'a missing history.json is created and the run is a first run' (
            @(Read-JsonFile $historyFile).Length -eq 1 -and (Get-Section $thirdText 'Scores').Contains('| overall | 4 | 5 | 80.0 | 0 | 1 | first run |'))
        Test-Check 'a newest version with no release diff repeats the target figures' ((Get-Section $thirdText 'Against the newest version').StartsWith('Release 8.23.0. This run has no release diff'))
        $bytes = [System.IO.File]::ReadAllBytes($historyFile)
        Test-Check 'written files have no byte order mark and no carriage return' ($bytes[0] -eq 0x5B -and -not ($bytes -contains 13) -and -not ([System.IO.File]::ReadAllBytes((Join-Path $ScorecardsDirectory '2026-10-10_0900.md')) -contains 13))
    }
    finally {
        if (Test-Path -LiteralPath $temp) { Remove-Item -LiteralPath $temp -Recurse -Force }
    }

    if ($script:failures -gt 0) { Write-Output "$($script:failures) check(s) failed."; exit 1 }
    Write-Output 'All checks passed.'
}

if ($SelfTest) { Invoke-SelfTest; return }
if (-not $RunDirectory) { throw 'Pass -RunDirectory <run folder>, or -SelfTest.' }
if (-not $Stamp) { $Stamp = Split-Path -Leaf ([System.IO.Path]::GetFullPath($RunDirectory).TrimEnd('\', '/')) }
if ($Stamp -notmatch '^\d{4}-\d{2}-\d{2}_\d{4}$') { throw "Stamp '$Stamp' is not yyyy-MM-dd_HHmm." }
$written = New-GapScorecard
Write-Output "Wrote $written"
