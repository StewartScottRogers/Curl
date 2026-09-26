<#
.SYNOPSIS
    Operates the Curl task board: the Tasks shared project, where each task is one
    Markdown file and the folder it sits in is its status.

.DESCRIPTION
    Every agent numbers, moves and archives tasks through this script so those
    steps are identical no matter who performs them. Transitions are validated,
    the Log line and completion date are written, and task IDs are never counted
    by hand.

    Commands:
      status   The whole board, with each Backlog task marked ready, waiting on
               its dependencies, or needing Stewart.
      next     The task /task-run should take next, or "No task is ready."
      next-id  The next free task ID.
      new      Create a task in Backlog from TASK-TEMPLATE.md.
      move     Move a task to another state, appending a Log line.
      archive  Move finished tasks into Done\<yyyy-MM-dd_HHmm>\.

    Keep this file ASCII only: Windows PowerShell 5.1 reads a script without a
    byte-order mark in the system code page, so a non-ASCII literal would be
    corrupted when it runs.

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File .claude/skills/task-board/task-board.ps1 status

.EXAMPLE
    ... task-board.ps1 new -Title "Parse --max-time" -Pipeline feature -DependsOn BL-012,BL-013

.EXAMPLE
    ... task-board.ps1 move -Id BL-014 -To Blocked -Reason "Needs Stewart to approve an ADR on X."

.EXAMPLE
    ... task-board.ps1 archive -OlderThanDays 0
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [ValidateSet('status', 'next', 'next-id', 'new', 'move', 'archive')]
    [string] $Command,

    [string] $Id,

    [ValidateSet('Backlog', 'Doing', 'Blocked', 'Deferred', 'Done')]
    [string] $To,

    [string] $Reason,

    [string] $Title,

    [ValidateSet('High', 'Normal', 'Low')]
    [string] $Priority = 'Normal',

    [ValidateSet('Claude', 'Stewart')]
    [string] $Assignee = 'Claude',

    [ValidateSet('feature', 'protocol', 'docs', 'direct')]
    [string] $Pipeline = 'feature',

    [string[]] $DependsOn = @(),

    [string] $Requirement = 'none',

    [ValidateRange(0, 3650)]
    [int] $OlderThanDays = 7
)

$ErrorActionPreference = 'Stop'

$Utf8NoBom = New-Object System.Text.UTF8Encoding($false)
if ($env:CLAUDE_PROJECT_DIR) { $RepoRoot = $env:CLAUDE_PROJECT_DIR }
else { $RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path }
$Board = [IO.Path]::GetFullPath((Join-Path $RepoRoot 'Tasks'))
$DoneFolder = Join-Path $Board 'Done'
$Template = Join-Path $PSScriptRoot 'TASK-TEMPLATE.md'
$States = @('Backlog', 'Doing', 'Blocked', 'Deferred', 'Done')
$Today = (Get-Date).ToString('yyyy-MM-dd')
$PriorityRank = @{ 'High' = 0; 'Normal' = 1; 'Low' = 2 }
$AllowedMoves = @{
    'Backlog'  = @('Doing', 'Blocked', 'Deferred')
    'Doing'    = @('Done', 'Blocked', 'Deferred', 'Backlog')
    'Blocked'  = @('Backlog', 'Doing', 'Deferred')
    'Deferred' = @('Backlog')
    'Done'     = @()
}

if (-not (Test-Path -LiteralPath $Board)) { throw "Task board not found at $Board." }

function Read-Text([string] $Path) { return [IO.File]::ReadAllText($Path, $Utf8NoBom) }

function Write-Text([string] $Path, [string] $Text) { [IO.File]::WriteAllText($Path, $Text, $Utf8NoBom) }

function ConvertTo-Task([IO.FileInfo] $File) {
    $text = Read-Text $File.FullName
    $fields = @{}
    $header = [regex]::Match($text, '(?s)\A\s*---\r?\n(.*?)\r?\n---')
    if ($header.Success) {
        foreach ($line in ($header.Groups[1].Value -split '\r?\n')) {
            $pair = [regex]::Match($line, '^([A-Za-z][A-Za-z-]*):\s*(.*)$')
            if ($pair.Success) { $fields[$pair.Groups[1].Value] = $pair.Groups[2].Value.Trim() }
        }
    }

    $relative = $File.FullName.Substring($Board.Length).TrimStart('\', '/')
    $state = ($relative -split '[\\/]')[0]

    $taskId = [string]$fields['id']
    if (-not $taskId) { $taskId = [regex]::Match($File.Name, '^BL-\d+').Value }

    $dependencies = @()
    if ($fields['depends-on']) {
        $dependencies = @([regex]::Matches($fields['depends-on'], 'BL-\d+') | ForEach-Object { $_.Value })
    }

    $taskPriority = [string]$fields['priority']
    if (-not $PriorityRank.ContainsKey($taskPriority)) { $taskPriority = 'Normal' }

    return [pscustomobject]@{
        Id        = $taskId
        Number    = [int]($taskId -replace '\D', '')
        Title     = [string]$fields['title']
        Priority  = $taskPriority
        Assignee  = [string]$fields['assignee']
        Pipeline  = [string]$fields['pipeline']
        DependsOn = $dependencies
        Completed = [string]$fields['completed']
        State     = $state
        Archived  = ($state -eq 'Done') -and ($File.DirectoryName -ne $DoneFolder)
        Path      = $File.FullName
        Relative  = 'Tasks\' + $relative
    }
}

function Get-Tasks {
    $files = @(Get-ChildItem -Path $Board -Recurse -File -Filter 'BL-*.md')
    $tasks = @($files | ForEach-Object { ConvertTo-Task $_ } | Where-Object { $States -contains $_.State })
    foreach ($duplicate in @($tasks | Group-Object Id | Where-Object { $_.Count -gt 1 })) {
        $where = ($duplicate.Group | ForEach-Object { $_.Relative }) -join ', '
        Write-Warning "Duplicate task ID $($duplicate.Name): $where"
    }
    return , $tasks
}

function Find-Task([object[]] $Tasks, [string] $TaskId) {
    if (-not $TaskId) { throw 'Name the task with -Id, for example -Id BL-007.' }
    $wanted = $TaskId.Trim().ToUpperInvariant()
    $live = @($Tasks | Where-Object { $_.Id -eq $wanted -and -not $_.Archived })
    if ($live.Count -gt 0) { return $live[0] }
    if (@($Tasks | Where-Object { $_.Id -eq $wanted }).Count -gt 0) {
        throw "$wanted is archived. Archived tasks are never changed; file a new task instead."
    }
    throw "There is no task $wanted on the board."
}

function Get-DoneIds([object[]] $Tasks) {
    return @($Tasks | Where-Object { $_.State -eq 'Done' } | ForEach-Object { $_.Id })
}

function Get-MissingDependencies($Task, [object[]] $DoneIds) {
    return @($Task.DependsOn | Where-Object { $DoneIds -notcontains $_ })
}

function Get-ReadyTasks([object[]] $Tasks) {
    $doneIds = Get-DoneIds $Tasks
    $ready = @($Tasks | Where-Object {
            $_.State -eq 'Backlog' -and
            $_.Assignee -eq 'Claude' -and
            @(Get-MissingDependencies $_ $doneIds).Count -eq 0
        })
    return , @($ready | Sort-Object -Property @{ Expression = { $PriorityRank[$_.Priority] } }, Number)
}

function Get-NextId([object[]] $Tasks) {
    $highest = -1
    foreach ($task in $Tasks) { if ($task.Number -gt $highest) { $highest = $task.Number } }
    return 'BL-{0:D3}' -f ($highest + 1)
}

function Set-FrontMatterField([string] $Text, [string] $Name, [string] $Value) {
    $pattern = [regex]::new("(?m)^$([regex]::Escape($Name)):[^\r\n]*")
    if ($pattern.IsMatch($Text)) { return $pattern.Replace($Text, "${Name}: $Value", 1) }
    $closing = [regex]::new('(?s)\A(\s*---\r?\n.*?)(\r?\n---)')
    return $closing.Replace($Text, "`$1`n${Name}: $Value`$2", 1)
}

function Add-LogLine([string] $Text, [string] $Line) {
    if ($Text.Contains("`r`n")) { $newline = "`r`n" } else { $newline = "`n" }
    $body = $Text.TrimEnd()
    if (-not [regex]::IsMatch($body, '(?m)^## Log\s*$')) { $body += "$newline$newline## Log$newline" }
    return "$body$newline$Line$newline"
}

switch ($Command) {

    'status' {
        $tasks = Get-Tasks
        $doneIds = Get-DoneIds $tasks
        $readyIds = @((Get-ReadyTasks $tasks) | ForEach-Object { $_.Id })

        foreach ($state in $States) {
            $inState = @($tasks | Where-Object { $_.State -eq $state -and -not $_.Archived } | Sort-Object Number)
            Write-Output ('{0} ({1})' -f $state, $inState.Count)
            foreach ($task in $inState) {
                $flag = ''
                if ($state -eq 'Backlog') {
                    $missing = @(Get-MissingDependencies $task $doneIds)
                    if ($task.Assignee -ne 'Claude') { $flag = "needs $($task.Assignee)" }
                    elseif ($missing.Count -gt 0) { $flag = 'waiting on ' + ($missing -join ', ') }
                    else { $flag = 'ready, #' + ([array]::IndexOf($readyIds, $task.Id) + 1) + ' in queue' }
                }
                elseif ($state -eq 'Done' -and $task.Completed) { $flag = "completed $($task.Completed)" }

                $line = '  {0,-7} {1,-6} {2,-7} {3}' -f $task.Id, $task.Priority, $task.Assignee, $task.Title
                if ($flag) { $line += "  [$flag]" }
                Write-Output $line
            }
        }

        $archived = @($tasks | Where-Object { $_.Archived })
        $folders = @($archived | ForEach-Object { Split-Path (Split-Path $_.Path -Parent) -Leaf } | Sort-Object -Unique)
        Write-Output ('Done archive: {0} task(s) in {1} folder(s)' -f $archived.Count, $folders.Count)
        Write-Output ('Next free ID: {0}' -f (Get-NextId $tasks))
    }

    'next' {
        $ready = Get-ReadyTasks (Get-Tasks)
        if ($ready.Count -eq 0) { Write-Output 'No task is ready.'; break }
        $task = $ready[0]
        Write-Output ('{0}  pipeline: {1}  {2}' -f $task.Id, $task.Pipeline, $task.Relative)
    }

    'next-id' {
        Write-Output (Get-NextId (Get-Tasks))
    }

    'new' {
        if (-not $Title) { throw 'Give the task a title with -Title.' }
        $tasks = Get-Tasks
        $newId = Get-NextId $tasks

        # powershell -File passes "BL-001,BL-002" as one string, so split it here.
        $dependencies = @($DependsOn | ForEach-Object { $_ -split '[,\s]+' } | Where-Object { $_ } |
            ForEach-Object { $_.ToUpperInvariant() })
        foreach ($dependency in $dependencies) {
            if ($dependency -notmatch '^BL-\d+$') { throw "'$dependency' is not a task ID." }
            if (@($tasks | Where-Object { $_.Id -eq $dependency }).Count -eq 0) {
                throw "Dependency $dependency is not on the board."
            }
        }

        $slug = ($Title.ToLowerInvariant() -replace '[^a-z0-9]+', '-').Trim('-')
        if ($slug.Length -gt 60) { $slug = $slug.Substring(0, 60).TrimEnd('-') }
        $fileName = "$newId-$slug.md"
        $path = Join-Path (Join-Path $Board 'Backlog') $fileName
        if (Test-Path -LiteralPath $path) { throw "$path already exists." }

        $text = (Read-Text $Template).
            Replace('{{ID}}', $newId).
            Replace('{{TITLE}}', $Title.Trim()).
            Replace('{{PRIORITY}}', $Priority).
            Replace('{{ASSIGNEE}}', $Assignee).
            Replace('{{PIPELINE}}', $Pipeline).
            Replace('{{DEPENDS}}', ($dependencies -join ', ')).
            Replace('{{REQUIREMENT}}', $Requirement).
            Replace('{{DATE}}', $Today)
        Write-Text $path $text
        Write-Output "$newId  Tasks\Backlog\$fileName"
    }

    'move' {
        if (-not $To) { throw 'Give the destination state with -To.' }
        $tasks = Get-Tasks
        $task = Find-Task $tasks $Id
        $from = $task.State

        if ($from -eq $To) { throw "$($task.Id) is already in $To." }
        if ($AllowedMoves[$from] -notcontains $To) {
            $allowed = $AllowedMoves[$from] -join ', '
            if (-not $allowed) { $allowed = 'nothing; Done is final' }
            throw "$($task.Id) cannot move from $from to $To. From $from it may move to: $allowed."
        }
        if ($To -ne 'Doing' -and -not $Reason) {
            throw "Moving to $To needs -Reason: a completion note for Done, the blocker and who can clear it for Blocked, why and when to revisit for Deferred."
        }
        if ($To -eq 'Doing') {
            if ($task.Assignee -ne 'Claude') { throw "$($task.Id) is assigned to $($task.Assignee); Claude does not claim it." }
            $missing = @(Get-MissingDependencies $task (Get-DoneIds $tasks))
            if ($missing.Count -gt 0) { throw "$($task.Id) is waiting on $($missing -join ', ')." }
        }

        $text = Read-Text $task.Path
        if ($To -eq 'Done') {
            if ([regex]::IsMatch($text, '(?m)^\s*[-*] \[ \]')) {
                throw "$($task.Id) still has unticked acceptance criteria. Tick them, or move it to Blocked and say why."
            }
            $text = Set-FrontMatterField $text 'completed' $Today
        }

        $line = "- ${Today}: $from -> $To."
        if ($Reason) { $line += ' ' + $Reason.Trim() }
        $text = Add-LogLine $text $line

        $fileName = Split-Path $task.Path -Leaf
        $destination = Join-Path (Join-Path $Board $To) $fileName
        if (Test-Path -LiteralPath $destination) { throw "$destination already exists." }

        Write-Text $task.Path $text
        Move-Item -LiteralPath $task.Path -Destination $destination
        Write-Output "$($task.Id)  $from -> $To  Tasks\$To\$fileName"
    }

    'archive' {
        $tasks = Get-Tasks
        $cutoff = (Get-Date).Date.AddDays(-$OlderThanDays)
        $due = @()

        foreach ($task in @($tasks | Where-Object { $_.State -eq 'Done' -and -not $_.Archived })) {
            $completed = [datetime]::MinValue
            $parsed = [datetime]::TryParseExact($task.Completed, 'yyyy-MM-dd',
                [Globalization.CultureInfo]::InvariantCulture, [Globalization.DateTimeStyles]::None, [ref]$completed)
            if (-not $parsed) {
                $completed = (Get-Item -LiteralPath $task.Path).LastWriteTime.Date
                Write-Warning ("{0} has no completed date; using its file date, {1}." -f $task.Id, $completed.ToString('yyyy-MM-dd'))
            }
            if ($completed -le $cutoff) { $due += $task }
        }

        if ($due.Count -eq 0) {
            Write-Output ('Nothing in Done was completed on or before {0}.' -f $cutoff.ToString('yyyy-MM-dd'))
            break
        }

        $stamp = (Get-Date).ToString('yyyy-MM-dd_HHmm')
        $folder = Join-Path $DoneFolder $stamp
        New-Item -ItemType Directory -Path $folder -Force | Out-Null
        foreach ($task in $due) {
            Move-Item -LiteralPath $task.Path -Destination (Join-Path $folder (Split-Path $task.Path -Leaf))
        }
        $ids = ($due | Sort-Object Number | ForEach-Object { $_.Id }) -join ', '
        Write-Output ('Archived {0} task(s) to Tasks\Done\{1}: {2}' -f $due.Count, $stamp, $ids)
    }
}
