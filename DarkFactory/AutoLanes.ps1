# The pure logic behind -Lanes Auto: burn rate, pace targets, the next lane count, the
# unpaced ramp, the split-hotspot choice, and -TestAutoLanes. Dot-sourced by
# RunDarkFactory.ps1 in its order; see the loader there.

# ---- auto lanes
# The pure logic behind -Lanes Auto (ADR-0130 items 2 to 5 and 9): no file, git, board,
# Claude or clock inside, so -TestAutoLanes proves it on recorded readings.

function New-UsageSample {
    # One burn-rate sample: a usage reading taken at -At while -ActiveLanes lanes held a
    # task and were not waiting for tokens.
    param([datetime]$At, $Reading, [double]$ActiveLanes)
    return [pscustomobject]@{
        At = $At; FiveHour = [double]$Reading.FiveHour; FiveHourResets = [datetime]$Reading.FiveHourResets
        Week = [double]$Reading.Week; WeekResets = [datetime]$Reading.WeekResets; ActiveLanes = $ActiveLanes
    }
}

function Get-BurnRate {
    # Percentage points per hour per active lane that -Window rose by, over the span from
    # the oldest sample inside the window sharing the newest sample's reset time to the
    # newest sample. $null when the span is too short or the lanes were idle (mean < 0.5).
    # Usage comes in whole points, so a span must hold enough of them that one point of
    # rounding does not swing the rate: 30 to 60 minutes for the 5-hour window (BL-808).
    param([object[]]$Samples, [ValidateSet('FiveHour', 'Week')][string]$Window)
    $windowMinutes = if ($Window -eq 'FiveHour') { 60 } else { 180 }
    $minimumMinutes = if ($Window -eq 'FiveHour') { 30 } else { 60 }
    $resetsName = "$($Window)Resets"
    $sorted = @($Samples | Where-Object { $_ } | Sort-Object At)
    if ($sorted.Count -lt 2) { return $null }
    $newest = $sorted[-1]
    $from = $newest.At.AddMinutes(-$windowMinutes)
    $span = @($sorted | Where-Object { $_.At -ge $from -and $_.$resetsName -eq $newest.$resetsName })
    $hours = ($newest.At - $span[0].At).TotalHours
    if ($hours * 60 -lt $minimumMinutes) { return $null }
    $meanLanes = ($span | Measure-Object ActiveLanes -Average).Average
    if ($meanLanes -lt 0.5) { return $null }
    $rise = [math]::Max(0.0, ($newest.$Window - $span[0].$Window) * 100)
    return $rise / $hours / $meanLanes
}

function Get-WindowTarget {
    # Lanes that spend what is left of one window up to -Stop just as it resets:
    # infinity for no rate or a reset already due, 0 for a budget already spent.
    param([double]$Used, [datetime]$Resets, [datetime]$At, $Rate, [double]$Stop)
    $hours = ($Resets - $At).TotalHours
    if ($null -eq $Rate -or $Rate -le 0 -or $hours -le 0) { return [double]::PositiveInfinity }
    $budget = [math]::Max(0.0, ($Stop - $Used) * 100)
    return $budget / ($Rate * $hours)
}

function Get-PaceTarget {
    # The 5-hour and weekly pace targets, in lanes, for -Sample; the lower is the Target
    # and names the Binding pace. $null while there is no 5-hour rate yet.
    param($Sample, $FiveHourRate, $WeeklyRate, [bool]$WeeklyPace = $false, [double]$StopAtUsage, [double]$StopAtWeeklyUsage)
    if ($null -eq $FiveHourRate) { return $null }
    $fiveHour = Get-WindowTarget -Used $Sample.FiveHour -Resets $Sample.FiveHourResets -At $Sample.At -Rate $FiveHourRate -Stop $StopAtUsage
    $weekly = if ($WeeklyPace) {
        Get-WindowTarget -Used $Sample.Week -Resets $Sample.WeekResets -At $Sample.At -Rate $WeeklyRate -Stop $StopAtWeeklyUsage
    } else { [double]::PositiveInfinity }
    $binding = if ($weekly -lt $fiveHour) { 'weekly pace' } else { '5-hour pace' }
    return [pscustomobject]@{ FiveHour = $fiveHour; Weekly = $weekly; Target = [math]::Min($fiveHour, $weekly); Binding = $binding }
}

function Get-NextLaneCount {
    # One step from -Current lanes toward the pace, capped by the ceilings: below the
    # unpaced floor, min(-Unpaced, ceiling), straight up to that floor (2026-10-21); at or
    # above it, up one when the desired count is a whole lane above, so the meter samples
    # each count; and when it is more than a quarter lane below, straight down to
    # max(1, floor(desired + 0.25)) (BL-823). A measured pace must be low two steps running
    # before lanes retire (-PreviousLow, BL-808); a ceiling below -Current retires at once.
    # Up to -Unpaced lanes (capped by the ceilings) are wanted whatever the pace says, so the
    # pace neither holds them back nor retires below them. Low says whether this step's
    # pace was low. Reason is the log line, e.g. "lanes 3 -> 4 (5-hour pace allows 4.9)".
    param([int]$Current, $Pace, [int]$Capacity, [int]$MachineCap, [int]$MaxLanes, [bool]$PreviousLow = $false, [int]$Unpaced = $UnpacedLanes)
    $ceiling = [math]::Min($Capacity, [math]::Min($MachineCap, $MaxLanes))
    $unpacedFloor = [math]::Min($Unpaced, $ceiling)
    $paceValue = if ($Pace) { [double]$Pace.Target } else { [double]$Current }
    $desired = [math]::Max([math]::Min($paceValue, [double]$ceiling), [double]$unpacedFloor)
    $lanes = $Current
    $low = $false
    $lowOnce = $false
    $jumped = $false
    if ($Current -lt $unpacedFloor) { $lanes = $unpacedFloor; $jumped = $true }
    elseif ($desired -ge $Current + 1) { $lanes = $Current + 1 }
    elseif ($desired -lt $Current - 0.25) {
        $low = $paceValue -le $ceiling
        if (-not $low -or $PreviousLow) { $lanes = [int][math]::Max(1, [math]::Floor($desired + 0.25)) } else { $lowOnce = $true }
    }
    $culture = [Globalization.CultureInfo]::InvariantCulture
    # The floor binds when the pace wants fewer lanes than it; a ceiling below -Unpaced is the
    # ceiling binding, and an -Unpaced of 1 is the max(1, ...) floor the pace always had.
    # A jump straight to the floor is the floor binding too, whatever the pace allows above it.
    $floorBinds = $paceValue -lt $unpacedFloor -and $Unpaced -gt 1
    if (($floorBinds -or ($jumped -and $Unpaced -gt 1)) -and $Unpaced -le $ceiling) {
        $binding = 'unpaced'
        $limit = "up to $Unpaced lanes run unpaced"
    } elseif ($paceValue -le $ceiling -and -not $floorBinds) {
        $binding = if ($Pace) { $Pace.Binding } else { 'no burn rate' }
        $limit = if ($Pace) { "$($Pace.Binding) allows $($paceValue.ToString('0.0', $culture))" } else { 'no burn rate yet' }
    } elseif ($Capacity -eq $ceiling) {
        $binding = 'capacity'
        $limit = if ($Capacity -eq 1) { '1 ready task can run at once' } else { "$Capacity ready tasks can run at once" }
    } elseif ($MachineCap -eq $ceiling) { $binding = 'machine'; $limit = "machine sustains $MachineCap" }
    else { $binding = 'maximum'; $limit = "lane maximum $MaxLanes" }
    if ($lowOnce) { $limit += ', low once' }
    $step = if ($lanes -eq $Current) { "lanes $Current held" } else { "lanes $Current -> $lanes" }
    # Binding names the limit for status.json's autoLanes (BL-770).
    return [pscustomobject]@{ Lanes = $lanes; Changed = ($lanes -ne $Current); Desired = $desired; Low = $low; Binding = $binding; Reason = "$step ($limit)" }
}

function Get-AutoStartCount {
    # Where an Auto shift starts: the lane count auto-lanes.json saved (none on the first
    # Auto shift), raised to -MinStart, capped by the ceilings and at least 1. Why says which.
    param($Saved, [int]$Capacity, [int]$MachineCap, [int]$MinStart = $MinStartLanes, [int]$Max = $MaxLanes)
    $count = $MinStart
    $why = 'first auto shift'
    if ($Saved -and $Saved.lanes) { $count = [int]$Saved.lanes; $why = "last shift saved $count" }
    if ($count -lt $MinStart) { $count = $MinStart; $why += ", raised to $count by -MinStartLanes" }
    $ceiling = [math]::Min($Capacity, [math]::Min($MachineCap, $Max))
    if ($count -gt $ceiling) { $count = [math]::Max(1, $ceiling); $why += ", capped at $count" }
    return [pscustomobject]@{ Count = $count; Why = $why }
}

function Test-UnpacedRampDue {
    # Whether an early Auto step would add lanes now: -Current is below the unpaced floor,
    # min(-Unpaced, ceiling), at the board's -Capacity. The coordinator checks this every
    # 2 minutes between 15-minute steps, so a task finishing and freeing capacity is not
    # left waiting for the step interval (2026-10-21).
    param([int]$Current, [int]$Capacity, [int]$MachineCap, [int]$MaxLanes, [int]$Unpaced = $UnpacedLanes)
    $ceiling = [math]::Min($Capacity, [math]::Min($MachineCap, $MaxLanes))
    return ($Current -lt [math]::Min($Unpaced, $ceiling))
}

# The split-task filer (2026-10-21, ADR-0130 amendment): when a shared file keeps ready tasks
# from running side by side, file a task to split it. Pure, so -TestAutoLanes proves it.
$SplitHotspotMinimum = 5

function Get-OverlapWaitingCount {
    # How many ready, lane-eligible Backlog tasks cannot start now because of overlapping
    # touches: the "ready, #n in queue" lines of task-board.ps1 status, less the "<n> more can
    # start" of its capacity line. 0 when either cannot be read.
    param([string[]]$StatusLines, [string]$CapacityLine)
    $ready = @($StatusLines | Where-Object { "$_" -match '\[ready, #\d+ in queue' }).Count
    if ($CapacityLine -match 'none more can start') { $more = 0 }
    elseif ($CapacityLine -match '(\d+) more can start') { $more = [int]$Matches[1] }
    else { return 0 }
    return [math]::Max(0, $ready - $more)
}

function Get-CapacityReadyIds {
    # The ready task IDs task-board.ps1 capacity names in parentheses ("Capacity 3: 2 in
    # Doing, 1 more can start (BL-5)."), or none.
    param([string]$CapacityLine)
    if ($CapacityLine -notmatch '\(([^)]*)\)') { return @() }
    return @([regex]::Matches($Matches[1], '\bBL-\d+\b') | ForEach-Object { $_.Value })
}

function Get-ClaimableCapacity {
    # The capacity line's count less the ready tasks it names that are not claimable now
    # (-NotClaimableIds: over their cost cap or interactive only), so -Lanes Auto adds no
    # lane for a task no lane may claim (BL-3090). $null when the line has no count.
    param([string]$CapacityLine, [string[]]$NotClaimableIds = @())
    if ($CapacityLine -notmatch '(?m)^Capacity (\d+):') { return $null }
    $count = [int]$Matches[1]
    $passed = @(Get-CapacityReadyIds $CapacityLine | Where-Object { $NotClaimableIds -contains $_ }).Count
    return [math]::Max(0, $count - $passed)
}

function Get-CapacityStreak {
    # The Auto steps running whose binding limit was the board's capacity: -Previous plus
    # one when -Binding is 'capacity', else 0.
    param([string]$Binding, [int]$Previous)
    if ($Binding -eq 'capacity') { return $Previous + 1 }
    return 0
}

function Test-SplitHotspotDue {
    # Whether this step looks for a hotspot to split: capacity bound 2 steps running and at
    # least one ready task waits on overlapping touches.
    param([int]$CapacityStreak, [int]$Waiting)
    return ($CapacityStreak -ge 2 -and $Waiting -gt 0)
}

function Get-SplitTaskTitle {
    # The split task's title; it names the path, which is how a later step finds it live.
    param([string]$Path)
    return "Split $Path so tasks touching it can run in parallel"
}

function Select-SplitHotspot {
    # What to do with task-board.ps1 hotspots' output (-Lines, -ExitCode): Action is
    # 'unavailable' when the command failed or is unknown, 'none' for "No hotspots." or no
    # hotspot line, 'covered' when every hotspot is named by a -LiveTitles title or -Filed
    # (paths filed this shift), and 'file' with the top remaining hotspot's Path, Count and
    # Ids. Reason is the trace line.
    param([string[]]$Lines, [int]$ExitCode, [string[]]$LiveTitles = @(), [string[]]$Filed = @())
    $text = (@($Lines) -join "`n")
    if ($ExitCode -ne 0 -or $text -match '(?i)unknown command|cannot validate argument|does not belong to the set') {
        return [pscustomobject]@{ Action = 'unavailable'; Path = ''; Count = 0; Ids = @(); Reason = 'hotspots unavailable' }
    }
    $hotspots = @(foreach ($line in @($Lines)) {
        if ("$line" -match '^Hotspot (\d+) (.+?): (.+)$') {
            [pscustomobject]@{ Count = [int]$Matches[1]; Path = $Matches[2].Trim(); Ids = @($Matches[3] -split ',\s*' | Where-Object { $_ }) }
        }
    })
    # The command prints them sorted by count, highest first; that order is kept.
    $hotspots = @($hotspots | Where-Object { $_.Count -ge $SplitHotspotMinimum })
    if (-not $hotspots.Count) { return [pscustomobject]@{ Action = 'none'; Path = ''; Count = 0; Ids = @(); Reason = 'no hotspots' } }
    foreach ($hotspot in $hotspots) {
        $norm = $hotspot.Path.Replace('\', '/')
        # A live title names the hotspot by its file name ("Split UpstreamCaseRunner.cs so ..."),
        # with or without the folders before it, as a whole word.
        $leaf = $norm.TrimEnd('/').Split('/')[-1]
        $pattern = "(?<![\w.])$([regex]::Escape($leaf))(?![\w])"
        $named = @($LiveTitles | Where-Object { [regex]::IsMatch("$_".Replace('\', '/'), $pattern, 'IgnoreCase') }).Count
        $filedNow = @($Filed | Where-Object { "$_".Replace('\', '/') -ieq $norm }).Count
        if ($named -or $filedNow) { continue }
        return [pscustomobject]@{ Action = 'file'; Path = $hotspot.Path; Count = $hotspot.Count; Ids = $hotspot.Ids
            Reason = "hotspot $($hotspot.Path) holds $($hotspot.Count) tasks ($($hotspot.Ids -join ', ')); filing a split task" }
    }
    return [pscustomobject]@{ Action = 'covered'; Path = ''; Count = 0; Ids = @(); Reason = 'every hotspot already has a live split task' }
}

# Which lane scaling touches (ADR-0130 item 6), pure so -TestAutoLanes proves them.
function Get-LaneToAdd {
    # The lowest lane number from 1 to -Max that is not active, or $null when all are.
    param([int[]]$Active, [int]$Max = 16)
    foreach ($n in 1..$Max) { if ($Active -notcontains $n) { return $n } }
    return $null
}

function Get-LaneToRetire {
    # The highest-numbered active lane not already retiring, or $null when there is none.
    # One among -Idle (lanes holding no task) is preferred, so it stops within a minute
    # instead of after a task (BL-1374).
    param([int[]]$Active, [int[]]$Retiring, [int[]]$Idle = @())
    $candidates = @($Active | Where-Object { $Retiring -notcontains $_ } | Sort-Object -Descending)
    $idleCandidates = @($candidates | Where-Object { $Idle -contains $_ })
    if ($idleCandidates.Count) { return $idleCandidates[0] }
    if ($candidates.Count) { return $candidates[0] }
    return $null
}

function Get-CapacityLaneCount {
    # One capacity step of a fixed -Lanes N shift (BL-1374): straight down to the board's
    # -Capacity (at least 1) when more lanes run, but by no more than the -Idle lanes; up
    # one lane when the capacity allows it, never above -Requested. Reason is the log line,
    # e.g. "lanes 9 -> 4 (4 ready tasks can run at once)".
    param([int]$Current, [int]$Capacity, [int]$Requested, [int]$Idle)
    $ceiling = [math]::Max(1, [math]::Min($Capacity, $Requested))
    $lanes = $Current
    if ($Current -gt $ceiling) { $lanes = [math]::Max($ceiling, $Current - $Idle) }
    elseif ($Current -lt $ceiling) { $lanes = $Current + 1 }
    $limit = if ($Capacity -ge $Requested) { "-Lanes $Requested" }
        elseif ($Capacity -eq 1) { '1 ready task can run at once' }
        elseif ($Capacity -le 0) { 'no ready task can run' }
        else { "$Capacity ready tasks can run at once" }
    $step = if ($lanes -eq $Current) { "lanes $Current held" } else { "lanes $Current -> $lanes" }
    return [pscustomobject]@{ Lanes = $lanes; Changed = ($lanes -ne $Current); Reason = "$step ($limit)" }
}

function Get-FixedStartCount {
    # The lanes a fixed -Lanes N shift starts with (BL-1384, AF-0041): -Requested capped by
    # the board's -Capacity (at least 1), never below -Adopted, the highest lane number
    # adopted from the previous shift. Why is the log text, e.g. "-Lanes 9, capped at 4: 4
    # ready tasks can run at once".
    param([int]$Requested, [int]$Capacity, [int]$Adopted = 0)
    $count = [math]::Max(1, [math]::Min($Requested, $Capacity))
    $why = if ($count -ge $Requested) { "-Lanes $Requested" }
        elseif ($Capacity -le 0) { "-Lanes $Requested, capped at 1: no ready task can run" }
        elseif ($Capacity -eq 1) { "-Lanes $Requested, capped at 1: 1 ready task can run at once" }
        else { "-Lanes $Requested, capped at ${count}: $Capacity ready tasks can run at once" }
    if ($Adopted -gt $count) { $count = $Adopted; $why = "lane $Adopted adopted from the previous shift" }
    return [pscustomobject]@{ Count = $count; Why = $why }
}

function Test-LanesFinished {
    # True once every active lane is among the finished ones (has written its summary).
    param([int[]]$Active, [int[]]$Finished)
    return (@($Active | Where-Object { $Finished -notcontains $_ }).Count -eq 0)
}

function Get-CiFixLaneDecision {
    # Whether the coordinator adds a lane for a CI fix (ADR-1158): a claimable CI-fix task
    # waits in Backlog on origin (-Waiting: ID -> minutes since the coordinator first saw it
    # there) for -GraceMinutes or more, no lane is free to claim it (-FreeLanes: lanes holding
    # no task), and no lane was added for it lately (-Served). A free lane's next claim takes
    # it within a minute (Get-ReadyCiFixTaskId); with every lane busy, BL-4052 and BL-4299
    # waited 21 and 41 minutes for one. The lane is one above the pace and the machine cap,
    # never above -Max; the next lane step retires it once it is idle. Id is the CI fix or '',
    # Why the trace text.
    param([hashtable]$Waiting, [int]$FreeLanes, [string[]]$Served = @(), [int]$Active, [int]$Max = 16, [double]$GraceMinutes = 2)
    $due = @($Waiting.Keys | Where-Object { $Served -notcontains $_ -and $Waiting[$_] -ge $GraceMinutes } |
        Sort-Object { [int]("$_" -replace '^BL-', '') })
    if (-not $due.Count) { return [pscustomobject]@{ Id = ''; Why = 'no CI fix has waited for a lane' } }
    if ($FreeLanes -gt 0) { return [pscustomobject]@{ Id = ''; Why = "a free lane claims $($due[0])" } }
    if ($Active -ge $Max) { return [pscustomobject]@{ Id = ''; Why = "all $Max lanes are active" } }
    $minutes = [math]::Floor([double]$Waiting[$due[0]])
    return [pscustomobject]@{ Id = $due[0]; Why = "waiting $minutes min in Backlog with all $Active lanes busy" }
}

if ($TestAutoLanes) {
    # Recorded readings on one arbitrary date D, each case checked against its expected log line.
    $D = [datetime]'2026-01-05'
    function New-TestSample {
        param([string]$At, [double]$FiveHour, [datetime]$FiveHourResets, [double]$Week, [datetime]$WeekResets, [double]$ActiveLanes)
        $reading = [pscustomobject]@{ FiveHour = $FiveHour; FiveHourResets = $FiveHourResets; Week = $Week; WeekResets = $WeekResets }
        return New-UsageSample -At ($D + [TimeSpan]$At) -Reading $reading -ActiveLanes $ActiveLanes
    }
    function Get-SampledLaneCount {
        param([object[]]$Samples, [int]$Current, [bool]$WeeklyPace, [int]$Capacity, [int]$MachineCap, [int]$MaxLanes, [bool]$PreviousLow = $false, [int]$Unpaced = $UnpacedLanes)
        $pace = Get-PaceTarget -Sample $Samples[-1] -FiveHourRate (Get-BurnRate -Samples $Samples -Window FiveHour) `
            -WeeklyRate (Get-BurnRate -Samples $Samples -Window Week) -WeeklyPace $WeeklyPace -StopAtUsage 0.85 -StopAtWeeklyUsage 0.97
        return (Get-NextLaneCount -Current $Current -Pace $pace -Capacity $Capacity -MachineCap $MachineCap -MaxLanes $MaxLanes -PreviousLow $PreviousLow -Unpaced $Unpaced).Reason
    }
    function New-TestPace { param([double]$Target) return [pscustomobject]@{ FiveHour = $Target; Weekly = [double]::PositiveInfinity; Target = $Target; Binding = '5-hour pace' } }

    $fiveHourBinds = @(
        New-TestSample '12:00' 0.20 ($D.AddHours(15.5)) 0.10 ($D.AddDays(3).AddHours(12.5)) 3
        New-TestSample '12:15' 0.23 ($D.AddHours(15.5)) 0.10 ($D.AddDays(3).AddHours(12.5)) 3
        New-TestSample '12:30' 0.26 ($D.AddHours(15.5)) 0.11 ($D.AddDays(3).AddHours(12.5)) 3)
    $weeklyBinds = @(
        New-TestSample '10:30' 0.10 ($D.AddHours(15.5)) 0.50 ($D.AddDays(2).AddHours(12.5)) 4
        New-TestSample '12:00' 0.20 ($D.AddHours(15.5)) 0.53 ($D.AddDays(2).AddHours(12.5)) 4
        New-TestSample '12:30' 0.22 ($D.AddHours(15.5)) 0.54 ($D.AddDays(2).AddHours(12.5)) 4)
    $resetInWindow = @(
        New-TestSample '15:29' 0.84 ($D.AddHours(15.5)) 0.30 ($D.AddDays(3)) 3
        New-TestSample '15:44' 0.02 ($D.AddHours(20.5)) 0.30 ($D.AddDays(3)) 3
        New-TestSample '15:59' 0.05 ($D.AddHours(20.5)) 0.30 ($D.AddDays(3)) 3
        New-TestSample '16:14' 0.08 ($D.AddHours(20.5)) 0.30 ($D.AddDays(3)) 3)
    $fiveHourTooShort = @($resetInWindow | Select-Object -First 3)
    $idle = @(
        New-TestSample '12:00' 0.20 ($D.AddHours(15.5)) 0.10 ($D.AddDays(3)) 0
        New-TestSample '12:30' 0.26 ($D.AddHours(15.5)) 0.11 ($D.AddDays(3)) 0)
    $infinite = New-TestPace ([double]::PositiveInfinity)
    $splitStatus = @(
        'Backlog (6)'
        '  BL-5    Normal Claude  Fifth  [ready, #1 in queue]'
        '  BL-6    Normal Claude  Sixth  [ready, #2 in queue, overlaps BL-1 in Doing]'
        '  BL-7    Normal Claude  Seventh  [ready, #3 in queue, held back behind BL-6]'
        '  BL-8    Normal Claude  Eighth  [ready, #4 in queue]'
        '  BL-9    Normal Claude  Ninth  [waiting on BL-5]'
        '  BL-10   Normal Claude  Tenth  [ready, interactive only]')
    $splitHotspots = @(
        'Hotspot 7 Curl.Conformance.UnitTests/KnownDivergences.cs: BL-1, BL-2, BL-3, BL-4, BL-5, BL-6, BL-7'
        'Hotspot 5 Curl.Tests.Upstream/StandIns.cs: BL-8, BL-9, BL-10, BL-11, BL-12')
    $splitUnknown = @('Cannot validate argument on parameter ''Command''. The argument "hotspots" does not belong to the set')

    $cases = @(
        ,@('five-hour-binds', 'lanes 3 -> 4 (5-hour pace allows 4.9)', (Get-SampledLaneCount $fiveHourBinds 3 $true 6 8 16))
        ,@('five-hour-binds weekly rate', 'null', "$(if ($null -eq (Get-BurnRate $fiveHourBinds Week)) { 'null' } else { 'a rate' })")
        ,@('weekly-binds low once', 'lanes 4 held (weekly pace allows 1.8, low once)', (Get-SampledLaneCount $weeklyBinds 4 $true 6 8 16 $false 1))
        ,@('weekly-binds low twice', 'lanes 4 -> 2 (weekly pace allows 1.8)', (Get-SampledLaneCount $weeklyBinds 4 $true 6 8 16 $true 1))
        ,@('five-hour span under 30 min', 'null', "$(if ($null -eq (Get-BurnRate $fiveHourTooShort FiveHour)) { 'null' } else { 'a rate' })")
        ,@('weekly-pace-off', 'lanes 4 -> 5 (6 ready tasks can run at once)', (Get-SampledLaneCount $weeklyBinds 4 $false 6 8 16))
        ,@('weekly-pace-off by default', 'Infinity', "$((Get-PaceTarget -Sample $weeklyBinds[-1] -FiveHourRate 1.0 -WeeklyRate 5.0 -StopAtUsage 0.85 -StopAtWeeklyUsage 0.97).Weekly)")
        ,@('five-hour-reset-in-window', 'lanes 3 -> 4 (5-hour pace allows 4.5)', (Get-SampledLaneCount $resetInWindow 3 $true 8 8 16))
        ,@('hold-inside-band 3.8', 'lanes 4 held (5-hour pace allows 3.8)', (Get-NextLaneCount 4 (New-TestPace 3.8) 16 16 16).Reason)
        ,@('below-band low once', 'lanes 4 held (5-hour pace allows 3.7, low once)', (Get-NextLaneCount 4 (New-TestPace 3.7) 16 16 16).Reason)
        ,@('below-band low twice', 'lanes 4 -> 3 (5-hour pace allows 3.7)', (Get-NextLaneCount 4 (New-TestPace 3.7) 16 16 16 $true).Reason)
        ,@('below-band Low flag', 'True', "$((Get-NextLaneCount 4 (New-TestPace 3.7) 16 16 16).Low)")
        ,@('capacity-ceiling 3', 'lanes 5 -> 3 (3 ready tasks can run at once)', (Get-NextLaneCount 5 (New-TestPace 9.0) 3 16 16).Reason)
        ,@('pace drop 6 to 2.4 low once', 'lanes 6 held (5-hour pace allows 2.4, low once)', (Get-NextLaneCount 6 (New-TestPace 2.4) 16 16 16 $false 1).Reason)
        ,@('pace drop 6 to 2.4 low twice', 'lanes 6 -> 2 (5-hour pace allows 2.4)', (Get-NextLaneCount 6 (New-TestPace 2.4) 16 16 16 $true 1).Reason)
        ,@('pace drop 6 to 2.8 rounds up', 'lanes 6 -> 3 (5-hour pace allows 2.8)', (Get-NextLaneCount 6 (New-TestPace 2.8) 16 16 16 $true 1).Reason)
        ,@('pace drop 5 to spent budget', 'lanes 5 -> 1 (5-hour pace allows 0.0)', (Get-NextLaneCount 5 (New-TestPace 0.0) 16 16 16 $true 1).Reason)
        ,@('ceiling drop 6 to capacity 2', 'lanes 6 -> 2 (2 ready tasks can run at once)', (Get-NextLaneCount 6 (New-TestPace 9.0) 2 16 16).Reason)
        ,@('ceiling drop 6 to machine 3', 'lanes 6 -> 3 (machine sustains 3)', (Get-NextLaneCount 6 $infinite 20 3 16).Reason)
        ,@('up one from 3 at ceiling 6', 'lanes 3 -> 4 (lane maximum 6)', (Get-NextLaneCount 3 $infinite 20 16 6).Reason)
        ,@('capacity-ceiling 1', 'lanes 2 -> 1 (1 ready task can run at once)', (Get-NextLaneCount 2 $null 1 16 16).Reason)
        ,@('machine-ceiling', 'lanes 7 held (machine sustains 7)', (Get-NextLaneCount 7 $infinite 20 7 16).Reason)
        ,@('max-ceiling', 'lanes 2 held (lane maximum 2)', (Get-NextLaneCount 2 $infinite 20 16 2).Reason)
        ,@('no-rate', 'lanes 2 held (no burn rate yet)', (Get-NextLaneCount 2 $null 6 8 16 $false 1).Reason)
        ,@('binding pace', '5-hour pace', (Get-NextLaneCount 4 (New-TestPace 3.8) 16 16 16).Binding)
        ,@('binding capacity', 'capacity', (Get-NextLaneCount 5 (New-TestPace 9.0) 3 16 16).Binding)
        ,@('binding machine', 'machine', (Get-NextLaneCount 7 $infinite 20 7 16).Binding)
        ,@('binding maximum', 'maximum', (Get-NextLaneCount 2 $infinite 20 16 2).Binding)
        ,@('binding no rate', 'no burn rate', (Get-NextLaneCount 2 $null 6 8 16 $false 1).Binding)
        ,@('unpaced up from 1 with no rate', 'lanes 1 -> 3 (up to 3 lanes run unpaced)', (Get-NextLaneCount 1 $null 6 8 16).Reason)
        ,@('unpaced jump from 1 past a high pace', 'lanes 1 -> 3 (up to 3 lanes run unpaced)', (Get-NextLaneCount 1 (New-TestPace 9.0) 6 8 16).Reason)
        ,@('unpaced jump capped by capacity 2', 'lanes 1 -> 2 (2 ready tasks can run at once)', (Get-NextLaneCount 1 $null 2 16 16).Reason)
        ,@('unpaced jump to -Unpaced 5', 'lanes 2 -> 5 (up to 5 lanes run unpaced)', (Get-NextLaneCount 2 $null 8 16 16 $false 5).Reason)
        ,@('above the floor still up one', 'lanes 3 -> 4 (machine sustains 16)', (Get-NextLaneCount 3 $infinite 20 16 16).Reason)
        ,@('jump binding unpaced', 'unpaced', (Get-NextLaneCount 1 (New-TestPace 9.0) 6 8 16).Binding)
        ,@('ramp due 1 lane capacity 4', 'True', "$(Test-UnpacedRampDue -Current 1 -Capacity 4 -MachineCap 16 -MaxLanes 16 -Unpaced 3)")
        ,@('ramp due 2 lanes capacity 2', 'False', "$(Test-UnpacedRampDue -Current 2 -Capacity 2 -MachineCap 16 -MaxLanes 16 -Unpaced 3)")
        ,@('ramp due 3 lanes capacity 9', 'False', "$(Test-UnpacedRampDue -Current 3 -Capacity 9 -MachineCap 16 -MaxLanes 16 -Unpaced 3)")
        ,@('overlap waiting 4 ready 1 more', '3', "$(Get-OverlapWaitingCount -StatusLines $splitStatus -CapacityLine 'Capacity 3: 2 in Doing, 1 more can start (BL-5).')")
        ,@('overlap waiting none more', '4', "$(Get-OverlapWaitingCount -StatusLines $splitStatus -CapacityLine 'Capacity 2: 2 in Doing, none more can start.')")
        ,@('overlap waiting unreadable', '0', "$(Get-OverlapWaitingCount -StatusLines $splitStatus -CapacityLine 'error')")
        ,@('capacity streak 1 -> 2', '2', "$(Get-CapacityStreak -Binding 'capacity' -Previous 1)")
        ,@('capacity streak reset by pace', '0', "$(Get-CapacityStreak -Binding '5-hour pace' -Previous 3)")
        ,@('split due streak 2 waiting 3', 'True', "$(Test-SplitHotspotDue -CapacityStreak 2 -Waiting 3)")
        ,@('split not due streak 1', 'False', "$(Test-SplitHotspotDue -CapacityStreak 1 -Waiting 3)")
        ,@('split not due nothing waiting', 'False', "$(Test-SplitHotspotDue -CapacityStreak 4 -Waiting 0)")
        ,@('split unknown command', 'unavailable: hotspots unavailable', "$((Select-SplitHotspot -Lines $splitUnknown -ExitCode 1) | ForEach-Object { "$($_.Action): $($_.Reason)" })")
        ,@('split no hotspots', 'none: no hotspots', "$((Select-SplitHotspot -Lines @('No hotspots.') -ExitCode 0) | ForEach-Object { "$($_.Action): $($_.Reason)" })")
        ,@('split files the top hotspot', 'file: hotspot Curl.Conformance.UnitTests/KnownDivergences.cs holds 7 tasks (BL-1, BL-2, BL-3, BL-4, BL-5, BL-6, BL-7); filing a split task', "$((Select-SplitHotspot -Lines $splitHotspots -ExitCode 0) | ForEach-Object { "$($_.Action): $($_.Reason)" })")
        ,@('split skips a live one', 'file: Curl.Tests.Upstream/StandIns.cs', "$((Select-SplitHotspot -Lines $splitHotspots -ExitCode 0 -LiveTitles @((Get-SplitTaskTitle 'Curl.Conformance.UnitTests/KnownDivergences.cs'))) | ForEach-Object { "$($_.Action): $($_.Path)" })")
        ,@('split skips one a title names by file name', 'file: Curl.Tests.Upstream/StandIns.cs', "$((Select-SplitHotspot -Lines $splitHotspots -ExitCode 0 -LiveTitles @('Split KnownDivergences.cs so each divergence lives in its own file')) | ForEach-Object { "$($_.Action): $($_.Path)" })")
        ,@('split ignores a longer file name', 'file: Curl.Conformance.UnitTests/KnownDivergences.cs', "$((Select-SplitHotspot -Lines $splitHotspots -ExitCode 0 -LiveTitles @('Split OldKnownDivergences.cs.bak first')) | ForEach-Object { "$($_.Action): $($_.Path)" })")
        ,@('split skips one filed this shift', 'file: Curl.Tests.Upstream/StandIns.cs', "$((Select-SplitHotspot -Lines $splitHotspots -ExitCode 0 -Filed @('Curl.Conformance.UnitTests/KnownDivergences.cs')) | ForEach-Object { "$($_.Action): $($_.Path)" })")
        ,@('split all covered', 'covered', "$((Select-SplitHotspot -Lines $splitHotspots -ExitCode 0 -LiveTitles @((Get-SplitTaskTitle 'Curl.Conformance.UnitTests/KnownDivergences.cs'), (Get-SplitTaskTitle 'Curl.Tests.Upstream/StandIns.cs'))).Action)")
        ,@('split title', 'Split Curl.Tests.Upstream/StandIns.cs so tasks touching it can run in parallel', (Get-SplitTaskTitle 'Curl.Tests.Upstream/StandIns.cs'))
        ,@('unpaced up from 2 past a low pace', 'lanes 2 -> 3 (up to 3 lanes run unpaced)', (Get-NextLaneCount 2 (New-TestPace 0.5) 6 8 16).Reason)
        ,@('unpaced holds 3 with no rate', 'lanes 3 held (no burn rate yet)', (Get-NextLaneCount 3 $null 6 8 16).Reason)
        ,@('paced above 3', 'lanes 3 -> 4 (5-hour pace allows 4.9)', (Get-NextLaneCount 3 (New-TestPace 4.9) 6 8 16).Reason)
        ,@('pace holds at 3', 'lanes 3 held (5-hour pace allows 3.4)', (Get-NextLaneCount 3 (New-TestPace 3.4) 6 8 16).Reason)
        ,@('low pace holds at 3', 'lanes 3 held (up to 3 lanes run unpaced)', (Get-NextLaneCount 3 (New-TestPace 1.2) 6 8 16).Reason)
        ,@('pace drop 6 retires to 3 not below', 'lanes 6 -> 3 (up to 3 lanes run unpaced)', (Get-NextLaneCount 6 (New-TestPace 0.0) 16 16 16 $true).Reason)
        ,@('unpaced capped by capacity 2', 'lanes 2 held (2 ready tasks can run at once)', (Get-NextLaneCount 2 (New-TestPace 0.5) 2 16 16).Reason)
        ,@('unpaced capacity 1 retires', 'lanes 2 -> 1 (1 ready task can run at once)', (Get-NextLaneCount 2 $null 1 16 16).Reason)
        ,@('binding unpaced', 'unpaced', (Get-NextLaneCount 1 $null 6 8 16).Binding)
        ,@('idle-lanes', 'null', "$(if ($null -eq (Get-BurnRate $idle FiveHour)) { 'null' } else { 'a rate' })")
        ,@('lane-to-add 1,2,4', '3', "$(Get-LaneToAdd -Active 1, 2, 4 -Max 16)")
        ,@('lane-to-add 1..16', 'null', "$(if ($null -eq (Get-LaneToAdd -Active (1..16) -Max 16)) { 'null' } else { 'a lane' })")
        ,@('lane-to-retire 1,2,3 retiring 3', '2', "$(Get-LaneToRetire -Active 1, 2, 3 -Retiring 3)")
        ,@('lane-to-retire 1,2,3 retiring 1,2,3', 'null', "$(if ($null -eq (Get-LaneToRetire -Active 1, 2, 3 -Retiring 1, 2, 3)) { 'null' } else { 'a lane' })")
        ,@('lane-to-retire 1,2,3,4 idle 1,2', '2', "$(Get-LaneToRetire -Active 1, 2, 3, 4 -Retiring @() -Idle 1, 2)")
        ,@('lane-to-retire 1,2,3 retiring 2 idle 2', '3', "$(Get-LaneToRetire -Active 1, 2, 3 -Retiring 2 -Idle 2)")
        ,@('claimable capacity of capped-only ready tasks', '0', "$(Get-ClaimableCapacity -CapacityLine 'Capacity 2: 0 in Doing, 2 more can start (BL-5, BL-6).' -NotClaimableIds 'BL-5', 'BL-6')")
        ,@('claimable capacity less one capped task', '2', "$(Get-ClaimableCapacity -CapacityLine 'Capacity 3: 1 in Doing, 2 more can start (BL-5, BL-6).' -NotClaimableIds 'BL-6', 'BL-9')")
        ,@('claimable capacity with nothing capped', '1', "$(Get-ClaimableCapacity -CapacityLine 'Capacity 1: 0 in Doing, 1 more can start (BL-5).')")
        ,@('claimable capacity with no count', 'null', "$(if ($null -eq (Get-ClaimableCapacity -CapacityLine 'oops')) { 'null' } else { 'a count' })")
        ,@('capacity ready ids none more', '', ((Get-CapacityReadyIds 'Capacity 2: 2 in Doing, none more can start.') -join ','))
        ,@('capacity 9 lanes capacity 4 idle 6', 'lanes 9 -> 4 (4 ready tasks can run at once)', (Get-CapacityLaneCount -Current 9 -Capacity 4 -Requested 9 -Idle 6).Reason)
        ,@('capacity 9 lanes capacity 4 idle 2', 'lanes 9 -> 7 (4 ready tasks can run at once)', (Get-CapacityLaneCount -Current 9 -Capacity 4 -Requested 9 -Idle 2).Reason)
        ,@('capacity 3 lanes capacity 0', 'lanes 3 -> 1 (no ready task can run)', (Get-CapacityLaneCount -Current 3 -Capacity 0 -Requested 9 -Idle 3).Reason)
        ,@('capacity 1 lane capacity 1', 'lanes 1 held (1 ready task can run at once)', (Get-CapacityLaneCount -Current 1 -Capacity 1 -Requested 9 -Idle 1).Reason)
        ,@('capacity 4 lanes capacity 6', 'lanes 4 -> 5 (6 ready tasks can run at once)', (Get-CapacityLaneCount -Current 4 -Capacity 6 -Requested 9 -Idle 0).Reason)
        ,@('capacity 9 lanes capacity 12', 'lanes 9 held (-Lanes 9)', (Get-CapacityLaneCount -Current 9 -Capacity 12 -Requested 9 -Idle 0).Reason)
        ,@('capacity 8 lanes capacity 12', 'lanes 8 -> 9 (-Lanes 9)', (Get-CapacityLaneCount -Current 8 -Capacity 12 -Requested 9 -Idle 0).Reason)
        ,@('fixed start 9 capacity 4', '4 (-Lanes 9, capped at 4: 4 ready tasks can run at once)', "$((Get-FixedStartCount -Requested 9 -Capacity 4) | ForEach-Object { "$($_.Count) ($($_.Why))" })")
        ,@('fixed start 9 capacity 12', '9 (-Lanes 9)', "$((Get-FixedStartCount -Requested 9 -Capacity 12) | ForEach-Object { "$($_.Count) ($($_.Why))" })")
        ,@('fixed start 9 capacity 1', '1 (-Lanes 9, capped at 1: 1 ready task can run at once)', "$((Get-FixedStartCount -Requested 9 -Capacity 1) | ForEach-Object { "$($_.Count) ($($_.Why))" })")
        ,@('fixed start 9 capacity 0', '1 (-Lanes 9, capped at 1: no ready task can run)', "$((Get-FixedStartCount -Requested 9 -Capacity 0) | ForEach-Object { "$($_.Count) ($($_.Why))" })")
        ,@('fixed start 9 capacity 2 adopted 6', '6 (lane 6 adopted from the previous shift)', "$((Get-FixedStartCount -Requested 9 -Capacity 2 -Adopted 6) | ForEach-Object { "$($_.Count) ($($_.Why))" })")
        ,@('lanes-finished 1,3 of 1,2', 'False', "$(Test-LanesFinished -Active 1, 3 -Finished 1, 2)")
        ,@('lanes-finished 1,3 of 1,3', 'True', "$(Test-LanesFinished -Active 1, 3 -Finished 1, 3)")
        ,@('ci-fix lane for the oldest CI fix past the grace with every lane busy', 'BL-4052: waiting 21 min in Backlog with all 8 lanes busy', "$((Get-CiFixLaneDecision -Waiting @{ 'BL-4299' = 40.5; 'BL-4052' = 21.2 } -FreeLanes 0 -Active 8) | ForEach-Object { "$($_.Id): $($_.Why)" })")
        ,@('ci-fix lane not while a lane is free', ': a free lane claims BL-4052', "$((Get-CiFixLaneDecision -Waiting @{ 'BL-4052' = 5 } -FreeLanes 1 -Active 8) | ForEach-Object { "$($_.Id): $($_.Why)" })")
        ,@('ci-fix lane not within the grace', ': no CI fix has waited for a lane', "$((Get-CiFixLaneDecision -Waiting @{ 'BL-4052' = 1.5 } -FreeLanes 0 -Active 8) | ForEach-Object { "$($_.Id): $($_.Why)" })")
        ,@('ci-fix lane not twice for one fix; the next one gets it', 'BL-4299', "$((Get-CiFixLaneDecision -Waiting @{ 'BL-4299' = 3; 'BL-4052' = 21 } -FreeLanes 0 -Served 'BL-4052' -Active 9).Id)")
        ,@('ci-fix lane not above 16', ': all 16 lanes are active', "$((Get-CiFixLaneDecision -Waiting @{ 'BL-4052' = 9 } -FreeLanes 0 -Active 16) | ForEach-Object { "$($_.Id): $($_.Why)" })")
        ,@('ci-fix lane with nothing waiting', '', "$((Get-CiFixLaneDecision -Waiting @{} -FreeLanes 0 -Active 4).Id)")
        ,@('start saved 2', '3 (last shift saved 2, raised to 3 by -MinStartLanes)', "$((Get-AutoStartCount ([pscustomobject]@{ lanes = 2 }) 16 16 3 16) | ForEach-Object { "$($_.Count) ($($_.Why))" })")
        ,@('start saved 5', '5 (last shift saved 5)', "$((Get-AutoStartCount ([pscustomobject]@{ lanes = 5 }) 16 16 3 16) | ForEach-Object { "$($_.Count) ($($_.Why))" })")
        ,@('start first shift', '3 (first auto shift)', "$((Get-AutoStartCount $null 16 16 3 16) | ForEach-Object { "$($_.Count) ($($_.Why))" })")
        ,@('start min 4 ceiling 2', '2 (first auto shift, capped at 2)', "$((Get-AutoStartCount $null 2 16 4 16) | ForEach-Object { "$($_.Count) ($($_.Why))" })")
        ,@('start default at ceiling', '9 (last shift saved 2, raised to 16 by -MinStartLanes, capped at 9)', "$((Get-AutoStartCount ([pscustomobject]@{ lanes = 2 }) 16 16) | ForEach-Object { "$($_.Count) ($($_.Why))" })")
        ,@('start default at capacity', '4 (first auto shift, capped at 4)', "$((Get-AutoStartCount $null 4 16) | ForEach-Object { "$($_.Count) ($($_.Why))" })")
        ,@('start min 3 saved 2', '3 (last shift saved 2, raised to 3 by -MinStartLanes)', "$((Get-AutoStartCount ([pscustomobject]@{ lanes = 2 }) 16 16 3) | ForEach-Object { "$($_.Count) ($($_.Why))" })")
        ,@('start min 1 saved 1', '1 (last shift saved 1)', "$((Get-AutoStartCount ([pscustomobject]@{ lanes = 1 }) 16 16 1 16) | ForEach-Object { "$($_.Count) ($($_.Why))" })"))
    $failed = 0
    foreach ($case in $cases) {
        if ($case[1] -ceq $case[2]) { Write-Host "PASS $($case[0]): $($case[2])" -ForegroundColor Green }
        else { Write-Host "FAIL $($case[0]): expected $($case[1]), got $($case[2])" -ForegroundColor Red; $failed++ }
    }
    exit $(if ($failed) { 1 } else { 0 })
}

# This file ran to its end; RunDarkFactory.ps1 goes on to the next one.
$AreaFileFinished = $true
