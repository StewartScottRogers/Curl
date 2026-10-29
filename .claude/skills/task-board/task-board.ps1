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
      next     The task /task-run should take next; "No task is ready." when no
               task is ready; or "No task can start yet: ..." when every ready task
               overlaps one in Doing or waits behind one that does. A ready
               task whose touches overlap a task in Doing is not offered, so lanes
               of the dark factory never work on the same files at once. A task
               whose front matter says 'lane: no', or whose touches name an audit
               path - Audit, Audit/..., .claude/agents/audit-*, Gap, Gap/...,
               .claude/agents/gap-*, or one of the guard files
               .claude/hooks/guard-audit-paths.ps1, .claude/settings.json,
               .github/workflows/ci.yml and .claude/skills/task-board/task-board.ps1,
               or a folder that holds any of them (.claude, .claude/skills, .github and
               the like) (Test-AuditPath) - is never offered either: it is interactive only,
               run by naming it (/task-run BL-###).
               Inside a dark factory shift (CURL_DARK_FACTORY_LANE set), 'new'
               refuses an audit-path task without -NoLane and 'move -To Doing'
               refuses an interactive-only task (ADR-0267, ADR-0433).
      capacity How many tasks the board could have running at once right now: the
               tasks in Doing, plus the ready tasks that could start beside them,
               picked in 'next' order so no two overlap in touches. One line,
               parseable with '^Capacity (\d+):'. The dark factory's -Lanes Auto
               caps its lane count with it. Every task in Doing counts, interactive-
               only ones included; of the ready tasks, only lane-eligible ones do.
      hotspots The paths that serialise the board: for every path in the touches of
               an unfinished, lane-eligible task (Backlog, Doing, Blocked; not 'lane:
               no', not an audit path, not a shared list file: the ratchet or a SharedListFiles.txt entry),
               the tasks whose touches cover it: name it, a folder that holds it, or
               '*'. A task naming only something beneath it does not count, so every
               task counted for a path overlaps every other one there. One line
               per path that -Min (default 5) or more tasks touch, most first, then
               by path: 'Hotspot <count> <path>: BL-a, BL-b, ...' with IDs ascending,
               or 'No hotspots.'. Always exits 0. RunDarkFactory.ps1's coordinator
               parses these lines; task-admin.ps1 check prints them as warnings.
      next-id  The next free task ID.
      new      Create a task in Backlog from TASK-TEMPLATE.md. -NoLane writes
               'lane: no' so no dark factory lane is offered it.
      move     Move a task to another state, appending a Log line. A move out of
               Backlog also whispers the Backlog's depth ("Backlog depth, 63.") a minute
               later, once per burst of moves, without waiting for it (BL-1182).
      archive  Move finished tasks completed -OlderThanDays (default 7) or more days ago
               into Done\<yyyy-MM-dd>\, one folder per day: a later archive the same
               day adds to it. A plain 'archive' leaves the last week's Done tasks in
               place; -OlderThanDays 0 moves them all. With -WhenDoneIsLong, move all
               of them, whatever -OlderThanDays says, but only once Done holds more
               than 20.
      dedupe   Renumber tasks that share an ID with another. A task file that exists
               at -Since (a git ref; the shared branch before this lane's work) keeps
               its ID; the others get the next free IDs, and the old ID is rewritten in
               every Markdown file changed since -Since. A live task that shares its ID
               with an archived one is always renumbered; archived tasks never are.
               Dark factory lanes run it after rebasing, before they push.

    Keep this file ASCII only: Windows PowerShell 5.1 reads a script without a
    byte-order mark in the system code page, so a non-ASCII literal would be
    corrupted when it runs.

    It runs alike under Windows PowerShell 5.1 and under pwsh on Windows, Linux and
    macOS (BL-3926); off Windows, start it with pwsh in place of powershell.

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File .claude/skills/task-board/task-board.ps1 status

.EXAMPLE
    pwsh -NoProfile -File .claude/skills/task-board/task-board.ps1 status

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
    [ValidateSet('status', 'next', 'capacity', 'hotspots', 'next-id', 'new', 'move', 'archive', 'dedupe')]
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
    [switch] $WhenDoneIsLong,

    # For 'hotspots': the fewest tasks a path must have touching it to be listed.
    [ValidateRange(1, 100000)]
    [int] $Min = 5
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
    # that touches an audit path is interactive only whatever it says (ADR-0267, ADR-0433).
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

# An audit path is outside the dark factory's reach: Audit or anything under it, an
# auditor agent .claude/agents/audit-* (ADR-0267), Gap or anything under it, a gap
# analyst agent .claude/agents/gap-* (ADR-0433), or one of the guard files that protect
# them, in any case, or a folder that holds one (.claude, .github and the like). Takes a
# path ConvertTo-TouchPath has normalised. * is not one: the hook and CI catch real
# writes, and refusing * would refuse every task filed without touches.
# The guard files are the ones Audit/Guard/Test-AuditPathsUntouched.ps1's
# $GuardedPatterns names, in its order: a lane that changes one turns CI's audit guard
# red (BL-1209). That script's -SelfTest fails when this list misses one of them, so
# the list stays inside this function, where the self-test reads it.
function Test-AuditPath([string] $TouchPath) {
    $guardFiles = @(
        '.claude/hooks/guard-audit-paths.ps1',
        '.claude/settings.json',
        '.github/workflows/ci.yml',
        '.claude/skills/task-board/task-board.ps1'
    )
    # A folder that holds a guard file or an auditor or analyst agent (".claude", ".github",
    # ".claude/skills" and the like) counts too: such a touch lets a lane reach them (BL-1876).
    $p = $TouchPath.Replace([string][char]92, '/').TrimEnd('/')
    $holders = @($guardFiles + '.claude/agents/audit-x' + '.claude/agents/gap-x' | Where-Object { $_.StartsWith("$p/", [StringComparison]::OrdinalIgnoreCase) })
    if ($p -and $holders.Count -gt 0) { return $true }
    return $TouchPath -ieq 'Audit' -or
        $TouchPath.StartsWith('Audit/', [StringComparison]::OrdinalIgnoreCase) -or
        $TouchPath -ilike '.claude/agents/audit-*' -or
        $TouchPath -ieq 'Gap' -or
        $TouchPath.StartsWith('Gap/', [StringComparison]::OrdinalIgnoreCase) -or
        $TouchPath -ilike '.claude/agents/gap-*' -or
        @($guardFiles | Where-Object { $_ -ieq $TouchPath }).Count -gt 0
}

# True while this process runs inside a dark factory shift (RunDarkFactory.ps1's LANE
# MARKER, BL-995).
function Test-DarkFactoryLane {
    return -not [string]::IsNullOrEmpty($env:CURL_DARK_FACTORY_LANE)
}

# Lists many tasks add lines to at once: the upstream case ratchet, plus every path in
# Tasks/SharedListFiles.txt (one repo-relative path per line, forward slashes, '*'
# matching any characters except '/'; blank and '#' lines ignored; a missing file adds
# nothing). Each is one entry per line, order-free and marked merge=union in
# .gitattributes, so two tasks that both edit one never conflict, and naming one in
# touches makes no overlap (ADR-0689).
$script:RatchetFile = 'Curl.Conformance.UnitTests/PassingUpstreamCases.txt'
$script:SharedListPatterns = $null

function Get-SharedListPatterns {
    if ($null -ne $script:SharedListPatterns) { return $script:SharedListPatterns }
    $entries = @($script:RatchetFile)
    $listFile = Join-Path $Board 'SharedListFiles.txt'
    if (Test-Path -LiteralPath $listFile) {
        foreach ($line in [IO.File]::ReadAllLines($listFile)) {
            $entry = $line.Trim()
            if ($entry -ne '' -and -not $entry.StartsWith('#')) { $entries += $entry }
        }
    }
    $script:SharedListPatterns = @($entries | ForEach-Object {
            $regex = '^' + ([regex]::Escape($_) -replace '\\\*', '[^/]*') + '$'
            [regex]::new($regex, [Text.RegularExpressions.RegexOptions]::IgnoreCase)
        })
    return $script:SharedListPatterns
}

# True when a touches path is a shared list file: the ratchet, or a SharedListFiles.txt
# entry, read as a wildcard where '*' matches anything but '/'.
function Test-SharedListFile([string] $Path) {
    foreach ($pattern in (Get-SharedListPatterns)) { if ($pattern.IsMatch($Path)) { return $true } }
    return $false
}

# Two tasks overlap when either may touch anything, or one names a path equal to or
# inside a path the other names. Curl.Core.UnitLibrary overlaps
# Curl.Core.UnitLibrary/Transfer.cs; it does not overlap Curl.Core.UnitTests. A shared
# list file overlaps nothing, not even the project folder it is in.
function Test-Overlap([string[]] $A, [string[]] $B) {
    if ($A -contains '*' -or $B -contains '*') { return $true }
    foreach ($x in $A) {
        if (Test-SharedListFile $x) { continue }
        foreach ($y in $B) {
            if (Test-SharedListFile $y) { continue }
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
    # Archived tasks that share an ID are history and never change, so only a pair with a live
    # task in it is warned about; 'dedupe' renumbers that live task (BL-2521).
    foreach ($duplicate in @($tasks | Group-Object Id | Where-Object { $_.Count -gt 1 -and @($_.Group | Where-Object { -not $_.Archived }).Count -gt 0 })) {
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

# How many whole folders a task's touches name: an entry with no '/' is a project folder,
# and '*' counts as more than any list. A shared list file counts for nothing. Of two
# otherwise equal ready tasks the narrower goes first, so tasks that name files in a
# busy project run side by side before one that holds the whole project (ADR-0694).
function Get-TouchBreadth($Task) {
    if ($Task.Touches -contains '*') { return [int]::MaxValue }
    return @($Task.Touches | Where-Object { -not (Test-SharedListFile $_) -and -not $_.Contains('/') }).Count
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
        @{ Expression = { $waiting[$_.Id] }; Descending = $true },
        @{ Expression = { Get-TouchBreadth $_ } }, Number)
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
# Only a wait on a lane-eligible Doing task reserves: lanes finish those, so holding
# work back lets them drain. An interactive-only task in Doing ends only when a session
# finishes it, which may be days away, so a task waiting on one reserves nothing and the
# lanes keep working beside it (AF-0220: a * task behind an interactive claim idled every
# lane for five hours).
function Get-StartOrder([object[]] $Ready, [object[]] $Tasks) {
    $reserved = @()
    foreach ($task in $Ready) {
        $busy = Get-Collision $task $Tasks
        if ($busy) {
            $laneBusy = @($Tasks | Where-Object { $_.State -eq 'Doing' -and $_.LaneAllowed -and (Test-Overlap $task.Touches $_.Touches) })
            if ($laneBusy.Count -gt 0) { $reserved += $task }
            [pscustomobject]@{ Task = $task; Start = $false; Behind = $busy; HeldBy = $null }
            continue
        }
        $holder = @($reserved | Where-Object { Test-Overlap $task.Touches $_.Touches }) | Select-Object -First 1
        [pscustomobject]@{ Task = $task; Start = (-not $holder); Behind = $null; HeldBy = $holder }
    }
}

# True when a task's touches cover a path: they name '*', the path itself, or a folder
# that holds it. A touch beneath the path does not cover it, so every task that covers
# a path overlaps every other task that covers it. A shared list file covers nothing.
function Test-TouchesCover([string[]] $Touches, [string] $Path) {
    foreach ($touch in $Touches) {
        if ($touch -eq '*') { return $true }
        if (Test-SharedListFile $touch) { continue }
        if ($Path -ieq $touch -or $Path.StartsWith("$touch/", [StringComparison]::OrdinalIgnoreCase)) { return $true }
    }
    return $false
}

# The paths at least $Minimum unfinished lane-eligible tasks cover (Test-TouchesCover),
# most first, as objects with Count, Path and Ids. Every path named in such a task's
# touches is a candidate, except '*' and a shared list file. Since the tasks counted for
# a path all overlap each other on it, each hotspot is a real queue of serialised work.
function Get-Hotspots([object[]] $Tasks, [int] $Minimum) {
    $open = @($Tasks | Where-Object { @('Backlog', 'Doing', 'Blocked') -contains $_.State -and $_.LaneAllowed })
    $paths = @{}
    foreach ($task in $open) {
        foreach ($path in $task.Touches) {
            if ($path -eq '*' -or (Test-SharedListFile $path)) { continue }
            if (-not $paths.ContainsKey($path.ToLowerInvariant())) { $paths[$path.ToLowerInvariant()] = $path }
        }
    }
    $hotspots = @(foreach ($path in $paths.Values) {
            $ids = @($open | Where-Object { Test-TouchesCover $_.Touches $path } | Sort-Object Number | ForEach-Object { $_.Id } | Select-Object -Unique)
            if ($ids.Count -ge $Minimum) { [pscustomobject]@{ Count = $ids.Count; Path = $path; Ids = $ids } }
        })
    return , @($hotspots | Sort-Object -Property @{ Expression = { $_.Count }; Descending = $true }, @{ Expression = { $_.Path.ToLowerInvariant() } })
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

function Start-BacklogDepthWhisper {
    # Tells Stewart the Backlog's depth ("Backlog depth, 63.") after a move out of Backlog
    # (BL-1182). The phrase is spoken by whisper-milestone.ps1, detached and hidden, a
    # minute later and only if no later move has restamped the shared file, so a burst
    # of moves across lanes gives one phrase with the latest depth, after two silent
    # seconds holding the queue so it is heard apart from any other phrase (BL-1186).
    # Only the real board speaks: a scratch, temporary or test board is not a checkout
    # of StewartScottRogers/Curl, and its depth is not one Stewart wants to hear. The
    # move never waits for it and never fails because of it; off Windows and in CI it
    # is silent.
    try {
        if ($env:CI -or [Environment]::OSVersion.Platform -ne 'Win32NT') { return }
        $origin = "$(& git -C $RepoRoot config --get remote.origin.url 2>$null)".Trim()
        if ($origin -notmatch '[/:]StewartScottRogers/Curl(\.git)?$') { return }
        $speaker = Join-Path $RepoRoot '.claude\hooks\whisper-milestone.ps1'
        if (-not (Test-Path -LiteralPath $speaker)) { return }
        $depth = @(Get-ChildItem -LiteralPath (Join-Path $Board 'Backlog') -Filter 'BL-*.md' -File).Count
        $stampFile = Join-Path ([IO.Path]::GetTempPath()) 'CurlBacklogDepthWhisper.txt'
        $stamp = [Guid]::NewGuid().ToString('N')
        [IO.File]::WriteAllText($stampFile, "$stamp|$depth", $Utf8NoBom)
        $arguments = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$speaker`"",
            '-Phrase', "`"Backlog depth, $depth.`"", '-DelaySeconds', '60', '-PauseSeconds', '2',
            '-StampFile', "`"$stampFile`"", '-Stamp', $stamp)
        Start-Process -FilePath (Get-Process -Id $PID).Path -ArgumentList $arguments -WindowStyle Hidden | Out-Null
    } catch { }
}

function Add-LogLine([string] $Text, [string] $Line) {
    if ($Text.Contains("`r`n")) { $newline = "`r`n" } else { $newline = "`n" }
    $body = $Text.TrimEnd()
    if (-not [regex]::IsMatch($body, '(?m)^## Log\s*$')) { $body += "$newline$newline## Log$newline" }
    return "$body$newline$Line$newline"
}

# The acceptance criteria as they read with every box unticked, one string per criterion
# (a "- [ ]" or "- [x]" line with its indented continuation lines), whitespace collapsed,
# so ticking a box or rewrapping a line changes nothing and rewording one does (BL-2488).
function Get-AcceptanceCriteria([string] $Text) {
    $section = [regex]::Match($Text, '(?ms)^## Acceptance criteria[ \t]*\r?$(.*?)(?=^## |\z)')
    if (-not $section.Success) { return @() }
    $criteria = New-Object System.Collections.Generic.List[string]
    foreach ($line in ($section.Groups[1].Value -split '\r?\n')) {
        if ($line -match '^\s*[-*] \[[ xX]\]\s*(.*)$') { $criteria.Add($Matches[1]); continue }
        if ($criteria.Count -gt 0 -and $line -match '^\s+\S') { $criteria[$criteria.Count - 1] += ' ' + $line.Trim() }
    }
    return @($criteria | ForEach-Object { ($_ -replace '\s+', ' ').Trim() })
}

# The first 8 hex digits of a criterion's SHA-256: short enough for a Log line, long
# enough that a reworded criterion never matches by chance.
function Get-CriterionFingerprint([string] $Criterion) {
    $sha = [Security.Cryptography.SHA256]::Create()
    try { $hash = $sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($Criterion)) } finally { $sha.Dispose() }
    return (-join ($hash[0..3] | ForEach-Object { $_.ToString('x2') }))
}

# The " Criteria at claim: <fingerprints>." text a claim adds to its Log line, or '' when
# the task has no acceptance criteria.
function Get-ClaimSnapshot([string] $Text) {
    $prints = @(Get-AcceptanceCriteria $Text | ForEach-Object { Get-CriterionFingerprint $_ })
    if ($prints.Count -eq 0) { return '' }
    return " Criteria at claim: $($prints -join ' ')."
}

# Throws, naming each criterion added or reworded and counting those removed, when the
# criteria differ from the latest claim's snapshot. A task claimed before snapshots
# existed has none and is not checked.
function Assert-CriteriaUnchangedSinceClaim($Task, [string] $Text) {
    $claims = [regex]::Matches($Text, '(?m)^\s*-\s.*-> Doing\..*Criteria at claim: ([0-9a-f ]+)\.')
    if ($claims.Count -eq 0) { return }
    $atClaim = @($claims[$claims.Count - 1].Groups[1].Value -split ' ' | Where-Object { $_ })
    $now = @(Get-AcceptanceCriteria $Text)
    $nowPrints = @($now | ForEach-Object { Get-CriterionFingerprint $_ })
    $changed = @(for ($i = 0; $i -lt $now.Count; $i++) { if ($atClaim -notcontains $nowPrints[$i]) { $now[$i] } })
    $removed = @($atClaim | Where-Object { $nowPrints -notcontains $_ }).Count
    if ($changed.Count -eq 0 -and $removed -eq 0) { return }
    $lines = @($changed | ForEach-Object { "  changed or added: $_" })
    if ($removed -gt 0) { $lines += "  removed: $removed criteria" }
    throw ("$($Task.Id)'s acceptance criteria differ from when it was claimed:`n" + ($lines -join "`n") +
        "`nA run meets its criteria as written. If one cannot be met, move the task to Blocked and say why; " +
        "to change criteria, move it to Backlog, edit them there, and claim it again.")
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

    'hotspots' {
        $hotspots = Get-Hotspots (Get-Tasks) $Min
        if ($hotspots.Count -eq 0) { Write-Output 'No hotspots.'; break }
        foreach ($hotspot in $hotspots) {
            Write-Output ('Hotspot {0} {1}: {2}' -f $hotspot.Count, $hotspot.Path, ($hotspot.Ids -join ', '))
        }
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
                throw "Dark factory lanes may not file a task that touches ${auditTouch}: the audit and gap analysis offices are outside the factory's reach (ADR-0267, ADR-0433). File it with -NoLane for an interactive session."
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
            Assert-CriteriaUnchangedSinceClaim $task $text
            $text = Set-FrontMatterField $text 'completed' $Today
        }

        $line = "- ${Today}: $from -> $To."
        if ($Reason) { $line += ' ' + $Reason.Trim() }
        if ($To -eq 'Doing') { $line += Get-ClaimSnapshot $text }
        $text = Add-LogLine $text $line

        $fileName = Split-Path $task.Path -Leaf
        $destination = Join-Path (Join-Path $Board $To) $fileName
        if (Test-Path -LiteralPath $destination) { throw "$destination already exists." }

        Write-Text $task.Path $text
        Move-Item -LiteralPath $task.Path -Destination $destination
        Write-Output "$($task.Id)  $from -> $To  Tasks\$To\$fileName"
        if ($from -eq 'Backlog') { Start-BacklogDepthWhisper }
    }

    'dedupe' {
        if (-not $Since) { throw 'Name the shared ref with -Since, for example -Since origin/work/dark-factory.' }
        # git reports "not in <ref>" on stderr, which is the answer here, not a failure;
        # under 'Stop' Windows PowerShell would turn it into a terminating error.
        $ErrorActionPreference = 'Continue'
        $tasks = Get-Tasks
        $changed = @(& git -C $RepoRoot diff --name-only "$Since" -- '*.md' 2>$null | Where-Object { $_ } |
            ForEach-Object { [IO.Path]::GetFullPath((Join-Path $RepoRoot $_)) } | Where-Object { Test-Path -LiteralPath $_ })
        $renamed = 0
        # A group counts when it holds a live task: another session can archive its Done copy of
        # an ID while this one files a new task with it. An archived task never changes, so
        # every live task beside an archived one is renumbered (BL-2521).
        foreach ($group in @($tasks | Group-Object Id | Where-Object { $_.Count -gt 1 -and @($_.Group | Where-Object { -not $_.Archived }).Count -gt 0 })) {
            $shared = @(); $mine = @()
            $archivedCopies = @($group.Group | Where-Object { $_.Archived })
            foreach ($task in @($group.Group | Where-Object { -not $_.Archived })) {
                if ($archivedCopies.Count -gt 0) { $mine += $task; continue }
                $relative = $task.Path.Substring($RepoRoot.Length).TrimStart('\', '/').Replace([string][char]92, '/')
                & git -C $RepoRoot cat-file -e "${Since}:$relative" 2>$null
                if ($LASTEXITCODE -eq 0) { $shared += $task } else { $mine += $task }
            }
            # With nothing shared (both new), the first keeps the ID.
            if ($shared.Count -eq 0 -and $archivedCopies.Count -eq 0) { $mine = @($mine | Select-Object -Skip 1) }
            $groupPaths = @($group.Group | ForEach-Object { [IO.Path]::GetFullPath($_.Path) })
            foreach ($task in $mine) {
                $old = $task.Id
                $new = Get-NextId (Get-Tasks)
                $text = Read-Text $task.Path
                $text = [regex]::Replace($text, "(?m)^id: $old\b", "id: $new")
                $text = [regex]::Replace($text, "(?m)^# $old\b", "# $new")
                Write-Text $task.Path $text
                $target = Join-Path (Split-Path $task.Path -Parent) ((Split-Path $task.Path -Leaf) -replace "^$old", $new)
                Move-Item -LiteralPath $task.Path -Destination $target
                # This lane's other changes that mention the old ID meant this task; the
                # group's other task files keep their own ID line.
                foreach ($file in $changed) {
                    if ($groupPaths -contains $file -or -not (Test-Path -LiteralPath $file)) { continue }
                    $body = Read-Text $file
                    $updated = [regex]::Replace($body, "\b$old\b", $new)
                    if ($updated -ne $body) { Write-Text $file $updated }
                }
                $changed = @($changed | ForEach-Object { if ($_ -eq [IO.Path]::GetFullPath($task.Path)) { [IO.Path]::GetFullPath($target) } else { $_ } })
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

        # One folder per day: a later archive the same day adds to it, so Done stays short.
        $stamp = (Get-Date).ToString('yyyy-MM-dd')
        $folder = Join-Path $DoneFolder $stamp
        New-Item -ItemType Directory -Path $folder -Force | Out-Null
        foreach ($task in $due) {
            Move-Item -LiteralPath $task.Path -Destination (Join-Path $folder (Split-Path $task.Path -Leaf))
        }
        $ids = ($due | Sort-Object Number | ForEach-Object { $_.Id }) -join ', '
        Write-Output ('Archived {0} task(s) to Tasks\Done\{1}: {2}' -f $due.Count, $stamp, $ids)
    }
}
