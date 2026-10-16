<#
.SYNOPSIS
    Checks that task-board.ps1 dedupe renumbers a live task whose ID an archived Done task also
    carries, never renumbers an archived task, and still renumbers the second of two new
    tasks (BL-2521).

.DESCRIPTION
    Builds a scratch board in a temporary git repository (CLAUDE_PROJECT_DIR points
    task-board.ps1 at it), runs each case through the real script, prints a PASS or FAIL line
    per check, and exits 1 on any FAIL. The real board is never touched. Runs under Windows
    PowerShell 5.1 and PowerShell 7. The script is ASCII only.

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File .claude/skills/task-board/Test-TaskBoardDedupe.ps1
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$script:failed = 0
$boardScript = Join-Path $PSScriptRoot 'task-board.ps1'
$root = Join-Path ([IO.Path]::GetTempPath()) ("task-board-dedupe-" + [guid]::NewGuid().ToString('N'))
$savedProjectDir = $env:CLAUDE_PROJECT_DIR
$savedLane = $env:CURL_DARK_FACTORY_LANE

function Report([bool] $Passed, [string] $Check) {
    if ($Passed) { Write-Output "PASS $Check" } else { Write-Output "FAIL $Check"; $script:failed++ }
}

# Runs task-board.ps1 in its own process, as /task-run does, and returns its exit code and output.
function Invoke-Board([string[]] $Arguments) {
    # Warnings and refusals are written to the other streams, which are the answer here.
    $ErrorActionPreference = 'Continue'
    # The same edition as this test: pwsh can be a dotnet global tool, whose process is dotnet.
    $shell = if ($PSVersionTable.PSEdition -eq 'Core') { 'pwsh' } else { 'powershell' }
    $output = & $shell -NoProfile -ExecutionPolicy Bypass -File $boardScript @Arguments *>&1 | Out-String
    $exitCode = $LASTEXITCODE
    $output = (($output -replace "\x1b\[[0-9;]*m", '') -replace '\r?\n\s*\|', ' ') -replace '\s+', ' '
    return [pscustomobject]@{ ExitCode = $exitCode; Output = $output }
}

function Invoke-Git([string[]] $Arguments) {
    $ErrorActionPreference = 'Continue'
    & git -C $root @Arguments 2>&1 | Out-Null
}

# Writes a task file directly, so two files can be given the same ID on purpose.
function Write-CaseTask([string] $Folder, [int] $Number, [string] $Slug, [string] $DependsOn = '') {
    $id = 'BL-{0:D3}' -f $Number
    $state = ($Folder -split '/')[0]
    $completed = if ($state -eq 'Done') { '2026-01-01' } else { '' }
    $depends = if ($DependsOn) { "[$DependsOn]" } else { '[]' }
    $text = "---`nid: $id`ntitle: $Slug`npriority: Normal`nassignee: Claude`npipeline: direct`ndepends-on: $depends`ntouches: []`nrequirement: none`ncreated: 2026-01-01`ncompleted: $completed`n---`n# $id - $Slug`n`n## Goal`n`n## Context`n`n## Acceptance criteria`n`n- [ ] It works.`n`n## Notes`n`n## Log`n`n- 2026-01-01: Created.`n"
    $folderPath = Join-Path $root "Tasks/$Folder"
    New-Item -ItemType Directory -Path $folderPath -Force | Out-Null
    $path = Join-Path $folderPath "$id-$Slug.md"
    [IO.File]::WriteAllText($path, $text)
    return $path
}

function Get-CaseFiles([string] $Pattern) {
    return @(Get-ChildItem -Path (Join-Path $root 'Tasks') -Recurse -File -Filter $Pattern | ForEach-Object { $_.FullName })
}

try {
    foreach ($state in 'Backlog', 'Doing', 'Blocked', 'Deferred', 'Done') { New-Item -ItemType Directory -Path (Join-Path $root "Tasks/$state") -Force | Out-Null }
    $env:CLAUDE_PROJECT_DIR = $root
    $env:CURL_DARK_FACTORY_LANE = $null
    Invoke-Git @('init', '-q')
    Invoke-Git @('config', 'user.email', 'test@example.invalid')
    Invoke-Git @('config', 'user.name', 'test')

    # On the shared ref: an archived BL-010 and a live BL-011 that both sessions already had,
    # plus an archived BL-011 another session archived, and two archived BL-020s from history.
    $archivedTen = Write-CaseTask 'Done/2026-01-01_0000' 10 'archived-ten'
    $sharedLiveEleven = Write-CaseTask 'Doing' 11 'shared-live-eleven'
    $archivedEleven = Write-CaseTask 'Done/2026-01-01_0000' 11 'archived-eleven'
    $archivedTwentyA = Write-CaseTask 'Done/2026-01-01_0000' 20 'archived-twenty-a'
    $archivedTwentyB = Write-CaseTask 'Done/2026-01-02_0000' 20 'archived-twenty-b'
    Invoke-Git @('add', '-A')
    Invoke-Git @('commit', '-q', '-m', 'shared')
    Invoke-Git @('tag', 'shared')

    # This session's own work: a new live BL-010, a task naming it, and two new BL-030s.
    $newTen = Write-CaseTask 'Backlog' 10 'new-ten'
    $dependent = Write-CaseTask 'Backlog' 25 'waits-on-ten' 'BL-010'
    Write-CaseTask 'Backlog' 30 'new-thirty-a' | Out-Null
    Write-CaseTask 'Backlog' 30 'new-thirty-b' | Out-Null
    # A lane commits its work, rebases onto the shared ref, then runs dedupe.
    Invoke-Git @('add', '-A')
    Invoke-Git @('commit', '-q', '-m', 'this session')

    $status = Invoke-Board @('status')
    Report ($status.Output -notmatch 'Duplicate task ID BL-020') 'status does not warn about two archived tasks sharing an ID'
    Report ($status.Output -match 'Duplicate task ID BL-010') 'status warns about a live task sharing an ID with an archived one'

    $result = Invoke-Board @('dedupe', '-Since', 'shared')
    Report ($result.ExitCode -eq 0) 'dedupe succeeds'

    Report ((Test-Path -LiteralPath $archivedTen) -and ([IO.File]::ReadAllText($archivedTen) -match '(?m)^id: BL-010\b')) 'an archived task keeps its file name and id'
    Report (-not (Test-Path -LiteralPath $newTen) -and @(Get-CaseFiles '*-new-ten.md').Count -eq 1 -and (Get-CaseFiles '*-new-ten.md')[0] -notmatch 'BL-010-') 'a new live task sharing an archived ID is renumbered'
    $newTenName = Split-Path -Leaf -Path @(Get-CaseFiles '*-new-ten.md')[0]
    $newTenId = [regex]::Match($newTenName, '^BL-\d+').Value
    Report ([IO.File]::ReadAllText($dependent) -match "depends-on: \[$newTenId\]") 'a changed file naming the renumbered task follows it'

    Report ((Test-Path -LiteralPath $archivedEleven) -and -not (Test-Path -LiteralPath $sharedLiveEleven) -and @(Get-CaseFiles '*-shared-live-eleven.md').Count -eq 1) 'a live task already on the shared ref is renumbered when an archived task has its ID'

    Report ((Test-Path -LiteralPath $archivedTwentyA) -and (Test-Path -LiteralPath $archivedTwentyB)) 'two archived tasks sharing an ID are left as they are'

    $thirtyA = @(Get-CaseFiles '*-new-thirty-a.md'); $thirtyB = @(Get-CaseFiles '*-new-thirty-b.md')
    $kept = @(@($thirtyA + $thirtyB) | Where-Object { (Split-Path $_ -Leaf) -like 'BL-030-*' })
    Report ($kept.Count -eq 1) 'of two new tasks sharing an ID, exactly one keeps it'

    $after = Invoke-Board @('status')
    Report ($after.Output -notmatch 'Duplicate task ID') 'status warns about nothing after dedupe'
    $ids = @(Get-ChildItem -Path (Join-Path $root 'Tasks') -Recurse -File -Filter 'BL-*.md' | Where-Object { $_.DirectoryName -notmatch '\d{4}-\d{2}-\d{2}_\d{4}$' } |
        ForEach-Object { [regex]::Match($_.Name, '^BL-\d+').Value })
    Report (@($ids | Group-Object | Where-Object { $_.Count -gt 1 }).Count -eq 0) 'no two live tasks share an ID after dedupe'
} finally {
    $env:CLAUDE_PROJECT_DIR = $savedProjectDir
    $env:CURL_DARK_FACTORY_LANE = $savedLane
    if (Test-Path -LiteralPath $root) { Remove-Item -LiteralPath $root -Recurse -Force }
}

if ($script:failed -gt 0) { exit 1 }
