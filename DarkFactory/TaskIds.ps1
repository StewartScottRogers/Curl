# Calls to the task board, task states and titles, reading task IDs from its output, the
# interactive-only hold, and -TestTaskIds. Dot-sourced by RunDarkFactory.ps1 in its order;
# see the loader there.

# ---------------------------------------------------------------------------- board

function Invoke-Board {
    param([string[]]$BoardArgs)
    $out = & (Get-FactoryPowerShellExe) -NoProfile -ExecutionPolicy Bypass -File $Board @BoardArgs 2>&1
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

# ---------------------------------------------------------------------------- claimable now

# One predicate decides whether a ready task can be claimed now: the claim loop, the shift's
# continue decision and -Lanes Auto's capacity all ask it, so a board whose only ready tasks
# are over their cost cap never starts an empty shift (BL-3090: 13 shifts on 2026-10-22).

function Test-TaskClaimableNow {
    # Whether the ready task -Id may be claimed now: it is not over its cost cap (-CapSpentWhy,
    # from Get-TaskCapSpentWhy, is '') and its task file text is not interactive only (lane: no).
    param([string]$Id, [string]$CapSpentWhy, [string]$TaskText)
    return [bool]($Id -and -not $CapSpentWhy -and $TaskText -notmatch '(?m)^lane: no\s*$')
}

function Get-TaskNotClaimableWhy {
    # Why the ready task -Id may not be claimed now ("cost cap: ..." or "interactive only
    # ..."), or '' when Test-TaskClaimableNow says it may. -TasksDir is the board's Tasks folder.
    param([string]$Id, [string]$TasksDir = (Join-Path $Root 'Tasks'))
    $file = Get-ChildItem (Join-Path $TasksDir 'Backlog') -Filter "$Id-*.md" -ErrorAction SilentlyContinue | Select-Object -First 1
    $text = if ($file) { Get-Content -Raw -LiteralPath $file.FullName } else { '' }
    $capWhy = Get-TaskCapSpentWhy $Id
    if (Test-TaskClaimableNow $Id $capWhy $text) { return '' }
    if ($capWhy) { return "cost cap: $capWhy" }
    return 'interactive only (lane: no)'
}

function Get-ClaimableTaskId {
    # The first task next offers that is claimable now, asking next again with each one that
    # is not in -Skip until it offers a claimable one or none. Id is '' when none is;
    # Capped lists the ready tasks passed over for their cost cap, Passed every one passed over.
    # -OfferNext (skip IDs -> next's text) and -NotClaimableWhy (ID -> why) default to the
    # board and Get-TaskNotClaimableWhy; -TestTaskIds injects both.
    param([string[]]$Skip = @(), [scriptblock]$OfferNext, [scriptblock]$NotClaimableWhy)
    if (-not $OfferNext) {
        $OfferNext = { param([string[]]$s) $a = @('next'); if ($s.Count) { $a += @('-Skip', ($s -join ',')) }; (Invoke-Board $a) -join "`n" }
    }
    if (-not $NotClaimableWhy) { $NotClaimableWhy = { param([string]$i) Get-TaskNotClaimableWhy $i } }
    $skipNow = @($Skip | Where-Object { "$_" -match '^BL-\d+$' })
    $passed = @(); $capped = @()
    while ($true) {
        $text = & $OfferNext $skipNow
        $id = Get-NextTaskId $text
        if (-not $id -or $skipNow -contains $id) { return [pscustomobject]@{ Id = ''; Text = $text; Passed = $passed; Capped = $capped } }
        $why = & $NotClaimableWhy $id
        if (-not $why) { return [pscustomobject]@{ Id = $id; Text = $text; Passed = $passed; Capped = $capped } }
        $passed += $id; $skipNow += $id
        if ($why -match '^cost cap') { $capped += $id }
    }
}

function Get-ShiftEndDecision {
    # Whether a -Continuous shift that has ended starts the next one: only when a task is
    # claimable now (-ClaimableId). With only cost-capped tasks ready it does not, and Trace
    # says when the first cap frees (-CapFreesAt, $null when unknown); BL-3092 polls for it.
    param([bool]$Continuous, [string]$ClaimableId, [string[]]$CappedIds = @(), $CapFreesAt = $null)
    $trace = ''
    if (-not $ClaimableId -and $CappedIds.Count) {
        $at = if ($CapFreesAt) { ([datetime]$CapFreesAt).ToString('HH:mm', [System.Globalization.CultureInfo]::InvariantCulture) } else { 'an unknown time' }
        $trace = "only cost-capped tasks ready ($($CappedIds -join ', ')); next cap frees at $at"
    }
    return [pscustomobject]@{ Continue = [bool]($Continuous -and $ClaimableId); Trace = $trace }
}

if ($TestTaskIds) {
    # A fake board: next offers the first of -Ready not in the skip list.
    $fakeBoard = {
        param([string[]]$Ready, [hashtable]$Why)
        $offer = { param([string[]]$s) $i = @($Ready | Where-Object { $s -notcontains $_ }) | Select-Object -First 1; if ($i) { "$i  pipeline: direct  Tasks\Backlog\$i-x.md" } else { 'No task is ready.' } }.GetNewClosure()
        $whyOf = { param([string]$i) if ($Why.ContainsKey($i)) { $Why[$i] } else { '' } }.GetNewClosure()
        Get-ClaimableTaskId -OfferNext $offer -NotClaimableWhy $whyOf
    }
    $capWhy = 'cost cap: its runs of the last day already cost 3.00 of its 3.50 US dollar cost cap'
    $uncapped = & $fakeBoard @('BL-1') @{}
    $cappedFirst = & $fakeBoard @('BL-1', 'BL-2') @{ 'BL-1' = $capWhy }
    $cappedOnly = & $fakeBoard @('BL-1', 'BL-2') @{ 'BL-1' = $capWhy; 'BL-2' = $capWhy }
    $cappedEnd = Get-ShiftEndDecision -Continuous $true -ClaimableId $cappedOnly.Id -CappedIds $cappedOnly.Capped -CapFreesAt ([datetime]'2026-10-22 07:15')
    $readyEnd = Get-ShiftEndDecision -Continuous $true -ClaimableId 'BL-2' -CappedIds 'BL-1'
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
        ,@('a wait reason with no warning', 'No task is ready.', (Get-WaitReason 'No task is ready.'))
        ,@('a ready uncapped task is claimable', 'True', "$(Test-TaskClaimableNow 'BL-1' '' "depends-on: []")")
        ,@('a ready capped task is not claimable', 'False', "$(Test-TaskClaimableNow 'BL-1' $capWhy "depends-on: []")")
        ,@('an interactive-only task is not claimable', 'False', "$(Test-TaskClaimableNow 'BL-1' '' "depends-on: []`nlane: no")")
        ,@('no offered task is not claimable', 'False', "$(Test-TaskClaimableNow '' '' '')")
        ,@('a ready uncapped task is offered', 'BL-1', $uncapped.Id)
        ,@('a capped task is passed over for the one behind it', 'BL-2;BL-1', "$($cappedFirst.Id);$($cappedFirst.Capped -join ',')")
        ,@('only capped tasks ready gives no claimable task', ';BL-1,BL-2', "$($cappedOnly.Id);$($cappedOnly.Capped -join ',')")
        ,@('an interactive-only task is passed over but not counted capped', ';BL-1;', "$((& $fakeBoard @('BL-1') @{ 'BL-1' = 'interactive only (lane: no)' }) | ForEach-Object { "$($_.Id);$($_.Passed -join ',');$($_.Capped -join ',')" })")
        ,@('only capped tasks ready starts no next shift', 'False;only cost-capped tasks ready (BL-1, BL-2); next cap frees at 07:15', "$($cappedEnd.Continue);$($cappedEnd.Trace)")
        ,@('a claimable task starts the next shift', 'True;', "$($readyEnd.Continue);$($readyEnd.Trace)")
        ,@('without -Continuous no next shift starts', 'False', "$((Get-ShiftEndDecision -Continuous $false -ClaimableId 'BL-2').Continue)")
        ,@('a cap with no known reset time', 'only cost-capped tasks ready (BL-1); next cap frees at an unknown time', (Get-ShiftEndDecision -Continuous $true -ClaimableId '' -CappedIds 'BL-1').Trace))
    $failed = 0
    foreach ($case in $cases) {
        if ($case[1] -ceq $case[2]) { Write-Host "PASS $($case[0]): $($case[2])" -ForegroundColor Green }
        else { Write-Host "FAIL $($case[0]): expected $($case[1]), got $($case[2])" -ForegroundColor Red; $failed++ }
    }
    exit $(if ($failed) { 1 } else { 0 })
}

# This file ran to its end; RunDarkFactory.ps1 goes on to the next one.
$AreaFileFinished = $true
