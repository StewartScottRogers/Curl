<#
.SYNOPSIS
    Writes one gap run's scorecard, Gap/Scorecards/<stamp>.md, and appends the run to
    Gap/Scorecards/history.json, one entry per measured platform.

.DESCRIPTION
    Reads a gap run's area measurements (<run>/measurements/<platform>/<area>.json, or
    <run>/measurements/<area>.json in a run from before ADR-0504), the optional
    <run>/platforms.json that names a platform the run could not measure and why, the
    optional release diff (<run>/measurements/release-<newest>.json), the gap findings and
    the history of earlier runs, and writes the scorecard in the shape
    Gap/Instructions/Gap-Format.md section 8 defines (ADR-0433 decisions 2 and 3, BL-1732,
    ADR-0504). Each measurement's platform field (windows when it has none) is its platform.

    The scorecard has exactly these sections, in this order, each saying "None." when it
    has nothing to list: Run, Scores, Platforms, Against the newest version, New gaps,
    Closed, Regressions, Unmeasured by reason.

    Rules:

    - Per platform and area: X = counts.x and Y = counts.y from its measurement, and
      % = 100 * X / Y, rounded to one decimal place (midpoints away from zero). An area
      with Y = 0 prints "n/a" for its percentage and its change, and adds nothing to the
      overall score.
    - A platform's overall = sum of X over sum of Y. An area this run did not measure on a
      platform, but an earlier run on that platform did, is shown with its X and Y from the
      newest such history entry, marked "(carried from <stamp>)", and is included in that
      platform's overall score, so a partial run does not jump the trend. A carried area's
      unmeasured, excluded and change columns print "-", because history.json does not
      record them.
    - Change since the last run: the percentage now minus the percentage in the newest
      history.json entry for the same platform, in percentage points, "+n.n" or "-n.n".
      The overall row says "first run" when no entry has the platform; an area row says
      "first run" when no entry for the platform measured that area.
    - Scores: first the headline, "Overall: X of Y, P%, on <platform>, the lowest of
      <measured platforms>.", the lowest platform overall score, so no platform hides
      behind another; a platform whose overall Y is 0 has no score and is passed over. A
      platform platforms.json lists but no measurement holds is named after it as
      "Missing: <platform> (<reason>)". Then one row per area and an overall row, one
      column per platform (measured ones first, in the order windows, linux, macos, then
      the missing ones): "X of Y, P% (<change>)", "not measured" for an area that platform
      has neither measured nor carried, "missing" in a missing platform's column.
    - Platforms: one "### <platform>" table per measured platform with X, Y, %,
      Unmeasured, Excluded and Change per area and overall.
    - Against the newest version (Gap-Format.md section 10): for each measured area, X is
      the items measured "match" whose key the release diff neither removes nor changes; Y
      is the items not measured "excluded" whose key the diff does not remove, plus the keys
      the diff adds to the area. That one rule gives both section 10's inventory-area rule
      and its behaviour rule (added and changed cases count against X, removed cases leave
      both). A carried area keeps its target figures. When the newest version equals the
      target, the section says so and repeats the target figures. When it does not and the
      run holds no release-<newest>.json, the section says so and repeats the target
      figures, since nothing measured the difference. The section has its own lowest-
      platform headline and the same per-platform table, "X of Y, P%".
    - New gaps: findings whose "opened" is this run's stamp. Closed: findings with status
      "closed" whose "closed" is this run's stamp. Regressions: open findings with
      "regression: true" whose Log has a line "- <stamp>: ..." that mentions "reopen".
      Each lists "- <id> <title> (<severity>, <platform>)", by ID; a finding without a
      platform field is a windows one. Rejected findings are never listed.
    - Unmeasured by reason: one row per measured area, platform and reason for its
      "unmeasured" and "excluded" items, with the count; platforms in the order above,
      areas in the order below, reasons sorted ordinally.
    - Areas are listed in the order options, protocols, features, writeout, exitcodes,
      environment, behaviour, then any other name ordinally; the overall row is last.
    - history.json gains exactly one entry per measured platform, at its end, written as
      text so every earlier byte stays as it was; the file is created as "[]" first when
      missing. An entry's "areas" holds only the areas this run measured on that platform;
      "overall" and "newest" include carried areas. A run whose stamp history.json already
      holds for a measured platform, or whose scorecard already exists, is refused, so a
      rerun never appends twice. A missing platform gets no entry.

    The run's commit and target version come from its measurements, which must all agree
    on both; each platform's reference comes from its own measurements. Files are written
    UTF-8 without a byte order mark, LF line endings, ASCII only. Runs under Windows
    PowerShell 5.1 and PowerShell 7 on Windows, Linux and macOS.

.PARAMETER RunDirectory
    The gap run folder, <repo>.gap\<stamp>. The *.json under its measurements folder, at
    any depth, are the area measurements, apart from release-*.json, which are release
    diffs. Required unless -SelfTest.

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
    Copies the fixtures in Gap/Tools/Fixtures/scorecard to a temporary folder, writes
    scorecards there for a first run, a later partial run (again after history.json is
    removed), and a run measured on windows and linux with macos missing, and prints one PASS or FAIL line per check. Exits 1 when
    any check fails.

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
$PlatformOrder = @('windows', 'linux', 'macos')
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

function Format-PercentText($Percent) {
    # "71.4%", or "n/a" when there is no percentage.
    if ($null -eq $Percent) { return 'n/a' }
    return (Format-Percent $Percent) + '%'
}

function Format-Change($Now, $Before) {
    if ($null -eq $Now -or $null -eq $Before) { return 'n/a' }
    $change = [Math]::Round([double]$Now - [double]$Before, 1, [MidpointRounding]::AwayFromZero)
    $text = ([Math]::Abs($change)).ToString('0.0', $Invariant)
    if ($change -lt 0) { return "-$text" }
    return "+$text"
}

function Get-PlatformRank([string]$Platform) {
    $rank = [Array]::IndexOf($PlatformOrder, $Platform)
    if ($rank -lt 0) { return $PlatformOrder.Length }
    return $rank
}

function Get-SortedPlatforms($Names) {
    $list = New-Object System.Collections.Generic.List[string]
    foreach ($name in $Names) { if (-not $list.Contains([string]$name)) { $list.Add([string]$name) } }
    return [string[]]@($list.ToArray() | Sort-Object { Get-PlatformRank $_ }, { $_ })
}

function Read-Measurements([string]$Directory) {
    # platform -> (area -> measurement), from every measurement under <run>/measurements at
    # any depth: measurements/<platform>/<area>.json, or measurements/<area>.json in a run
    # from before ADR-0504. A measurement without a platform field is a windows one.
    $folder = Join-Path $Directory 'measurements'
    if (-not (Test-Path -LiteralPath $folder -PathType Container)) { throw "No measurements folder in $Directory." }
    $platforms = @{}
    foreach ($file in @(Get-ChildItem -LiteralPath $folder -Filter '*.json' -File -Recurse)) {
        if ($file.Name.StartsWith('release-')) { continue }
        $measurement = Read-JsonFile $file.FullName
        if ($null -eq $measurement -or $null -eq $measurement.PSObject.Properties['area']) { continue }
        $platform = 'windows'
        if ($null -ne $measurement.PSObject.Properties['platform'] -and $measurement.platform) { $platform = [string]$measurement.platform }
        if (-not $platforms.ContainsKey($platform)) { $platforms[$platform] = @{} }
        $platforms[$platform][[string]$measurement.area] = $measurement
    }
    if ($platforms.Count -eq 0) { throw "No area measurements in $folder." }
    return $platforms
}

function Read-PlatformStates([string]$Directory) {
    # platform -> { State, Reason } from <run>/platforms.json, written by RunGapAnalysis.ps1;
    # empty when the run has none.
    $states = @{}
    $json = Read-JsonFile (Join-Path $Directory 'platforms.json')
    if ($null -eq $json) { return $states }
    foreach ($property in $json.PSObject.Properties) {
        $reason = $null
        if ($null -ne $property.Value.PSObject.Properties['reason'] -and $property.Value.reason) { $reason = [string]$property.Value.reason }
        $states[$property.Name] = [pscustomobject]@{ State = [string]$property.Value.state; Reason = $reason }
    }
    return $states
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
    # A finding without a platform field is a windows finding (ADR-0504).
    $platform = 'windows'; if ($front.ContainsKey('platform') -and $front['platform']) { $platform = $front['platform'] }
    return [pscustomobject]@{
        Id         = $front['id']
        Line       = "- $($front['id']) $($front['title']) ($($front['severity']), $platform)"
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

function Get-PlatformScore([string]$Platform, $Measurements, $History, $Diff) {
    # One platform's scores: its detail rows, newest rows, reason rows, history areas and
    # sums, and one Scores cell per area, against the newest history entries of the same
    # platform.
    $samePlatform = @($History | Where-Object { $_.platform -eq $Platform })
    $previous = $null
    if ($samePlatform.Length -gt 0) { $previous = $samePlatform[$samePlatform.Length - 1] }
    $carried = @{}
    foreach ($entry in $samePlatform) {
        foreach ($property in $entry.areas.PSObject.Properties) {
            if (-not $Measurements.ContainsKey($property.Name)) { $carried[$property.Name] = [pscustomobject]@{ Stamp = [string]$entry.stamp; X = [long]$property.Value.x; Y = [long]$property.Value.y } }
        }
    }
    $rows = New-Object System.Collections.Generic.List[string]
    $reasonRows = New-Object System.Collections.Generic.List[string]
    $areasJson = New-Object System.Collections.Generic.List[string]
    $cells = @{}
    $newestCells = @{}
    $sumX = 0; $sumY = 0; $sumUnmeasured = 0; $sumExcluded = 0; $newestX = 0; $newestY = 0
    $allAreas = @($Measurements.Keys) + @($carried.Keys)
    foreach ($area in (Get-SortedAreas $allAreas)) {
        if ($Measurements.ContainsKey($area)) {
            $measurement = $Measurements[$area]
            $x = [long]$measurement.counts.x; $y = [long]$measurement.counts.y
            $unmeasured = [long]$measurement.counts.unmeasured; $excluded = [long]$measurement.counts.excluded
            $percent = Get-Percent $x $y
            $before = $null
            foreach ($entry in $samePlatform) { if ($null -ne $entry.areas.PSObject.Properties[$area]) { $before = $entry.areas.$area } }
            if ($null -eq $before) { $change = 'first run' }
            else { $change = Format-Change $percent (Get-Percent ([long]$before.x) ([long]$before.y)) }
            $rows.Add("| $area | $x | $y | $(Format-Percent $percent) | $unmeasured | $excluded | $change |")
            $cells[$area] = "$x of $y, $(Format-PercentText $percent) ($change)"
            $sumUnmeasured += $unmeasured; $sumExcluded += $excluded
            $areasJson.Add("""$area"": { ""x"": $x, ""y"": $y }")
            if ($null -ne $Diff) { $score = Get-NewestScore $measurement $Diff } else { $score = @{ x = $x; y = $y } }
            $reasons = @{}
            foreach ($item in @($measurement.items)) {
                if ($item.state -ne 'unmeasured' -and $item.state -ne 'excluded') { continue }
                $reason = '(none)'; if ($null -ne $item.reason) { $reason = [string]$item.reason }
                if ($reasons.ContainsKey($reason)) { $reasons[$reason]++ } else { $reasons[$reason] = 1 }
            }
            $reasonNames = New-Object System.Collections.Generic.List[string]
            foreach ($name in $reasons.Keys) { $reasonNames.Add($name) }
            $reasonNames.Sort([System.StringComparer]::Ordinal)
            foreach ($name in $reasonNames) { $reasonRows.Add("| $area | $Platform | $name | $($reasons[$name]) |") }
        }
        else {
            $carry = $carried[$area]
            $x = $carry.X; $y = $carry.Y
            $rows.Add("| $area (carried from $($carry.Stamp)) | $x | $y | $(Format-Percent (Get-Percent $x $y)) | - | - | - |")
            $cells[$area] = "$x of $y, $(Format-PercentText (Get-Percent $x $y)) (carried from $($carry.Stamp))"
            $score = @{ x = $x; y = $y }
        }
        $sumX += $x; $sumY += $y
        $newestX += $score.x; $newestY += $score.y
        $newestCells[$area] = "$($score.x) of $($score.y), $(Format-PercentText (Get-Percent $score.x $score.y))"
    }
    $overallPercent = Get-Percent $sumX $sumY
    if ($null -eq $previous) { $overallChange = 'first run' }
    else { $overallChange = Format-Change $overallPercent (Get-Percent ([long]$previous.overall.x) ([long]$previous.overall.y)) }
    $rows.Add("| overall | $sumX | $sumY | $(Format-Percent $overallPercent) | $sumUnmeasured | $sumExcluded | $overallChange |")
    $cells['overall'] = "$sumX of $sumY, $(Format-PercentText $overallPercent) ($overallChange)"
    $newestCells['overall'] = "$newestX of $newestY, $(Format-PercentText (Get-Percent $newestX $newestY))"
    return [pscustomobject]@{
        Platform = $Platform; Rows = $rows.ToArray(); ReasonRows = $reasonRows.ToArray()
        AreasJson = $areasJson.ToArray(); Cells = $cells; NewestCells = $newestCells; Areas = (Get-SortedAreas $allAreas)
        X = [long]$sumX; Y = [long]$sumY; Percent = $overallPercent; NewestX = [long]$newestX; NewestY = [long]$newestY
        NewestPercent = (Get-Percent $newestX $newestY)
    }
}

function Get-LowestPlatform($Scores, [string]$Property) {
    # The platform score whose $Property (Percent or NewestPercent) is lowest; a platform
    # with Y = 0 has none and is passed over. $null when no platform has a percentage.
    $lowest = $null
    foreach ($score in @($Scores)) {
        if ($null -eq $score.$Property) { continue }
        if ($null -eq $lowest -or [double]$score.$Property -lt [double]$lowest.$Property) { $lowest = $score }
    }
    return $lowest
}

function Format-PlatformTable($Scores, [string[]]$Columns, [string]$CellsProperty) {
    # The Scores (or newest) table: one row per area and an overall row, one column per
    # platform; a platform with no measurement says "missing", an area a platform did not
    # measure says "not measured".
    $areas = New-Object System.Collections.Generic.List[string]
    foreach ($score in @($Scores)) { foreach ($area in $score.Areas) { if (-not $areas.Contains($area)) { $areas.Add($area) } } }
    $lines = New-Object System.Collections.Generic.List[string]
    $lines.Add('| Area | ' + ($Columns -join ' | ') + ' |')
    $lines.Add('| --- |' + ((@($Columns) | ForEach-Object { ' --- |' }) -join ''))
    $ordered = Get-SortedAreas $areas.ToArray()
    foreach ($area in @($ordered) + @('overall')) {
        $cells = foreach ($platform in $Columns) {
            $score = @($Scores | Where-Object { $_.Platform -eq $platform }) | Select-Object -First 1
            if ($null -eq $score) { 'missing' } elseif ($score.$CellsProperty.ContainsKey($area)) { $score.$CellsProperty[$area] } else { 'not measured' }
        }
        $lines.Add("| $area | " + (@($cells) -join ' | ') + ' |')
    }
    return [string]::Join("`n", $lines.ToArray())
}

function New-GapScorecard {
    $byPlatform = Read-Measurements $RunDirectory
    $states = Read-PlatformStates $RunDirectory
    $first = $null
    $references = @{}
    foreach ($platform in $byPlatform.Keys) {
        foreach ($measurement in $byPlatform[$platform].Values) {
            if ($null -eq $first) { $first = $measurement }
            foreach ($field in 'candidateCommit', 'targetVersion') {
                if ([string]$measurement.$field -ne [string]$first.$field) { throw "Measurements disagree on ${field}: '$($first.$field)' and '$($measurement.$field)'." }
            }
            if (-not $references.ContainsKey($platform)) {
                $references[$platform] = 'documents'
                if ($null -ne $measurement.reference) { $references[$platform] = [string]$measurement.reference }
            }
        }
    }
    $commit = [string]$first.candidateCommit
    $target = [string]$first.targetVersion
    $newest = $NewestVersion
    if (-not $newest) {
        $newestJson = Read-JsonFile (Join-Path (Join-Path $gapRoot 'Baselines') 'newest.json')
        if ($null -ne $newestJson) { $newest = [string]$newestJson.version } else { $newest = $target }
    }
    $measured = @(Get-SortedPlatforms @($byPlatform.Keys))
    $missing = @(Get-SortedPlatforms @($states.Keys | Where-Object { -not $byPlatform.ContainsKey($_) }))
    $columns = [string[]]@($measured + $missing)

    if (-not (Test-Path -LiteralPath $ScorecardsDirectory -PathType Container)) { [void](New-Item -ItemType Directory -Path $ScorecardsDirectory -Force) }
    $historyFile = Join-Path $ScorecardsDirectory 'history.json'
    $scorecardFile = Join-Path $ScorecardsDirectory "$Stamp.md"
    if (Test-Path -LiteralPath $scorecardFile) { throw "Scorecard $scorecardFile already exists." }
    $history = @()
    $historyJson = Read-JsonFile $historyFile
    if ($null -ne $historyJson) { $history = @($historyJson) }
    foreach ($platform in $measured) {
        if (@($history | Where-Object { $_.stamp -eq $Stamp -and $_.platform -eq $platform }).Length -gt 0) { throw "history.json already holds run $Stamp on $platform." }
    }

    $diff = $null
    if ($newest -ne $target) { $diff = Read-ReleaseDiff $RunDirectory $newest }
    $scores = @(foreach ($platform in $measured) { Get-PlatformScore $platform $byPlatform[$platform] $history $diff })
    $lowest = Get-LowestPlatform $scores 'Percent'
    $newestLowest = Get-LowestPlatform $scores 'NewestPercent'

    $missingReasons = @(foreach ($platform in $missing) {
            $reason = $states[$platform].Reason
            if (-not $reason) { $reason = 'no reason given' }
            "$platform ($reason)"
        })
    $missingText = ''
    if ($missingReasons.Length -gt 0) { $missingText = ' Missing: ' + ($missingReasons -join '; ') + '.' }
    if ($null -eq $lowest) { $headline = "Overall: n/a; no platform has a score.$missingText" }
    else { $headline = "Overall: $($lowest.X) of $($lowest.Y), $(Format-PercentText $lowest.Percent), on $($lowest.Platform), the lowest of $($measured -join ', ').$missingText" }
    if ($null -eq $newestLowest) { $newestHeadline = 'Overall: n/a.' }
    else { $newestHeadline = "Overall: $($newestLowest.NewestX) of $($newestLowest.NewestY), $(Format-PercentText $newestLowest.NewestPercent), on $($newestLowest.Platform), the lowest of $($measured -join ', ')." }

    if ($newest -eq $target) { $newestIntro = "Newest version $newest is the target; the target figures are repeated." }
    elseif ($null -eq $diff) { $newestIntro = "Release $newest. This run has no release diff for it; the target figures are repeated." }
    else { $newestIntro = "Release $newest." }

    $platformSections = New-Object System.Collections.Generic.List[string]
    foreach ($score in $scores) {
        if ($platformSections.Count -gt 0) { $platformSections.Add('') }
        $platformSections.Add("### $($score.Platform)")
        $platformSections.Add('')
        $platformSections.Add('| Area | X | Y | % | Unmeasured | Excluded | Change |')
        $platformSections.Add('| --- | --- | --- | --- | --- | --- | --- |')
        foreach ($row in $score.Rows) { $platformSections.Add($row) }
    }
    $reasonRows = @(foreach ($score in $scores) { $score.ReasonRows })
    $reasonText = 'None.'
    if ($reasonRows.Length -gt 0) { $reasonText = "| Area | Platform | Reason | Count |`n| --- | --- | --- | --- |`n" + [string]::Join("`n", [string[]]$reasonRows) }
    $runLines = @("- Commit: $commit", "- Platforms: $($measured -join ', ')")
    foreach ($line in $missingReasons) { $runLines += "- Missing: $line" }
    $runLines += @("- Target: $target", "- Newest: $newest")
    foreach ($platform in $measured) { $runLines += "- Reference on ${platform}: $($references[$platform])" }

    $markdown = @(
        "# Gap scorecard $Stamp", '',
        '## Run', '',
        [string]::Join("`n", [string[]]$runLines), '',
        '## Scores', '',
        $headline, '',
        (Format-PlatformTable $scores $columns 'Cells'), '',
        '## Platforms', '',
        [string]::Join("`n", $platformSections.ToArray()), '',
        '## Against the newest version', '',
        $newestIntro, '', $newestHeadline, '',
        (Format-PlatformTable $scores $columns 'NewestCells'), '',
        '## New gaps', '', (Format-Lines (Get-FindingLines 'new')), '',
        '## Closed', '', (Format-Lines (Get-FindingLines 'closed')), '',
        '## Regressions', '', (Format-Lines (Get-FindingLines 'regressions')), '',
        '## Unmeasured by reason', '', $reasonText
    )
    $scorecard = [string]::Join("`n", [string[]]$markdown) + "`n"
    if ($scorecard -match '[^\x00-\x7F]') { throw 'The scorecard would hold a non-ASCII character.' }

    $entries = foreach ($score in $scores) {
        [string]::Join("`n", [string[]]@(
                '  {',
                "    ""stamp"": ""$Stamp"",",
                "    ""commit"": ""$commit"",",
                "    ""platform"": ""$($score.Platform)"",",
                "    ""targetVersion"": ""$target"",",
                "    ""newestVersion"": ""$newest"",",
                "    ""areas"": { $([string]::Join(', ', [string[]]$score.AreasJson)) },",
                "    ""overall"": { ""x"": $($score.X), ""y"": $($score.Y) },",
                "    ""newest"": { ""x"": $($score.NewestX), ""y"": $($score.NewestY) }",
                '  }'))
    }
    $entryText = [string]::Join(",`n", [string[]]@($entries))
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
        $scores = Get-Section $firstText 'Platforms'
        Test-Check 'per-area X, Y and % for the first run' ($scores.Contains('| options | 3 | 5 | 60.0 | 1 | 1 | first run |') -and $scores.Contains('| writeout | 2 | 2 | 100.0 | 0 | 0 | first run |'))
        Test-Check 'an area with Y = 0 prints n/a' ($scores.Contains('| exitcodes | 0 | 0 | n/a | 0 | 1 | first run |'))
        Test-Check 'overall is the sum of X over the sum of Y (5 of 7)' ($scores.Contains('| overall | 5 | 7 | 71.4 | 1 | 2 | first run |'))
        Test-Check 'first run when only another platform has history' (-not $scores.Contains('environment') -and $scores.Contains('| overall | 5 | 7 | 71.4 | 1 | 2 | first run |'))
        Test-Check 'the Scores table has one column per platform and an overall row' ((Get-Section $firstText 'Scores') -ceq "Overall: 5 of 7, 71.4%, on windows, the lowest of windows.`n`n| Area | windows |`n| --- | --- |`n| options | 3 of 5, 60.0% (first run) |`n| writeout | 2 of 2, 100.0% (first run) |`n| exitcodes | 0 of 0, n/a (first run) |`n| overall | 5 of 7, 71.4% (first run) |")
        $newestFirst = Get-Section $firstText 'Against the newest version'
        Test-Check 'newest equal to the target says so and repeats the target figures' ($newestFirst.StartsWith('Newest version 8.21.0 is the target') -and $newestFirst.Contains('| overall | 5 of 7, 71.4% |'))
        $headings = [string]::Join('|', [string[]]@([regex]::Matches($firstText, '(?m)^## (.+)$') | ForEach-Object { $_.Groups[1].Value }))
        Test-Check "the scorecard has exactly the fixed sections in order ($headings)" ($headings -ceq 'Run|Scores|Platforms|Against the newest version|New gaps|Closed|Regressions|Unmeasured by reason')
        Test-Check 'the Run section names commit, platforms, target, newest and each reference' ((Get-Section $firstText 'Run') -ceq "- Commit: d15d115db0a1b2c3d4e5f60718293a4b5c6d7e01`n- Platforms: windows`n- Target: 8.21.0`n- Newest: 8.21.0`n- Reference on windows: curl 8.21.0 (x86_64-w64-mingw32) libcurl/8.21.0 Schannel")
        Test-Check 'unmeasured by reason sums each area and platform per reason' ((Get-Section $firstText 'Unmeasured by reason') -ceq "| Area | Platform | Reason | Count |`n| --- | --- | --- | --- |`n| options | windows | needs-server:smtp | 1 |`n| options | windows | platform:linux | 1 |`n| exitcodes | windows | obsolete-code | 1 |")
        Test-Check 'the first run lists the findings it opened, with their platforms' ((Get-Section $firstText 'New gaps') -ceq "- GF-0002 The --d option differs (Medium, windows)`n- GF-0004 The --b help line differs (Low, windows)" -and (Get-Section $firstText 'Closed') -ceq 'None.')

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
        $scores = Get-Section $secondText 'Platforms'
        Test-Check 'the change against the previous same-platform entry (+20.0 and +14.3)' ($scores.Contains('| options | 4 | 5 | 80.0 | 0 | 1 | +20.0 |') -and $scores.Contains('| overall | 6 | 7 | 85.7 | 0 | 1 | +14.3 |'))
        Test-Check 'an unmeasured area is carried with its stamp and counted in overall' ($scores.Contains('| writeout (carried from 2026-10-09_1430) | 2 | 2 | 100.0 | - | - | - |') -and $scores.Contains('| exitcodes (carried from 2026-10-09_1430) | 0 | 0 | n/a | - | - | - |') -and (Get-Section $secondText 'Scores').Contains('| writeout | 2 of 2, 100.0% (carried from 2026-10-09_1430) |'))
        $newestSecond = Get-Section $secondText 'Against the newest version'
        Test-Check 'the newest-version section follows Gap-Format.md section 10' (
            $newestSecond.StartsWith('Release 8.22.0.') -and $newestSecond.Contains('| options | 2 of 6, 33.3% |') -and
            $newestSecond.Contains('| writeout | 2 of 2, 100.0% |') -and $newestSecond.Contains('| overall | 4 of 8, 50.0% |') -and $newestSecond.Contains('Overall: 4 of 8, 50.0%, on windows'))
        Test-Check 'new gaps, closed and regressions list this run''s findings only' (
            (Get-Section $secondText 'New gaps') -ceq '- GF-0001 The --e option is missing (High, windows)' -and
            (Get-Section $secondText 'Closed') -ceq '- GF-0002 The --d option differs (Medium, windows)' -and
            (Get-Section $secondText 'Regressions') -ceq '- GF-0003 The --c alias differs (Low, windows)')
        Test-Check 'a section with nothing to list says None.' ((Get-Section $secondText 'Unmeasured by reason') -ceq "| Area | Platform | Reason | Count |`n| --- | --- | --- | --- |`n| options | windows | platform:linux | 1 |" -and (Get-Section $firstText 'Regressions') -ceq 'None.')

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
            @(Read-JsonFile $historyFile).Length -eq 1 -and (Get-Section $thirdText 'Platforms').Contains('| overall | 4 | 5 | 80.0 | 0 | 1 | first run |'))
        Test-Check 'a newest version with no release diff repeats the target figures' ((Get-Section $thirdText 'Against the newest version').StartsWith('Release 8.23.0. This run has no release diff'))
        $bytes = [System.IO.File]::ReadAllBytes($historyFile)
        Test-Check 'written files have no byte order mark and no carriage return' ($bytes[0] -eq 0x5B -and -not ($bytes -contains 13) -and -not ([System.IO.File]::ReadAllBytes((Join-Path $ScorecardsDirectory '2026-10-10_0900.md')) -contains 13))

        # Platforms (ADR-0504): windows 3 of 5, linux 2 of 5, macos missing per platforms.json.
        $script:RunDirectory = Join-Path $temp 'platforms'
        $script:ScorecardsDirectory = Join-Path $temp 'PlatformScorecards'
        $script:Stamp = '2026-10-15_1200'
        $script:NewestVersion = '8.21.0'
        $platformText = Read-TextFile (New-GapScorecard)
        $platformScores = Get-Section $platformText 'Scores'
        Test-Check 'the overall headline is the lowest platform''s score' ($platformScores.StartsWith('Overall: 2 of 5, 40.0%, on linux, the lowest of windows, linux.'))
        Test-Check 'a missing platform is named with its reason in the headline, the Run section and its column' ($platformScores.Contains('Missing: macos (gap-platforms run 42 timed out after 180 minutes).') -and (Get-Section $platformText 'Run').Contains('- Missing: macos (gap-platforms run 42 timed out after 180 minutes)') -and $platformScores.Contains('| Area | windows | linux | macos |') -and $platformScores.Contains('| options | 3 of 5, 60.0% (first run) | 2 of 5, 40.0% (first run) | missing |'))
        Test-Check 'each platform has its own detail table and reference' ((Get-Section $platformText 'Platforms').Contains("### linux`n`n| Area | X | Y | % | Unmeasured | Excluded | Change |") -and (Get-Section $platformText 'Run').Contains('- Reference on linux: curl 8.21.0 (x86_64-pc-linux-gnu) libcurl/8.21.0 OpenSSL/3.0.13'))
        $platformHistory = @(Read-JsonFile (Join-Path $ScorecardsDirectory 'history.json'))
        Test-Check 'history gains one entry per measured platform, none for the missing one' ($platformHistory.Length -eq 2 -and $platformHistory[0].platform -eq 'windows' -and $platformHistory[1].platform -eq 'linux' -and $platformHistory[1].overall.x -eq 2 -and $platformHistory[1].stamp -eq '2026-10-15_1200')
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
