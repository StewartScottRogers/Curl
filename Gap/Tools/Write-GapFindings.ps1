<#
.SYNOPSIS
    Turns one gap run's area measurements and analyst report blocks into
    Gap/Findings/GF-####-<slug>.md files, and closes or reopens findings only on
    re-measurement on their own platform.

.DESCRIPTION
    ADR-0433 decisions 3 and 5, ADR-0505. Formats are in Gap/Instructions/Gap-Format.md
    (sections 4 to 7); files follow Gap/Findings/FINDING-TEMPLATE.md's front matter and
    section order. Every rule is mechanical: read from the measurement files, never from an
    analyst's verdict.

    The areas measured are the <area>.json files under <RunDirectory>/measurements, at any
    depth: measurements/<platform>/<area>.json, or measurements/<area>.json in a run from
    before ADR-0505 (release-*.json files are release diffs, not areas). Each measurement's
    platform field (windows, linux or macos) is its platform. Every finding has one
    platform; a finding without a platform field is a windows finding. Rules:

    0. A platform's own gaps: a gap on platform P is covered, and left to another
       platform's finding, when an earlier platform Q in the order windows, linux, macos
       measured the area in this run and measured the item as gap too, or did not measure
       the area and has an open finding listing it. Every windows gap is windows' own. Only
       a platform's own gaps are filed in its findings (rules 1 and 2), so one cause seen on
       every platform is one finding, not three.
    1. The last fenced json block of each <RunDirectory>/reports/<platform>/gap-<area>.md
       (for windows also reports/gap-<area>.md, as runs before ADR-0505 wrote it) is the
       analyst's report block for that platform. A group's key is its "key" field when it
       has one, else "<area>:<slug of its title>". A group whose key and platform equal an
       existing finding's is that finding: items it names are added to the finding's items.
       Otherwise a new GF-#### (the next number after the highest in the folder) is filed
       with the platform, status open, scope target, opened <Stamp>, every field from the
       group, and its evidence and suggestion in their sections. On linux and macos a
       group's items are first cut to the platform's own gaps, and a group left empty files
       nothing.
    2. A platform's own gap item that no group covers and no finding of the area and
       platform lists is filed in one catch-all finding per area and platform, keyed
       <area>:ungrouped, severity Low. A warning names the analyst and the platform.
    3. Every finding of a measured area and platform gets a Measurements line,
       "<Stamp>: <n> of <m> items are gaps." (FINDING-TEMPLATE.md's wording), with
       "<k> not in this run's measurement." added when items are absent. A rerun with the
       same stamp does not add a second line.
    4. An open finding whose every item its platform's measurement in this run measures as
       match, or as excluded with a reason, gets status closed, closed <Stamp> and a Log
       line naming the platform. Nothing else closes a finding: not a task reaching Done,
       not a missing report, not an absent item, not another platform's measurement.
    5. A closed finding with any item measured as gap on its platform goes back to status
       open, with regression true, closed cleared and a Log line naming the run and the
       platform. No new finding is filed for it.
    6. A rejected finding is never changed, apart from a Measurements line.
    7. Findings of areas or platforms this run did not measure are not touched at all.
    8. -ReleaseDiff items become one scope newest finding per changed area, keyed
       <area>:newest, platform windows, with introduced-in the diff's toVersion and the
       changed items listed. Severity (ADR-0433 decision 3): added is High (missing
       outright), changed is Medium, removed is Low; the finding takes the highest. A later
       diff that lists the same item key adds nothing; new keys are added to the same
       finding.
    9. One summary line: new, still open, closed, reopened as regressions, ungrouped items.

.PARAMETER RunDirectory
    The run folder: measurements/<platform>/<area>.json and
    reports/<platform>/gap-<area>.md. Required unless -SelfTest is given.

.PARAMETER Stamp
    The run stamp, yyyy-MM-dd_HHmm. Required unless -SelfTest is given.

.PARAMETER FindingsDirectory
    The findings folder. Default Gap/Findings beside this script's Gap folder.

.PARAMETER ReleaseDiff
    Optional release-<version>.json (Gap-Format.md section 5); rule 8.

.PARAMETER ListUncovered
    Prints each platform's own gap items (rule 0), one "<platform> <area> <key>" line each,
    and changes nothing. RunGapAnalysis.ps1 hands the linux and macos lines to the analysts.

.PARAMETER WhatIf
    Prints each file it would write or change and changes nothing.

.PARAMETER SelfTest
    Copies Gap/Tools/Fixtures/findings to a temporary folder before each check, runs the
    script against it, then runs it against a two-platform run built in a temporary folder;
    prints a PASS or FAIL line per check, and exits 1 on any FAIL.
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string] $RunDirectory,
    [string] $Stamp,
    [string] $FindingsDirectory,
    [string] $ReleaseDiff,
    [switch] $ListUncovered,
    [switch] $SelfTest
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2
# Windows PowerShell 5.1 leaves $PSScriptRoot empty inside param defaults.
if (-not $FindingsDirectory) { $FindingsDirectory = Join-Path (Split-Path $PSScriptRoot -Parent) 'Findings' }

$script:FrontOrder = @('id', 'title', 'area', 'platform', 'key', 'severity', 'status', 'scope', 'introduced-in',
    'opened', 'closed', 'regression', 'items', 'touches', 'task', 'tasks')
$script:SectionOrder = @('Summary', 'Evidence', 'Suggestion', 'Measurements', 'Log')
$script:SeverityRank = @{ 'Low' = 0; 'Medium' = 1; 'High' = 2; 'Critical' = 3 }
$script:PlatformOrder = @('windows', 'linux', 'macos')

function ConvertTo-List([string] $Value) {
    $inner = $Value.Trim().TrimStart('[').TrimEnd(']').Trim()
    if ($inner -eq '') { return , @() }
    return , @($inner -split '\s*,\s*' | Where-Object { $_ -ne '' })
}

function ConvertTo-Slug([string] $Text) {
    $slug = ($Text.ToLowerInvariant() -replace '[^a-z0-9]+', '-').Trim('-')
    if ($slug.Length -gt 60) { $slug = $slug.Substring(0, 60).Trim('-') }
    return $slug
}

function Read-Finding([string] $Path) {
    $lines = ([IO.File]::ReadAllText($Path) -replace "`r`n", "`n") -split "`n"
    $front = [ordered]@{}
    $sections = [ordered]@{}
    $index = 1
    while ($index -lt $lines.Count -and $lines[$index] -ne '---') {
        if ($lines[$index] -match '^([a-z-]+):\s?(.*)$') { $front[$Matches[1]] = $Matches[2].Trim() }
        $index++
    }
    # A finding filed before findings carried a platform is a Windows finding (ADR-0505).
    if (-not $front.Contains('platform') -or -not $front['platform']) {
        $withPlatform = [ordered]@{}
        foreach ($name in @($front.Keys)) {
            if ($name -ne 'platform') { $withPlatform[$name] = $front[$name] }
            if ($name -eq 'area') { $withPlatform['platform'] = 'windows' }
        }
        if (-not $withPlatform.Contains('platform')) { $withPlatform['platform'] = 'windows' }
        $front = $withPlatform
    }
    $current = $null
    for ($index++; $index -lt $lines.Count; $index++) {
        $line = $lines[$index]
        if ($line -match '^## (.+)$') { $current = $Matches[1].Trim(); $sections[$current] = New-Object Collections.Generic.List[string]; continue }
        if ($null -ne $current -and $line -ne '') { $sections[$current].Add($line) }
    }
    $finding = [pscustomobject]@{ Path = $Path; Front = $front; Sections = $sections; Original = (Format-Finding -Front $front -Sections $sections); IsNew = $false }
    return $finding
}

function Format-Finding($Front, $Sections) {
    $builder = New-Object Text.StringBuilder
    [void] $builder.Append("---`n")
    foreach ($name in $script:FrontOrder) {
        $value = if ($Front.Contains($name)) { [string] $Front[$name] } else { '' }
        if ($value -eq '') { [void] $builder.Append("${name}:`n") } else { [void] $builder.Append("${name}: $value`n") }
    }
    [void] $builder.Append("---`n# $($Front['id']) - $($Front['title'])`n")
    foreach ($name in $script:SectionOrder) {
        [void] $builder.Append("`n## $name`n`n")
        if ($Sections.Contains($name)) { foreach ($line in $Sections[$name]) { [void] $builder.Append("$line`n") } }
    }
    return $builder.ToString()
}

function Get-Items($Finding) { return , (ConvertTo-List $Finding.Front['items']) }

function Set-Items($Finding, [string[]] $Items) { $Finding.Front['items'] = '[' + ($Items -join ', ') + ']' }

function Add-SectionLine($Finding, [string] $Section, [string] $Line) {
    if (-not $Finding.Sections.Contains($Section)) { $Finding.Sections[$Section] = New-Object Collections.Generic.List[string] }
    $Finding.Sections[$Section].Add($Line)
}

function New-Finding([string] $Area, [string] $Platform, [string] $Key, [string] $Title, [string] $Severity, [string] $Scope, [string] $IntroducedIn, [string[]] $Items, [string[]] $Touches) {
    $script:NextNumber++
    $id = 'GF-{0:D4}' -f $script:NextNumber
    $front = [ordered]@{
        'id' = $id; 'title' = $Title; 'area' = $Area; 'platform' = $Platform; 'key' = $Key; 'severity' = $Severity; 'status' = 'open'
        'scope' = $Scope; 'introduced-in' = $IntroducedIn; 'opened' = $Stamp; 'closed' = ''; 'regression' = 'false'
        'items' = '[' + ($Items -join ', ') + ']'; 'touches' = '[' + ($Touches -join ', ') + ']'; 'task' = ''; 'tasks' = '[]'
    }
    $path = Join-Path $FindingsDirectory ("$id-" + (ConvertTo-Slug $Title) + '.md')
    $finding = [pscustomobject]@{ Path = $path; Front = $front; Sections = [ordered]@{}; Original = ''; IsNew = $true }
    $script:Findings.Add($finding)
    return $finding
}

function Get-ReportBlock([string] $Path) {
    $text = [IO.File]::ReadAllText($Path) -replace "`r`n", "`n"
    $blocks = [regex]::Matches($text, '(?ms)^```json[ \t]*\n(.*?)^```')
    if ($blocks.Count -eq 0) { return $null }
    return ($blocks[$blocks.Count - 1].Groups[1].Value | ConvertFrom-Json)
}

function Find-ByKey([string] $Key, [string] $Platform) {
    foreach ($finding in $script:Findings) { if ($finding.Front['key'] -eq $Key -and $finding.Front['platform'] -eq $Platform) { return $finding } }
    return $null
}

function Get-PlatformRank([string] $Platform) {
    $rank = [Array]::IndexOf($script:PlatformOrder, $Platform)
    if ($rank -lt 0) { return $script:PlatformOrder.Count }
    return $rank
}

# Rule 0 (ADR-0505): the gap items of each platform and area that are that platform's own.
# A gap on platform P is covered, and left to another platform's finding, when an earlier
# platform Q in the order windows, linux, macos measured the area in this run and measured
# the item as gap too, or did not measure the area and has an open finding listing it.
# Returns a hashtable "<platform>|<area>" -> hashtable of uncovered gap keys.
function Get-UncoveredGaps($Measured) {
    $uncovered = @{}
    foreach ($entry in @($Measured | Sort-Object { Get-PlatformRank $_.Platform }, { $_.Area })) {
        $own = @{}
        foreach ($item in @($entry.Measurement.items | Where-Object { $_.state -eq 'gap' })) {
            $covered = $false
            foreach ($earlier in @($script:PlatformOrder | Where-Object { (Get-PlatformRank $_) -lt (Get-PlatformRank $entry.Platform) })) {
                $other = @($Measured | Where-Object { $_.Platform -eq $earlier -and $_.Area -eq $entry.Area }) | Select-Object -First 1
                if ($null -ne $other) {
                    if (@($other.Measurement.items | Where-Object { $_.key -eq $item.key -and $_.state -eq 'gap' }).Count -gt 0) { $covered = $true }
                }
                elseif (@($script:Findings | Where-Object { $_.Front['platform'] -eq $earlier -and $_.Front['area'] -eq $entry.Area -and $_.Front['status'] -eq 'open' -and (Get-Items $_) -contains $item.key }).Count -gt 0) { $covered = $true }
                if ($covered) { break }
            }
            if (-not $covered) { $own[[string] $item.key] = $true }
        }
        $uncovered["$($entry.Platform)|$($entry.Area)"] = $own
    }
    return $uncovered
}

# Every area measurement under <RunDirectory>/measurements, at any depth (one folder per
# platform, or the files themselves in a run from before ADR-0505), except release diffs.
function Get-RunMeasurements {
    $folder = Join-Path $RunDirectory 'measurements'
    if (-not (Test-Path $folder)) { return , @() }
    $files = @(Get-ChildItem -Path $folder -Filter '*.json' -Recurse -File | Where-Object { $_.Name -notlike 'release-*' })
    $measured = @(foreach ($file in $files) {
            $measurement = [IO.File]::ReadAllText($file.FullName) | ConvertFrom-Json
            if (-not $measurement.PSObject.Properties['area']) { continue }
            $platform = if ($measurement.PSObject.Properties['platform'] -and $measurement.platform) { [string] $measurement.platform } else { 'windows' }
            [pscustomobject]@{ Platform = $platform; Area = [string] $measurement.area; Measurement = $measurement }
        })
    return , @($measured | Sort-Object { Get-PlatformRank $_.Platform }, { $_.Area })
}

# The analyst report of one platform and area: reports/<platform>/gap-<area>.md, or for
# windows reports/gap-<area>.md as a run from before ADR-0505 wrote it.
function Get-ReportPath([string] $Platform, [string] $Area) {
    $reports = Join-Path $RunDirectory 'reports'
    $path = Join-Path (Join-Path $reports $Platform) "gap-$Area.md"
    if ((-not (Test-Path $path)) -and $Platform -eq 'windows') { $path = Join-Path $reports "gap-$Area.md" }
    return $path
}

function Invoke-ReleaseDiff([string] $Path) {
    $diff = [IO.File]::ReadAllText($Path) | ConvertFrom-Json
    $severityOf = @{ 'added' = 'High'; 'changed' = 'Medium'; 'removed' = 'Low' }
    foreach ($areaGroup in @($diff.items | Group-Object { ($_.key -split ':', 2)[0] })) {
        $area = $areaGroup.Name
        $severity = 'Low'
        foreach ($item in $areaGroup.Group) { if ($script:SeverityRank[$severityOf[$item.change]] -gt $script:SeverityRank[$severity]) { $severity = $severityOf[$item.change] } }
        $keys = @($areaGroup.Group | ForEach-Object { $_.key })
        $finding = Find-ByKey "${area}:newest" 'windows'
        if ($null -eq $finding) {
            $finding = New-Finding -Area $area -Platform 'windows' -Key "${area}:newest" -Title "Upstream curl $($diff.toVersion) changed $area items" -Severity $severity -Scope 'newest' -IntroducedIn $diff.toVersion -Items $keys -Touches @()
            Add-SectionLine $finding 'Summary' "Upstream curl $($diff.toVersion) added, changed or removed $area items since $($diff.fromVersion)."
            foreach ($item in $areaGroup.Group) { Add-SectionLine $finding 'Evidence' "- $($item.key): $($item.change) in $($diff.toVersion)." }
            Add-SectionLine $finding 'Suggestion' "Bring these items to Curl when the target moves to $($diff.toVersion)."
            Add-SectionLine $finding 'Log' "- ${Stamp}: Opened from the release diff $($diff.fromVersion) to $($diff.toVersion)."
            $script:Summary.New++
            continue
        }
        if ($finding.Front['status'] -eq 'rejected') { continue }
        $existing = Get-Items $finding
        $added = @($keys | Where-Object { $existing -notcontains $_ })
        if ($added.Count -eq 0) { continue }
        Set-Items $finding (@($existing) + $added)
        if ($script:SeverityRank[$severity] -gt $script:SeverityRank[$finding.Front['severity']]) { $finding.Front['severity'] = $severity }
        foreach ($item in @($areaGroup.Group | Where-Object { $added -contains $_.key })) { Add-SectionLine $finding 'Evidence' "- $($item.key): $($item.change) in $($diff.toVersion)." }
        Add-SectionLine $finding 'Log' "- ${Stamp}: Added $($added -join ', ') from the release diff to $($diff.toVersion)."
    }
}

function Invoke-ReportGroups([string] $Area, [string] $Platform, $Block, [hashtable] $Own) {
    foreach ($group in @($Block.groups)) {
        $key = if ($group.PSObject.Properties['key'] -and $group.key) { [string] $group.key } else { "${Area}:" + (ConvertTo-Slug $group.title) }
        # A platform's finding lists only that platform's own gaps (rule 0); for windows every gap is its own.
        $groupItems = @($group.items | ForEach-Object { [string] $_ } | Where-Object { $Platform -eq 'windows' -or $Own.ContainsKey($_) })
        if ($groupItems.Count -eq 0) { continue }
        $finding = Find-ByKey $key $Platform
        if ($null -eq $finding) {
            $introduced = if ($group.introducedIn) { [string] $group.introducedIn } else { '' }
            $finding = New-Finding -Area $Area -Platform $Platform -Key $key -Title $group.title -Severity $group.severity -Scope 'target' -IntroducedIn $introduced -Items $groupItems -Touches @($group.touches | ForEach-Object { [string] $_ })
            $where = if ($Platform -eq 'windows') { '' } else { " on $Platform" }
            Add-SectionLine $finding 'Summary' "Curl differs from upstream curl in $($Area)${where}: $($group.title)."
            Add-SectionLine $finding 'Evidence' $group.evidence
            Add-SectionLine $finding 'Suggestion' $group.suggestion
            Add-SectionLine $finding 'Log' "- ${Stamp}: Opened by $($Block.analyst)."
            $script:Summary.New++
            continue
        }
        if ($finding.Front['status'] -eq 'rejected') { continue }
        $existing = Get-Items $finding
        $added = @($groupItems | Where-Object { $existing -notcontains $_ })
        if ($added.Count -gt 0) {
            Set-Items $finding (@($existing) + $added)
            Add-SectionLine $finding 'Log' "- ${Stamp}: Added $($added -join ', ') from $($Block.analyst)."
        }
    }
}

function Invoke-Ungrouped([string] $Area, [string] $Platform, [string] $Analyst, $Measurement, [hashtable] $Own) {
    $listed = @{}
    foreach ($finding in @($script:Findings | Where-Object { $_.Front['area'] -eq $Area -and $_.Front['platform'] -eq $Platform })) { foreach ($item in (Get-Items $finding)) { $listed[$item] = $true } }
    $loose = @($Measurement.items | Where-Object { $_.state -eq 'gap' -and $Own.ContainsKey([string] $_.key) -and -not $listed.ContainsKey($_.key) })
    if ($loose.Count -eq 0) { return }
    Write-Warning "$Analyst left $($loose.Count) $Platform gap item(s) in no group: $(@($loose | ForEach-Object { $_.key }) -join ', ')"
    $script:Summary.Ungrouped += $loose.Count
    $keys = @($loose | ForEach-Object { $_.key })
    $finding = Find-ByKey "${Area}:ungrouped" $Platform
    if ($null -eq $finding) {
        $title = if ($Platform -eq 'windows') { "Gap items $Analyst left in no group" } else { "Gap items $Analyst left in no group on $Platform" }
        $finding = New-Finding -Area $Area -Platform $Platform -Key "${Area}:ungrouped" -Title $title -Severity 'Low' -Scope 'target' -IntroducedIn '' -Items $keys -Touches @()
        Add-SectionLine $finding 'Summary' "Gap items in $Area on $Platform that no analyst group covered, kept so no gap is dropped."
        Add-SectionLine $finding 'Suggestion' "Group these items under their causes in the next $Analyst report."
        Add-SectionLine $finding 'Log' "- ${Stamp}: Opened for items $Analyst left in no group."
        $script:Summary.New++
    }
    else {
        Set-Items $finding (@(Get-Items $finding) + $keys)
        Add-SectionLine $finding 'Log' "- ${Stamp}: Added $($keys -join ', ') that $Analyst left in no group."
    }
    foreach ($item in $loose) { Add-SectionLine $finding 'Evidence' "- $($item.key): expected $($item.expected), actual $($item.actual); $($item.evidence)" }
}

function Update-Status([string] $Area, [string] $Platform, $Measurement) {
    $states = @{}
    foreach ($item in @($Measurement.items)) { $states[$item.key] = $item }
    # A finding is measured, closed and reopened only by its own platform's measurement.
    foreach ($finding in @($script:Findings | Where-Object { $_.Front['area'] -eq $Area -and $_.Front['platform'] -eq $Platform })) {
        $items = Get-Items $finding
        $gaps = @($items | Where-Object { $states.ContainsKey($_) -and $states[$_].state -eq 'gap' })
        $absent = @($items | Where-Object { -not $states.ContainsKey($_) })
        $line = "- ${Stamp}: $($gaps.Count) of $($items.Count) items are gaps."
        if ($absent.Count -gt 0) { $line += " $($absent.Count) not in this run's measurement." }
        $already = $finding.Sections.Contains('Measurements') -and @($finding.Sections['Measurements'] | Where-Object { $_.StartsWith("- ${Stamp}:") }).Count -gt 0
        if (-not $already) { Add-SectionLine $finding 'Measurements' $line }
        $status = $finding.Front['status']
        if ($status -eq 'open') {
            $gone = @($items | Where-Object { $states.ContainsKey($_) -and ($states[$_].state -eq 'match' -or ($states[$_].state -eq 'excluded' -and $states[$_].reason)) })
            if ($items.Count -gt 0 -and $gone.Count -eq $items.Count) {
                $finding.Front['status'] = 'closed'
                $finding.Front['closed'] = $Stamp
                Add-SectionLine $finding 'Log' "- ${Stamp}: Closed: run $Stamp measured every item as match or excluded on $Platform."
                $script:Summary.Closed++
            }
            elseif (-not $finding.IsNew) { $script:Summary.StillOpen++ }
        }
        elseif ($status -eq 'closed' -and $gaps.Count -gt 0) {
            $finding.Front['status'] = 'open'
            $finding.Front['closed'] = ''
            $finding.Front['regression'] = 'true'
            Add-SectionLine $finding 'Log' "- ${Stamp}: Reopened as a regression: run $Stamp measured $($gaps -join ', ') as gap again on $Platform."
            $script:Summary.Reopened++
        }
    }
}

function Invoke-WriteGapFindings {
    if (-not $RunDirectory -or -not $Stamp) { throw '-RunDirectory and -Stamp are required.' }
    $script:Findings = New-Object Collections.Generic.List[object]
    $script:NextNumber = 0
    $script:Summary = @{ New = 0; StillOpen = 0; Closed = 0; Reopened = 0; Ungrouped = 0 }
    if (Test-Path $FindingsDirectory) {
        foreach ($file in @(Get-ChildItem -Path $FindingsDirectory -Filter 'GF-*.md')) {
            if ($file.Name -match '^GF-(\d{4})') { $script:NextNumber = [Math]::Max($script:NextNumber, [int] $Matches[1]) }
            $script:Findings.Add((Read-Finding $file.FullName))
        }
    }
    $measured = Get-RunMeasurements
    $uncovered = Get-UncoveredGaps $measured
    if ($ListUncovered) {
        foreach ($entry in $measured) {
            $own = $uncovered["$($entry.Platform)|$($entry.Area)"]
            $keys = [string[]]@($own.Keys)
            [Array]::Sort($keys, [StringComparer]::Ordinal)
            foreach ($key in $keys) { Write-Output "$($entry.Platform) $($entry.Area) $key" }
        }
        return
    }
    if ($ReleaseDiff) { Invoke-ReleaseDiff $ReleaseDiff }
    foreach ($entry in $measured) {
        $area = $entry.Area; $platform = $entry.Platform
        $own = $uncovered["$platform|$area"]
        $analyst = "gap-$area"
        $reportPath = Get-ReportPath $platform $area
        if (Test-Path $reportPath) {
            $block = Get-ReportBlock $reportPath
            if ($null -ne $block) { $analyst = [string] $block.analyst; Invoke-ReportGroups $area $platform $block $own }
        }
        Invoke-Ungrouped $area $platform $analyst $entry.Measurement $own
        Update-Status $area $platform $entry.Measurement
    }
    foreach ($finding in $script:Findings) {
        $text = Format-Finding -Front $finding.Front -Sections $finding.Sections
        if ($text -eq $finding.Original) { continue }
        $action = if ($finding.IsNew) { 'File new finding' } else { 'Update finding' }
        if ($PSCmdlet.ShouldProcess($finding.Path, $action)) {
            if (-not (Test-Path $FindingsDirectory)) { [void] (New-Item -ItemType Directory -Path $FindingsDirectory) }
            [IO.File]::WriteAllText($finding.Path, $text, (New-Object Text.UTF8Encoding $false))
        }
    }
    $s = $script:Summary
    Write-Output "Gap findings ${Stamp}: $($s.New) new, $($s.StillOpen) still open, $($s.Closed) closed, $($s.Reopened) reopened as regressions, $($s.Ungrouped) ungrouped."
}

# The platform checks (ADR-0505), on a run built in a temporary folder: windows and linux
# measure options; macos measures nothing. Items: a (gap on both), b (gap on linux only),
# c (match on windows, gap on linux), d (gap on windows only). Existing findings: GF-0001
# windows lists a and d; GF-0002 linux lists c; GF-0003 macos lists a (not measured here);
# GF-0004 has no platform field and lists e, which windows measures match.
function Test-Platforms {
    $folder = Join-Path ([IO.Path]::GetTempPath()) ('gapplatforms-' + [Guid]::NewGuid().ToString('N'))
    $findings = Join-Path $folder 'findings'
    $run = Join-Path $folder 'run'
    try {
        foreach ($sub in @($findings, (Join-Path $run 'measurements/windows'), (Join-Path $run 'measurements/linux'), (Join-Path $run 'reports/linux'))) { [void] (New-Item -ItemType Directory -Force -Path $sub) }
        $utf8 = New-Object Text.UTF8Encoding $false
        $measurement = {
            param([string] $Platform, [string[]] $States)
            $items = @(for ($i = 0; $i -lt $States.Count; $i += 2) { '{ "key": "options:' + $States[$i] + '", "state": "' + $States[$i + 1] + '", "reason": null, "expected": "x", "actual": "y", "evidence": "z", "introducedIn": null }' })
            return '{ "area": "options", "targetVersion": "9.9.9", "candidateCommit": "' + ('0' * 40) + '", "platform": "' + $Platform + '", "reference": null, "referenceFallback": "docs", "measuredAt": "2026-10-15T00:00:00Z", "items": [' + ($items -join ', ') + '], "counts": { "match": 0, "gap": 0, "unmeasured": 0, "excluded": 0, "x": 0, "y": 0 } }'
        }
        [IO.File]::WriteAllText((Join-Path $run 'measurements/windows/options.json'), (& $measurement 'windows' @('--a', 'gap', '--b', 'match', '--c', 'match', '--d', 'gap', '--e', 'match')), $utf8)
        [IO.File]::WriteAllText((Join-Path $run 'measurements/linux/options.json'), (& $measurement 'linux' @('--a', 'gap', '--b', 'gap', '--c', 'gap', '--d', 'match', '--e', 'match')), $utf8)
        $finding = {
            param([string] $Id, [string] $Platform, [string] $Key, [string] $Items)
            $platformLine = if ($Platform) { "platform: $Platform`n" } else { '' }
            [IO.File]::WriteAllText((Join-Path $findings "$Id-x.md"), "---`nid: $Id`ntitle: T $Id`narea: options`n${platformLine}key: $Key`nseverity: Low`nstatus: open`nscope: target`nintroduced-in:`nopened: 2026-10-01_0000`nclosed:`nregression: false`nitems: [$Items]`ntouches: []`ntask:`ntasks: []`n---`n# $Id - T $Id`n`n## Summary`n`ns`n`n## Evidence`n`ne`n`n## Suggestion`n`nx`n`n## Measurements`n`n## Log`n`n- 2026-10-01_0000: Opened.`n", $utf8)
        }
        & $finding 'GF-0001' 'windows' 'options:a-and-d' 'options:--a, options:--d'
        & $finding 'GF-0002' 'linux' 'options:c-on-linux' 'options:--c'
        & $finding 'GF-0003' 'macos' 'options:a-on-macos' 'options:--a'
        & $finding 'GF-0004' '' 'options:e' 'options:--e'
        $listed = @(& $PSCommandPath -RunDirectory $run -Stamp '2026-10-15_1200' -FindingsDirectory $findings -ListUncovered)
        Assert-Check 'rule 0: -ListUncovered lists every windows gap and only the linux gaps windows does not share' ((($listed | Where-Object { $_ -like 'linux *' }) -join ';') -eq 'linux options options:--b;linux options options:--c' -and (($listed | Where-Object { $_ -like 'windows *' }) -join ';') -eq 'windows options options:--a;windows options options:--d')
        $report = "# r`n`n" + '```json' + "`n" + '{ "analyst": "gap-options", "area": "options", "run": "2026-10-15_1200", "groups": [ { "key": "options:a-and-b", "title": "A and B differ", "severity": "Medium", "introducedIn": null, "items": ["options:--a", "options:--b"], "evidence": "ev", "suggestion": "su", "touches": ["Curl.Console"] } ], "notes": "" }' + "`n" + '```' + "`n"
        [IO.File]::WriteAllText((Join-Path $run 'reports/linux/gap-options.md'), $report, $utf8)
        $output = & $PSCommandPath -RunDirectory $run -Stamp '2026-10-15_1200' -FindingsDirectory $findings 3>&1 | Out-String
        $all = @(Get-ChildItem -Path $findings -Filter 'GF-*.md' | ForEach-Object { Read-Finding $_.FullName })
        $byId = @{}; foreach ($f in $all) { $byId[$f.Front['id']] = $f }
        $newLinux = @($all | Where-Object { $_.Front['key'] -eq 'options:a-and-b' })
        Assert-Check 'rule 1: a linux group files a linux finding cut to the linux-only gaps' ($newLinux.Count -eq 1 -and $newLinux[0].Front['platform'] -eq 'linux' -and $newLinux[0].Front['items'] -eq '[options:--b]')
        Assert-Check 'rule 2: a linux gap shared with windows files no linux ungrouped finding' (@($all | Where-Object { $_.Front['key'] -eq 'options:ungrouped' }).Count -eq 0)
        Assert-Check 'rule 4: a linux finding stays open on a linux gap although windows measures its item match' ($byId['GF-0002'].Front['status'] -eq 'open')
        Assert-Check 'rule 4: a windows finding stays open on a windows gap although linux measures that item match' ($byId['GF-0001'].Front['status'] -eq 'open')
        Assert-Check 'rule 4: a finding with no platform field is a windows finding and closes on the windows measurement' ($byId['GF-0004'].Front['status'] -eq 'closed' -and $byId['GF-0004'].Front['platform'] -eq 'windows' -and [IO.File]::ReadAllText($byId['GF-0004'].Path) -match 'excluded on windows\.')
        Assert-Check 'rule 7: a finding of a platform the run did not measure is not touched' ([IO.File]::ReadAllText($byId['GF-0003'].Path) -notmatch '2026-10-15_1200')
        Assert-Check 'the summary counts the run across platforms' ($output -match 'Gap findings 2026-10-15_1200: 1 new, 2 still open, 1 closed')
    }
    finally { Remove-Item -Path $folder -Recurse -Force -ErrorAction SilentlyContinue }
}

function Invoke-SelfTest {
    $fixtures = Join-Path $PSScriptRoot 'Fixtures\findings'
    $script:failed = $false
    function Assert-Check([string] $Check, [bool] $Passed) {
        if ($Passed) { Write-Output "PASS $Check" } else { Write-Output "FAIL $Check"; $script:failed = $true }
    }
    function New-Copy {
        $folder = Join-Path ([IO.Path]::GetTempPath()) ('gapfindings-' + [Guid]::NewGuid().ToString('N'))
        Copy-Item -Path $fixtures -Destination $folder -Recurse
        return $folder
    }
    function Get-Front([string] $Folder, [string] $Id) {
        return (Read-Finding (Get-ChildItem -Path (Join-Path $Folder 'existing') -Filter "$Id-*.md")[0].FullName).Front
    }
    function Get-Text([string] $Folder, [string] $Id) {
        return [IO.File]::ReadAllText((Get-ChildItem -Path (Join-Path $Folder 'existing') -Filter "$Id-*.md")[0].FullName)
    }
    function Invoke-Run([string] $Folder, [string] $RunStamp, [string] $Diff, [switch] $DryRun) {
        $arguments = @{ RunDirectory = (Join-Path $Folder 'run'); Stamp = $RunStamp; FindingsDirectory = (Join-Path $Folder 'existing') }
        if ($Diff) { $arguments.ReleaseDiff = (Join-Path $Folder $Diff) }
        if ($DryRun) { $arguments.WhatIf = $true }
        return (& $PSCommandPath @arguments 3>&1 | Out-String)
    }
    $stampOne = '2026-10-09_1430'
    $folder = New-Copy
    try {
        $before06 = Get-Text $folder 'GF-0006'
        $before05 = Read-Finding (Get-ChildItem -Path (Join-Path $folder 'existing') -Filter 'GF-0005-*.md')[0].FullName
        $output = Invoke-Run $folder $stampOne 'run\measurements\release-9.9.10.json'
        $all = @(Get-ChildItem -Path (Join-Path $folder 'existing') -Filter 'GF-*.md' | ForEach-Object { Read-Finding $_.FullName })
        $echFinding = @($all | Where-Object { $_.Front['key'] -eq 'options:encrypted-client-hello-options-are-missing' })[0]
        $new07 = $echFinding.Front
        $keys = @(Get-ChildItem -Path (Join-Path $folder 'existing') -Filter 'GF-*.md' | ForEach-Object { (Read-Finding $_.FullName).Front['key'] })
        Assert-Check 'rule 1: a new group is filed as the next GF number, open, scope target, fields from the group' ([int] $new07['id'].Substring(3) -gt 6 -and @($all | ForEach-Object { $_.Front['id'] } | Sort-Object -Unique).Count -eq $all.Count -and $new07['status'] -eq 'open' -and $new07['scope'] -eq 'target' -and $new07['opened'] -eq $stampOne -and $new07['severity'] -eq 'High' -and $new07['introduced-in'] -eq '8.8.0' -and $new07['items'] -eq '[options:--ech, options:--ech:argument]' -and $new07['touches'] -eq '[Curl.Console, Curl.Console.UnitTests]' -and [IO.File]::ReadAllText($echFinding.Path) -match '(?s)## Evidence\s+curl --ech true.*## Suggestion\s+Parse --ech')
        Assert-Check 'rule 1: a group whose key matches an existing finding files nothing new' (@($keys | Where-Object { $_ -eq 'options:fail-option-is-missing' }).Count -eq 1 -and (Get-Front $folder 'GF-0001')['items'] -eq '[options:--fail, options:--fail-early]')
        $ungrouped = @(Get-ChildItem -Path (Join-Path $folder 'existing') -Filter 'GF-*.md' | ForEach-Object { Read-Finding $_.FullName } | Where-Object { $_.Front['key'] -eq 'options:ungrouped' })
        Assert-Check 'rule 2: a gap item in no group is filed in the area catch-all, severity Low, with a warning naming the analyst' ($ungrouped.Count -eq 1 -and $ungrouped[0].Front['severity'] -eq 'Low' -and $ungrouped[0].Front['items'] -eq '[options:--loose]' -and $output -match 'gap-options left 1 windows gap item')
        Assert-Check 'rule 3: an open finding gets a Measurements line for the run' ((Get-Text $folder 'GF-0001') -match "- ${stampOne}: 2 of 2 items are gaps\.")
        Assert-Check 'rule 4: a finding whose task is Done but whose items still measure gap stays open' ((Get-Front $folder 'GF-0001')['status'] -eq 'open')
        $f02 = Get-Text $folder 'GF-0002'
        Assert-Check 'rule 4: an item absent from the measurement does not close its finding' ($f02 -match 'status: open' -and $f02 -match "1 not in this run's measurement")
        $f03 = Get-Front $folder 'GF-0003'
        Assert-Check 'rule 4: a finding whose items all measure match or excluded with a reason closes' ($f03['status'] -eq 'closed' -and $f03['closed'] -eq $stampOne -and (Get-Text $folder 'GF-0003') -match "- ${stampOne}: Closed")
        $f04 = Get-Front $folder 'GF-0004'
        Assert-Check 'rule 5: a closed finding with a gap item reopens with regression true and closed cleared' ($f04['status'] -eq 'open' -and $f04['regression'] -eq 'true' -and $f04['closed'] -eq '' -and (Get-Text $folder 'GF-0004') -match "Reopened as a regression: run $stampOne" -and @($keys | Where-Object { $_ -eq 'options:max-time-ignored' }).Count -eq 1)
        $after05 = Read-Finding (Get-ChildItem -Path (Join-Path $folder 'existing') -Filter 'GF-0005-*.md')[0].FullName
        $sameFront = (($before05.Front.Keys | ForEach-Object { "$_=$($before05.Front[$_])" }) -join ';') -eq (($after05.Front.Keys | ForEach-Object { "$_=$($after05.Front[$_])" }) -join ';')
        Assert-Check 'rule 6: a rejected finding gains only a Measurements line' ($sameFront -and ($after05.Original -replace "(?m)^- ${stampOne}: .*\n", '') -eq $before05.Original -and (@($after05.Sections['Log']).Count -eq @($before05.Sections['Log']).Count))
        Assert-Check 'rule 7: a finding of an area the run did not measure is not touched' ((Get-Text $folder 'GF-0006') -ceq $before06)
        $newest = @(Get-ChildItem -Path (Join-Path $folder 'existing') -Filter 'GF-*.md' | ForEach-Object { Read-Finding $_.FullName } | Where-Object { $_.Front['key'] -eq 'writeout:newest' })
        Assert-Check 'rule 8: a release diff files one scope newest finding per area with introduced-in and severity from the change' ($newest.Count -eq 1 -and $newest[0].Front['scope'] -eq 'newest' -and $newest[0].Front['introduced-in'] -eq '9.9.10' -and $newest[0].Front['severity'] -eq 'High' -and $newest[0].Front['items'] -eq '[writeout:time_example]')
        Assert-Check 'rule 9: one summary line counts new, still open, closed, reopened and ungrouped' ($output -match "Gap findings ${stampOne}: 4 new, 2 still open, 1 closed, 1 reopened as regressions, 1 ungrouped\.")
        $templatePath = Join-Path (Split-Path $PSScriptRoot -Parent) 'Findings\FINDING-TEMPLATE.md'
        $template = Read-Finding $templatePath
        $written = Read-Finding $echFinding.Path
        Assert-Check 'written files follow FINDING-TEMPLATE.md front matter and section order' ((@($template.Front.Keys) -join ',') -eq (@($written.Front.Keys) -join ',') -and (@($template.Sections.Keys) -join ',') -eq (@($written.Sections.Keys) -join ','))
        $countBefore = @(Get-ChildItem -Path (Join-Path $folder 'existing') -Filter 'GF-*.md').Count
        $output = Invoke-Run $folder '2026-11-10_0900' 'release-9.9.11.json'
        $newest = @(Get-ChildItem -Path (Join-Path $folder 'existing') -Filter 'GF-*.md' | ForEach-Object { Read-Finding $_.FullName } | Where-Object { $_.Front['key'] -eq 'writeout:newest' })
        Assert-Check 'rule 8: a later release diff listing the same item adds new keys to that finding rather than filing again' ($newest.Count -eq 1 -and $newest[0].Front['items'] -eq '[writeout:time_example, writeout:time_other]' -and @(Get-ChildItem -Path (Join-Path $folder 'existing') -Filter 'GF-*.md').Count -eq $countBefore)
    }
    finally { Remove-Item -Path $folder -Recurse -Force }
    $folder = New-Copy
    try {
        $hashBefore = (Get-ChildItem -Path (Join-Path $folder 'existing') | ForEach-Object { $_.Name + ':' + [IO.File]::ReadAllText($_.FullName) }) -join '|'
        [void] (Invoke-Run $folder $stampOne 'run\measurements\release-9.9.10.json' -DryRun)
        $hashAfter = (Get-ChildItem -Path (Join-Path $folder 'existing') | ForEach-Object { $_.Name + ':' + [IO.File]::ReadAllText($_.FullName) }) -join '|'
        Assert-Check '-WhatIf changes nothing' ($hashBefore -ceq $hashAfter)
    }
    finally { Remove-Item -Path $folder -Recurse -Force }
    Test-Platforms
    if ($script:failed) { exit 1 }
}

if ($SelfTest) { Invoke-SelfTest; return }
Invoke-WriteGapFindings
