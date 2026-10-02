<#
.SYNOPSIS
    Files a Curl task for each audit finding Stewart accepted that has no task yet, and writes
    the task's ID into the finding.

.DESCRIPTION
    Step 3 of Audit/Triage.md (BL-1018, ADR-0267). For each Audit/Findings/AF-*.md with
    `status: accepted` and `task: none`, it runs task-board.ps1 new with:

      -Title     "Fix AF-NNNN: (the finding's title)"
      -Priority  High for a Critical or High finding, Normal for Medium, Low for Low
      -Pipeline  docs for a truthfulness finding, direct for a process finding, feature otherwise
      -Touches   the repository folders (or, for a file at the root, the file) the finding's
                 location names - never an audit path;
                 RunDarkFactory.ps1 for a process finding located in the logs; none
                 otherwise when it names no folder, so the task runs alone

    then fills the task's Goal, Context (the finding's ID, evidence and reproduction, and that
    the finding closes only when a re-audit confirms the fix) and Acceptance criteria (the
    reproduction no longer reproduces; the build and the fast tests pass), and writes the new
    task's ID into the finding's `task` field. Every other finding is left byte-identical. The
    tasks are lane-eligible: they change product code, not audit paths.

    It refuses to run inside a dark factory process (CURL_DARK_FACTORY_LANE set): accepting and
    filing audit work is for an interactive session.

.PARAMETER FindingsDirectory
    Default: Audit/Findings in this repository.

.PARAMETER WhatIf
    Print what would be filed and change nothing.

.PARAMETER SelfTest
    Run on a scratch board and scratch findings and check the rules.
#>
param(
    [string]$FindingsDirectory,
    [switch]$WhatIf,
    [switch]$SelfTest
)

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$Utf8 = New-Object Text.UTF8Encoding $false

function Read-Finding([string]$Path) {
    $text = [IO.File]::ReadAllText($Path)
    $fields = @{}
    if ($text -match '(?s)^---\r?\n(.*?)\r?\n---') {
        foreach ($line in ($Matches[1] -split '\r?\n')) { if ($line -match '^([a-z-]+):\s?(.*)$') { $fields[$Matches[1]] = $Matches[2].Trim() } }
    }
    $section = { param([string]$Name) if ($text -match "(?s)## $Name\r?\n(.*?)(\r?\n## |\z)") { $Matches[1].Trim() } else { '' } }
    $location = if ($text -match '(?m)^Location: `([^`]*)`') { $Matches[1] } else { '' }
    return [pscustomobject]@{ Path = $Path; Text = $text; Id = $fields['id']; Title = $fields['title']; Auditor = $fields['auditor']; Severity = $fields['severity']; Status = $fields['status']; Task = $fields['task']; Location = $location; Evidence = (& $section 'Evidence'); Reproduction = (& $section 'Reproduction') }
}

function Get-TaskPriority([string]$Severity) {
    switch ($Severity) { 'Critical' { 'High' } 'High' { 'High' } 'Medium' { 'Normal' } default { 'Low' } }
}

function Get-TaskPipeline([string]$Auditor) {
    switch ($Auditor) { 'truthfulness' { 'docs' } 'process' { 'direct' } default { 'feature' } }
}

function Get-TaskTouches([string]$Location, [string]$Root) {
    # The repository folders the location names: its first path segment, when that folder exists
    # and is not an audit path. A location is "path:line" or several joined by ", ".
    $touches = @()
    foreach ($part in ($Location -split ',\s*')) {
        $path = ($part -replace '\\', '/' -replace ':\d+(-\d+)?$', '').Trim()
        $first = ($path -split '/')[0]
        if (-not $first -or $first -eq 'Audit' -or $path -like '.claude/agents/audit-*') { continue }
        if (Test-Path -LiteralPath (Join-Path $Root $first) -PathType Container) { $touches += $first }
        elseif (Test-Path -LiteralPath (Join-Path $Root $path) -PathType Leaf) { $touches += $path }
    }
    return @($touches | Select-Object -Unique)
}

function Set-TaskBody([string]$Path, $Finding) {
    $goal = "The defect the audit office reported as $($Finding.Id) is fixed: its reproduction no longer reproduces, so a re-audit can close the finding."
    $context = @(
        "Filed from audit finding $($Finding.Id) ($($Finding.Severity), $($Finding.Auditor) auditor), which Stewart accepted. The finding is ``Audit/Findings/$(Split-Path $Finding.Path -Leaf)``."
        ''
        "Location: ``$($Finding.Location)``"
        ''
        $Finding.Evidence
        ''
        'Reproduction, from the finding:'
        ''
        $Finding.Reproduction
        ''
        "The finding closes only when a later re-audit by the $($Finding.Auditor) auditor confirms the fix, never because this task reaches Done."
    ) -join "`n"
    $criteria = "- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.`n- [ ] ``dotnet build`` and the fast tests (``dotnet test --filter `"TestCategory!=Integration`"``) pass."
    $text = [IO.File]::ReadAllText($Path)
    $text = $text.Replace('<!-- One sentence: the observable outcome once this task is done. -->', $goal)
    $text = $text -replace '<!-- Why this matters.*?-->', $context.Replace('$', '$$')
    $text = $text.Replace('- [ ] <!-- A statement someone else can check from the repository without asking a question. -->', $criteria)
    [IO.File]::WriteAllText($Path, $text, $Utf8)
}

function Invoke-Triage([string]$Findings, [string]$BoardScript, [string]$Root) {
    if ($env:CURL_DARK_FACTORY_LANE) { Write-Error 'Refused: a dark factory process may not file tasks from audit findings; run this in an interactive session (Audit/Triage.md).' -ErrorAction Continue; return 1 }
    $filed = 0
    foreach ($file in @(Get-ChildItem -LiteralPath $Findings -Filter 'AF-*.md' | Sort-Object Name)) {
        $f = Read-Finding $file.FullName
        if ($f.Status -ne 'accepted' -or $f.Task -ne 'none') { continue }
        $newArgs = @('new', '-Title', "Fix $($f.Id): $($f.Title)", '-Priority', (Get-TaskPriority $f.Severity), '-Pipeline', (Get-TaskPipeline $f.Auditor))
        $touches = @(Get-TaskTouches $f.Location $Root)
        # A process finding is about how the factory works, and its location is in the logs, which
        # are no repository path; its fix is in the factory's script.
        if (-not $touches.Count -and $f.Auditor -eq 'process') { $touches = @('RunDarkFactory.ps1') }
        if ($touches.Count) { $newArgs += @('-Touches', ($touches -join ',')) }
        if ($WhatIf) { Write-Host "would file: $($newArgs -join ' ')"; continue }
        $out = (& powershell -NoProfile -ExecutionPolicy Bypass -File $BoardScript @newArgs 2>&1 | ForEach-Object { "$_" }) -join "`n"
        if ($out -notmatch '(?m)^(BL-\d+)\s+(Tasks\S+\.md)') { Write-Error "task-board.ps1 new failed for $($f.Id): $out" -ErrorAction Continue; return 1 }
        $id = $Matches[1]
        Set-TaskBody (Join-Path $Root $Matches[2]) $f
        [IO.File]::WriteAllText($f.Path, ([regex]::new('(?m)^task: none').Replace($f.Text, "task: $id", 1)), $Utf8)
        Write-Host "filed $id for $($f.Id)"
        $filed++
    }
    Write-Host "tasks filed: $filed"
    return 0
}

if ($SelfTest) {
    $work = Join-Path ([IO.Path]::GetTempPath()) ('triage-selftest-' + [guid]::NewGuid().ToString('N'))
    foreach ($s in 'Backlog', 'Doing', 'Blocked', 'Deferred', 'Done') { New-Item -ItemType Directory -Force (Join-Path $work "Tasks\$s") | Out-Null }
    New-Item -ItemType Directory -Force (Join-Path $work 'Curl.Cli.UnitTests'), (Join-Path $work 'findings') | Out-Null
    $failed = 0
    function Check([string]$Name, [bool]$Ok, [string]$Detail) { if (-not $Ok) { $script:failed++ }; Write-Host "$(if ($Ok) { 'PASS' } else { 'FAIL' }) ${Name}: $Detail" }
    $finding = { param($Id, $Status, $Task, $Severity, $Auditor)
        $t = [IO.File]::ReadAllText((Join-Path $repo 'Audit\Findings\FINDING-TEMPLATE.md'))
        $t = $t.Replace('{{ID}}', $Id).Replace('{{TITLE}}', "Finding $Id").Replace('{{AUDITOR}}', $Auditor).Replace('{{SEVERITY}}', $Severity).Replace('{{KEY}}', "k:$Id").Replace('{{FOUND}}', '2026-10-14').Replace('{{FOUND_AT}}', 'abc').Replace('{{SCORECARD}}', '2026-10-14_0930.md').Replace('{{SUMMARY}}', 's').Replace('{{LOCATION}}', 'Curl.Cli.UnitTests/ParserTests.cs:30').Replace('{{EVIDENCE}}', 'IsNotNull only').Replace('{{REPRODUCTION_COMMAND}}', 'Select-String x').Replace('{{REPRODUCTION_EXPECTED}}', 'e').Replace('{{REPRODUCTION_ACTUAL}}', 'a')
        $t = $t.Replace('status: proposed', "status: $Status").Replace('task: none', "task: $Task")
        [IO.File]::WriteAllText((Join-Path $work "findings\$Id-x.md"), $t, $Utf8)
    }
    & $finding 'AF-0001' 'accepted' 'none' 'High' 'quality'
    & $finding 'AF-0002' 'accepted' 'BL-900' 'Medium' 'quality'
    & $finding 'AF-0003' 'proposed' 'none' 'High' 'security'
    & $finding 'AF-0004' 'rejected' 'none' 'Low' 'truthfulness'
    & $finding 'AF-0005' 'deferred' 'none' 'High' 'quality'
    & $finding 'AF-0006' 'blocked' 'none' 'High' 'quality'
    $before = @{}; foreach ($f in Get-ChildItem (Join-Path $work 'findings')) { $before[$f.Name] = [IO.File]::ReadAllText($f.FullName) }
    $board = Join-Path $repo '.claude\skills\task-board\task-board.ps1'
    $savedDir = $env:CLAUDE_PROJECT_DIR; $savedLane = $env:CURL_DARK_FACTORY_LANE
    try {
        $env:CLAUDE_PROJECT_DIR = $work
        $env:CURL_DARK_FACTORY_LANE = '1'
        $refused = Invoke-Triage (Join-Path $work 'findings') $board $work
        $unchanged = -not (@(Get-ChildItem (Join-Path $work 'findings') | Where-Object { [IO.File]::ReadAllText($_.FullName) -cne $before[$_.Name] }).Count) -and -not (Get-ChildItem (Join-Path $work 'Tasks\Backlog'))
        Check 'refused inside a factory process, nothing changed' (($refused -eq 1) -and $unchanged) "exit $refused"
        $env:CURL_DARK_FACTORY_LANE = ''
        $script:WhatIf = $true
        $null = Invoke-Triage (Join-Path $work 'findings') $board $work
        $unchanged = -not (@(Get-ChildItem (Join-Path $work 'findings') | Where-Object { [IO.File]::ReadAllText($_.FullName) -cne $before[$_.Name] }).Count) -and -not (Get-ChildItem (Join-Path $work 'Tasks\Backlog'))
        Check '-WhatIf changes no file' $unchanged 'no task, findings unchanged'
        $script:WhatIf = $false
        $null = Invoke-Triage (Join-Path $work 'findings') $board $work
        $tasks = @(Get-ChildItem (Join-Path $work 'Tasks\Backlog') -Filter 'BL-*.md')
        Check 'exactly one task filed' ($tasks.Count -eq 1) "$($tasks.Count)"
        if ($tasks.Count -eq 1) {
            $t = [IO.File]::ReadAllText($tasks[0].FullName)
            $id = ($tasks[0].Name -split '-')[0..1] -join '-'
            Check 'priority High for a High finding, pipeline feature for quality' ($t -match '(?m)^priority: High?$' -and $t -match '(?m)^pipeline: feature?$') 'High, feature'
            $touchesLine = if ($t -match '(?m)^touches:[^\r\n]*') { $Matches[0] } else { '(none)' }
            Check 'touches the folder the location names' ($touchesLine -eq 'touches: [Curl.Cli.UnitTests]') $touchesLine
            Check 'no template comment left' ($t -notmatch '<!--') 'none'
            Check 'the finding names the task' ([IO.File]::ReadAllText((Join-Path $work 'findings\AF-0001-x.md')) -match "(?m)^task: $id$") $id
        }
        $others = @('AF-0002-x.md', 'AF-0003-x.md', 'AF-0004-x.md', 'AF-0005-x.md', 'AF-0006-x.md' | Where-Object { [IO.File]::ReadAllText((Join-Path $work "findings\$_")) -cne $before[$_] })
        Check 'the other five findings, deferred and blocked included, are byte-identical' ($others.Count -eq 0) "changed: $($others -join ',')"
    }
    finally {
        $env:CLAUDE_PROJECT_DIR = $savedDir; $env:CURL_DARK_FACTORY_LANE = $savedLane
        Remove-Item -Recurse -Force -LiteralPath $work -ErrorAction SilentlyContinue
    }
    exit $(if ($failed) { 1 } else { 0 })
}

if (-not $FindingsDirectory) { $FindingsDirectory = Join-Path $repo 'Audit\Findings' }
$root = if ($env:CLAUDE_PROJECT_DIR) { $env:CLAUDE_PROJECT_DIR } else { $repo }
exit (Invoke-Triage $FindingsDirectory (Join-Path $repo '.claude\skills\task-board\task-board.ps1') $root)
