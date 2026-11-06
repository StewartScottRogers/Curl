# Time spans for the trace and speech, and the account's usage reading: how much of the
# 5-hour and weekly windows is used, and when a shift must stop for it. Dot-sourced by
# RunDarkFactory.ps1 in its order; see the loader there.

# ---------------------------------------------------------------------------- usage limit

function Format-Span {
    # 2 h 55 min, or 40 min, rounded up to the minute.
    param([TimeSpan]$Span)
    $mins = [int][math]::Max(0, [math]::Ceiling($Span.TotalMinutes))
    if ($mins -ge 60) { return "$([math]::Floor($mins / 60)) h $($mins % 60) min" }
    return "$mins min"
}

function Format-SpokenSpan {
    # 2 hours 55 minutes, or 1 minute, rounded up to the minute.
    param([TimeSpan]$Span)
    $mins = [int][math]::Max(1, [math]::Ceiling($Span.TotalMinutes))
    $h = [int][math]::Floor($mins / 60); $m = $mins % 60
    $parts = @()
    if ($h) { $parts += if ($h -eq 1) { '1 hour' } else { "$h hours" } }
    if ($m) { $parts += if ($m -eq 1) { '1 minute' } else { "$m minutes" } }
    return $parts -join ' '
}

function ConvertTo-Unix { param([datetime]$When) return ([DateTimeOffset]$When).ToUnixTimeSeconds() }
function ConvertFrom-Unix { param([long]$Seconds) return [DateTimeOffset]::FromUnixTimeSeconds($Seconds).LocalDateTime }

function ConvertFrom-RateLimitEvent {
    # The usage windows one stream-json rate_limit_event line reports, as a hashtable from
    # window name ('five_hour', 'seven_day') to @{ Used; ResetsUnix }. Claude Code used to
    # list every window under unifiedWindows; since October 2026 an event lists only the
    # window it is about (rateLimitType), with utilization and resetsAt beside it, and
    # leaves a window out until it nears its limit. Both shapes are read; a window named
    # in unifiedWindows wins over the top-level fields. Pure, so -TestAutoLanes proves it.
    param([string]$Line)
    $windows = @{}
    if ($Line -notmatch '"rate_limit_event"') { return $windows }
    foreach ($m in [regex]::Matches($Line, '"(five_hour|seven_day)":\{"utilization":([0-9.]+),"resetsAt":(\d+)')) {
        $windows[$m.Groups[1].Value] = @{ Used = [double]$m.Groups[2].Value; ResetsUnix = [long]$m.Groups[3].Value }
    }
    if ($Line -match '"rate_limit_info":\{[^{}]*"rateLimitType":"(five_hour|seven_day)"') {
        $type = $Matches[1]
        if (-not $windows.ContainsKey($type) -and $Line -match '"rate_limit_info":\{[^{}]*?"utilization":([0-9.]+)') {
            $used = [double]$Matches[1]
            if ($Line -match '"rate_limit_info":\{[^{}]*?"resetsAt":(\d+)') {
                $windows[$type] = @{ Used = $used; ResetsUnix = [long]$Matches[1] }
            }
        }
    }
    return $windows
}

function Get-UsageReading {
    # The newest usage reading the runs logged: the share of the 5-hour and the weekly window
    # used, and when each resets, each window taken from the newest event that reports it.
    # A window whose reset has passed reads 0, and so does one no event reports: Claude
    # Code leaves a window out until it nears its limit, so an unreported window is not
    # near it (FiveHourReported and WeekReported say which were seen). -ThisShift looks
    # only at this shift's runs, so a lane never stops on a reading from before a reset
    # that was lifted early. $null when no run has logged a rate_limit_event.
    param([switch]$ThisShift)
    $filter = if ($ThisShift) { "*-$Stamp*.jsonl" } else { '*.jsonl' }
    $files = Get-ChildItem $LogDir -Filter $filter -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 40
    $found = @{}
    $anyEvent = $false
    foreach ($f in $files) {
        $lines = @(Select-String -LiteralPath $f.FullName -Pattern '"rate_limit_event"' | ForEach-Object Line)
        for ($i = $lines.Count - 1; $i -ge 0; $i--) {
            $anyEvent = $true
            $windows = ConvertFrom-RateLimitEvent $lines[$i]
            foreach ($name in $windows.Keys) { if (-not $found.ContainsKey($name)) { $found[$name] = $windows[$name] } }
            if ($found.ContainsKey('five_hour') -and $found.ContainsKey('seven_day')) { break }
        }
        if ($found.ContainsKey('five_hour') -and $found.ContainsKey('seven_day')) { break }
    }
    if (-not $anyEvent) { return $null }
    $reading = [pscustomobject]@{
        FiveHour = 0.0; FiveHourResets = [datetime]::MinValue; FiveHourReported = $false
        Week = 0.0; WeekResets = [datetime]::MinValue; WeekReported = $false
    }
    if ($found.ContainsKey('five_hour')) {
        $reading.FiveHour = $found['five_hour'].Used; $reading.FiveHourResets = ConvertFrom-Unix $found['five_hour'].ResetsUnix; $reading.FiveHourReported = $true
    }
    if ($found.ContainsKey('seven_day')) {
        $reading.Week = $found['seven_day'].Used; $reading.WeekResets = ConvertFrom-Unix $found['seven_day'].ResetsUnix; $reading.WeekReported = $true
    }
    if ($reading.FiveHourResets -le (Get-Date)) { $reading.FiveHour = 0.0 }
    if ($reading.WeekResets -le (Get-Date)) { $reading.Week = 0.0 }
    return $reading
}

function Test-CiFixTitle {
    # Whether a task title is one the coordinator files for a red CI: "Fix CI failure ...",
    # "Fix CI build ..." or "Fix flaky CI test ..." (Get-CiTaskTitle).
    param([string]$Title)
    return [bool]($Title -match '^(Fix CI failure|Fix CI build|Fix flaky CI test)\b')
}

function Test-CiFixFileName {
    # The same for a task file name, whose slug starts with the title: BL-4083-fix-flaky-ci-test-...
    param([string]$Name)
    return [bool]($Name -match '^BL-\d+-fix-(ci-failure|ci-build|flaky-ci-test)-')
}

function Get-UsageStop {
    # Why no new task should be claimed now - the weekly window is at least
    # -StopAtWeeklyUsage used or the 5-hour one -StopAtUsage - or '' when there is room or
    # this shift has no reading yet.
    $u = Get-UsageReading -ThisShift
    if (-not $u) { return '' }
    if ($u.Week -ge $StopAtWeeklyUsage) { return "weekly tokens $([math]::Round($u.Week * 100))% used, reset $($u.WeekResets.ToString('ddd HH:mm'))" }
    if ($u.FiveHour -ge $StopAtUsage) { return "session tokens $([math]::Round($u.FiveHour * 100))% used, reset $($u.FiveHourResets.ToString('HH:mm'))" }
    return ''
}

# This file ran to its end; RunDarkFactory.ps1 goes on to the next one.
$AreaFileFinished = $true
