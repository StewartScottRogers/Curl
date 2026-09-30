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
      next     The task /task-run should take next, or "No task is ready.". A ready
               task whose touches overlap a task in Doing is not offered, so lanes
               of the dark factory never work on the same files at once. A task
               whose front matter says 'lane: no', or whose touches name an audit
               path (Audit, Audit/..., .claude/agents/audit-*), is never offered
               either: it is interactive only, run by naming it (/task-run BL-###).
               Inside a dark factory shift (CURL_DARK_FACTORY_LANE set), 'new'
               refuses an audit-path task without -NoLane and 'move -To Doing'
               refuses an interactive-only task (ADR-0267).
      capacity How many tasks the board could have running at once right now: the
               tasks in Doing, plus the ready tasks that could start beside them,
               picked in 'next' order so no two overlap in touches. One line,
               parseable with '^Capacity (\d+):'. The dark factory's -Lanes Auto
               caps its lane count with it. Interactive-only tasks do not count.
      next-id  The next free task ID.
      new      Create a task in Backlog from TASK-TEMPLATE.md. -NoLane writes
               'lane: no' so no dark factory lane is offered it.
      move     Move a task to another state, appending a Log line.
      archive  Move finished tasks into Done\<yyyy-MM-dd_HHmm>\. With -WhenDoneIsLong,
               move all of them, but only once Done holds more than 20.
      dedupe   Renumber tasks that share an ID with another. A task file that exists
               at -Since (a git ref; the shared branch before this lane's work) keeps
               its ID; the others get the next free IDs, and the old ID is rewritten in
               every Markdown file changed since -Since. Dark factory lanes run it
               after rebasing, before they push.

    Keep this file ASCII only: Windows PowerShell 5.1 reads a script without a
    byte-order mark in the system code page, so a non-ASCII literal would be
    corrupted when it runs.

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File .claude/skills/task-board/task-board.ps1 status

.EXAMPLE
    ... task-board.ps1 new -Title "Parse --max-time" -Pipeline feature -DependsOn BL-012,BL-013 -Touches Curl.Cli.UnitLibrary,Curl.Cli.UnitTests

.EXAMPLE
    ... task-board.ps1 move -Id BL-014 -To Blocked -Reason "Needs Stewart to approve an ADR on X."

.EXAMPLE
    ... task-board.ps1 archive -OlderThanDays 0
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [ValidateSet('status', 'next', 'capacity', 'next-id', 'new', 'move', 'archive', 'dedupe')]
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

    # Projects, folders or files the task will change; '*' or nothing means it may
    # change anything, so it never runs beside another task.
    [string[]] $Touches = @(),

    # For 'new': write 'lane: no', so 'next' and 'capacity' never offer the task to a
    # dark factory lane; an interactive session runs it by naming it.
    [switch] $NoLane,

    # Task IDs 'next' must not offer, e.g. ones a lane already tried this shift.
    [string[]] $Skip = @(),

    # For 'dedupe': the git ref whose task files keep their IDs.
    [string] $Since = '',

    [string] $Requirement = 'none',

    [ValidateRange(0, 3650)]
    [int] $OlderThanDays = 7,

    # For 'archive': archive all of Done, whatever its age, but only once Done holds
    # more than $DoneListLimit tasks; otherwise do nothing.
    [switch] $WhenDoneIsLong
)

$ErrorActionPreference = 'Stop'

$Utf8NoBom = New-Object System.Text.UTF8Encoding($false)
if ($env:CLAUDE_PROJECT_DIR) { $RepoRoot = $env:CLAUDE_PROJECT_DIR }
else { $RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path }
$Board = [IO.Path]::GetFullPath((Join-Path $RepoRoot 'Tasks'))
$DoneFolder = Join-Path $Board 'Done'
# The most finished tasks Done may hold before 'archive -WhenDoneIsLong' clears it:
# few enough that Stewart can read Done in 'status' at a glance.
$DoneListLimit = 20
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

    $touched = @()
    if ($fields['touches']) {
        $touched = @(($fields['touches'].Trim('[', ']', ' ') -split ',') |
            ForEach-Object { ConvertTo-TouchPath $_ } | Where-Object { $_ })
    }
    if ($touched.Count -eq 0) { $touched = @('*') }

    $taskPriority = [string]$fields['priority']
    if (-not $PriorityRank.ContainsKey($taskPriority)) { $taskPriority = 'Normal' }

    # 'lane: no' means interactive only; absent, empty or 'yes' means any runner. A task
    # that touches an audit path is interactive only whatever it says (ADR-0267).
    $laneAllowed = ([string]$fields['lane']).Trim() -ine 'no' -and
        @($touched | Where-Object { Test-AuditPath $_ }).Count -eq 0

    return [pscustomobject]@{
        Id        = $taskId
        Number    = [int]($taskId -replace '\D', '')
        Title     = [string]$fields['title']
        Priority  = $taskPriority
        Assignee  = [string]$fields['assignee']
        Pipeline  = [string]$fields['pipeline']
        DependsOn = $dependencies
        Touches   = $touched
        LaneAllowed = $laneAllowed
        Completed = [string]$fields['completed']
        State     = $state
        Archived  = ($state -eq 'Done') -and ($File.DirectoryName -ne $DoneFolder)
        Path      = $File.FullName
        Relative  = 'Tasks\' + $relative
    }
}

function ConvertTo-TouchPath([string] $Item) {
    return $Item.Trim().Trim('"', "'").Replace([string][char]92, '/').TrimEnd('/')
}

# An audit path is the audit office's, outside the dark factory's reach (ADR-0267):
# Audit or anything under it, or an auditor agent .claude/agents/audit-*, in any case.
# Takes a path ConvertTo-TouchPath has normalised. An ancestor such as .claude or * is
# not one: the hook and CI catch real writes, and refusing * would refuse every task
# filed without touches.
function Test-AuditPath([string] $TouchPath) {
    return $TouchPath -ieq 'Audit' -or
        $TouchPath.StartsWith('Audit/', [StringComparison]::OrdinalIgnoreCase) -or
        $TouchPath -ilike '.claude/agents/audit-*'
}

# True while this process runs inside a dark factory shift (RunDarkFactory.ps1's LANE
# MARKER, BL-995).
function Test-DarkFactoryLane {
    return -not [string]::IsNullOrEmpty($env:CURL_DARK_FACTORY_LANE)
}

# Two tasks overlap when either may touch anything, or one names a path equal to or
# inside a path the other names. Curl.Core.UnitLibrary overlaps
# Curl.Core.UnitLibrary/Transfer.cs; it does not overlap Curl.Core.UnitTests.
function Test-Overlap([string[]] $A, [string[]] $B) {
    if ($A -contains '*' -or $B -contains '*') { return $true }
    foreach ($x in $A) {
        foreach ($y in $B) {
            if ($x -ieq $y) { return $true }
            if ($x.StartsWith("$y/", [StringComparison]::OrdinalIgnoreCase)) { return $true }
            if ($y.StartsWith("$x/", [StringComparison]::OrdinalIgnoreCase)) { return $true }
        }
    }
    return $false
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
    if ($live.Count -gt 1) {
        # Acting on the first match would move the wrong task. Parallel lanes can file the
        # same ID; 'dedupe' renumbers the later ones.
        $where = ($live | ForEach-Object { $_.Relative }) -join ', '
        throw "$wanted names $($live.Count) tasks ($where). Renumber the duplicates with the 'dedupe' command first."
    }
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

# How many unfinished tasks wait on this one, directly or through others. Ready tasks
# with more waiting on them go first, so the work that unlocks the most parallel work
# lands first.
function Get-WaitingCounts([object[]] $Tasks) {
    $open = @($Tasks | Where-Object { $_.State -ne 'Done' })
    $dependents = @{}
    foreach ($task in $open) {
        foreach ($dependency in $task.DependsOn) {
            if (-not $dependents.ContainsKey($dependency)) { $dependents[$dependency] = @() }
            $dependents[$dependency] += $task.Id
        }
    }
    $counts = @{}
    foreach ($task in $open) {
        $seen = @{}
        $queue = New-Object System.Collections.Queue
        $queue.Enqueue($task.Id)
        while ($queue.Count -gt 0) {
            $current = $queue.Dequeue()
            foreach ($waiter in @($dependents[$current])) {
                if ($waiter -and -not $seen.ContainsKey($waiter)) { $seen[$waiter] = $true; $queue.Enqueue($waiter) }
            }
        }
        $counts[$task.Id] = $seen.Count
    }
    return $counts
}

function Get-ReadyTasks([object[]] $Tasks) {
    $doneIds = Get-DoneIds $Tasks
    $waiting = Get-WaitingCounts $Tasks
    $ready = @($Tasks | Where-Object {
            $_.State -eq 'Backlog' -and
            $_.Assignee -eq 'Claude' -and
            @(Get-MissingDependencies $_ $doneIds).Count -eq 0
        })
    return , @($ready | Sort-Object -Property @{ Expression = { $PriorityRank[$_.Priority] } },
        @{ Expression = { $waiting[$_.Id] }; Descending = $true }, Number)
}

# The ready tasks a dark factory lane may be offered: every ready task but the
# interactive-only ones ('lane: no'), in the same order.
function Get-LaneReadyTasks([object[]] $Tasks) {
    return , @((Get-ReadyTasks $Tasks) | Where-Object { $_.LaneAllowed })
}

# The Doing task a ready task would collide with, or $null if it can start now.
function Get-Collision($Task, [object[]] $Tasks) {
    foreach ($busy in @($Tasks | Where-Object { $_.State -eq 'Doing' })) {
        if (Test-Overlap $Task.Touches $busy.Touches) { return $busy }
    }
    return $null
}

# The ready tasks, in 'next' order, each marked with why it can or cannot start now.
# Walking the queue in order, a task whose touches overlap one in Doing is waiting, and
# reserves its touches: a lower task that overlaps a reservation is held back behind it,
# even when it overlaps nothing in Doing. Without that, smaller tasks on the same projects
# keep starting as each Doing task ends, and a large task is never free to start (BL-717,
# BL-1041). A task that touches * reserves everything, so it runs once Doing empties.
function Get-StartOrder([object[]] $Ready, [object[]] $Tasks) {
    $reserved = @()
    foreach ($task in $Ready) {
        $busy = Get-Collision $task $Tasks
        if ($busy) {
            $reserved += $task
            [pscustomobject]@{ Task = $task; Start = $false; Behind = $busy; HeldBy = $null }
            continue
        }
        $holder = @($reserved | Where-Object { Test-Overlap $task.Touches $_.Touches }) | Select-Object -First 1
        [pscustomobject]@{ Task = $task; Start = (-not $holder); Behind = $null; HeldBy = $holder }
    }
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
        $readyIds = @((Get-LaneReadyTasks $tasks) | ForEach-Object { $_.Id })
        $heldBy = @{}
        foreach ($entry in @(Get-StartOrder (Get-LaneReadyTasks $tasks) $tasks)) {
            if ($entry.HeldBy) { $heldBy[$entry.Task.Id] = $entry.HeldBy.Id }
        }

        foreach ($state in $States) {
            $inState = @($tasks | Where-Object { $_.State -eq $state -and -not $_.Archived } | Sort-Object Number)
            Write-Output ('{0} ({1})' -f $state, $inState.Count)
            foreach ($task in $inState) {
                $flag = ''
                if ($state -eq 'Backlog') {
                    $missing = @(Get-MissingDependencies $task $doneIds)
                    if ($task.Assignee -ne 'Claude') { $flag = "needs $($task.Assignee)" }
                    elseif ($missing.Count -gt 0) { $flag = 'waiting on ' + ($missing -join ', ') }
                    elseif (-not $task.LaneAllowed) { $flag = 'ready, interactive only' }
                    else {
                        $flag = 'ready, #' + ([array]::IndexOf($readyIds, $task.Id) + 1) + ' in queue'
                        $collision = Get-Collision $task $tasks
                        if ($collision) { $flag += ", overlaps $($collision.Id) in Doing" }
                        elseif ($heldBy.ContainsKey($task.Id)) { $flag += ", held back behind $($heldBy[$task.Id])" }
                    }
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
        $tasks = Get-Tasks
        $skipIds = @($Skip | ForEach-Object { $_ -split '[,\s]+' } | Where-Object { $_ } | ForEach-Object { $_.ToUpperInvariant() })
        $ready = @((Get-LaneReadyTasks $tasks) | Where-Object { $skipIds -notcontains $_.Id })
        if ($ready.Count -eq 0) { Write-Output 'No task is ready.'; break }
        $order = @(Get-StartOrder $ready $tasks)
        $free = @($order | Where-Object { $_.Start } | ForEach-Object { $_.Task })
        if ($free.Count -eq 0) {
            $first = $order[0]
            $held = @($order | Where-Object { $_.HeldBy }).Count
            $also = if ($held) { "; $held more held back behind the tasks waiting" } else { '' }
            Write-Output ('No task can start yet: every ready task overlaps one in Doing or waits behind one that does, e.g. {0} with {1}{2}.' -f $first.Task.Id, $first.Behind.Id, $also)
            break
        }
        $task = $free[0]
        Write-Output ('{0}  pipeline: {1}  {2}' -f $task.Id, $task.Pipeline, $task.Relative)
    }

    'capacity' {
        # Greedy in 'next' order: a ready task is picked when 'next' would let it start
        # (Get-StartOrder: it overlaps no Doing task and is not held back behind a waiting
        # one) and its touches overlap no task picked before it.
        $tasks = Get-Tasks
        $doing = @($tasks | Where-Object { $_.State -eq 'Doing' })
        $claimed = @()
        $picked = @()
        foreach ($entry in @(Get-StartOrder (Get-LaneReadyTasks $tasks) $tasks)) {
            if (-not $entry.Start) { continue }
            $overlaps = $false
            foreach ($touches in $claimed) {
                if (Test-Overlap $entry.Task.Touches $touches) { $overlaps = $true; break }
            }
            if ($overlaps) { continue }
            $picked += $entry.Task.Id
            $claimed += , $entry.Task.Touches
        }
        if ($picked.Count -eq 0) { $more = 'none more can start' }
        else { $more = '{0} more can start ({1})' -f $picked.Count, ($picked -join ', ') }
        Write-Output ('Capacity {0}: {1} in Doing, {2}.' -f ($doing.Count + $picked.Count), $doing.Count, $more)
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

        $touchList = @($Touches | ForEach-Object { $_ -split ',' } | ForEach-Object { ConvertTo-TouchPath $_ } | Where-Object { $_ })
        if ((Test-DarkFactoryLane) -and -not $NoLane) {
            $auditTouch = @($touchList | Where-Object { Test-AuditPath $_ }) | Select-Object -First 1
            if ($auditTouch) {
                throw "Dark factory lanes may not file a task that touches ${auditTouch}: the audit office is outside the factory's reach (ADR-0267). File it with -NoLane for an interactive session."
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
            Replace('{{TOUCHES}}', ($touchList -join ', ')).
            Replace('{{REQUIREMENT}}', $Requirement).
            Replace('{{DATE}}', $Today)
        if ($NoLane) { $text = [regex]::new('(?m)^(touches:[^\r\n]*)(\r?\n)').Replace($text, "`$1`$2lane: no`$2", 1) }
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
            if ((Test-DarkFactoryLane) -and -not $task.LaneAllowed) { throw "$($task.Id) is interactive only; a dark factory lane does not claim it." }
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

    'dedupe' {
        if (-not $Since) { throw 'Name the shared ref with -Since, for example -Since origin/work/dark-factory.' }
        # git reports "not in <ref>" on stderr, which is the answer here, not a failure;
        # under 'Stop' Windows PowerShell would turn it into a terminating error.
        $ErrorActionPreference = 'Continue'
        $tasks = Get-Tasks
        $changed = @(& git -C $RepoRoot diff --name-only "$Since" -- '*.md' 2>$null | Where-Object { $_ } |
            ForEach-Object { Join-Path $RepoRoot $_ } | Where-Object { Test-Path -LiteralPath $_ })
        $renamed = 0
        foreach ($group in @($tasks | Where-Object { -not $_.Archived } | Group-Object Id | Where-Object { $_.Count -gt 1 })) {
            $shared = @(); $mine = @()
            foreach ($task in $group.Group) {
                $relative = $task.Path.Substring($RepoRoot.Length).TrimStart('\', '/').Replace([string][char]92, '/')
                & git -C $RepoRoot cat-file -e "${Since}:$relative" 2>$null
                if ($LASTEXITCODE -eq 0) { $shared += $task } else { $mine += $task }
            }
            # With nothing shared (both new), the first keeps the ID.
            if ($shared.Count -eq 0) { $mine = @($mine | Select-Object -Skip 1) }
            foreach ($task in $mine) {
                $old = $task.Id
                $new = Get-NextId (Get-Tasks)
                $text = Read-Text $task.Path
                $text = [regex]::Replace($text, "(?m)^id: $old\b", "id: $new")
                $text = [regex]::Replace($text, "(?m)^# $old\b", "# $new")
                Write-Text $task.Path $text
                $target = Join-Path (Split-Path $task.Path -Parent) ((Split-Path $task.Path -Leaf) -replace "^$old", $new)
                Move-Item -LiteralPath $task.Path -Destination $target
                # This lane's other changes that mention the old ID meant this task.
                foreach ($file in $changed) {
                    if ($file -eq $task.Path -or -not (Test-Path -LiteralPath $file)) { continue }
                    $body = Read-Text $file
                    $updated = [regex]::Replace($body, "\b$old\b", $new)
                    if ($updated -ne $body) { Write-Text $file $updated }
                }
                $changed = @($changed | ForEach-Object { if ($_ -eq $task.Path) { $target } else { $_ } })
                Write-Output "$old -> $new  $($task.Title)"
                $renamed++
            }
        }
        if ($renamed -eq 0) { Write-Output 'No duplicate IDs.' }
    }

    'archive' {
        $tasks = Get-Tasks
        $finished = @($tasks | Where-Object { $_.State -eq 'Done' -and -not $_.Archived })
        if ($WhenDoneIsLong) {
            if ($finished.Count -le $DoneListLimit) {
                Write-Output ('Done holds {0} task(s); it is archived once it holds more than {1}.' -f $finished.Count, $DoneListLimit)
                break
            }
            $OlderThanDays = 0
        }
        $cutoff = (Get-Date).Date.AddDays(-$OlderThanDays)
        $due = @()

        foreach ($task in $finished) {
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
