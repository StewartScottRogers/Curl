# Calls to the task board, task states and titles, reading task IDs from its output, the
# interactive-only hold, and -TestTaskIds. Dot-sourced by RunDarkFactory.ps1 in its order;
# see the loader there.

# ---------------------------------------------------------------------------- board

function Invoke-Board {
    param([string[]]$BoardArgs)
    $out = & powershell -NoProfile -ExecutionPolicy Bypass -File $Board @BoardArgs 2>&1
    return ($out | ForEach-Object { "$_" })
}

function Get-TaskState {
    param([string]$Id)
    foreach ($state in 'Doing', 'Blocked', 'Done', 'Backlog', 'Deferred') {
        if (Get-ChildItem (Join-Path $Root "Tasks\$state") -Filter "$Id-*.md" -ErrorAction SilentlyContinue) { return $state }
    }
    return 'Unknown'
}

function Get-TaskTitle {
    param([string]$Id)
    $file = Get-ChildItem (Join-Path $Root 'Tasks') -Recurse -Filter "$Id-*.md" | Select-Object -First 1
    if (-not $file) { return '' }
    $m = Select-String -Path $file.FullName -Pattern '^title:\s*(.+)$' | Select-Object -First 1
    if ($m) { return $m.Matches[0].Groups[1].Value.Trim('"', "'", ' ') }
    return ''
}

# Task IDs have three digits or more: task-board.ps1 numbers with 'BL-{0:D3}', so BL-999 is
# followed by BL-1000 (BL-992). Every reading of an ID goes through these four.

function Get-TaskIdFromFileName {
    # The ID a task file name starts with: BL-1003-x.md gives BL-1003.
    param([string]$Name)
    if ($Name -match '^(BL-\d+)') { return $Matches[1] }
    return ''
}

function Get-NextTaskId {
    # The ID that task-board.ps1 next printed at the start of a line, or '' when it offered none.
    param([string]$Text)
    if ($Text -match '(?m)^(BL-\d+)\s') { return $Matches[1] }
    return ''
}

function Get-WaitReason {
    # Why task-board.ps1 next offered nothing: its 'No task ...' line, never a WARNING it
    # printed first (a duplicate ID) or that warning's wrapped continuation lines (BL-1054).
    param([string]$Text)
    $reason = @($Text -split "`r?`n" | Where-Object { $_ -match '^No task ' }) | Select-Object -First 1
    if ($reason) { return $reason.Trim() }
    return @($Text -split "`r?`n" | Where-Object { $_ -notmatch '^WARNING:' -and $_ -notmatch '^\s*Tasks[\\/]' }) -join ' '
}

function Get-TaskIdsNamed {
    # Every distinct ID a Log line or -Reason names, in order.
    param([string]$Text)
    return @([regex]::Matches($Text, '\bBL-\d+\b') | ForEach-Object { $_.Value } | Select-Object -Unique)
}

function Get-NeedsStewartId {
    # The ID on a task-board.ps1 status line marked [needs Stewart], or ''.
    param([string]$Line)
    if ($Line -match '^\s+(BL-\d+)\s.*\[needs Stewart\]') { return $Matches[1] }
    return ''
}

function Get-InteractiveHoldNote {
    # When a lane waits with nothing ready: the Backlog tasks that only an interactive session
    # can run (lane: no) and the tasks queued behind them through depends-on, as one sentence
    # for the wait line, or '' when no Backlog task is held that way (AF-0189: three lanes sat
    # idle for 78 minutes behind interactive-only BL-2125 without the log saying so).
    param([string]$BacklogDir)
    $tasks = @{}
    foreach ($file in @(Get-ChildItem -LiteralPath $BacklogDir -Filter 'BL-*.md' -ErrorAction SilentlyContinue)) {
        $id = Get-TaskIdFromFileName $file.Name
        if (-not $id) { continue }
        $text = Get-Content -Raw -LiteralPath $file.FullName
        $deps = if ($text -match '(?m)^depends-on:\s*\[(.*)\]\s*$') { @(Get-TaskIdsNamed $Matches[1]) } else { @() }
        $tasks[$id] = @{ Interactive = ($text -match '(?m)^lane: no\s*$'); Deps = $deps }
    }
    $interactive = @($tasks.Keys | Where-Object { $tasks[$_].Interactive } | Sort-Object)
    if (-not $interactive.Count) { return '' }
    $held = @($interactive)
    do {
        $grew = $false
        foreach ($id in @($tasks.Keys | Sort-Object)) {
            if ($held -contains $id) { continue }
            if (@($tasks[$id].Deps | Where-Object { $held -contains $_ }).Count) { $held += $id; $grew = $true }
        }
    } while ($grew)
    $behind = @($held | Where-Object { $interactive -notcontains $_ })
    $note = "interactive only, waiting on Stewart or an interactive session: $($interactive -join ', ')"
    if ($behind.Count) { $note += "; queued behind them: $($behind -join ', ')" }
    return $note
}

if ($TestTaskIds) {
    $holdDir = Join-Path ([IO.Path]::GetTempPath()) "hold-$([guid]::NewGuid().ToString('N'))"
    New-Item -ItemType Directory -Path $holdDir | Out-Null
    Set-Content -LiteralPath (Join-Path $holdDir 'BL-1-a.md') -Value "depends-on: []`nlane: no"
    Set-Content -LiteralPath (Join-Path $holdDir 'BL-2-b.md') -Value 'depends-on: [BL-1]'
    Set-Content -LiteralPath (Join-Path $holdDir 'BL-3-c.md') -Value 'depends-on: [BL-2]'
    Set-Content -LiteralPath (Join-Path $holdDir 'BL-4-d.md') -Value 'depends-on: []'
    $holdNote = Get-InteractiveHoldNote $holdDir
    Remove-Item -LiteralPath $holdDir -Recurse -Force
    $cases = @(
        ,@('an interactive-only task and the chain behind it', 'interactive only, waiting on Stewart or an interactive session: BL-1; queued behind them: BL-2, BL-3', $holdNote)
        ,@('no Backlog directory holds nothing', '', (Get-InteractiveHoldNote (Join-Path ([IO.Path]::GetTempPath()) 'no-such-backlog-dir')))
        ,@('next offers a four-digit ID', 'BL-1003', (Get-NextTaskId "BL-1003  pipeline: direct  Tasks\Backlog\BL-1003-x.md"))
        ,@('next offers a three-digit ID', 'BL-992', (Get-NextTaskId 'BL-992  pipeline: direct  Tasks\Backlog\BL-992-x.md'))
        ,@('next offers nothing', '', (Get-NextTaskId 'No task is ready.'))
        ,@('a reason names two IDs', 'BL-1003,BL-999', ((Get-TaskIdsNamed 'Waiting on BL-1003 and BL-999') -join ','))
        ,@('a needs-Stewart status line', 'BL-1005', (Get-NeedsStewartId '  BL-1005 Normal Stewart Title  [needs Stewart]'))
        ,@('a file name with a four-digit ID', 'BL-1003', (Get-TaskIdFromFileName 'BL-1003-accept-four-digit-ids.md'))
        ,@('a wait reason behind a wrapped duplicate-ID warning', 'No task can start yet: every ready task overlaps one in Doing or waits behind one that does, e.g. BL-806 with BL-797.',
            (Get-WaitReason "WARNING: Duplicate task ID BL-806: `r`nTasks\Backlog\BL-806-write-curl-s-v-tls-lines.md, `r`nTasks\Done\2026-09-28_1849\BL-806-stop-lanes-auto.md`r`nNo task can start yet: every ready task overlaps one in Doing or waits behind one that does, e.g. BL-806 with BL-797."))
        ,@('a wait reason when nothing is ready, after a warning', 'No task is ready.', (Get-WaitReason "WARNING: Duplicate task ID BL-806: Tasks\Backlog\a.md, Tasks\Done\b.md`nNo task is ready."))
        ,@('a wait reason with no warning', 'No task is ready.', (Get-WaitReason 'No task is ready.')))
    $failed = 0
    foreach ($case in $cases) {
        if ($case[1] -ceq $case[2]) { Write-Host "PASS $($case[0]): $($case[2])" -ForegroundColor Green }
        else { Write-Host "FAIL $($case[0]): expected $($case[1]), got $($case[2])" -ForegroundColor Red; $failed++ }
    }
    exit $(if ($failed) { 1 } else { 0 })
}

# This file ran to its end; RunDarkFactory.ps1 goes on to the next one.
$AreaFileFinished = $true
