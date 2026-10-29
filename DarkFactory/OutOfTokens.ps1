# Running out of tokens: the limit file, the notices, the probe that ends a wait early, the
# wait for the new session, -Wake and -TestOutOfTokens. Dot-sourced by RunDarkFactory.ps1 in
# its order; see the loader there.

function Get-OutOfTokensUntil {
    # When the last run was refused for the account's usage limit, the local time the
    # limit resets; otherwise $null. The run's rate_limit_event gives it exactly; the
    # result's text is the fallback: "You've hit your session limit <U+00B7> resets 12:50pm".
    if ($script:LimitResetAt) { return $script:LimitResetAt }
    $text = if ($script:RunResult) { "$($script:RunResult.result)" } else { '' }
    if ($text -notmatch '(?i)hit your .*limit|usage limit|limit reached') { return $null }
    if ($text -match '\|(\d{10})') { return (ConvertFrom-Unix ([long]$Matches[1])) }
    if ($text -match '(?i)resets\s+(?:at\s+)?(\d{1,2})(?::(\d{2}))?\s*(am|pm)') {
        $hour = [int]$Matches[1] % 12
        if ($Matches[3] -ieq 'pm') { $hour += 12 }
        $min = if ($Matches[2]) { [int]$Matches[2] } else { 0 }
        $at = (Get-Date).Date.AddHours($hour).AddMinutes($min)
        if ($at -lt (Get-Date).AddMinutes(-10)) { $at = $at.AddDays(1) }
        return $at
    }
    # Refused, but no reset time given: look again in half an hour.
    return (Get-Date).AddMinutes(30)
}

function Add-LimitMark {
    # Lanes write at the same moment when the limit hits them all; retry a busy file.
    param([string]$Line)
    foreach ($try in 1..10) {
        try { Add-Content -Path $LimitFile -Value $Line -Encoding UTF8 -ErrorAction Stop; return }
        catch { Start-Sleep -Milliseconds 200 }
    }
}

function Show-LimitNotice {
    # A one-off notice, not the alarm: a coloured block, a chime and one spoken sentence.
    param([string]$Headline, [string]$Detail, [string]$Spoken, [string]$Color)
    $bar = '=' * 78
    foreach ($l in @($bar, "  $Headline", "  $Detail", $bar)) { Write-Host $l.PadRight(78) -ForegroundColor Black -BackgroundColor $Color }
    Write-Trace '-' 'TOKENS' "$Headline  $Detail" $Color
    if (Test-AlarmSilent) { return }
    Invoke-Chime
    $voice = Get-Voice
    if ($voice) {
        $voice.SpeakAsyncCancelAll()
        $voice.Volume = 100
        $voice.Rate = 0
        [void]$voice.SpeakAsync($Spoken)
    }
}

$script:Notice = @{ Reset = 0L; Warned = $false; Resumed = $false }

function Test-WaitingForSession { return ($script:Notice.Reset -and -not $script:Notice.Resumed) }

function Update-LimitNotice {
    # Tells Stewart about the usage limit once per stage however many lanes hit it: out of
    # tokens, the new session about to start, and the new session in use. Runners record
    # "reset <unix>" and "resumed <unix> <ID>" in the limit file; whoever Stewart watches -
    # the coordinator, or a lone runner - calls this to announce them.
    if (-not (Test-Path $LimitFile)) { return }
    $lines = @(Get-Content $LimitFile -ErrorAction SilentlyContinue)
    $resets = @($lines | Where-Object { $_ -match '^reset \d+$' } | ForEach-Object { [long]($_ -split ' ')[1] })
    if (-not $resets.Count) { return }
    $latest = [long]($resets | Measure-Object -Maximum).Maximum
    $reset = ConvertFrom-Unix $latest
    $at = $reset.ToString('HH:mm')
    $n = $script:Notice
    if ($n.Reset -ne $latest) {
        $n.Reset = $latest; $n.Warned = $false; $n.Resumed = $false
        $left = $reset - (Get-Date)
        Show-LimitNotice 'OUT OF TOKENS' "Out of tokens at $(Get-Date -Format 'HH:mm'). New session starts at $at, in $(Format-Span $left)." `
            "Stewart, $SpokenName is out of tokens. The new session starts at $($reset.ToString('h:mm tt')), in $(Format-SpokenSpan $left)." 'Yellow'
    }
    $resumed = @($lines | Where-Object { $_ -match "^resumed $latest \S+$" } | ForEach-Object { ($_ -split ' ')[2] })
    if (-not $n.Resumed -and $resumed.Count) {
        $n.Resumed = $true; $n.Warned = $true
        Show-LimitNotice 'NEW SESSION STARTED' "Started using the new session at $(Get-Date -Format 'HH:mm'); resuming $($resumed[0])." `
            "Stewart, the new session has started. $SpokenName is working again." 'Green'
        try { $Host.UI.RawUI.WindowTitle = if ($AutoLanes -or $LaneCount -gt 1) { "Dark factory - $LaneCount lanes" } else { 'Dark factory - running' } } catch { }
        return
    }
    if ($n.Resumed) { return }
    $left = $reset - (Get-Date)
    if (-not $n.Warned -and $left.TotalSeconds -le $LimitWarnSeconds) {
        $n.Warned = $true
        Show-LimitNotice 'NEW SESSION SOON' "The new session starts at $at, in $(Format-Span $left)." `
            "Stewart, the new session for $SpokenName will be ready in about $(Format-SpokenSpan $left)." 'Cyan'
    }
    try { $Host.UI.RawUI.WindowTitle = "Dark factory - out of tokens, new session at $at (in $(Format-Span $left))" } catch { }
}

function Test-WakeRequested {
    # True once "wake <unix>" for this reset is in the limit file: tokens came back early,
    # because Stewart reset the limit or the probe found the account answering again.
    # Only a wake written after the last time a runner hit this limit counts: a runner
    # that hits it again after a wake finds the tokens were not back after all, and must
    # wait, not spin through wait after wait on the stale wake.
    param([long]$Unix)
    if (-not (Test-Path $LimitFile)) { return $false }
    $marks = @(Get-Content $LimitFile -ErrorAction SilentlyContinue)
    return ([array]::LastIndexOf($marks, "wake $Unix") -gt [array]::LastIndexOf($marks, "reset $Unix"))
}

$script:NextProbe = [datetime]::MinValue
function Invoke-LimitProbe {
    # While the shift waits, asks Claude for one word every -LimitProbeMinutes. A refused
    # probe costs nothing; one that is answered means the limit was lifted early, and
    # every waiting runner is woken.
    if (-not (Test-WaitingForSession) -or $LimitProbeMinutes -le 0 -or (Get-Date) -lt $script:NextProbe) { return }
    $script:NextProbe = (Get-Date).AddMinutes($LimitProbeMinutes)
    $reset = $script:Notice.Reset
    if (Test-WakeRequested $reset) { return }
    try {
        $psi = New-Object System.Diagnostics.ProcessStartInfo
        Set-FactoryShellCommand $psi "claude -p --model $ProbeModel --output-format stream-json --verbose --max-turns 1 2>$(Get-FactoryNullDevice)"
        $psi.WorkingDirectory = $Root
        $psi.UseShellExecute = $false
        $psi.RedirectStandardInput = $true
        $psi.RedirectStandardOutput = $true
        $p = [System.Diagnostics.Process]::Start($psi)
        $p.StandardInput.Write('Reply with the single word OK.')
        $p.StandardInput.Close()
        $read = $p.StandardOutput.ReadToEndAsync()
        if (-not $p.WaitForExit(120000)) { Stop-FactoryProcessTree -ProcessId $p.Id; return }
        $out = $read.Result
    } catch { return }
    $answered = $out -match '"type":"result"' -and $out -notmatch '"status":"rejected"' -and $out -notmatch '"is_error":true'
    if ($answered) {
        Add-LimitMark "wake $reset"
        Write-Trace '-' 'TOKENS' 'probe answered: tokens are back before the reset; waking the lanes' 'Green'
    }
}

function Wait-ForNewSession {
    # Holds this runner until just after the usage limit resets, and returns how long it
    # waited so the shift can add it back. A lone runner announces as it waits; a lane
    # leaves the announcing to the coordinator.
    # -UsageOnly: the tokens are not out, the shift is choosing to start on a fresh session
    # (-StopAtUsage). Then nothing is marked in the limit file and no probe or wake can
    # end the wait: a probe is always answered while tokens remain, so it would start the
    # shift at once, its lanes would stop on the same reading, and -Continuous would start
    # empty shift after empty shift until the reset.
    param([string]$Id, [datetime]$Until, [switch]$UsageOnly)
    $began = Get-Date
    $unix = ConvertTo-Unix $Until
    if (-not $UsageOnly) { Add-LimitMark "reset $unix" }
    # A weekly reset can be days away, so a reset on another day names the day.
    $when = $Until.ToString($(if ($Until.Date -eq (Get-Date).Date) { 'HH:mm' } else { 'ddd HH:mm' }))
    Write-Trace $Id 'tokens' "$(if ($UsageOnly) { 'saving tokens' } else { 'out of tokens' }); waiting for the new session at $when" 'Yellow'
    Set-OwnTabLabel "tokens back $when"
    Write-Heartbeat 'tokens' "new session at $(Get-UtcStamp $Until)"
    # A little past the reset, so the first request lands in the new session.
    $resume = $Until.AddSeconds(20)
    $nextTrace = (Get-Date).AddMinutes(30)
    while ((Get-Date) -lt $resume) {
        Write-HeartbeatIfDue
        # A coordinator waiting to start its shift still watches CI: a failure on the last
        # shift's commits otherwise went unfiled until the reset, hours later (BL-1031).
        if (-not $Lane -and $script:CiWatchBranch) { Invoke-CiWatch -Branch $script:CiWatchBranch }
        if ($UsageOnly) { Start-Sleep -Seconds 5; continue }
        if (Test-WakeRequested $unix) { Write-Trace $Id 'wake' 'tokens are back before the reset' 'Green'; break }
        if ($Lane) { try { $Host.UI.RawUI.WindowTitle = "Dark factory - lane $Lane out of tokens until $($Until.ToString('HH:mm'))" } catch { } }
        else { Update-LimitNotice; Invoke-LimitProbe }
        if ((Get-Date) -ge $nextTrace) {
            Write-Trace $Id 'wait' "new session in $(Format-Span ($Until - (Get-Date)))" 'DarkGray'
            $nextTrace = (Get-Date).AddMinutes(30)
        }
        Start-Sleep -Seconds 1
    }
    if ($UsageOnly) { Write-Trace $Id 'resume' 'new session; starting the shift' 'Green'; Set-OwnTabLabel ''; return ((Get-Date) - $began) }
    Add-LimitMark "resumed $unix $Id"
    Write-Trace $Id 'resume' 'new session; running the task again' 'Green'
    Set-OwnTabLabel $(if ($Id -match '^BL-') { "$Id $(Get-TaskTitle $Id)" } else { '' })
    if ($Lane) { try { $Host.UI.RawUI.WindowTitle = "Dark factory - lane $Lane" } catch { } }
    else { Update-LimitNotice }
    return ((Get-Date) - $began)
}

function Wait-ForFreshSession {
    # A shift starts on a fresh session: while the weekly window is at least
    # -StopAtWeeklyUsage used, or the 5-hour one -StopAtUsage, announce the reset and wait
    # for it - a notice, not the alarm, since running out is the plan working (BL-806).
    # Returns '' once there is room.
    $u = Get-UsageReading
    if (-not $u) { return '' }
    if ($u.Week -ge $StopAtWeeklyUsage) {
        Write-Trace '-' 'tokens' "weekly tokens $([math]::Round($u.Week * 100))% used; this shift starts when they reset, $($u.WeekResets.ToString('dddd d MMM HH:mm'))" 'Yellow'
        [void](Wait-ForNewSession -Id '-' -Until $u.WeekResets -UsageOnly)
        $u = Get-UsageReading
        if (-not $u) { return '' }
    }
    if ($u.FiveHour -lt $StopAtUsage) { return '' }
    Write-Trace '-' 'tokens' "session tokens $([math]::Round($u.FiveHour * 100))% used; this shift starts on the new session at $($u.FiveHourResets.ToString('HH:mm'))" 'Yellow'
    [void](Wait-ForNewSession -Id '-' -Until $u.FiveHourResets -UsageOnly)
    return ''
}

if ($Wake) {
    # Tells every runner of the newest shift that is waiting for tokens to carry on now.
    $file = Get-ChildItem $LogDir -Filter 'limit-*.txt' -ErrorAction SilentlyContinue | Sort-Object LastWriteTime | Select-Object -Last 1
    $resets = if ($file) { @(Get-Content $file.FullName | Where-Object { $_ -match '^reset \d+$' }) } else { @() }
    if (-not $resets.Count) { Write-Host 'No shift is waiting for tokens.'; exit 0 }
    $script:LimitFile = $file.FullName
    $LimitFile = $file.FullName
    Add-LimitMark "wake $(($resets[-1] -split ' ')[1])"
    Write-Host "Woke the shift waiting in $($file.Name)."
    exit 0
}

if ($TestOutOfTokens) {
    New-Item -ItemType Directory -Force -Path $LogDir | Out-Null
    [void](Wait-ForNewSession -Id 'TEST' -Until (Get-Date).AddSeconds(90))
    if ($script:Voice) { Start-Sleep -Seconds 6 }
    exit 0
}

# This file ran to its end; RunDarkFactory.ps1 goes on to the next one.
$AreaFileFinished = $true
