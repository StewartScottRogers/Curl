<#
.SYNOPSIS
    Files a Curl task for each audit finding Stewart accepted that has no task yet, and a Re-fix
    task for one a re-audit found still reproducing after its tasks were Done, and writes the
    task's ID into the finding.

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
    reproduction no longer reproduces; the build and the fast tests pass) - for a finding whose
    key ends in :cost-outlier, instead, a Goal and criteria to find what drove the past run's
    cost and fix it or record that nothing can be, and a Context saying the first audit after the
    task is Done closes the finding - and writes the new
    task's ID into the finding's `task` field and its `tasks` list. The tasks are lane-eligible:
    they change product code, not audit paths. The exception is any other process finding
    (ADR-0761): its reproduction measures a frozen log copy, which reproduces forever, so its
    task's first criterion is the same measurement run over the live logs written after the fix
    landed, with the window and the numbers in the task's Notes; and since that measurement runs
    Audit/Tools/Measure-FactoryProcess.ps1, which a lane may not read, the task is filed with
    -NoLane (interactive only).

    The reopen loop (ADR-0422), for every accepted finding that already has tasks:

      history   Its tasks are its `task`, its `tasks` list, and every task on the board (archives
                included) whose title starts "Fix" or "Re-fix" and names its ID - so a Re-fix task
                a session filed by hand, or one covering two findings, is found. When that differs
                from what the finding records, `tasks` is rewritten (all of them, by number) and
                `task` set to the newest one still open, else the newest. `task` keeps meaning
                "the current task".
      open      Any of them not Done, or not on the board: nothing is filed; it is being worked.
      reopen    All Done, and the finding's latest Re-audits line is its own auditor's
                "reproduces: yes" dated after the last of them was completed: it files
                "Re-fix AF-NNNN: (title)" as above, with that line and the earlier tasks in its
                Context. A latest line that is set aside ("not re-audited", a planted-defect
                overlap) reopens nothing.
      historical  A finding whose key ends in :cost-outlier (one past run's cost) never reopens: no
                Re-fix task is filed for it, whatever its re-audits say. Once all its tasks are
                Done, the next audit's Write-AuditFindings.ps1 closes it with closed-how
                task-done-historical (ADR-0422, amended 2026-10-14). Its first Fix task is filed
                as for any finding.

    Every other finding is left byte-identical.

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
# Windows PowerShell 5.1 on Windows, PowerShell 7 elsewhere (BL-3927).
$PowerShellExe = if ($PSVersionTable.PSVersion.Major -lt 6 -or $IsWindows) { 'powershell' } else { 'pwsh' }
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
    $tasks = @("$($fields['tasks'])" -split ',\s*' | Where-Object { $_ -match '^BL-\d+$' })
    return [pscustomobject]@{ Path = $Path; Text = $text; Id = $fields['id']; Title = $fields['title']; Auditor = $fields['auditor']; Severity = $fields['severity']; Status = $fields['status']; Key = "$($fields['key'])"; Task = $fields['task']; Tasks = $tasks; Location = $location; Evidence = (& $section 'Evidence'); Reproduction = (& $section 'Reproduction') }
}

# The front matter keys in template order; the same as Write-AuditFindings.ps1.
$FrontMatterOrder = @('id', 'title', 'auditor', 'severity', 'status', 'reason', 'key', 'reproduction', 'task', 'tasks', 'found', 'found-at', 'scorecard', 'duplicate-of', 'closed', 'closed-how', 'closed-by')

function Set-FrontMatter([string]$Text, [string]$Name, [string]$Value) {
    # Replaces the key's value, or inserts the key after the nearest earlier one in template order
    # (a finding filed before ADR-0422 has no tasks key). The same as Write-AuditFindings.ps1.
    $line = "${Name}: $Value".TrimEnd()
    $existing = [regex]::new("(?m)^$([regex]::Escape($Name)):[^\r\n]*")
    if ($existing.IsMatch($Text)) { return $existing.Replace($Text, $line.Replace('$', '$$'), 1) }
    $newline = if ($Text.Contains("`r`n")) { "`r`n" } else { "`n" }
    $before = @($FrontMatterOrder[0..([array]::IndexOf($FrontMatterOrder, $Name) - 1)])
    [array]::Reverse($before)
    foreach ($previous in $before) {
        $at = [regex]::Match($Text, "(?m)^$([regex]::Escape($previous)):[^\r\n]*")
        if ($at.Success) { return $Text.Insert($at.Index + $at.Length, $newline + $line) }
    }
    return $Text
}

function Read-BoardTasks([string]$Root) {
    # Every task on the board, archives included: Id, Title, Done (in Tasks/Done or an archive
    # under it) and Completed.
    $tasks = @()
    $board = Join-Path $Root 'Tasks'
    if (-not (Test-Path -LiteralPath $board)) { return $tasks }
    foreach ($file in @(Get-ChildItem -LiteralPath $board -Recurse -File -Filter 'BL-*.md')) {
        $head = [IO.File]::ReadAllText($file.FullName)
        $title = if ($head -match '(?m)^title:[ \t]*([^\r\n]*)') { $Matches[1].Trim() } else { '' }
        $completed = if ($head -match '(?m)^completed:[ \t]*(\d{4}-\d\d-\d\d)') { $Matches[1] } else { '' }
        $done = $file.FullName.Substring($board.Length) -match '^[\\/]Done[\\/]'
        $tasks += [pscustomobject]@{ Id = (($file.Name -split '-')[0..1] -join '-'); Title = $title; Done = $done; Completed = $completed }
    }
    return $tasks
}

function Get-TaskHistory($Finding, [object[]]$Board) {
    # Every task filed for the finding, by number: its task and tasks fields, and every board task
    # whose title starts "Fix" or "Re-fix" and names the finding's ID (one Re-fix task may cover
    # two findings, BL-1608).
    $ids = @($Finding.Tasks)
    if ($Finding.Task -match '^BL-\d+$') { $ids += $Finding.Task }
    $pattern = "\b$([regex]::Escape($Finding.Id))\b"
    $ids += @($Board | Where-Object { $_.Title -match '^(Re-fix|Fix)\b' -and $_.Title -match $pattern } | ForEach-Object { $_.Id })
    return @($ids | Where-Object { $_ } | Sort-Object { [int]($_ -replace '\D', '') } -Unique)
}

function Get-LatestReaudit([string]$Text) {
    # The last Re-audits line, whatever its verdict: Date, Verdict (the third field, e.g.
    # "reproduces: yes", "not re-audited"), Own (not marked "(re-audited by <auditor>)"), Line.
    $section = if ($Text -match '(?s)## Re-audits[ \t]*\r?\n(.*?)(\r?\n## |\z)') { $Matches[1] } else { '' }
    $last = $null
    foreach ($line in ($section -split '\r?\n')) {
        if ($line -match '^- (\S+) \| (\S+) \| ([^|]+?) \| ?(.*)$') { $last = [pscustomobject]@{ Date = $Matches[1]; Verdict = $Matches[3].Trim(); Own = ($Matches[4] -notlike '(re-audited by *'); Line = $line } }
    }
    return $last
}

function Get-ReopenReaudit($Finding, [object[]]$HistoryTasks) {
    # The re-audit that reopens a finding whose tasks are all Done, or $null: the finding's latest
    # Re-audits line is its own auditor's "reproduces: yes" (or the runner's "reproduces: yes
    # (runner rerun on clean commit)"), dated after the last task was completed.
    # A set-aside or "not re-audited" latest line reopens nothing.
    $latest = Get-LatestReaudit $Finding.Text
    if (-not $latest -or -not $latest.Own -or $latest.Verdict -notlike 'reproduces: yes*') { return $null }
    $completed = @($HistoryTasks | ForEach-Object { $_.Completed } | Sort-Object)
    if (-not $completed.Count -or -not $completed[-1] -or $latest.Date -le $completed[-1]) { return $null }
    return $latest
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

function Set-TaskBody([string]$Path, $Finding, $Reopen, [string[]]$Earlier) {
    $goal = "The defect the audit office reported as $($Finding.Id) is fixed: its reproduction no longer reproduces, so a re-audit can close the finding."
    # A finding about one past run's cost cannot stop reproducing: its task is the evidence, and the
    # next audit closes it once the task is Done (ADR-0422, amended 2026-10-14).
    $historical = "$($Finding.Key)".EndsWith(':cost-outlier')
    $closing = if ($historical) { "The finding describes a past run's cost, which no fix can change, so no re-audit can close it: the first audit after this task reaches Done closes it (closed-how task-done-historical, ADR-0422 amended 2026-10-14). Look for avoidable waste in what drove the cost and fix its cause, or record that there is none." } else { "The finding closes only when a later re-audit by the $($Finding.Auditor) auditor confirms the fix, never because this task reaches Done." }
    # Any other process finding describes past factory behaviour in a frozen log copy, so its
    # reproduction reproduces forever; the task's evidence is the same measurement over the logs
    # written after the fix landed, as the process auditor's re-audit measures it (ADR-0761).
    $liveWindow = ($Finding.Auditor -eq 'process') -and -not $historical
    if ($liveWindow) { $closing = "The reproduction measures a frozen copy of the logs, which no fix can change, so this task's evidence is the same measurement run over the live logs written after the fix landed (ADR-0761). $closing" }
    $again = @()
    if ($Reopen) {
        $again = @(
            "Re-fix: the earlier task(s) $($Earlier -join ', ') reached Done, and a later re-audit by the $($Finding.Auditor) auditor found the reproduction still reproduces:"
            ''
            $Reopen.Line
            '')
    }
    $context = @(
        "Filed from audit finding $($Finding.Id) ($($Finding.Severity), $($Finding.Auditor) auditor), which Stewart accepted. The finding is ``Audit/Findings/$(Split-Path $Finding.Path -Leaf)``."
        ''
        $again
        "Location: ``$($Finding.Location)``"
        ''
        $Finding.Evidence
        ''
        'Reproduction, from the finding:'
        ''
        $Finding.Reproduction
        ''
        $closing
    ) -join "`n"
    $criteria = "- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.`n- [ ] ``dotnet build`` and the fast tests (``dotnet test --filter `"TestCategory!=Integration`"``) pass."
    if ($historical) {
        $goal = "What drove the past run's cost that the audit office reported as $($Finding.Id) is found, and any avoidable waste in it is fixed, or the task records that there is none."
        $criteria = "- [ ] The cause of the cost is named in this task, and either fixed or recorded as unavoidable.`n- [ ] ``dotnet build`` and the fast tests (``dotnet test --filter `"TestCategory!=Integration`"``) pass."
    }
    if ($liveWindow) {
        $goal = "The cause of the factory behaviour the audit office reported as $($Finding.Id) is fixed: the finding's measurement, run over the logs written after the fix, gives its expected result, so a re-audit of that window can close the finding."
        $criteria = "- [ ] The finding's measurement, run over the live logs written after the fix landed (``-LogRoot`` the repository's ``.logs`` folder, ``-CiRunsJson`` a fresh ``gh run list --workflow CI --branch work/dark-factory --json databaseId,status,conclusion,headSha,createdAt,updatedAt --limit 500``, counting only trace lines and CI runs from the fix's landing time), gives the finding's expected result; Notes name the fix, its landing time, the window and the numbers. If no factory shift has run since the fix, the task waits in Backlog.`n- [ ] ``dotnet build`` and the fast tests (``dotnet test --filter `"TestCategory!=Integration`"``) pass."
    }
    $text = [IO.File]::ReadAllText($Path)
    $text = $text.Replace('<!-- One sentence: the observable outcome once this task is done. -->', $goal)
    $text = $text -replace '<!-- Why this matters.*?-->', $context.Replace('$', '$$')
    $text = $text.Replace('- [ ] <!-- A statement someone else can check from the repository without asking a question. -->', $criteria)
    [IO.File]::WriteAllText($Path, $text, $Utf8)
}

function Invoke-Triage([string]$Findings, [string]$BoardScript, [string]$Root) {
    if ($env:CURL_DARK_FACTORY_LANE) { Write-Error 'Refused: a dark factory process may not file tasks from audit findings; run this in an interactive session (Audit/Triage.md).' -ErrorAction Continue; return 1 }
    $filed = 0
    $board = @(Read-BoardTasks $Root)
    foreach ($file in @(Get-ChildItem -LiteralPath $Findings -Filter 'AF-*.md' | Sort-Object Name)) {
        $f = Read-Finding $file.FullName
        if ($f.Status -ne 'accepted') { continue }
        $history = @(Get-TaskHistory $f $board)
        Update-FindingTasks $f $history (Get-CurrentTask $history $board)
        $reopen = $null
        if ($history.Count) {
            # Skip a finding with any task still open (or not on this board): it is being worked,
            # a Re-fix task included. Reopen only when all are Done and a later re-audit says it
            # still reproduces (ADR-0422).
            $tasks = @($board | Where-Object { $history -contains $_.Id })
            if ($tasks.Count -lt $history.Count -or @($tasks | Where-Object { -not $_.Done }).Count) { continue }
            # A finding about one past run's cost never reopens: history cannot change, so a re-audit
            # always says it reproduces. The next audit closes it, task-done-historical (ADR-0422,
            # amended 2026-10-14).
            if ($f.Key.EndsWith(':cost-outlier')) { continue }
            $reopen = Get-ReopenReaudit $f $tasks
            if (-not $reopen) { continue }
        }
        $id = New-FindingTask $f $reopen $history $BoardScript $Root
        if ($id -eq 'failed') { return 1 }
        if ($id) { Update-FindingTasks $f @($history + $id) -Current $id; $filed++ }
    }
    Write-Host "tasks filed: $filed"
    return 0
}

function Get-CurrentTask([string[]]$History, [object[]]$Board) {
    # The finding's current task: the newest of its tasks still open on the board, else the newest.
    $open = @($Board | Where-Object { $History -contains $_.Id -and -not $_.Done } | ForEach-Object { $_.Id } | Sort-Object { [int]($_ -replace '\D', '') })
    if ($open.Count) { return $open[-1] }
    return ''
}

function Update-FindingTasks($Finding, [string[]]$History, [string]$Current) {
    # Writes the finding's tasks list (every task filed for it) and its task (the newest one), when
    # they differ from what the file says. task keeps meaning "the current task" (ADR-0422).
    if (-not $History.Count) { return }
    $sorted = @($History | Sort-Object { [int]($_ -replace '\D', '') } -Unique)
    # The current task: the one given (newly filed, or the newest still open), else the recorded
    # one when it is in the history, else the newest.
    $newest = if ($Current) { $Current } elseif ($sorted -contains $Finding.Task) { $Finding.Task } else { $sorted[-1] }
    $recorded = @(@($Finding.Tasks) + @($Finding.Task) | Where-Object { $_ -match '^BL-\d+$' } | Sort-Object { [int]($_ -replace '\D', '') } -Unique)
    # A finding whose task alone names its one task is left as it is.
    if ((($recorded -join ',') -eq ($sorted -join ',')) -and $Finding.Task -eq $newest) { return }
    $text = Set-FrontMatter (Set-FrontMatter $Finding.Text 'task' $newest) 'tasks' ($sorted -join ', ')
    if ($text -ceq $Finding.Text) { return }
    if ($WhatIf) { Write-Host "would record for $($Finding.Id): task $newest, tasks $($sorted -join ', ')"; return }
    [IO.File]::WriteAllText($Finding.Path, $text, $Utf8)
    $Finding.Text = $text
    $Finding.Task = $newest
    $Finding.Tasks = $sorted
}

function New-FindingTask($Finding, $Reopen, [string[]]$Earlier, [string]$BoardScript, [string]$Root) {
    # Files "Fix AF-####: <title>", or "Re-fix AF-####: <title>" when $Reopen holds the re-audit that
    # reopened it, through task-board.ps1 new. Returns the new ID, '' under -WhatIf, 'failed'.
    $verb = if ($Reopen) { 'Re-fix' } else { 'Fix' }
    $newArgs = @('new', '-Title', "$verb $($Finding.Id): $($Finding.Title)", '-Priority', (Get-TaskPriority $Finding.Severity), '-Pipeline', (Get-TaskPipeline $Finding.Auditor))
    $touches = @(Get-TaskTouches $Finding.Location $Root)
    # A process finding is about how the factory works, and its location is in the logs, which
    # are no repository path; its fix is in the factory's script.
    if (-not $touches.Count -and $Finding.Auditor -eq 'process') { $touches = @('RunDarkFactory.ps1') }
    if ($touches.Count) { $newArgs += @('-Touches', ($touches -join ',')) }
    # Its evidence runs the audit office's process measurement, which a lane may not read (ADR-0761).
    if ($Finding.Auditor -eq 'process' -and -not "$($Finding.Key)".EndsWith(':cost-outlier')) { $newArgs += '-NoLane' }
    if ($WhatIf) { Write-Host "would file: $($newArgs -join ' ')"; return '' }
    $out = (& $PowerShellExe -NoProfile -ExecutionPolicy Bypass -File $BoardScript @newArgs 2>&1 | ForEach-Object { "$_" }) -join "`n"
    if ($out -notmatch '(?m)^(BL-\d+)\s+(Tasks\S+\.md)') { Write-Error "task-board.ps1 new failed for $($Finding.Id): $out" -ErrorAction Continue; return 'failed' }
    $id = $Matches[1]
    Set-TaskBody (Join-Path $Root $Matches[2]) $Finding $Reopen $Earlier
    Write-Host "filed $id for $($Finding.Id)$(if ($Reopen) { ' (re-fix)' })"
    return $id
}

if ($SelfTest) {
    $work = Join-Path ([IO.Path]::GetTempPath()) ('triage-selftest-' + [guid]::NewGuid().ToString('N'))
    foreach ($s in 'Backlog', 'Doing', 'Blocked', 'Deferred', 'Done') { New-Item -ItemType Directory -Force (Join-Path $work "Tasks\$s") | Out-Null }
    New-Item -ItemType Directory -Force (Join-Path $work 'Curl.Cli.UnitTests'), (Join-Path $work 'findings') | Out-Null
    $failed = 0
    function Check([string]$Name, [bool]$Ok, [string]$Detail) { if (-not $Ok) { $script:failed++ }; Write-Host "$(if ($Ok) { 'PASS' } else { 'FAIL' }) ${Name}: $Detail" }
    $finding = { param($Id, $Status, $Task, $Severity, $Auditor, $Key)
        if (-not $Key) { $Key = "k:$Id" }
        $t = [IO.File]::ReadAllText((Join-Path $repo 'Audit\Findings\FINDING-TEMPLATE.md'))
        $t = $t.Replace('{{ID}}', $Id).Replace('{{TITLE}}', "Finding $Id").Replace('{{AUDITOR}}', $Auditor).Replace('{{SEVERITY}}', $Severity).Replace('{{KEY}}', $Key).Replace('{{FOUND}}', '2026-10-14').Replace('{{FOUND_AT}}', 'abc').Replace('{{SCORECARD}}', '2026-10-14_0930.md').Replace('{{SUMMARY}}', 's').Replace('{{LOCATION}}', 'Curl.Cli.UnitTests/ParserTests.cs:30').Replace('{{EVIDENCE}}', 'IsNotNull only').Replace('{{REPRODUCTION_COMMAND}}', 'Select-String x').Replace('{{REPRODUCTION_EXPECTED}}', 'e').Replace('{{REPRODUCTION_ACTUAL}}', 'a')
        $t = $t.Replace('status: proposed', "status: $Status").Replace('task: none', "task: $Task")
        [IO.File]::WriteAllText((Join-Path $work "findings\$Id-x.md"), $t, $Utf8)
    }
    & $finding 'AF-0001' 'accepted' 'none' 'High' 'quality'
    & $finding 'AF-0002' 'accepted' 'BL-900' 'Medium' 'quality'
    & $finding 'AF-0003' 'proposed' 'none' 'High' 'security'
    & $finding 'AF-0004' 'rejected' 'none' 'Low' 'truthfulness'
    & $finding 'AF-0005' 'deferred' 'none' 'High' 'quality'
    & $finding 'AF-0006' 'blocked' 'none' 'High' 'quality'
    # The reopen loop (ADR-0422): a board of earlier tasks and findings with re-audits after them.
    $task = { param($Id, $State, $Title, $Completed)
        New-Item -ItemType Directory -Force (Join-Path $work "Tasks\$State") | Out-Null
        [IO.File]::WriteAllText((Join-Path $work "Tasks\$State\$Id-x.md"), "---`nid: $Id`ntitle: $Title`npriority: Normal`ncompleted: $Completed`n---`n# $Id`n", $Utf8)
    }
    $reaudit = { param($Id, [string[]]$Lines)
        $path = Join-Path $work "findings\$Id-x.md"
        $text = [IO.File]::ReadAllText($path) -replace '(## Re-audits\r?\n)', ('$1' + "`n" + ($Lines -join "`n") + "`n")
        [IO.File]::WriteAllText($path, $text, $Utf8)
    }
    & $task 'BL-901' 'Done\2026-10-05' 'Fix AF-0007: x' '2026-10-05'
    & $task 'BL-902' 'Done' 'Fix AF-0008: x' '2026-10-05'
    & $task 'BL-903' 'Doing' 'Re-fix AF-0008 and AF-0009 in one test' ''
    & $task 'BL-904' 'Done' 'Fix AF-0010: x' '2026-10-08'
    & $task 'BL-905' 'Done' 'Fix AF-0011: x' '2026-10-05'
    & $task 'BL-906' 'Done' 'Fix AF-0009: x' '2026-10-05'
    & $task 'BL-907' 'Done' 'Fix AF-0012: x' '2026-10-05'
    & $finding 'AF-0007' 'accepted' 'BL-901' 'Medium' 'quality'
    & $reaudit 'AF-0007' @('- 2026-10-07 | 2026-10-07_0844.md | reproduces: yes | the mutant survived again')
    & $finding 'AF-0008' 'accepted' 'BL-902' 'Medium' 'quality'
    & $reaudit 'AF-0008' @('- 2026-10-07 | 2026-10-07_0844.md | reproduces: yes | still fails')
    & $finding 'AF-0009' 'accepted' 'BL-906' 'Medium' 'quality'
    & $finding 'AF-0010' 'accepted' 'BL-904' 'Medium' 'quality'
    & $reaudit 'AF-0010' @('- 2026-10-07 | 2026-10-07_0844.md | reproduces: yes | before the fix')
    & $finding 'AF-0011' 'accepted' 'BL-905' 'Medium' 'quality'
    # A past run's cost (ADR-0422, amended 2026-10-14): Done, re-audited "yes" after, never reopens;
    # a new one gets a Fix task that says the first audit after it is Done closes the finding.
    & $finding 'AF-0012' 'accepted' 'BL-907' 'Low' 'process' 'process:logs:BL-800:cost-outlier'
    & $reaudit 'AF-0012' @('- 2026-10-07 | 2026-10-07_0844.md | reproduces: yes | BL-800 still cost 4 times the median')
    & $finding 'AF-0013' 'accepted' 'none' 'Low' 'process' 'process:logs:BL-801:cost-outlier'
    # Any other process finding measures a frozen log copy: its task measures the live logs (ADR-0761).
    & $finding 'AF-0014' 'accepted' 'none' 'Medium' 'process' 'process:logs:BL-802:requeues'
    & $reaudit 'AF-0011' @('- 2026-10-06 | 2026-10-06_0900.md | reproduces: yes | still fails', '- 2026-10-07 | 2026-10-07_0844.md | not re-audited | overlaps planted defect PD-303 in Curl.Cli.UnitLibrary/CommandLineOptionTable.cs, so the auditor''s verdict (reproduces yes) is set aside: 282 options')
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
        $tasks = @(Get-ChildItem (Join-Path $work 'Tasks\Backlog') -Filter 'BL-*.md' | Where-Object { [IO.File]::ReadAllText($_.FullName) -match '(?m)^title: Fix AF-0001' })
        Check 'exactly one task filed' ($tasks.Count -eq 1) "$($tasks.Count)"
        $refix = @(Get-ChildItem (Join-Path $work 'Tasks\Backlog') -Filter 'BL-*.md' | Where-Object { [IO.File]::ReadAllText($_.FullName) -match '(?m)^title: Re-fix ' })
        Check 'one Re-fix task, for the finding whose Done task a later re-audit found still reproducing' ($refix.Count -eq 1 -and [IO.File]::ReadAllText($refix[0].FullName) -match '(?m)^title: Re-fix AF-0007: Finding AF-0007\r?$') "$($refix.Count)"
        if ($refix.Count -eq 1) {
            $rt = [IO.File]::ReadAllText($refix[0].FullName)
            $rid = ($refix[0].Name -split '-')[0..1] -join '-'
            $af7 = [IO.File]::ReadAllText((Join-Path $work 'findings\AF-0007-x.md'))
            Check 'the Re-fix task carries the latest evidence and the earlier task' ($rt -match 'the mutant survived again' -and $rt -match 'BL-901' -and $rt -notmatch '<!--') $rid
            Check 'the finding''s task is the Re-fix task and tasks keeps the history' ($af7 -match "(?m)^task: $rid\r?$" -and $af7 -match "(?m)^tasks: BL-901, $rid\r?$") $rid
        }
        $af8 = [IO.File]::ReadAllText((Join-Path $work 'findings\AF-0008-x.md'))
        Check 'an open Re-fix task naming the finding files nothing, and joins its history' ($af8 -match '(?m)^tasks: BL-902, BL-903\r?$' -and $af8 -match '(?m)^task: BL-903\r?$') 'AF-0008'
        $af9 = [IO.File]::ReadAllText((Join-Path $work 'findings\AF-0009-x.md'))
        Check 'one Re-fix task covering two findings is in both histories, the open one current' ($af9 -match '(?m)^tasks: BL-903, BL-906\r?$' -and $af9 -match '(?m)^task: BL-903\r?$') 'AF-0009'
        Check 'a re-audit before the task was completed reopens nothing' ([IO.File]::ReadAllText((Join-Path $work 'findings\AF-0010-x.md')) -ceq $before['AF-0010-x.md']) 'AF-0010'
        Check 'a set-aside latest re-audit reopens nothing' ([IO.File]::ReadAllText((Join-Path $work 'findings\AF-0011-x.md')) -ceq $before['AF-0011-x.md']) 'AF-0011'
        Check 'a cost-outlier finding whose task is Done files no Re-fix task, whatever its re-audit says' ([IO.File]::ReadAllText((Join-Path $work 'findings\AF-0012-x.md')) -ceq $before['AF-0012-x.md'] -and -not @(Get-ChildItem (Join-Path $work 'Tasks\Backlog') -Filter 'BL-*.md' | Where-Object { [IO.File]::ReadAllText($_.FullName) -match 'AF-0012' }).Count) 'AF-0012'
        $costTask = @(Get-ChildItem (Join-Path $work 'Tasks\Backlog') -Filter 'BL-*.md' | Where-Object { [IO.File]::ReadAllText($_.FullName) -match '(?m)^title: Fix AF-0013' })
        $costText = if ($costTask.Count -eq 1) { [IO.File]::ReadAllText($costTask[0].FullName) } else { '' }
        Check 'a new cost-outlier finding gets one Fix task saying the first audit after it is Done closes the finding' ($costTask.Count -eq 1 -and $costText -match 'task-done-historical' -and $costText -match 'recorded as unavoidable' -and $costText -notmatch 'confirms the fix' -and $costText -notmatch '<!--') "$($costTask.Count)"
        Check 'a cost-outlier task stays lane-eligible' ($costText -notmatch '(?m)^lane: no') 'AF-0013'
        $liveTask = @(Get-ChildItem (Join-Path $work 'Tasks\Backlog') -Filter 'BL-*.md' | Where-Object { [IO.File]::ReadAllText($_.FullName) -match '(?m)^title: Fix AF-0014' })
        $liveText = if ($liveTask.Count -eq 1) { [IO.File]::ReadAllText($liveTask[0].FullName) } else { '' }
        Check 'a process finding''s task measures the live logs after the fix, not the frozen copy' ($liveTask.Count -eq 1 -and $liveText -match 'run over the live logs written after the fix landed' -and $liveText -match 'ADR-0761' -and $liveText -notmatch 'run from the repository root, gives the expected result' -and $liveText -match 'confirms the fix' -and $liveText -notmatch '<!--') "$($liveTask.Count)"
        Check 'a process finding''s task is interactive only' ($liveText -match '(?m)^lane: no\r?$') 'AF-0014'
        if ($tasks.Count -eq 1) {
            $t = [IO.File]::ReadAllText($tasks[0].FullName)
            $id = ($tasks[0].Name -split '-')[0..1] -join '-'
            Check 'priority High for a High finding, pipeline feature for quality' ($t -match '(?m)^priority: High?$' -and $t -match '(?m)^pipeline: feature?$') 'High, feature'
            $touchesLine = if ($t -match '(?m)^touches:[^\r\n]*') { $Matches[0] } else { '(none)' }
            Check 'touches the folder the location names' ($touchesLine -eq 'touches: [Curl.Cli.UnitTests]') $touchesLine
            Check 'no template comment left' ($t -notmatch '<!--') 'none'
            Check 'the finding names the task' ([IO.File]::ReadAllText((Join-Path $work 'findings\AF-0001-x.md')) -match "(?m)^task: $id\r?$") $id
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
