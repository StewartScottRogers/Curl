<#
.SYNOPSIS
    Files one lane-eligible Curl task on the factory's board for each open target-scope
    gap finding that has no open task, and a Re-close task when a finding's tasks are all
    Done but the latest run still measures it. Writes each task's ID back into the finding.

.DESCRIPTION
    ADR-0433 decision 4: Claude accepts the suggestions and files the tasks itself. The
    finding format is in Gap/Instructions/Gap-Format.md, section 7; Gap/Triage.md explains
    the flow. Tasks are created only with .claude/skills/task-board/task-board.ps1 new, run
    as a child process with CLAUDE_PROJECT_DIR set to -BoardRoot (restored afterwards);
    the template's Goal, Context and Acceptance criteria are then rewritten, so none of its
    HTML comments survives.

    Mapping:

    | Finding                                                    | Task                          |
    | status open, scope target, no open task                    | Title "Close GF-####: <title>" |
    | every task Done (Tasks/Done or its archive) and the latest |                               |
    |   Measurements line, dated after the last completion,      |                               |
    |   still shows gaps                                         | Title "Re-close GF-####: <title>" |
    | scope newest, status rejected or closed                    | No task                       |
    | severity Critical or High                                  | Priority High                 |
    | severity Medium                                            | Priority Normal               |
    | severity Low                                               | Priority Low                  |
    | area protocols                                             | Pipeline protocol             |
    | any other area                                             | Pipeline feature              |
    | the finding's touches                                      | -Touches, dropping any Gap/, Audit/ or .claude/agents/ path; with none left the task has no touches, runs alone, and its Context says so |

    A finding's tasks are the IDs in its tasks list plus every board task whose title
    starts "Close GF-####" or "Re-close GF-####" for it, so a task filed by hand counts. A
    task is open when it sits in Backlog, Doing, Blocked or Deferred. "Dated after" compares
    the Measurements line's date (yyyy-MM-dd of its stamp) with the latest completed date
    of the finding's tasks, strictly: a run on the completion day may predate the fix.

    The task body stands alone, since lanes cannot read Gap/: its Goal says what Curl must
    do; its Context carries the finding's ID, area, severity, introduced-in, item keys,
    Evidence and Suggestion in full, the targeted curl version (Gap/Baselines/target.json)
    and the upstream documents the yardstick came from (the sources of
    Gap/Upstream/<version>/<area>.json), and says the gap closes only on re-measurement
    (ADR-0433 decision 5); its Acceptance criteria hold one box per item key, with the
    item's expected answer when -MeasurementsDirectory gives one, plus the standard gates.

    After filing, the finding's task is set to the new ID, the ID is appended to tasks, and
    a Log line "<yyyy-MM-dd_HHmm>: Filed BL-###." is added.

.PARAMETER FindingsDirectory
    The findings folder. Default Gap/Findings beside this script's Gap folder.

.PARAMETER BoardRoot
    The checkout whose Tasks/ receives the tasks. Default the repository root.

.PARAMETER MeasurementsDirectory
    Optional: a run's measurements folder (<area>.json, Gap-Format.md section 4). An item
    found there gives its acceptance box the item's expected answer.

.PARAMETER WhatIf
    Prints each task it would file and changes nothing.

.PARAMETER SelfTest
    Copies Gap/Tools/Fixtures/tasks to a temporary folder, runs the script against its
    board and findings with the real task-board.ps1, prints a PASS or FAIL line per check,
    and exits 1 on any FAIL. Its inner runs clear CURL_DARK_FACTORY_LANE, except the one
    that checks the refusal.

.NOTES
    Refuses to run when CURL_DARK_FACTORY_LANE is set: a lane never files from the office.
    Keep this file ASCII only (Windows PowerShell 5.1 reads it in the system code page).
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string] $FindingsDirectory,
    [string] $BoardRoot,
    [string] $MeasurementsDirectory,
    [switch] $SelfTest
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2
# Windows PowerShell 5.1 leaves $PSScriptRoot empty inside param defaults.
$script:GapRoot = Split-Path $PSScriptRoot -Parent
$script:RepoRoot = Split-Path $script:GapRoot -Parent
if (-not $FindingsDirectory) { $FindingsDirectory = Join-Path $script:GapRoot 'Findings' }
if (-not $BoardRoot) { $BoardRoot = $script:RepoRoot }
$script:TaskBoard = Join-Path $script:RepoRoot '.claude\skills\task-board\task-board.ps1'
$script:Utf8NoBom = New-Object Text.UTF8Encoding($false)

$script:FrontOrder = @('id', 'title', 'area', 'key', 'severity', 'status', 'scope', 'introduced-in',
    'opened', 'closed', 'regression', 'items', 'touches', 'task', 'tasks')
$script:SectionOrder = @('Summary', 'Evidence', 'Suggestion', 'Measurements', 'Log')
$script:PriorityBySeverity = @{ 'Critical' = 'High'; 'High' = 'High'; 'Medium' = 'Normal'; 'Low' = 'Low' }
$script:OpenStates = @('Backlog', 'Doing', 'Blocked', 'Deferred')

function ConvertTo-List([string] $Value) {
    $inner = $Value.Trim().TrimStart('[').TrimEnd(']').Trim()
    if ($inner -eq '') { return , @() }
    return , @($inner -split '\s*,\s*' | Where-Object { $_ -ne '' })
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
    $current = $null
    for ($index++; $index -lt $lines.Count; $index++) {
        $line = $lines[$index]
        if ($line -match '^## (.+)$') { $current = $Matches[1].Trim(); $sections[$current] = New-Object Collections.Generic.List[string]; continue }
        if ($null -ne $current -and $line -ne '') { $sections[$current].Add($line) }
    }
    return [pscustomobject]@{ Path = $Path; Front = $front; Sections = $sections }
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

function Get-SectionText($Finding, [string] $Name) {
    if (-not $Finding.Sections.Contains($Name) -or $Finding.Sections[$Name].Count -eq 0) { return 'None given.' }
    return (@($Finding.Sections[$Name]) -join "`n`n")
}

# Every BL-*.md under <BoardRoot>/Tasks: its ID, title, state (the first folder under
# Tasks) and completed date.
function Get-BoardTasks {
    $board = Join-Path $BoardRoot 'Tasks'
    $boardFull = [IO.Path]::GetFullPath($board).TrimEnd('\', '/')
    $tasks = New-Object Collections.Generic.List[object]
    foreach ($file in @(Get-ChildItem -Path $board -Recurse -File -Filter 'BL-*.md')) {
        $relative = $file.FullName.Substring($boardFull.Length + 1)
        $state = ($relative -split '[\\/]')[0]
        $text = [IO.File]::ReadAllText($file.FullName)
        $title = if ($text -match '(?m)^title:[ \t]*([^\r\n]*)') { $Matches[1].Trim() } else { '' }
        $completed = if ($text -match '(?m)^completed:[ \t]*([0-9]{4}-[0-9]{2}-[0-9]{2})') { $Matches[1] } else { '' }
        $tasks.Add([pscustomobject]@{ Id = ($file.BaseName -replace '^(BL-\d+).*$', '$1'); Title = $title; State = $state; Completed = $completed; Path = $file.FullName })
    }
    return , $tasks.ToArray()
}

function Get-FindingTasks($Finding, [object[]] $BoardTasks) {
    $id = $Finding.Front['id']
    $listed = ConvertTo-List $Finding.Front['tasks']
    $named = '^(Re-)?close ' + [regex]::Escape($id) + '\b'
    return , @($BoardTasks | Where-Object { $listed -contains $_.Id -or $_.Title -match $named })
}

# The latest Measurements line: its date and how many items are still gaps.
function Get-LatestMeasurement($Finding) {
    if (-not $Finding.Sections.Contains('Measurements')) { return $null }
    $latest = $null
    foreach ($line in $Finding.Sections['Measurements']) {
        if ($line -match '^- ([0-9]{4}-[0-9]{2}-[0-9]{2})_[0-9]{4}: ([0-9]+) of [0-9]+ items are gaps') {
            $latest = [pscustomobject]@{ Date = $Matches[1]; Gaps = [int] $Matches[2] }
        }
    }
    return $latest
}

# 'Close', 'Re-close' or $null: which task, if any, the finding needs now.
function Get-NeededTask($Finding, [object[]] $BoardTasks) {
    if ($Finding.Front['status'] -ne 'open' -or $Finding.Front['scope'] -ne 'target') { return $null }
    $tasks = Get-FindingTasks $Finding $BoardTasks
    $listed = (ConvertTo-List $Finding.Front['tasks'])
    if (@($tasks | Where-Object { $script:OpenStates -contains $_.State }).Count -gt 0) { return $null }
    if ($tasks.Count -eq 0) { return 'Close' }
    $known = @($tasks | ForEach-Object { $_.Id })
    if (@($listed | Where-Object { $known -notcontains $_ }).Count -gt 0) { return $null }
    $lastCompleted = @($tasks | ForEach-Object { $_.Completed } | Sort-Object)[-1]
    $latest = Get-LatestMeasurement $Finding
    if ($null -ne $latest -and $latest.Gaps -gt 0 -and [string]::CompareOrdinal($latest.Date, [string] $lastCompleted) -gt 0) { return 'Re-close' }
    return $null
}

function Get-TaskTouches($Finding) {
    $touches = ConvertTo-List $Finding.Front['touches']
    return , @($touches | Where-Object {
            $path = $_ -replace '\\', '/'
            -not ($path -match '^(Gap|Audit)(/|$)' -or $path -match '^\.claude/agents(/|$)')
        })
}

function Get-TargetVersion {
    $path = Join-Path $script:GapRoot 'Baselines\target.json'
    return ([IO.File]::ReadAllText($path) | ConvertFrom-Json).version
}

function Get-UpstreamSources([string] $Version, [string] $Area) {
    $path = Join-Path $script:GapRoot "Upstream\$Version\$Area.json"
    if (Test-Path -LiteralPath $path) {
        $sources = @(([IO.File]::ReadAllText($path) | ConvertFrom-Json).sources)
        if ($sources.Count -gt 0) { return (($sources | ForEach-Object { "``$_``" }) -join ', ') + " in the curl $Version release tarball" }
    }
    return "the curl $Version release tarball (no $Area inventory names its documents yet)"
}

function Get-Expected([string] $Area, [string] $Key) {
    if (-not $MeasurementsDirectory) { return $null }
    $path = Join-Path $MeasurementsDirectory "$Area.json"
    if (-not (Test-Path -LiteralPath $path)) { return $null }
    $item = @(([IO.File]::ReadAllText($path) | ConvertFrom-Json).items | Where-Object { $_.key -eq $Key }) | Select-Object -First 1
    if ($null -eq $item -or $null -eq $item.expected) { return $null }
    return [string] $item.expected
}

function Format-TaskBody($Finding, [string] $Kind, [string[]] $Touches) {
    $front = $Finding.Front
    $version = Get-TargetVersion
    $items = (ConvertTo-List $front['items'])
    $itemText = ($items | ForEach-Object { "``$_``" }) -join ', '
    $introduced = if ($front['introduced-in']) { $front['introduced-in'] } else { 'not stated upstream' }
    $lines = New-Object Collections.Generic.List[string]
    $lines.Add('## Goal'); $lines.Add('')
    $lines.Add("Curl behaves as curl $version does for every item of gap finding $($front['id']) ($($front['title'])), so a later gap analysis measures each of $itemText as ``match``.")
    $lines.Add(''); $lines.Add('## Context'); $lines.Add('')
    if ($Kind -eq 'Re-close') { $lines.Add("This is a Re-close task: every earlier task for $($front['id']) ($($front['tasks'])) is Done, but the latest gap analysis still measures a gap, so the fix did not hold.") ; $lines.Add('') }
    $lines.Add("- Finding: $($front['id']), filed by the gap analysis office (ADR-0433).")
    $lines.Add("- Area: $($front['area']). Severity: $($front['severity']). Introduced in: $introduced.")
    $lines.Add("- Items: $itemText.")
    $lines.Add("- Targeted curl version: $version (Gap/Baselines/target.json).")
    $lines.Add("- Yardstick: $(Get-UpstreamSources $version $front['area']).")
    if ($Touches.Count -eq 0) { $lines.Add('- Touches: none. The finding named no Curl project outside the office, so this task has no touches and runs alone.') }
    $lines.Add('- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.')
    $lines.Add(''); $lines.Add('Evidence, copied from the finding:'); $lines.Add('')
    $lines.Add((Get-SectionText $Finding 'Evidence'))
    $lines.Add(''); $lines.Add('Suggestion, copied from the finding:'); $lines.Add('')
    $lines.Add((Get-SectionText $Finding 'Suggestion'))
    $lines.Add(''); $lines.Add('## Acceptance criteria'); $lines.Add('')
    foreach ($item in $items) {
        $expected = Get-Expected $front['area'] $item
        if ($null -ne $expected) { $lines.Add("- [ ] ``$item``: Curl answers what curl $version answers, ``$expected``, so the item measures ``match``.") }
        else { $lines.Add("- [ ] ``$item``: Curl answers what curl $version answers, as the Evidence above shows, so the item measures ``match``.") }
    }
    $lines.Add('- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.')
    $lines.Add('- [ ] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).')
    $lines.Add('')
    return ($lines -join "`n")
}

# The executable of the running PowerShell edition, so task-board.ps1 runs under the same
# one. The process itself may be a host such as dotnet, so it is not asked.
function Get-PowerShellPath {
    if ($PSVersionTable.PSEdition -eq 'Desktop') { return (Join-Path $PSHOME 'powershell.exe') }
    $path = Join-Path $PSHOME 'pwsh'
    if (Test-Path -LiteralPath "$path.exe") { return "$path.exe" }
    return $path
}

function New-BoardTask([string] $Title, [string] $Priority, [string] $Pipeline, [string[]] $Touches) {
    $hostPath = Get-PowerShellPath
    $arguments = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $script:TaskBoard, 'new', '-Title', $Title, '-Priority', $Priority, '-Pipeline', $Pipeline)
    if ($Touches.Count -gt 0) { $arguments += @('-Touches', ($Touches -join ',')) }
    $saved = $env:CLAUDE_PROJECT_DIR
    try {
        $env:CLAUDE_PROJECT_DIR = [IO.Path]::GetFullPath($BoardRoot)
        $output = & $hostPath @arguments 2>&1 | Out-String
        if ($LASTEXITCODE -ne 0) { throw "task-board.ps1 new failed: $output" }
    }
    finally { $env:CLAUDE_PROJECT_DIR = $saved }
    if ($output -notmatch '(BL-\d+)\s+(Tasks\\Backlog\\\S+)') { throw "task-board.ps1 new printed no task: $output" }
    return [pscustomobject]@{ Id = $Matches[1]; Path = (Join-Path $BoardRoot ($Matches[2] -replace '\\', [IO.Path]::DirectorySeparatorChar)) }
}

function Set-TaskBody([string] $Path, [string] $Body) {
    $text = [IO.File]::ReadAllText($Path) -replace "`r`n", "`n"
    $start = $text.IndexOf("## Goal`n")
    $end = $text.IndexOf("## Notes`n")
    $text = $text.Substring(0, $start) + $Body + "`n" + $text.Substring($end)
    [IO.File]::WriteAllText($Path, $text, $script:Utf8NoBom)
}

function Invoke-NewTasksFromGaps {
    if ($env:CURL_DARK_FACTORY_LANE) { throw 'New-TasksFromGaps.ps1 refuses to run inside a dark factory lane (CURL_DARK_FACTORY_LANE is set): a lane never files from the gap analysis office.' }
    $boardTasks = Get-BoardTasks
    $stamp = (Get-Date).ToString('yyyy-MM-dd_HHmm')
    $filed = 0
    foreach ($file in @(Get-ChildItem -Path $FindingsDirectory -Filter 'GF-*.md' | Sort-Object Name)) {
        $finding = Read-Finding $file.FullName
        $kind = Get-NeededTask $finding $boardTasks
        if ($null -eq $kind) { continue }
        $front = $finding.Front
        $title = "${kind} $($front['id']): $($front['title'])"
        $priority = $script:PriorityBySeverity[$front['severity']]
        if (-not $priority) { throw "$($front['id']) has severity '$($front['severity'])'; expected Critical, High, Medium or Low." }
        $pipeline = if ($front['area'] -eq 'protocols') { 'protocol' } else { 'feature' }
        $touches = Get-TaskTouches $finding
        if ($WhatIfPreference) { Write-Output "What if: would file '$title' ($priority, $pipeline, touches [$($touches -join ', ')]) in $BoardRoot\Tasks\Backlog."; continue }
        $task = New-BoardTask $title $priority $pipeline $touches
        Set-TaskBody $task.Path (Format-TaskBody $finding $kind $touches)
        $front['task'] = $task.Id
        $front['tasks'] = '[' + (((ConvertTo-List $front['tasks']) + $task.Id) -join ', ') + ']'
        if (-not $finding.Sections.Contains('Log')) { $finding.Sections['Log'] = New-Object Collections.Generic.List[string] }
        $finding.Sections['Log'].Add("- ${stamp}: Filed $($task.Id).")
        [IO.File]::WriteAllText($finding.Path, (Format-Finding $front $finding.Sections), $script:Utf8NoBom)
        Write-Output "$($task.Id) filed for $($front['id']): $title"
        $filed++
    }
    Write-Output "New-TasksFromGaps: $filed task(s) filed."
}

function Invoke-SelfTest {
    $fixtures = Join-Path $PSScriptRoot 'Fixtures\tasks'
    $script:failed = $false
    function Assert-Check([string] $Check, [bool] $Passed) {
        if ($Passed) { Write-Output "PASS $Check" } else { Write-Output "FAIL $Check"; $script:failed = $true }
    }
    function Get-Snapshot([string] $Folder) {
        return (@(Get-ChildItem -Path $Folder -Recurse -File | Sort-Object FullName | ForEach-Object { $_.FullName + ':' + [IO.File]::ReadAllText($_.FullName) }) -join '|')
    }
    function Invoke-Run([string] $Folder, [switch] $DryRun, [switch] $AsLane) {
        $arguments = @{ FindingsDirectory = (Join-Path $Folder 'findings'); BoardRoot = (Join-Path $Folder 'board'); MeasurementsDirectory = (Join-Path $Folder 'measurements') }
        if ($DryRun) { $arguments.WhatIf = $true }
        $savedLane = $env:CURL_DARK_FACTORY_LANE
        try {
            if ($AsLane) { $env:CURL_DARK_FACTORY_LANE = 'self-test' } else { $env:CURL_DARK_FACTORY_LANE = $null }
            return (& $PSCommandPath @arguments *>&1 | Out-String)
        }
        catch { return "THREW $_" }
        finally { $env:CURL_DARK_FACTORY_LANE = $savedLane }
    }
    function Get-Filed([string] $Folder, [string] $FindingId) {
        return @(Get-ChildItem -Path (Join-Path $Folder 'board\Tasks\Backlog') -Filter 'BL-*.md' | Where-Object { [IO.File]::ReadAllText($_.FullName) -match ('(?m)^title: (Re-)?[Cc]lose ' + $FindingId + ':') })
    }
    function Get-FindingFront([string] $Folder, [string] $FindingId) {
        return (Read-Finding (Get-ChildItem -Path (Join-Path $Folder 'findings') -Filter "$FindingId-*.md")[0].FullName).Front
    }
    $folder = Join-Path ([IO.Path]::GetTempPath()) ('gaptasks-' + [Guid]::NewGuid().ToString('N'))
    Copy-Item -Path $fixtures -Destination $folder -Recurse
    try {
        $before = Get-Snapshot $folder
        $output = Invoke-Run $folder -AsLane
        Assert-Check 'refuses to run when CURL_DARK_FACTORY_LANE is set, and changes nothing' ($output -match 'refuses to run inside a dark factory lane' -and (Get-Snapshot $folder) -ceq $before)
        $output = Invoke-Run $folder -DryRun
        Assert-Check '-WhatIf names the tasks it would file and changes nothing' ($output -match "would file 'Close GF-0001" -and (Get-Snapshot $folder) -ceq $before)

        $output = Invoke-Run $folder
        $tasks = @{}
        foreach ($id in @('GF-0001', 'GF-0002', 'GF-0003', 'GF-0004', 'GF-0005', 'GF-0006', 'GF-0007', 'GF-0008', 'GF-0009', 'GF-0010')) { $tasks[$id] = @(Get-Filed $folder $id) }
        $text = @{}
        foreach ($id in @($tasks.Keys)) { if ($tasks[$id].Count -eq 1) { $text[$id] = [IO.File]::ReadAllText($tasks[$id][0].FullName) } }
        Assert-Check 'an open target finding with no task gets "Close GF-####: <title>"' ($tasks['GF-0001'].Count -eq 1 -and $text['GF-0001'] -match '(?m)^title: Close GF-0001: Encrypted Client Hello options are missing$')
        Assert-Check 'every task Done and a later Measurements line with gaps gets "Re-close GF-####: <title>"' ($tasks['GF-0003'].Count -eq 1 -and $text['GF-0003'] -match '(?m)^title: Re-close GF-0003: The time_queue write-out variable is missing$')
        Assert-Check 'every task Done but no Measurements line after the completion gets no task' ($tasks['GF-0004'].Count -eq 0)
        Assert-Check 'scope newest, rejected and closed findings get no task' ($tasks['GF-0005'].Count -eq 0 -and $tasks['GF-0006'].Count -eq 0 -and $tasks['GF-0007'].Count -eq 0)
        Assert-Check 'a hand-filed Close GF-#### task stops a second filing' ($tasks['GF-0008'].Count -eq 1 -and $tasks['GF-0008'][0].Name -like 'BL-103-*' -and (Get-FindingFront $folder 'GF-0008')['tasks'] -eq '[]')
        Assert-Check 'an open task in the finding''s tasks stops a second filing' ($tasks['GF-0009'].Count -eq 0)
        Assert-Check 'severity Critical and High give priority High' ($text['GF-0001'] -match '(?m)^priority: High$' -and $text['GF-0010'] -match '(?m)^priority: High$')
        Assert-Check 'severity Medium gives Normal and Low gives Low' ($text['GF-0002'] -match '(?m)^priority: Normal$' -and $text['GF-0003'] -match '(?m)^priority: Low$')
        Assert-Check 'area protocols gives pipeline protocol, any other area feature' ($text['GF-0002'] -match '(?m)^pipeline: protocol$' -and $text['GF-0001'] -match '(?m)^pipeline: feature$' -and $text['GF-0010'] -match '(?m)^pipeline: feature$')
        Assert-Check 'touches keep the finding''s Curl projects and drop Gap/ paths' ($text['GF-0001'] -match '(?m)^touches: \[Curl\.Console, Curl\.Console\.UnitTests\]$')
        Assert-Check 'with only Gap/, Audit/ and .claude/agents/ touches the task has none and its Context says it runs alone' ($text['GF-0002'] -match '(?m)^touches: \[\]$' -and $text['GF-0002'] -match 'runs alone')
        $allText = @($text.Values)
        Assert-Check 'a filed task contains the finding''s Evidence and Suggestion text' ($text['GF-0001'] -match 'Evidence text of GF-0001' -and $text['GF-0001'] -match 'Suggestion text of GF-0001' -and $text['GF-0003'] -match 'Evidence text of GF-0003')
        Assert-Check 'no filed task keeps an HTML comment from the template' (@($allText | Where-Object { $_ -match '<!--' }).Count -eq 0)
        Assert-Check 'the Context names the ID, area, severity, introduced-in, items, target version, yardstick and re-measurement rule' ($text['GF-0001'] -match 'Finding: GF-0001' -and $text['GF-0001'] -match 'Area: options\. Severity: Critical\. Introduced in: 8\.8\.0' -and $text['GF-0001'] -match 'Targeted curl version: 8\.21\.0' -and $text['GF-0001'] -match 'docs/cmdline-opts/\*\.md' -and $text['GF-0001'] -match 'ADR-0433 decision 5')
        Assert-Check 'acceptance criteria hold one box per item key, with the measured expected answer, and the standard gates' ($text['GF-0001'] -match '- \[ \] `options:--ech`: .*`known option`' -and $text['GF-0001'] -match '- \[ \] `options:--ech:argument`: .*`accepts false, true, hard and pn:<name>`' -and $text['GF-0001'] -match 'dotnet build -warnaserror' -and $text['GF-0001'] -match '--ai-help')
        $f01 = Get-FindingFront $folder 'GF-0001'
        $f03 = Get-FindingFront $folder 'GF-0003'
        $id01 = $tasks['GF-0001'][0].BaseName -replace '^(BL-\d+).*$', '$1'
        $id03 = $tasks['GF-0003'][0].BaseName -replace '^(BL-\d+).*$', '$1'
        $log01 = [IO.File]::ReadAllText((Get-ChildItem -Path (Join-Path $folder 'findings') -Filter 'GF-0001-*.md')[0].FullName)
        Assert-Check 'the finding gains task, tasks and a Log line' ($f01['task'] -eq $id01 -and $f01['tasks'] -eq "[$id01]" -and $log01 -match "(?m)^- [0-9]{4}-[0-9]{2}-[0-9]{2}_[0-9]{4}: Filed $id01\.$" -and $f03['task'] -eq $id03 -and $f03['tasks'] -eq "[BL-101, $id03]")
        Assert-Check 'the summary line counts the tasks filed' ($output -match 'New-TasksFromGaps: 4 task\(s\) filed\.')
        $saved = $env:CLAUDE_PROJECT_DIR
        try {
            $env:CLAUDE_PROJECT_DIR = Join-Path $folder 'board'
            $status = & (Get-PowerShellPath) -NoProfile -ExecutionPolicy Bypass -File $script:TaskBoard status *>&1 | Out-String
        }
        finally { $env:CLAUDE_PROJECT_DIR = $saved }
        Assert-Check 'task-board.ps1 status reads every filed task with no warning, and none says lane: no' ($status -notmatch 'WARN' -and $status -match $id01 -and @($allText | Where-Object { $_ -match '(?m)^lane: no' }).Count -eq 0)
        $again = Invoke-Run $folder
        Assert-Check 'a second run files nothing more' ($again -match 'New-TasksFromGaps: 0 task\(s\) filed\.')
    }
    finally { Remove-Item -Path $folder -Recurse -Force }
    if ($script:failed) { exit 1 }
}

if ($SelfTest) { Invoke-SelfTest; return }
Invoke-NewTasksFromGaps
