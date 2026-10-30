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

function Get-UsageReading {
    # The newest usage reading a run logged: the share of the 5-hour and the weekly window
    # used, and when each resets. A window whose reset has passed reads 0. -ThisShift looks
    # only at this shift's runs, so a lane never stops on a reading from before a reset
    # that was lifted early. $null when no run has logged a reading.
    param([switch]$ThisShift)
    $filter = if ($ThisShift) { "*-$Stamp*.jsonl" } else { '*.jsonl' }
    $files = Get-ChildItem $LogDir -Filter $filter -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 10
    foreach ($f in $files) {
        $line = Select-String -LiteralPath $f.FullName -Pattern '"five_hour":\{"utilization":' | Select-Object -Last 1
        if (-not $line) { continue }
        $text = $line.Line
        if ($text -notmatch '"five_hour":\{"utilization":([0-9.]+),"resetsAt":(\d+)') { continue }
        $reading = [pscustomobject]@{ FiveHour = [double]$Matches[1]; FiveHourResets = ConvertFrom-Unix ([long]$Matches[2]); Week = 0.0; WeekResets = [datetime]::MinValue }
        if ($text -match '"seven_day":\{"utilization":([0-9.]+),"resetsAt":(\d+)') {
            $reading.Week = [double]$Matches[1]; $reading.WeekResets = ConvertFrom-Unix ([long]$Matches[2])
        }
        if ($reading.FiveHourResets -le (Get-Date)) { $reading.FiveHour = 0.0 }
        if ($reading.WeekResets -le (Get-Date)) { $reading.Week = 0.0 }
        return $reading
    }
    return $null
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
