<#
.SYNOPSIS
    Checks that task-board.ps1 counts the upstream case ratchet as no overlap: two ready
    tasks that both name it can run at once, while two that share a project still cannot
    (ADR-0689); and that tasks naming files in a project go before an earlier one holding
    the whole project, so they run side by side (ADR-0694).

.DESCRIPTION
    Builds a scratch board in a temporary folder (CLAUDE_PROJECT_DIR points task-board.ps1
    at it), runs `capacity` through the real script for each case, prints a PASS or FAIL
    line per check, and exits 1 on any FAIL. The real board is never touched. Runs under
    Windows PowerShell 5.1 and PowerShell 7. The script is ASCII only.

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File .claude/skills/task-board/Test-TaskBoardOverlap.ps1
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$script:failed = 0
$boardScript = Join-Path $PSScriptRoot 'task-board.ps1'
$root = Join-Path ([IO.Path]::GetTempPath()) ("task-board-overlap-" + [guid]::NewGuid().ToString('N'))
$savedProjectDir = $env:CLAUDE_PROJECT_DIR
$savedLane = $env:CURL_DARK_FACTORY_LANE
$ratchet = 'Curl.Conformance.UnitTests/PassingUpstreamCases.txt'

function Report([bool] $Passed, [string] $Check) {
    if ($Passed) { Write-Output "PASS $Check" } else { Write-Output "FAIL $Check"; $script:failed++ }
}

# The capacity the real script reports for a board holding only these Backlog tasks, each
# given as its touches joined with ';'.
function Get-Capacity([string[]] $TaskTouches) {
    if (Test-Path $root) { Remove-Item $root -Recurse -Force }
    foreach ($folder in 'Backlog', 'Doing', 'Blocked', 'Deferred', 'Done') { New-Item -ItemType Directory -Path (Join-Path $root "Tasks/$folder") -Force | Out-Null }
    $number = 0
    foreach ($touches in $TaskTouches) {
        $number++
        $id = 'BL-{0:D3}' -f $number
        $text = "---`nid: $id`ntitle: case $number`npriority: Normal`nassignee: Claude`npipeline: direct`ndepends-on: []`ntouches: [$(($touches -split ';') -join ', ')]`nrequirement: none`ncreated: 2026-01-01`ncompleted:`n---`n# $id - case $number`n`n## Goal`n`n## Context`n`n## Acceptance criteria`n`n- [ ] It works.`n`n## Notes`n`n## Log`n`n- 2026-01-01: Created.`n"
        [IO.File]::WriteAllText((Join-Path $root "Tasks/Backlog/$id-case-$number.md"), $text)
    }
    $ErrorActionPreference = 'Continue'
    $shell = if ($PSVersionTable.PSEdition -eq 'Core') { 'pwsh' } else { 'powershell' }
    $output = & $shell -NoProfile -ExecutionPolicy Bypass -File $boardScript capacity *>&1 | Out-String
    if ($output -match 'Capacity (\d+):') { return [int] $matches[1] }
    return -1
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
} finally {
    $env:CLAUDE_PROJECT_DIR = $savedProjectDir
    $env:CURL_DARK_FACTORY_LANE = $savedLane
    if (Test-Path $root) { Remove-Item $root -Recurse -Force -ErrorAction SilentlyContinue }
}

if ($script:failed -gt 0) { exit 1 }
