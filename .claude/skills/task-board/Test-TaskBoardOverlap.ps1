<#
.SYNOPSIS
    Checks that task-board.ps1 counts the upstream case ratchet as no overlap: two ready
    tasks that both name it can run at once, while two that share a project still cannot
    (ADR-0689); that tasks naming files in a project go before an earlier one holding
    the whole project, so they run side by side (ADR-0694); and that `hotspots` lists the
    paths many unfinished lane-eligible tasks overlap, in its contract format.

.DESCRIPTION
    Builds a scratch board in a temporary folder (CLAUDE_PROJECT_DIR points task-board.ps1
    at it), runs `capacity` or `hotspots` through the real script for each case, prints a
    PASS or FAIL line per check, and exits 1 on any FAIL. The real board is never touched.
    Runs under Windows PowerShell 5.1 and PowerShell 7. The script is ASCII only.

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File .claude/skills/task-board/Test-TaskBoardOverlap.ps1
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$script:failed = 0
$boardScript = Join-Path $PSScriptRoot 'task-board.ps1'
$adminScript = Join-Path $PSScriptRoot 'task-admin.ps1'
$root = Join-Path ([IO.Path]::GetTempPath()) ("task-board-overlap-" + [guid]::NewGuid().ToString('N'))
$savedProjectDir = $env:CLAUDE_PROJECT_DIR
$savedLane = $env:CURL_DARK_FACTORY_LANE
$ratchet = 'Curl.Conformance.UnitTests/PassingUpstreamCases.txt'
$shell = if ($PSVersionTable.PSEdition -eq 'Core') { 'pwsh' } else { 'powershell' }

function Report([bool] $Passed, [string] $Check) {
    if ($Passed) { Write-Output "PASS $Check" } else { Write-Output "FAIL $Check"; $script:failed++ }
}

# A fresh scratch board holding one task per spec, numbered BL-001 up. A spec is its
# touches joined with ';', optionally prefixed 'State|' (default Backlog) and suffixed
# '|lane-no' for a 'lane: no' task.
function New-Board([string[]] $Specs) {
    if (Test-Path $root) { Remove-Item $root -Recurse -Force }
    foreach ($folder in 'Backlog', 'Doing', 'Blocked', 'Deferred', 'Done') { New-Item -ItemType Directory -Path (Join-Path $root "Tasks/$folder") -Force | Out-Null }
    $number = 0
    foreach ($spec in $Specs) {
        $number++
        $id = 'BL-{0:D3}' -f $number
        $parts = $spec -split '\|'
        $state = 'Backlog'; $touches = $spec; $lane = ''
        if ($parts.Count -ge 2 -and @('Backlog', 'Doing', 'Blocked', 'Deferred', 'Done') -contains $parts[0]) { $state = $parts[0]; $touches = $parts[1]; $parts = @($parts | Select-Object -Skip 1) }
        else { $touches = $parts[0] }
        if ($parts.Count -ge 2 -and $parts[1] -eq 'lane-no') { $lane = "lane: no`n" }
        $text = "---`nid: $id`ntitle: case $number`npriority: Normal`nassignee: Claude`npipeline: direct`ndepends-on: []`ntouches: [$(($touches -split ';') -join ', ')]`n${lane}requirement: none`ncreated: 2026-01-01`ncompleted:`n---`n# $id - case $number`n`n## Goal`n`n## Context`n`n## Acceptance criteria`n`n- [ ] It works.`n`n## Notes`n`n## Log`n`n- 2026-01-01: Created.`n"
        [IO.File]::WriteAllText((Join-Path $root "Tasks/$state/$id-case-$number.md"), $text)
    }
}

function Invoke-Board([string[]] $Arguments) {
    $ErrorActionPreference = 'Continue'
    $output = @(& $shell -NoProfile -ExecutionPolicy Bypass -File $boardScript @Arguments 2>&1 | ForEach-Object { "$_" })
    return [pscustomobject]@{ Lines = $output; ExitCode = $LASTEXITCODE }
}

# The capacity the real script reports for a board holding only these Backlog tasks, each
# given as its touches joined with ';'.
function Get-Capacity([string[]] $TaskTouches) {
    New-Board $TaskTouches
    $output = (Invoke-Board @('capacity')).Lines -join "`n"
    if ($output -match 'Capacity (\d+):') { return [int] $matches[1] }
    return -1
}

# The lines 'hotspots' prints for a board of these specs (see New-Board).
function Get-HotspotLines([string[]] $Specs, [string[]] $Extra = @()) {
    New-Board $Specs
    return (Invoke-Board (@('hotspots') + $Extra)).Lines
}

try {
    $env:CLAUDE_PROJECT_DIR = $root
    $env:CURL_DARK_FACTORY_LANE = $null
    Report ((Get-Capacity "$ratchet;Curl.Engine.UnitLibrary", "$ratchet;Libcurl") -eq 2) 'two tasks naming the ratchet and different projects run at once'
    Report ((Get-Capacity $ratchet, $ratchet) -eq 2) 'two tasks naming only the ratchet run at once'
    Report ((Get-Capacity $ratchet, 'Curl.Conformance.UnitTests') -eq 2) 'the ratchet does not overlap the project folder it is in'
    Report ((Get-Capacity "$ratchet;Curl.Engine.UnitLibrary", "$ratchet;Curl.Engine.UnitLibrary") -eq 1) 'two tasks sharing a project still run apart'
    Report ((Get-Capacity 'Curl.Conformance.UnitTests/Other.txt', 'Curl.Conformance.UnitTests') -eq 1) 'another file still overlaps its project folder'
    Report ((Get-Capacity 'Curl.Engine.UnitLibrary', 'Curl.Engine.UnitLibrary/A.cs', 'Curl.Engine.UnitLibrary/B.cs') -eq 2) 'two tasks naming files in a project run before an earlier task holding the whole project'

    # hotspots
    $lines = Get-HotspotLines @('A', 'A', 'A', 'B', 'B') @('-Min', '3')
    Report (($lines -join '|') -eq 'Hotspot 3 A: BL-001, BL-002, BL-003') 'hotspots lists a path at the threshold in the contract format and leaves one below it out'
    $lines = Get-HotspotLines @('A', 'A', 'A', 'A')
    Report (($lines -join '|') -eq 'No hotspots.') 'hotspots defaults to -Min 5 and prints No hotspots. when none reach it'
    $run = Invoke-Board @('hotspots')
    Report ($run.ExitCode -eq 0) 'hotspots exits 0 when there are none'
    $lines = Get-HotspotLines @('Curl.Engine.UnitLibrary', 'Curl.Engine.UnitLibrary/A.cs', 'Curl.Engine.UnitLibrary/B.cs') @('-Min', '2')
    Report (($lines -join '|') -eq 'Hotspot 3 Curl.Engine.UnitLibrary: BL-001, BL-002, BL-003|Hotspot 2 Curl.Engine.UnitLibrary/A.cs: BL-001, BL-002|Hotspot 2 Curl.Engine.UnitLibrary/B.cs: BL-001, BL-003') 'hotspots counts a folder entry as covering every file under it, sorted by count then path'
    $lines = Get-HotspotLines @($ratchet, $ratchet, $ratchet) @('-Min', '2')
    Report (($lines -join '|') -eq 'No hotspots.') 'hotspots never lists the ratchet'
    $lines = Get-HotspotLines @('A', 'A|lane-no', 'A') @('-Min', '2')
    Report (($lines -join '|') -eq 'Hotspot 2 A: BL-001, BL-003') 'hotspots leaves a lane: no task out'
    $lines = Get-HotspotLines @('A', 'Done|A', 'Deferred|A', 'Doing|A', 'Blocked|A') @('-Min', '2')
    Report (($lines -join '|') -eq 'Hotspot 3 A: BL-001, BL-004, BL-005') 'hotspots counts Backlog, Doing and Blocked and leaves Done and Deferred out'
    $lines = Get-HotspotLines @('A', 'B', 'B', 'B', 'A', 'B', 'A', 'A', 'A', 'A', 'B', 'B') @('-Min', '6')
    Report (($lines -join '|') -eq 'Hotspot 6 A: BL-001, BL-005, BL-007, BL-008, BL-009, BL-010|Hotspot 6 B: BL-002, BL-003, BL-004, BL-006, BL-011, BL-012') 'hotspots sorts equal counts by path with IDs ascending'

    # task-admin check prints them as warnings and does not fail on them.
    New-Board @('A', 'A', 'A', 'A', 'A')
    # check reports a gap at BL-000 on a board numbered from BL-001; renumber BL-005 to it.
    $last = Join-Path $root 'Tasks/Backlog/BL-005-case-5.md'
    [IO.File]::WriteAllText((Join-Path $root 'Tasks/Backlog/BL-000-case-5.md'), ([IO.File]::ReadAllText($last) -replace 'BL-005', 'BL-000'))
    Remove-Item $last
    $ErrorActionPreference = 'Continue'
    $check = @(& $shell -NoProfile -ExecutionPolicy Bypass -File $adminScript check -FailOnProblem 2>&1 | ForEach-Object { "$_" })
    $checkExit = $LASTEXITCODE
    $ErrorActionPreference = 'Stop'
    Report (@($check | Where-Object { $_ -eq '  warning: Hotspot 5 A: BL-000, BL-001, BL-002, BL-003, BL-004' }).Count -eq 1) 'check prints a hotspot as a warning'
    Report ($checkExit -eq 0 -and @($check | Where-Object { $_ -eq 'No problems found.' }).Count -eq 1) 'check does not fail on a hotspot'
} finally {
    $env:CLAUDE_PROJECT_DIR = $savedProjectDir
    $env:CURL_DARK_FACTORY_LANE = $savedLane
    if (Test-Path $root) { Remove-Item $root -Recurse -Force -ErrorAction SilentlyContinue }
}

if ($script:failed -gt 0) { exit 1 }
