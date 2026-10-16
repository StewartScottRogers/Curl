<#
.SYNOPSIS
    Checks that task-board.ps1 refuses a move to Done when a task's acceptance criteria were
    reworded, added or removed after it was claimed, and allows it otherwise (BL-2488).

.DESCRIPTION
    Builds a scratch board in a temporary folder (CLAUDE_PROJECT_DIR points task-board.ps1
    at it), runs each case through the real script, prints a PASS or FAIL line per check,
    and exits 1 on any FAIL. The real board is never touched. Runs under Windows PowerShell
    5.1 and PowerShell 7. The script is ASCII only.

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File .claude/skills/task-board/Test-TaskBoardCriteria.ps1
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$script:failed = 0
$boardScript = Join-Path $PSScriptRoot 'task-board.ps1'
$root = Join-Path ([IO.Path]::GetTempPath()) ("task-board-criteria-" + [guid]::NewGuid().ToString('N'))
$savedProjectDir = $env:CLAUDE_PROJECT_DIR
$savedLane = $env:CURL_DARK_FACTORY_LANE

function Report([bool] $Passed, [string] $Check) {
    if ($Passed) { Write-Output "PASS $Check" } else { Write-Output "FAIL $Check"; $script:failed++ }
}

# Runs task-board.ps1 in its own process, as /task-run does, and returns its exit code and output.
function Invoke-Board([string[]] $Arguments) {
    # A refusal is written to stderr, which is the answer here; under 'Stop' Windows
    # PowerShell would turn it into a terminating error.
    $ErrorActionPreference = 'Continue'
    # The same edition as this test: pwsh can be a dotnet global tool, whose process is dotnet.
    $shell = if ($PSVersionTable.PSEdition -eq 'Core') { 'pwsh' } else { 'powershell' }
    $output = & $shell -NoProfile -ExecutionPolicy Bypass -File $boardScript @Arguments 2>&1 | Out-String
    $exitCode = $LASTEXITCODE
    # pwsh colours error text and wraps it behind a "     | " margin: drop both and join the lines.
    $output = (($output -replace "\x1b\[[0-9;]*m", '') -replace '\r?\n\s*\|', ' ') -replace '\s+', ' '
    return [pscustomobject]@{ ExitCode = $exitCode; Output = $output }
}

function New-CaseTask([string] $Title) {
    $result = Invoke-Board @('new', '-Title', $Title, '-Pipeline', 'direct')
    $id = [regex]::Match($result.Output, 'BL-\d+').Value
    $path = Get-ChildItem (Join-Path $root 'Tasks/Backlog') -Filter "$id-*.md" | Select-Object -First 1 -ExpandProperty FullName
    Set-Criteria $path @('- [ ] The first thing works.', '- [ ] The second thing works,', '  across two lines.')
    return [pscustomobject]@{ Id = $id; Folder = 'Backlog'; Name = (Split-Path $path -Leaf) }
}

function Get-CasePath($Task) { return Join-Path (Join-Path $root "Tasks/$($Task.Folder)") $Task.Name }

function Set-Criteria([string] $Path, [string[]] $Lines) {
    $text = [IO.File]::ReadAllText($Path)
    $text = [regex]::Replace($text, '(?ms)(^## Acceptance criteria[ \t]*\r?\n)(.*?)(?=^## )', { param($m) $m.Groups[1].Value + "`n" + ($Lines -join "`n") + "`n`n" })
    [IO.File]::WriteAllText($Path, $text)
}

function Edit-Case($Task, [scriptblock] $Change) {
    $path = Get-CasePath $Task
    [IO.File]::WriteAllText($path, (& $Change ([IO.File]::ReadAllText($path))))
}

function Move-Case($Task, [string] $To) {
    $arguments = @('move', '-Id', $Task.Id, '-To', $To)
    if ($To -ne 'Doing') { $arguments += @('-Reason', 'test') }
    $result = Invoke-Board $arguments
    if ($result.ExitCode -eq 0) { $Task.Folder = $To }
    return $result
}

function Set-Ticked([string] $Text) { return $Text -replace '(?m)^(\s*[-*]) \[ \]', '$1 [x]' }

try {
    foreach ($state in 'Backlog', 'Doing', 'Blocked', 'Deferred', 'Done') { New-Item -ItemType Directory -Path (Join-Path $root "Tasks/$state") -Force | Out-Null }
    $env:CLAUDE_PROJECT_DIR = $root
    $env:CURL_DARK_FACTORY_LANE = $null

    $ticked = New-CaseTask 'Only ticked'
    Move-Case $ticked 'Doing' | Out-Null
    Report ((Get-Content (Get-CasePath $ticked) -Raw) -match '-> Doing\. Criteria at claim: [0-9a-f]{8} [0-9a-f]{8}\.') 'a claim records one fingerprint per criterion on its Log line'
    Edit-Case $ticked { param($t) Set-Ticked $t }
    Report ((Move-Case $ticked 'Done').ExitCode -eq 0) 'a task with only its boxes ticked moves to Done'

    $reworded = New-CaseTask 'Reworded'
    Move-Case $reworded 'Doing' | Out-Null
    Edit-Case $reworded { param($t) Set-Ticked ($t -replace 'The first thing works\.', 'The first thing is filed as a later task.') }
    $refusal = Move-Case $reworded 'Done'
    Report ($refusal.ExitCode -ne 0 -and $reworded.Folder -eq 'Doing') 'a task with a reworded criterion is refused'
    Report ($refusal.Output -match 'changed or added: The first thing is filed as a later task\.' -and $refusal.Output -match 'move it to Backlog, edit them there, and claim it again') 'the refusal names the changed line and the way to change criteria'

    $removed = New-CaseTask 'Removed'
    Move-Case $removed 'Doing' | Out-Null
    Edit-Case $removed { param($t) Set-Ticked ($t -replace '(?m)^- \[ \] The first thing works\.\r?\n', '') }
    $refusal = Move-Case $removed 'Done'
    Report ($refusal.ExitCode -ne 0 -and $refusal.Output -match 'removed: 1 criteria') 'a task with a removed criterion is refused and the removal counted'

    $added = New-CaseTask 'Added'
    Move-Case $added 'Doing' | Out-Null
    Edit-Case $added { param($t) Set-Ticked ($t -replace '(?m)^(- \[ \] The first thing works\.)', "`$1`n- [ ] A third thing.") }
    Report ((Move-Case $added 'Done').ExitCode -ne 0) 'a task with an added criterion is refused'

    $rewrapped = New-CaseTask 'Rewrapped'
    Move-Case $rewrapped 'Doing' | Out-Null
    Edit-Case $rewrapped { param($t) Set-Ticked ($t -replace 'The second thing works,\r?\n  across two lines\.', "The second thing   works, across`n    two lines.") }
    Report ((Move-Case $rewrapped 'Done').ExitCode -eq 0) 'rewrapping a criterion is not a change'

    $backlogEdit = New-CaseTask 'Edited in Backlog'
    Edit-Case $backlogEdit { param($t) $t -replace 'The first thing works\.', 'The first thing works on Linux too.' }
    Move-Case $backlogEdit 'Doing' | Out-Null
    Edit-Case $backlogEdit { param($t) Set-Ticked $t }
    Report ((Move-Case $backlogEdit 'Done').ExitCode -eq 0) 'criteria changed in Backlog before the claim are the ones checked'

    $reclaimed = New-CaseTask 'Reclaimed'
    Move-Case $reclaimed 'Doing' | Out-Null
    Move-Case $reclaimed 'Backlog' | Out-Null
    Edit-Case $reclaimed { param($t) $t -replace 'The first thing works\.', 'The first thing works, narrowed.' }
    Move-Case $reclaimed 'Doing' | Out-Null
    Edit-Case $reclaimed { param($t) Set-Ticked $t }
    Report ((Move-Case $reclaimed 'Done').ExitCode -eq 0) 'a task sent back, edited and claimed again is checked against the new claim'

    $legacy = New-CaseTask 'Claimed before snapshots'
    Move-Case $legacy 'Doing' | Out-Null
    Edit-Case $legacy { param($t) Set-Ticked (($t -replace ' Criteria at claim: [0-9a-f ]+\.', '') -replace 'The first thing works\.', 'Anything.') }
    Report ((Move-Case $legacy 'Done').ExitCode -eq 0) 'a task whose claim has no snapshot is not checked'
} finally {
    $env:CLAUDE_PROJECT_DIR = $savedProjectDir
    $env:CURL_DARK_FACTORY_LANE = $savedLane
    if (Test-Path -LiteralPath $root) { Remove-Item -LiteralPath $root -Recurse -Force }
}

if ($script:failed -gt 0) { exit 1 }
