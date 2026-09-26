<#
.SYNOPSIS
    Runs an unattended "dark factory" shift over the Curl task board.

.DESCRIPTION
    Takes the next ready task, hands it to a headless Claude Code run (/task-run <ID>)
    that must never ask a question, and repeats until nothing is ready, the shift's time
    is up, or -MaxTasks is reached. Work that needs Stewart ends in Blocked with the
    question in its Reason, and the shift moves on to the next ready task.

    The terminal shows a terse, timestamped trace. The same trace goes to
    logs\DarkFactory-<stamp>.log, and each run's raw stream to logs\<ID>-<stamp>.jsonl
    (logs\ is gitignored).

    When the shift ends with anything waiting on Stewart - a Blocked task, a Backlog task
    assigned to him, a run that stalled - it fills the screen with a flashing ASCII banner
    and raises an alarm that escalates until a key is pressed:

      0-2 min    chime and "Stewart, the dark factory needs your input" every 30 s
      2-5 min    chime and the waiting tasks read aloud every 15 s
      5-15 min   siren and slower speech every 10 s; volume raised to -AlarmMaxVolume, unmuted
      15 min+    siren and speech every 5 s

    Speech is Windows' built-in System.Speech. The key press restores volume and mute.

    KEEPING THE BOARD MOVING

    Runs decide design and behaviour questions themselves (Stewart delegated them; see
    CLAUDE.md "Decisions") and block only for a new package or a threshold change. A
    task that waits on other tasks goes back to Backlog with them in `depends-on`, and a
    run that needs a project outside `touches` widens it. Before each claim the shift
    also requeues any Blocked task whose reason names only tasks that are now Done.

    OUT OF TOKENS

    When the account's usage limit refuses a run, that is not a stall. The task stays
    claimed, the shift waits for the new session and then runs the same task again,
    telling it to carry on from the partial work. Stewart is told three times, each
    with a coloured notice, a chime and one spoken sentence (screen only under
    -QuietAlarm), never the escalating alarm:

      at once              out of tokens, when the new session starts and how long until then
      -LimitWarnSeconds    before the reset: the new session is about to start
      on resuming          the new session has started, and which task it resumed

    If the limit is lifted early, the shift carries on at once: every -LimitProbeMinutes
    the coordinator (or lone runner) asks Claude for one word, and an answer wakes every
    waiting runner. After resetting the limit by hand, `RunDarkFactory.cmd -Wake` does
    the same without waiting for the next probe.

    The reset time comes from the run's rate_limit_event. Time spent waiting is added
    to the shift, so -Hours is always working time. With lanes, every lane waits on its
    own and the coordinator makes the announcements, once for all of them.

    PARALLEL LANES (-Lanes 2 or more)

    The shift runs that many lanes at once, each an independent task runner in its own
    console window and its own git worktree (..\<repo>.lanes\lane-<n>, on a local
    branch factory/lane-<n>), all feeding the branch this checkout is on:

      claim      A lane takes the next task the board offers - one whose `touches` do
                 not overlap any task in Doing - moves it to Doing, commits and pushes
                 that move. The push is the lock: if another lane got there first, the
                 push is refused and the lane picks again.
      run        /task-run in the lane's worktree. The run commits but never pushes.
      integrate  The lane rebases its commits onto the shared branch, rebuilds, runs
                 the fast tests and pushes. A conflict gets one headless run to resolve
                 it. Work that still will not integrate is pushed to its own branch,
                 factory/<ID>-lane-<n>, and the task goes to Blocked for Stewart.

    Claims and integrations hold ..\<repo>.lanes\integrate.lock, so they happen one at
    a time; runs overlap freely. This window coordinates: it starts the lanes, waits
    for them, pulls the result and raises the alarm once for all of them. Each lane
    traces to logs\DarkFactory-<stamp>-L<n>.log in this checkout.

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File RunDarkFactory.ps1
    powershell -NoProfile -ExecutionPolicy Bypass -File RunDarkFactory.ps1 -Hours 4 -MaxTasks 3
    powershell -NoProfile -ExecutionPolicy Bypass -File RunDarkFactory.ps1 -Lanes 4
    powershell -NoProfile -ExecutionPolicy Bypass -File RunDarkFactory.ps1 -TestAlarm
    powershell -NoProfile -ExecutionPolicy Bypass -File RunDarkFactory.ps1 -TestAlarm -AlarmScale 0.1
    powershell -NoProfile -ExecutionPolicy Bypass -File RunDarkFactory.ps1 -TestOutOfTokens
#>
[CmdletBinding()]
param(
    # Wall-clock length of the shift. No new task is claimed after it runs out.
    [double]$Hours = 8,
    # Stop after this many tasks. 0 means no limit.
    [int]$MaxTasks = 0,
    # A single task run is killed after this long and filed as stalled.
    [int]$TaskMinutes = 120,
    # Model for each run. "opus" is the moving alias RunClaude.cmd also uses.
    [string]$Model = $(if ($env:CLAUDE_MODEL) { $env:CLAUDE_MODEL } else { 'opus' }),
    # Show the attention banner and exit, to check it can be seen across the room.
    [switch]$TestAlarm,
    # Multiplies the alarm's stage timings; 0.1 runs the whole ladder in about 90 seconds.
    [double]$AlarmScale = 1,
    # Installed Windows voice to speak with, e.g. "Microsoft Zira Desktop". Default voice if empty.
    [string]$AlarmVoice = '',
    # Master volume, in percent, that stage 3 raises the speakers to (and unmutes).
    [ValidateRange(0, 100)][int]$AlarmMaxVolume = 100,
    # Banner only: no chime, siren, speech or volume change. For nights.
    [switch]$QuietAlarm,
    # Rehearse the out-of-tokens notices with a pretend reset 90 seconds away, and exit.
    [switch]$TestOutOfTokens,
    # How long before the usage limit resets to say the new session is about to start.
    [ValidateRange(0, 3600)][int]$LimitWarnSeconds = 60,
    # While waiting for tokens, how often to check whether the limit was lifted early. 0 = never.
    [ValidateRange(0, 600)][int]$LimitProbeMinutes = 10,
    # Wake a shift that is waiting for tokens (after resetting the limit), and exit.
    [switch]$Wake,
    # How many tasks run at once, each in its own worktree and window. 1 is the classic
    # single-runner shift in this checkout.
    [ValidateRange(1, 8)][int]$Lanes = 1,
    # Start the shift somewhere of its own and return at once: a new herdr tab when this
    # is running inside herdr, otherwise a new console window. How Claude starts a shift.
    [switch]$NewTab,

    # The rest are set by the coordinator when it starts a lane; not for direct use.
    [int]$Lane = 0,
    [string]$Branch = '',
    [string]$LogRoot = '',
    [string]$ShiftStamp = ''
)

# Continue, not Stop: native stderr from git or dotnet must never kill an unattended shift.
$ErrorActionPreference = 'Continue'

$Root = $PSScriptRoot
# The board script and every Claude run use this checkout, never one inherited from a
# Claude Code session that happened to start the shift.
$env:CLAUDE_PROJECT_DIR = $Root
$Board = Join-Path $Root '.claude\skills\task-board\task-board.ps1'
$LogDir = if ($LogRoot) { $LogRoot } else { Join-Path $Root 'logs' }
$Stamp = if ($ShiftStamp) { $ShiftStamp } else { Get-Date -Format 'yyyyMMdd-HHmmss' }
$LaneTag = if ($Lane) { "-L$Lane" } else { '' }
$TraceFile = Join-Path $LogDir "DarkFactory-$Stamp$LaneTag.log"
# Lanes live beside the checkout: Z:\repos\Curl -> Z:\repos\Curl.lanes\lane-1. A lane
# is itself one of those folders, so its lanes directory is its parent.
$LanesDir = if ($Lane) { Split-Path $Root -Parent } else { "$Root.lanes" }
$LockFile = Join-Path $LanesDir 'integrate.lock'
# Every runner of a shift records usage-limit hits here; the coordinator reads it.
$LimitFile = Join-Path $LogDir "limit-$Stamp.txt"

# ---------------------------------------------------------------------------- trace

function Write-Trace {
    param([string]$Task, [string]$Verb, [string]$Detail = '', [string]$Color = 'Gray')
    if ($Lane) { $Task = "L$Lane $Task" }
    $line = '{0} {1,-6} {2,-7} {3}' -f (Get-Date -Format 'HH:mm:ss'), $Task, $Verb, $Detail
    $line = $line.TrimEnd()
    if ($line.Length -gt 118) { $line = $line.Substring(0, 117) + '~' }
    Write-Host $line -ForegroundColor $Color
    if (Test-Path $LogDir) { Add-Content -Path $TraceFile -Value $line -Encoding UTF8 }
}

function Get-Short {
    param([string]$Text, [int]$Max = 70)
    $one = ($Text -replace '\s+', ' ').Trim()
    if ($one.Length -gt $Max) { return $one.Substring(0, $Max - 1) + '~' }
    return $one
}

# ---------------------------------------------------------------------------- herdr

function Get-HerdrBin {
    # The herdr binary when this process runs inside a herdr pane, otherwise $null.
    if ($env:HERDR_ENV -ne '1') { return $null }
    if ($env:HERDR_BIN_PATH -and (Test-Path $env:HERDR_BIN_PATH)) { return $env:HERDR_BIN_PATH }
    $cmd = Get-Command herdr -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    return $null
}

function Start-Detached {
    # Runs RunDarkFactory.ps1 from $Dir with $ScriptArgs somewhere the user can watch: a
    # new tab in the same herdr workspace when inside herdr, else a new console window.
    # Returns @{ Process = <Process> } or @{ Tab = '<tab id>' }.
    param([string]$Label, [string]$Dir, [string[]]$ScriptArgs)
    $script = Join-Path $Dir 'RunDarkFactory.ps1'
    $herdr = Get-HerdrBin
    if ($herdr) {
        $create = @('tab', 'create', '--cwd', $Dir, '--label', $Label, '--no-focus')
        if ($env:HERDR_WORKSPACE_ID) { $create += @('--workspace', $env:HERDR_WORKSPACE_ID) }
        $created = (& $herdr @create) -join "`n" | ConvertFrom-Json
        $pane = $created.result.root_pane.pane_id
        if ($pane) {
            & $herdr pane wait-output $pane --match '>' --timeout 15000 2>&1 | Out-Null
            $line = "powershell -NoProfile -ExecutionPolicy Bypass -File `"$script`" " + ($ScriptArgs -join ' ')
            # Windows PowerShell passes a native argument's inner quotes through unescaped,
            # and herdr's argument parser would then strip them, splitting any path with a
            # space. \" survives as a literal quote.
            & $herdr pane run $pane $line.Replace('"', '\"') 2>&1 | Out-Null
            return @{ Tab = $created.result.tab.tab_id }
        }
        Write-Trace '-' 'herdr' 'could not create a herdr tab; using a console window' 'DarkYellow'
    }
    $argList = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$script`"") + $ScriptArgs
    return @{ Process = (Start-Process -FilePath 'powershell.exe' -ArgumentList $argList -WorkingDirectory $Dir -PassThru) }
}

if ($NewTab) {
    # Hand this exact shift, minus -NewTab, to a tab or window of its own, and return.
    $forward = @()
    foreach ($p in $PSBoundParameters.GetEnumerator()) {
        if ($p.Key -eq 'NewTab') { continue }
        if ($p.Value -is [System.Management.Automation.SwitchParameter]) { if ($p.Value) { $forward += "-$($p.Key)" } }
        else { $forward += @("-$($p.Key)", "`"$($p.Value)`"") }
    }
    $where = Start-Detached -Label "Dark factory - $(Split-Path $Root -Leaf)" -Dir $Root -ScriptArgs $forward
    if ($where.Tab) { Write-Host "Dark factory started in herdr tab $($where.Tab)." }
    else { Write-Host "Dark factory started in a new console window (pid $($where.Process.Id))." }
    exit 0
}

# ---------------------------------------------------------------------------- alarm

$Glyphs = @{
    'S' = @(' ####', '#    ', ' ### ', '    #', '#### ')
    'T' = @('#####', '  #  ', '  #  ', '  #  ', '  #  ')
    'E' = @('#####', '#    ', '#### ', '#    ', '#####')
    'W' = @('#   #', '#   #', '# # #', '## ##', '#   #')
    'A' = @(' ### ', '#   #', '#####', '#   #', '#   #')
    'R' = @('#### ', '#   #', '#### ', '#  # ', '#   #')
    'I' = @('#####', '  #  ', '  #  ', '  #  ', '#####')
    'N' = @('#   #', '##  #', '# # #', '#  ##', '#   #')
    'P' = @('#### ', '#   #', '#### ', '#    ', '#    ')
    'U' = @('#   #', '#   #', '#   #', '#   #', ' ### ')
    'D' = @('#### ', '#   #', '#   #', '#   #', '#### ')
    '!' = @('  #  ', '  #  ', '  #  ', '     ', '  #  ')
    ' ' = @('   ', '   ', '   ', '   ', '   ')
}

function Get-BigText {
    param([string]$Text)
    foreach ($row in 0..4) {
        (($Text.ToCharArray() | ForEach-Object { $Glyphs[[string]$_][$row] }) -join ' ')
    }
}

function Show-Banner {
    param([string[]]$Reasons, [int]$Frame)
    $colors = @(@('White', 'DarkRed'), @('Black', 'Yellow'))
    $fg, $bg = $colors[$Frame % 2]
    $width = 78
    try { Clear-Host } catch { }
    $lines = @('') + (Get-BigText 'STEWART!') + @('') + (Get-BigText 'INPUT NEEDED') + @('')
    $lines += '  The dark factory is waiting on you.  ' + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss')
    $lines += ''
    $lines += ($Reasons | Select-Object -First 8 | ForEach-Object { '  ' + (Get-Short $_ 74) })
    $lines += ''
    $lines += '  Press any key to silence.  Trace: logs\' + (Split-Path $TraceFile -Leaf)
    $lines += ''
    foreach ($l in $lines) { Write-Host ('  ' + $l).PadRight($width) -ForegroundColor $fg -BackgroundColor $bg }
}

# Core Audio, for raising and restoring the master volume from stage 3. Windows ships it;
# nothing is installed.
$script:AudioReady = $false
function Initialize-Audio {
    if ($script:AudioReady) { return $true }
    try {
        Add-Type -ErrorAction Stop -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
namespace DarkFactory {
[Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IAudioEndpointVolume {
    int NotImpl1(); int NotImpl2(); int NotImpl3(); int NotImpl4();
    int SetMasterVolumeLevelScalar(float level, Guid context);
    int NotImpl5();
    int GetMasterVolumeLevelScalar(out float level);
    int NotImpl6(); int NotImpl7(); int NotImpl8(); int NotImpl9();
    int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, Guid context);
    int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
}
[Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IMMDevice { int Activate(ref Guid id, int clsCtx, IntPtr parameters, [MarshalAs(UnmanagedType.IUnknown)] out object endpoint); }
[Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IMMDeviceEnumerator { int NotImpl1(); int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device); }
[ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")] class MMDeviceEnumerator { }
public static class Audio {
    static IAudioEndpointVolume Endpoint() {
        var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
        IMMDevice device;
        Marshal.ThrowExceptionForHR(enumerator.GetDefaultAudioEndpoint(0, 1, out device));
        Guid iid = typeof(IAudioEndpointVolume).GUID;
        object endpoint;
        Marshal.ThrowExceptionForHR(device.Activate(ref iid, 23, IntPtr.Zero, out endpoint));
        return (IAudioEndpointVolume)endpoint;
    }
    public static float Volume {
        get { float v; Marshal.ThrowExceptionForHR(Endpoint().GetMasterVolumeLevelScalar(out v)); return v; }
        set { Marshal.ThrowExceptionForHR(Endpoint().SetMasterVolumeLevelScalar(value, Guid.Empty)); }
    }
    public static bool Mute {
        get { bool m; Marshal.ThrowExceptionForHR(Endpoint().GetMute(out m)); return m; }
        set { Marshal.ThrowExceptionForHR(Endpoint().SetMute(value, Guid.Empty)); }
    }
}
}
'@
        $script:AudioReady = $true
    } catch { $script:AudioReady = $false }
    return $script:AudioReady
}

$script:Voice = $null
function Get-Voice {
    if ($script:Voice) { return $script:Voice }
    try {
        Add-Type -AssemblyName System.Speech -ErrorAction Stop
        $v = New-Object System.Speech.Synthesis.SpeechSynthesizer
        $v.SetOutputToDefaultAudioDevice()
        if ($AlarmVoice) { try { $v.SelectVoice($AlarmVoice) } catch { } }
        $script:Voice = $v
    } catch { $script:Voice = $null }
    return $script:Voice
}

function Get-Spoken {
    # "BL-004 DECIDE   Decide the licence - keep ..." -> "B L 0 0 4. Decide the licence"
    param([string]$Reason)
    $text = ($Reason -split '\s{2,}', 2)[-1]
    $text = ($text -split ' - ')[0]
    $text = $text -replace '^\d{4}-\d{2}-\d{2}:\s*\w+\s*->\s*\w+\.\s*', '' -replace '^Stewart:\s*', ''
    $id = ''
    if ($Reason -match '^BL-(\d)(\d)(\d)') { $id = "B L $($Matches[1]) $($Matches[2]) $($Matches[3]). " }
    return $id + $text
}

function Get-AlarmSpeech {
    param([string[]]$Reasons, [int]$Stage)
    if ($Stage -eq 0) { return 'Stewart, the dark factory needs your input.' }
    $n = $Reasons.Count
    $what = if ($n -eq 1) { 'One item is' } else { "$n items are" }
    $first = ($Reasons | Select-Object -First 2 | ForEach-Object { Get-Spoken $_ }) -join '. Then, '
    if ($Stage -eq 1) { return "Stewart. $what waiting on you. $first." }
    return "Stewart! Stewart! The dark factory has stopped. $what waiting on you. $first. Press any key at the terminal."
}

function Invoke-Chime { try { [Console]::Beep(880, 300); [Console]::Beep(660, 300); [Console]::Beep(880, 450) } catch { } }

function Invoke-Siren {
    param([int]$Sweeps)
    try {
        foreach ($s in 1..$Sweeps) {
            foreach ($f in 600, 800, 1000, 1200, 1400) { [Console]::Beep($f, 70) }
            foreach ($f in 1400, 1200, 1000, 800, 600) { [Console]::Beep($f, 70) }
        }
    } catch { }
}

$script:SavedVolume = $null
$script:SavedMute = $false
function Set-AlarmVolume {
    # Remembers the listener's volume and mute once, then goes to -AlarmMaxVolume.
    if (-not (Initialize-Audio)) { return }
    try {
        if ($null -eq $script:SavedVolume) { $script:SavedVolume = [DarkFactory.Audio]::Volume; $script:SavedMute = [DarkFactory.Audio]::Mute }
        [DarkFactory.Audio]::Mute = $false
        [DarkFactory.Audio]::Volume = [float]($AlarmMaxVolume / 100.0)
    } catch { }
}

function Restore-AlarmVolume {
    if ($null -eq $script:SavedVolume) { return }
    try { [DarkFactory.Audio]::Volume = $script:SavedVolume; [DarkFactory.Audio]::Mute = $script:SavedMute } catch { }
    $script:SavedVolume = $null
}

function Invoke-AlarmSound {
    param([string[]]$Reasons, [int]$Stage)
    if ($QuietAlarm) { return }
    if ($Stage -ge 2) { Set-AlarmVolume }
    switch ($Stage) { 0 { Invoke-Chime } 1 { Invoke-Chime } 2 { Invoke-Siren 2 } default { Invoke-Siren 3 } }
    $voice = Get-Voice
    if ($voice) {
        $voice.SpeakAsyncCancelAll()
        $voice.Volume = 100
        $voice.Rate = if ($Stage -ge 2) { -2 } else { 0 }
        [void]$voice.SpeakAsync((Get-AlarmSpeech -Reasons $Reasons -Stage $Stage))
    }
}

function Test-KeyPressed {
    # $null when there is no console to read (output redirected): the caller gives up.
    try {
        if ([Console]::KeyAvailable) { [void][Console]::ReadKey($true); return $true }
        return $false
    } catch { return $null }
}

function Invoke-Alarm {
    param([string[]]$Reasons)
    try { $Host.UI.RawUI.WindowTitle = '!!! STEWART - INPUT NEEDED !!!' } catch { }
    # Stage starts (minutes) and sound intervals (seconds); -AlarmScale shrinks both for a test.
    $starts = @(0, 2, 5, 15) | ForEach-Object { $_ * 60 * $AlarmScale }
    $every = @(30, 15, 10, 5) | ForEach-Object { [math]::Max(3, $_ * $AlarmScale) }
    $began = Get-Date
    $nextSound = $began
    $stage = -1
    $frame = 0
    try {
        while ($true) {
            $elapsed = ((Get-Date) - $began).TotalSeconds
            $now = 0
            foreach ($i in 0..3) { if ($elapsed -ge $starts[$i]) { $now = $i } }
            if ($now -ne $stage) { $stage = $now; Write-Trace '-' 'ALARM' "stage $($stage + 1) of 4"; $nextSound = Get-Date }
            Show-Banner -Reasons $Reasons -Frame $frame
            $frame++
            if ((Get-Date) -ge $nextSound) {
                Invoke-AlarmSound -Reasons $Reasons -Stage $stage
                $nextSound = (Get-Date).AddSeconds($every[$stage])
            }
            $key = Test-KeyPressed
            if ($null -eq $key) { Start-Sleep -Seconds 6; return }
            if ($key) { Write-Trace '-' 'ALARM' 'acknowledged'; return }
            Start-Sleep -Milliseconds 1000
        }
    } finally {
        if ($script:Voice) { try { $script:Voice.SpeakAsyncCancelAll() } catch { } }
        Restore-AlarmVolume
    }
}

if ($TestAlarm) {
    Invoke-Alarm -Reasons @(
        'BL-004 DECIDE   Decide the licence - keep GPL-3.0 or relicense to MIT/Apache-2.0',
        'BL-005 DECIDE   Decide how curl''s upstream test cases are driven from .NET')
    exit 0
}

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

function Get-OutOfTokensUntil {
    # When the last run was refused for the account's usage limit, the local time the
    # limit resets; otherwise $null. The run's rate_limit_event gives it exactly; the
    # result's text is the fallback: "You've hit your session limit · resets 12:50pm".
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
    if ($QuietAlarm) { return }
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
            "Stewart, the dark factory is out of tokens. The new session starts at $($reset.ToString('h:mm tt')), in $(Format-SpokenSpan $left)." 'Yellow'
    }
    $resumed = @($lines | Where-Object { $_ -match "^resumed $latest \S+$" } | ForEach-Object { ($_ -split ' ')[2] })
    if (-not $n.Resumed -and $resumed.Count) {
        $n.Resumed = $true; $n.Warned = $true
        Show-LimitNotice 'NEW SESSION STARTED' "Started using the new session at $(Get-Date -Format 'HH:mm'); resuming $($resumed[0])." `
            "Stewart, the new session has started. The dark factory is working again." 'Green'
        try { $Host.UI.RawUI.WindowTitle = if ($Lanes -gt 1) { "Dark factory - $Lanes lanes" } else { 'Dark factory - running' } } catch { }
        return
    }
    if ($n.Resumed) { return }
    $left = $reset - (Get-Date)
    if (-not $n.Warned -and $left.TotalSeconds -le $LimitWarnSeconds) {
        $n.Warned = $true
        Show-LimitNotice 'NEW SESSION SOON' "The new session starts at $at, in $(Format-Span $left)." `
            "Stewart, the new session will be ready in about $(Format-SpokenSpan $left)." 'Cyan'
    }
    try { $Host.UI.RawUI.WindowTitle = "Dark factory - out of tokens, new session at $at (in $(Format-Span $left))" } catch { }
}

function Test-WakeRequested {
    # True once "wake <unix>" for this reset is in the limit file: tokens came back early,
    # because Stewart reset the limit or the probe found the account answering again.
    param([long]$Unix)
    if (-not (Test-Path $LimitFile)) { return $false }
    return [bool](@(Get-Content $LimitFile -ErrorAction SilentlyContinue) -contains "wake $Unix")
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
        $psi.FileName = $env:ComSpec
        $psi.Arguments = "/d /c claude -p --model $Model --output-format stream-json --verbose --max-turns 1 2>nul"
        $psi.WorkingDirectory = $Root
        $psi.UseShellExecute = $false
        $psi.RedirectStandardInput = $true
        $psi.RedirectStandardOutput = $true
        $p = [System.Diagnostics.Process]::Start($psi)
        $p.StandardInput.Write('Reply with the single word OK.')
        $p.StandardInput.Close()
        $read = $p.StandardOutput.ReadToEndAsync()
        if (-not $p.WaitForExit(120000)) { & taskkill /T /F /PID $p.Id 2>&1 | Out-Null; return }
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
    param([string]$Id, [datetime]$Until)
    $began = Get-Date
    $unix = ConvertTo-Unix $Until
    Add-LimitMark "reset $unix"
    Write-Trace $Id 'tokens' "out of tokens; waiting for the new session at $($Until.ToString('HH:mm'))" 'Yellow'
    # A little past the reset, so the first request lands in the new session.
    $resume = $Until.AddSeconds(20)
    $nextTrace = (Get-Date).AddMinutes(30)
    while ((Get-Date) -lt $resume) {
        if (Test-WakeRequested $unix) { Write-Trace $Id 'wake' 'tokens are back before the reset' 'Green'; break }
        if ($Lane) { try { $Host.UI.RawUI.WindowTitle = "Dark factory - lane $Lane out of tokens until $($Until.ToString('HH:mm'))" } catch { } }
        else { Update-LimitNotice; Invoke-LimitProbe }
        if ((Get-Date) -ge $nextTrace) {
            Write-Trace $Id 'wait' "new session in $(Format-Span ($Until - (Get-Date)))" 'DarkGray'
            $nextTrace = (Get-Date).AddMinutes(30)
        }
        Start-Sleep -Seconds 1
    }
    Add-LimitMark "resumed $unix $Id"
    Write-Trace $Id 'resume' 'new session; running the task again' 'Green'
    if ($Lane) { try { $Host.UI.RawUI.WindowTitle = "Dark factory - lane $Lane" } catch { } }
    else { Update-LimitNotice }
    return ((Get-Date) - $began)
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

function Get-LastLogLine {
    param([string]$Id)
    $file = Get-ChildItem (Join-Path $Root 'Tasks') -Recurse -Filter "$Id-*.md" | Select-Object -First 1
    if (-not $file) { return '' }
    $log = Get-Content $file.FullName | Where-Object { $_ -match '^\s*-\s' } | Select-Object -Last 1
    return ("$log" -replace '^\s*-\s*', '')
}

function Get-WaitingOnStewart {
    $reasons = @()
    foreach ($f in Get-ChildItem (Join-Path $Root 'Tasks\Blocked') -Filter 'BL-*.md' -ErrorAction SilentlyContinue) {
        $id = $f.Name.Substring(0, 6)
        $reasons += "$id BLOCKED  $(Get-LastLogLine $id)"
    }
    foreach ($line in Invoke-Board @('status')) {
        if ($line -match '^\s+(BL-\d{3})\s.*\[needs Stewart\]') {
            $reasons += "$($Matches[1]) DECIDE   $(Get-TaskTitle $Matches[1])"
        }
    }
    return $reasons
}

function Test-TaskDone {
    param([string]$Id)
    return [bool](Get-ChildItem (Join-Path $Root 'Tasks\Done') -Recurse -Filter "$Id-*.md" -ErrorAction SilentlyContinue)
}

function Invoke-Requeue {
    # Moves back to Backlog every Blocked task whose blocker was only other tasks that are
    # now all Done: a last Log line that names BL-### IDs and is not a question for
    # Stewart. Returns the IDs it moved.
    $moved = @()
    foreach ($f in Get-ChildItem (Join-Path $Root 'Tasks\Blocked') -Filter 'BL-*.md' -ErrorAction SilentlyContinue) {
        $id = $f.Name.Substring(0, 6)
        $reason = Get-LastLogLine $id
        if ($reason -match 'Stewart') { continue }
        $waits = @([regex]::Matches($reason, 'BL-\d{3}') | ForEach-Object { $_.Value } | Where-Object { $_ -ne $id } | Select-Object -Unique)
        if (-not $waits.Count -or @($waits | Where-Object { -not (Test-TaskDone $_) }).Count) { continue }
        Invoke-Board @('move', '-Id', $id, '-To', 'Backlog', '-Reason', "Unblocked: $($waits -join ', ') now Done") | Out-Null
        if ((Get-TaskState $id) -eq 'Backlog') { $moved += $id; Write-Trace $id 'requeue' "unblocked: $($waits -join ', ') Done" 'Cyan' }
    }
    return $moved
}

# ---------------------------------------------------------------------------- git

function Get-Dirty { return @(git -C $Root status --porcelain) | Where-Object { $_ } }

function Save-StrayChanges {
    param([string]$Id)
    if (-not (Get-Dirty)) { return }
    git -C $Root stash push --include-untracked -m "darkfactory $Id $Stamp" | Out-Null
    Write-Trace $Id 'stash' "uncommitted work kept: git stash list" 'Yellow'
}

# ---------------------------------------------------------------------------- run

$Prompt = @'
DARK FACTORY SHIFT. Stewart is away and cannot answer. Never ask a question and never
wait for input; nobody will reply.

Run /task-run {ID}.

Rules for this unattended run, in addition to CLAUDE.md:
1. Where a choice has a sensible default, take it and record the choice and why under
   the task's Notes.
2. Design and behaviour decisions are yours: Stewart has delegated them (CLAUDE.md,
   "Decisions"). Decide by his standing rules - match the platform's curl, measure real
   curl before pinning output, BCL only - record the decision and why in an ADR marked
   "Decided by Claude under Stewart's delegation", and carry on. Only a new package or
   a quality-threshold change goes to Blocked, with a -Reason that starts "Stewart:" and
   asks the question in one line.
3. If the only thing stopping the task is other work - an existing task, or one you
   file with the board script - add those IDs to its `depends-on` and move it to
   Backlog, not Blocked, with a -Reason naming them. The board starts it again once
   they are Done.
4. When the task reaches Done with dotnet build clean and the fast tests green, commit
   by logical unit (Conventional Commits, including the task file) and push the current
   branch yourself with git, per the standing authorization in CLAUDE.md. Never push to
   master, never force push, never merge.
5. If the task ends Blocked or back in Backlog, commit only the task board change and
   push it. Leave any unfinished code uncommitted; the shift stashes it.
6. The task must not be left in Doing.

End your reply with exactly one line, either
FACTORY: DONE {ID} <what now works>
or
FACTORY: BLOCKED {ID} <the blocker>
'@

# Put in front of either prompt when a task runs again after the usage limit.
$ResumeNote = @'
RESUMING. The previous run of {ID} was cut off when the account ran out of tokens, before
the task was finished. Whatever that run had done is still here: read git status, git log
and the task file before changing anything, and carry on from that work rather than
starting over.

'@

# A lane's run: the shift has claimed the task already, and the shift - not the run -
# integrates and pushes, so parallel lanes never race each other to the shared branch.
$LanePrompt = @'
DARK FACTORY SHIFT, LANE {LANE}. Stewart is away and cannot answer. Never ask a question
and never wait for input; nobody will reply. Other lanes are working other tasks in
other checkouts at the same time; the task board guarantees their tasks touch different
projects from yours.

Run /task-run {ID}. The shift has already claimed {ID}: it is in Tasks/Doing. Skip the
claim step, do not move it to Doing again, and do not take any other task.

Rules for this unattended run, in addition to CLAUDE.md:
1. Where a choice has a sensible default, take it and record the choice and why under
   the task's Notes.
2. Design and behaviour decisions are yours: Stewart has delegated them (CLAUDE.md,
   "Decisions"). Decide by his standing rules - match the platform's curl, measure real
   curl before pinning output, BCL only - record the decision and why in an ADR marked
   "Decided by Claude under Stewart's delegation", and carry on. Only a new package or
   a quality-threshold change goes to Blocked, with a -Reason that starts "Stewart:" and
   asks the question in one line.
3. Stay inside the projects and files the task's `touches` field names. If the work
   truly needs another one, read the `touches` of every task in Tasks/Doing. When none
   of them names it, add it to this task's `touches`, say why under Notes, and carry
   on. When one does, add it anyway and move the task to Backlog with a -Reason naming
   the project and that task; the board will not offer it again until they no longer
   overlap.
4. If the only thing stopping the task is other work - an existing task, or one you
   file with the board script - add those IDs to its `depends-on` and move it to
   Backlog, not Blocked, with a -Reason naming them. The board starts it again once
   they are Done. Blocked is only for what needs Stewart.
5. When the task reaches Done with dotnet build clean and the fast tests green, commit
   by logical unit (Conventional Commits, including the task file). Do NOT push, pull,
   rebase, merge or switch branches: the shift integrates your commits.
6. If the task ends Blocked or back in Backlog, commit only the task board change.
   Leave any unfinished code uncommitted; the shift stashes it.
7. The task must not be left in Doing.

End your reply with exactly one line, either
FACTORY: DONE {ID} <what now works>
or
FACTORY: BLOCKED {ID} <the blocker>
'@

$ResolvePrompt = @'
DARK FACTORY SHIFT, LANE {LANE}. Stewart is away; never ask a question.

This checkout is in the middle of `git rebase origin/{BRANCH}`: this lane's commits for
task {ID} are being replayed on top of work other lanes pushed meanwhile, and git
stopped on conflicts. Resolve them:

1. `git status` to see the conflicted files. For each, keep BOTH sides' intent: the
   other lanes' work is already shared and must survive, and this lane's change must
   still do what its commit says. Never resolve by discarding one side wholesale.
2. Files under Tasks/: a task file that one side moved and the other edited keeps the
   move and the edits. Curl.slnx and other lists: keep every entry from both sides.
3. `git add` the resolved files, then `git -c core.editor=true rebase --continue`.
   Repeat until the rebase finishes.
4. Run `dotnet build` and `dotnet test --filter "TestCategory!=Integration"`; fix what
   the merge broke, and commit the fix.
5. Never run git rebase --abort, git reset, git push, or git checkout of another branch.

End your reply with exactly one line, either
FACTORY: RESOLVED {ID}
or
FACTORY: UNRESOLVED {ID} <why>
'@

$script:ToolLabels = @{}

# Commands only Stewart may authorize (CLAUDE.md). --dangerously-skip-permissions does
# not enforce "ask" rules, so each run gets these as a hard --disallowedTools deny.
$Forbidden = @(
    'git push --force', 'git push -f', 'git push --force-with-lease',
    'git push origin master', 'git push origin HEAD:master', 'git merge',
    'git tag', 'git branch -D', 'git branch -d', 'git reset --hard',
    'dotnet add package', 'dotnet remove package',
    'gh pr merge', 'gh release', 'gh repo'
)
# A lane's run also never touches the remote or the branch: the shift owns both.
$LaneForbidden = $Forbidden + @('git push', 'git pull', 'git fetch', 'git rebase', 'git checkout', 'git switch', 'git worktree', 'git stash')
# The conflict resolver needs `git rebase --continue`, and nothing that throws work away.
$ResolveForbidden = $Forbidden + @('git push', 'git pull', 'git rebase --abort', 'git rebase --skip', 'git checkout', 'git switch', 'git worktree', 'git stash')

function Get-ToolLabel {
    param($Tool)
    $in = $Tool.input
    switch ($Tool.name) {
        'Agent' { return @('agent', "$($in.subagent_type): $($in.description)") }
        'Task' { return @('agent', "$($in.subagent_type): $($in.description)") }
        'Skill' { return @('skill', "$($in.skill) $($in.args)") }
        'Edit' { return @('edit', (Split-Path "$($in.file_path)" -Leaf)) }
        'Write' { return @('write', (Split-Path "$($in.file_path)" -Leaf)) }
        { $_ -in 'Bash', 'PowerShell' } {
            $c = "$($in.command)"
            if ($c -match 'task-board\.ps1\s+move\b.*-To\s+(\w+)') { return @('move', $Matches[1]) }
            if ($c -match 'dotnet\s+build') { return @('build', '') }
            if ($c -match 'dotnet\s+test') { return @('test', '') }
            if ($c -match 'dotnet\s+format') { return @('format', '') }
            if ($c -match 'Measure-CodeQuality') { return @('quality', '') }
            if ($c -match 'git\s+commit') { return @('commit', '') }
            if ($c -match 'git\s+push') { return @('push', '') }
        }
    }
    return $null
}

function Get-ResultText {
    param($Content)
    if ($Content -is [string]) { return $Content }
    return (@($Content) | ForEach-Object { if ($_.PSObject.Properties['text']) { $_.text } }) -join "`n"
}

function Get-Outcome {
    param([string]$Verb, [string]$Text, [bool]$IsError)
    if ($Verb -eq 'test') {
        $t = [regex]::Matches($Text, 'total:\s*(\d+),\s*failed:\s*(\d+)', 'IgnoreCase')
        if ($t.Count -eq 0) { $t = [regex]::Matches($Text, 'Failed:\s*(\d+),\s*Passed:\s*(\d+)') ; $swap = $true } else { $swap = $false }
        if ($t.Count -gt 0) {
            $total = 0; $failed = 0
            foreach ($m in $t) {
                if ($swap) { $failed += [int]$m.Groups[1].Value; $total += [int]$m.Groups[1].Value + [int]$m.Groups[2].Value }
                else { $total += [int]$m.Groups[1].Value; $failed += [int]$m.Groups[2].Value }
            }
            return "$($total - $failed)/$total pass"
        }
    }
    if ($Verb -eq 'commit' -and $Text -match '\[[^\]]*\s([0-9a-f]{7,})\]\s*(.*)') { return "$($Matches[1]) $($Matches[2])" }
    if ($Verb -eq 'build' -and $Text -match '(\d+)\s+Error\(s\)' -and [int]$Matches[1] -gt 0) { return "FAIL $($Matches[1]) error(s)" }
    if ($IsError) { return 'FAIL ' + (Get-Short (($Text -split "`n" | Where-Object { $_.Trim() } | Select-Object -Last 1)) 50) }
    return 'ok'
}

function Write-Event {
    param([string]$Id, $Evt)
    switch ($Evt.type) {
        'assistant' {
            foreach ($c in @($Evt.message.content)) {
                if ($c.type -ne 'tool_use') { continue }
                $label = Get-ToolLabel $c
                if (-not $label) { continue }
                $script:ToolLabels[$c.id] = $label
                if ($label[0] -in 'agent', 'skill', 'edit', 'write') { Write-Trace $Id $label[0] (Get-Short $label[1]) }
            }
        }
        'user' {
            foreach ($c in @($Evt.message.content)) {
                if ($c.type -ne 'tool_result' -or -not $script:ToolLabels.ContainsKey($c.tool_use_id)) { continue }
                $verb, $detail = $script:ToolLabels[$c.tool_use_id]
                if ($verb -in 'agent', 'skill', 'edit', 'write') { continue }
                $isErr = [bool]($c.PSObject.Properties['is_error'] -and $c.is_error)
                $outcome = Get-Outcome $verb (Get-ResultText $c.content) $isErr
                $color = if ($outcome -like 'FAIL*') { 'Red' } else { 'Gray' }
                if ($verb -eq 'move') { $outcome = "$detail $outcome" }
                Write-Trace $Id $verb $outcome $color
            }
        }
        'rate_limit_event' {
            $info = $Evt.rate_limit_info
            if ($info.status -eq 'rejected' -and $info.resetsAt) { $script:LimitResetAt = ConvertFrom-Unix ([long]$info.resetsAt) }
        }
        'result' {
            $mins = [math]::Round($Evt.duration_ms / 60000, 1)
            $script:RunResult = $Evt
            Write-Trace $Id 'end' "$($Evt.num_turns) turns, $mins min"
        }
    }
}

function Invoke-TaskRun {
    # One headless Claude run in this checkout. By default it is the task run; a lane
    # passes its own prompt and deny list, and a log suffix for the resolver's run.
    # -Resume is the task run again after the usage limit cut the last one off.
    param([string]$Id, [string]$Text = '', [string[]]$Deny = $null, [string]$Suffix = '', [int]$Minutes = 0, [switch]$Resume)
    if (-not $Text) { $Text = if ($Lane) { $LanePrompt } else { $Prompt } }
    if ($Resume) { $Text = $ResumeNote + $Text; $Suffix += '-resumed' }
    if ($null -eq $Deny) { $Deny = if ($Lane) { $LaneForbidden } else { $Forbidden } }
    if ($Minutes -le 0) { $Minutes = $TaskMinutes }
    $Text = $Text.Replace('{ID}', $Id).Replace('{LANE}', "$Lane").Replace('{BRANCH}', $Branch)
    $raw = Join-Path $LogDir "$Id-$Stamp$LaneTag$Suffix.jsonl"
    $err = Join-Path $LogDir "$Id-$Stamp$LaneTag$Suffix.err.txt"
    $script:RunResult = $null
    $script:LimitResetAt = $null
    $script:ToolLabels = @{}

    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $env:ComSpec
    $denied = ($Deny | ForEach-Object { "`"Bash($_`:*)`" `"PowerShell($_`:*)`"" }) -join ' '
    $psi.Arguments = "/d /c claude -p --model $Model --dangerously-skip-permissions --output-format stream-json --verbose --disallowedTools $denied 2>`"$err`""
    $psi.WorkingDirectory = $Root
    $psi.UseShellExecute = $false
    $psi.RedirectStandardInput = $true
    $psi.RedirectStandardOutput = $true
    $psi.StandardOutputEncoding = [System.Text.Encoding]::UTF8
    $p = [System.Diagnostics.Process]::Start($psi)
    $p.StandardInput.Write($Text)
    $p.StandardInput.Close()

    $deadline = (Get-Date).AddMinutes($Minutes)
    $timedOut = $false
    $pending = $p.StandardOutput.ReadLineAsync()
    while ($true) {
        if ($pending.Wait(1000)) {
            $line = $pending.Result
            if ($null -eq $line) { break }
            Add-Content -Path $raw -Value $line -Encoding UTF8
            try { $evt = $line | ConvertFrom-Json } catch { $evt = $null }
            if ($evt) { Write-Event $Id $evt }
            $pending = $p.StandardOutput.ReadLineAsync()
        } elseif ((Get-Date) -gt $deadline) {
            & taskkill /T /F /PID $p.Id 2>&1 | Out-Null
            $timedOut = $true
            break
        }
    }
    $p.WaitForExit()
    return @{ ExitCode = $p.ExitCode; TimedOut = $timedOut }
}

# ---------------------------------------------------------------------------- lanes

function Invoke-Git {
    # git in this checkout; returns $true when it exited 0. Output goes to the trace only
    # on failure, so a quiet lane stays quiet.
    param([string[]]$GitArgs)
    $out = & git -C $Root @GitArgs 2>&1
    if ($LASTEXITCODE -ne 0) {
        $last = ($out | ForEach-Object { "$_" } | Where-Object { $_.Trim() } | Select-Object -Last 1)
        Write-Trace '-' 'git' "FAIL git $($GitArgs[0]): $(Get-Short "$last" 60)" 'DarkYellow'
        return $false
    }
    return $true
}

function Enter-Lock {
    # Claims and integrations take turns across every lane. Holding the file open with no
    # sharing is the lock; the operating system releases it if the lane dies.
    New-Item -ItemType Directory -Force -Path $LanesDir | Out-Null
    while ($true) {
        try { return [IO.File]::Open($LockFile, 'OpenOrCreate', 'ReadWrite', 'None') }
        catch { Start-Sleep -Seconds 3 }
    }
}

function Sync-Lane {
    # Puts this lane's checkout exactly on the shared branch as it is on the remote.
    Save-StrayChanges 'sync'
    if (-not (Invoke-Git @('fetch', '-q', 'origin', $Branch))) { return $false }
    if (-not (Invoke-Git @('checkout', '-q', '-B', "factory/lane-$Lane", "origin/$Branch"))) { return $false }
    return (Invoke-Git @('reset', '-q', '--hard', "origin/$Branch"))
}

function Test-Rebasing {
    foreach ($dir in 'rebase-merge', 'rebase-apply') {
        $path = (& git -C $Root rev-parse --git-path $dir).Trim()
        if (-not [IO.Path]::IsPathRooted($path)) { $path = Join-Path $Root $path }
        if (Test-Path $path) { return $true }
    }
    return $false
}

function Get-DoingCount { return @(Get-ChildItem (Join-Path $Root 'Tasks\Doing') -Filter 'BL-*.md' -ErrorAction SilentlyContinue).Count }

function Invoke-Claim {
    # Returns @{ Id = 'BL-###' } on success, or @{ Wait = $true } when every ready task
    # overlaps work in progress (or nothing is ready but other lanes may unlock more), or
    # @{ None = $true } when the board has nothing left for this shift.
    param([string[]]$Skip)
    $lock = Enter-Lock
    try {
        foreach ($attempt in 1..5) {
            if (-not (Sync-Lane)) { Start-Sleep -Seconds 10; continue }
            $requeued = @(Invoke-Requeue)
            if ($requeued.Count) {
                Invoke-Git @('add', '-A', 'Tasks') | Out-Null
                Invoke-Git @('commit', '-q', '-m', "chore(tasks): requeue $($requeued -join ', ') - blockers Done") | Out-Null
                if (-not (Invoke-Git @('push', '-q', 'origin', "HEAD:$Branch"))) { continue }
            }
            $boardArgs = @('next')
            if ($Skip.Count) { $boardArgs += @('-Skip', ($Skip -join ',')) }
            $next = (Invoke-Board $boardArgs) -join "`n"
            if ($next -notmatch '(?m)^(BL-\d{3})\s') {
                if ($next -match 'can start yet' -or (Get-DoingCount) -gt 0) { return @{ Wait = $true; Why = (Get-Short $next 80) } }
                return @{ None = $true }
            }
            $id = $Matches[1]
            Invoke-Board @('move', '-Id', $id, '-To', 'Doing') | Out-Null
            if ((Get-TaskState $id) -ne 'Doing') { continue }
            Invoke-Git @('add', '-A', 'Tasks') | Out-Null
            Invoke-Git @('commit', '-q', '-m', "chore(tasks): claim $id on dark factory lane $Lane") | Out-Null
            if (Invoke-Git @('push', '-q', 'origin', "HEAD:$Branch")) { return @{ Id = $id } }
            Write-Trace $id 'claim' 'lost the race; picking again' 'DarkYellow'
        }
        return @{ Wait = $true; Why = 'claim kept losing races' }
    } finally { $lock.Dispose() }
}

function Test-Green {
    # Build and fast tests in this checkout, after a rebase put other lanes' work under ours.
    & dotnet build $Root -nologo -v q 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) { return 'build failed' }
    & dotnet test $Root --no-build -nologo -v q --filter 'TestCategory!=Integration' 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) { return 'fast tests failed' }
    return ''
}

function Invoke-Integrate {
    # Rebases this lane's commits onto the shared branch, checks them, and pushes. Returns
    # '' on success or why it could not.
    param([string]$Id, [string]$State)
    $lock = Enter-Lock
    try {
        foreach ($attempt in 1..3) {
            if (-not (Invoke-Git @('fetch', '-q', 'origin', $Branch))) { Start-Sleep -Seconds 10; continue }
            if (-not (Invoke-Git @('rebase', '-q', "origin/$Branch"))) {
                if (Test-Rebasing) {
                    Write-Trace $Id 'resolve' 'rebase conflict; resolving' 'DarkYellow'
                    Invoke-TaskRun -Id $Id -Text $ResolvePrompt -Deny $ResolveForbidden -Suffix '-resolve' -Minutes 45 | Out-Null
                    if (Test-Rebasing) { & git -C $Root rebase --abort 2>&1 | Out-Null; return 'rebase conflict the resolver could not settle' }
                } else { return 'rebase failed' }
            }
            # Lanes number the tasks they file from their own copy of the board, so two
            # lanes can file the same ID. The other lane's is already shared and keeps it;
            # ours are renumbered, with our own references, before anyone else sees them.
            $dedupe = (Invoke-Board @('dedupe', '-Since', "origin/$Branch")) | Where-Object { $_ -match '->' }
            if ($dedupe) {
                Invoke-Git @('add', '-A') | Out-Null
                Invoke-Git @('commit', '-q', '-m', "chore(tasks): renumber task IDs another lane took first`n`n$($dedupe -join "`n")") | Out-Null
                foreach ($line in $dedupe) { Write-Trace $Id 'renum' $line 'DarkYellow' }
            }
            if ($State -eq 'Done') {
                $red = Test-Green
                if ($red) { return "$red after rebasing onto the other lanes' work" }
                Write-Trace $Id 'verify' 'build and fast tests green on the shared branch'
            }
            if (Invoke-Git @('push', '-q', 'origin', "HEAD:$Branch")) { return '' }
        }
        return 'push kept being refused'
    } finally { $lock.Dispose() }
}

function Invoke-Park {
    # Work that will not integrate is kept on a branch of its own, and the task goes to
    # back to Backlog on the shared branch, so a later run picks it up from that branch and
    # fixes what broke. Integration trouble is Claude's to solve, not Stewart's.
    param([string]$Id, [string]$Why)
    $park = "factory/$Id-lane-$Lane-$Stamp"
    # A local branch too: lanes may not fetch, but every worktree sees local branches.
    Invoke-Git @('branch', '-f', $park, 'HEAD') | Out-Null
    Invoke-Git @('push', '-q', 'origin', "HEAD:refs/heads/$park") | Out-Null
    $lock = Enter-Lock
    try {
        foreach ($attempt in 1..3) {
            if (-not (Sync-Lane)) { continue }
            if ((Get-TaskState $Id) -ne 'Doing') { return }
            Invoke-Board @('move', '-Id', $Id, '-To', 'Backlog', '-Reason', "Lane $Lane could not integrate: $Why. The work is on branch $park; start with git cherry-pick --no-commit $park and fix it.") | Out-Null
            Invoke-Git @('add', '-A', 'Tasks') | Out-Null
            Invoke-Git @('commit', '-q', '-m', "chore(tasks): block $Id - $Why") | Out-Null
            if (Invoke-Git @('push', '-q', 'origin', "HEAD:$Branch")) { return }
        }
    } finally { $lock.Dispose() }
}

function Write-LaneSummary {
    param([string[]]$Lines)
    $dir = Join-Path $LogDir "lanes-$Stamp"
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
    Set-Content -Path (Join-Path $dir "lane-$Lane.txt") -Value $Lines -Encoding UTF8
}

# ---------------------------------------------------------------------------- shift

New-Item -ItemType Directory -Force -Path $LogDir | Out-Null
$shiftEnd = (Get-Date).AddHours($Hours)

# ------------------------------------------------ coordinator: start lanes, wait, alarm

if ($Lanes -gt 1 -and -not $Lane) {
    try { $Host.UI.RawUI.WindowTitle = "Dark factory - $Lanes lanes" } catch { }
    $branch = (git -C $Root rev-parse --abbrev-ref HEAD).Trim()
    if ($branch -in 'master', 'main') { Write-Trace '-' 'refuse' "on $branch; switch to a feature branch" 'Red'; exit 1 }
    if (Get-Dirty) { Write-Trace '-' 'refuse' 'working tree not clean; commit or stash first' 'Red'; exit 1 }
    git -C $Root fetch -q origin $branch
    if ((git -C $Root rev-parse HEAD).Trim() -ne (git -C $Root rev-parse "origin/$branch").Trim()) {
        Write-Trace '-' 'refuse' "$branch differs from origin/$branch; push or pull first" 'Red'; exit 1
    }
    $stuck = @(Get-ChildItem (Join-Path $Root 'Tasks\Doing') -Filter 'BL-*.md' -ErrorAction SilentlyContinue)
    if ($stuck.Count) { Write-Trace '-' 'refuse' "task already in Doing: $($stuck[0].Name)" 'Red'; exit 1 }

    Write-Trace '-' 'shift' "start  $Lanes lanes  branch=$branch model=$Model until $($shiftEnd.ToString('HH:mm'))" 'Cyan'
    New-Item -ItemType Directory -Force -Path $LanesDir | Out-Null
    $procs = @()
    foreach ($n in 1..$Lanes) {
        $dir = Join-Path $LanesDir "lane-$n"
        if (-not (Test-Path (Join-Path $dir '.git'))) {
            git -C $Root worktree add -q --detach $dir "origin/$branch" 2>&1 | Out-Null
        }
        git -C $dir stash push -q --include-untracked -m "darkfactory lane-$n before $Stamp" 2>&1 | Out-Null
        git -C $dir checkout -q -B "factory/lane-$n" "origin/$branch" 2>&1 | Out-Null
        git -C $dir reset -q --hard "origin/$branch" 2>&1 | Out-Null
        $laneArgs = @('-Lane', $n, '-Branch', $branch, '-Hours', $Hours, '-MaxTasks', $MaxTasks,
            '-TaskMinutes', $TaskMinutes, '-Model', $Model, '-LogRoot', "`"$LogDir`"", '-ShiftStamp', $Stamp)
        $started = Start-Detached -Label "Dark factory lane $n" -Dir $dir -ScriptArgs $laneArgs
        $procs += $started
        $where = if ($started.Tab) { "herdr tab $($started.Tab)" } else { "pid $($started.Process.Id)" }
        Write-Trace '-' 'lane' "lane $n started in $dir ($where)"
        Start-Sleep -Seconds 15
    }
    # A lane is finished once it has written its summary. A herdr tab has no process to
    # watch, so the summaries are the signal; a lane that dies without one is given up on
    # after the shift's length plus one task's time limit.
    $summaries = Join-Path $LogDir "lanes-$Stamp"
    $giveUp = $shiftEnd.AddMinutes($TaskMinutes + 30)
    $tick = Get-Date
    while ((Get-Date) -lt $giveUp) {
        # The coordinator announces the usage limit for every lane, and lanes waiting for a
        # new session add that wait to their shift, so the coordinator waits longer too.
        Update-LimitNotice
        Invoke-LimitProbe
        if (Test-WaitingForSession) { $giveUp = $giveUp.Add((Get-Date) - $tick) }
        $tick = Get-Date
        $finished = @(Get-ChildItem $summaries -Filter 'lane-*.txt' -ErrorAction SilentlyContinue).Count
        if ($finished -ge $Lanes) { break }
        $running = @($procs | Where-Object { $_.Tab -or -not $_.Process.HasExited }).Count
        if ($running -eq 0) { break }
        Start-Sleep -Seconds 5
    }

    git -C $Root pull -q --ff-only origin $branch 2>&1 | Out-Null
    $stalls = @()
    foreach ($file in Get-ChildItem (Join-Path $LogDir "lanes-$Stamp") -Filter 'lane-*.txt' -ErrorAction SilentlyContinue) {
        foreach ($line in Get-Content $file.FullName) {
            if ($line -match '^SUMMARY ') { Write-Trace '-' 'lane' ($line -replace '^SUMMARY ', '') 'Cyan' }
            elseif ($line.Trim()) { $stalls += $line }
        }
    }
    Write-Trace '-' 'shift' "end  $Lanes lanes" 'Cyan'
    $reasons = @($stalls) + @(Get-WaitingOnStewart)
    if ($reasons.Count -gt 0) { Invoke-Alarm -Reasons $reasons; exit 2 }
    try { $Host.UI.RawUI.WindowTitle = 'Dark factory - shift complete' } catch { }
    exit 0
}

# ------------------------------------------------ one runner: this checkout, or a lane

try { $Host.UI.RawUI.WindowTitle = if ($Lane) { "Dark factory - lane $Lane" } else { 'Dark factory - running' } } catch { }

if ($Lane) {
    if (-not $Branch) { Write-Trace '-' 'refuse' 'a lane needs -Branch' 'Red'; exit 1 }
    $branch = $Branch
} else {
    $branch = (git -C $Root rev-parse --abbrev-ref HEAD).Trim()
    if ($branch -in 'master', 'main') { Write-Trace '-' 'refuse' "on $branch; switch to a feature branch" 'Red'; exit 1 }
    if (Get-Dirty) { Write-Trace '-' 'refuse' 'working tree not clean; commit or stash first' 'Red'; exit 1 }
    $stuck = @(Get-ChildItem (Join-Path $Root 'Tasks\Doing') -Filter 'BL-*.md' -ErrorAction SilentlyContinue)
    if ($stuck.Count) { Write-Trace '-' 'refuse' "task already in Doing: $($stuck[0].Name)" 'Red'; exit 1 }
}

Write-Trace '-' 'shift' "start  branch=$branch model=$Model until $($shiftEnd.ToString('HH:mm'))" 'Cyan'

$done = 0; $blocked = 0; $requeued = 0; $stalls = @(); $failStreak = 0; $attempted = @{}
$stopWhy = ''
# The task to run again once the usage limit resets; it is still claimed.
$resumeId = ''

while ($true) {
  try {
    # A task cut off by the usage limit is finished first, whatever else says stop.
    $resuming = [bool]$resumeId
    if (-not $resuming) {
        if ((Get-Date) -gt $shiftEnd) { $stopWhy = 'time up'; break }
        if ($MaxTasks -gt 0 -and ($done + $blocked + $stalls.Count) -ge $MaxTasks) { $stopWhy = 'max tasks'; break }
        if ($failStreak -ge 2) { $stopWhy = 'runs failing'; break }
    }

    if ($resuming) {
        $id = $resumeId
        $resumeId = ''
    } elseif ($Lane) {
        $claim = Invoke-Claim -Skip @($attempted.Keys)
        if ($claim.None) { $stopWhy = 'nothing ready'; break }
        if ($claim.Wait) { Write-Trace '-' 'wait' $claim.Why 'DarkGray'; Start-Sleep -Seconds 60; continue }
        $id = $claim.Id
    } else {
        $requeued = @(Invoke-Requeue)
        if ($requeued.Count) {
            git -C $Root add -A Tasks 2>&1 | Out-Null
            git -C $Root commit -q -m "chore(tasks): requeue $($requeued -join ', ') - blockers Done" 2>&1 | Out-Null
            git -C $Root push -q 2>&1 | Out-Null
        }
        $next = (Invoke-Board @('next')) -join "`n"
        if ($next -notmatch '(?m)^(BL-\d{3})\s') { $stopWhy = 'nothing ready'; break }
        $id = $Matches[1]
        if ($attempted.ContainsKey($id)) { $stopWhy = "$id offered twice"; $stalls += "$id offered again after a run"; break }
    }
    $attempted[$id] = $true

    if (-not $resuming) { Write-Trace $id 'claim' (Get-Short (Get-TaskTitle $id)) 'Cyan' }
    $run = Invoke-TaskRun $id -Resume:$resuming
    $state = Get-TaskState $id

    # Out of tokens is not a stall. The task keeps its claim and its partial work - nothing
    # is stashed or blocked - the shift waits for the new session and runs it again, and
    # the wait is added to the shift so it costs no working time.
    $until = if ($run.TimedOut) { $null } else { Get-OutOfTokensUntil }
    if ($until -and $state -in 'Doing', 'Backlog') {
        $waited = Wait-ForNewSession -Id $id -Until $until
        $shiftEnd = $shiftEnd.Add($waited)
        Write-Trace '-' 'shift' "waited $(Format-Span $waited) for tokens; shift now ends $($shiftEnd.ToString('HH:mm'))" 'Cyan'
        $resumeId = $id
        continue
    }

    if ($state -eq 'Doing') {
        $why = if ($run.TimedOut) { "timed out after $TaskMinutes min" } else { "run ended in Doing, exit $($run.ExitCode)" }
        Save-StrayChanges $id
        Invoke-Board @('move', '-Id', $id, '-To', 'Blocked', '-Reason', "Stewart: dark factory $why; see logs\$id-$Stamp$LaneTag.jsonl") | Out-Null
        git -C $Root add -A Tasks 2>&1 | Out-Null
        git -C $Root commit -q -m "chore(tasks): block $id - dark factory $why" 2>&1 | Out-Null
        if (-not $Lane) { git -C $Root push -q 2>&1 | Out-Null }
        $state = Get-TaskState $id
    }
    Save-StrayChanges $id

    if ($Lane -and $state -in 'Done', 'Blocked', 'Backlog') {
        $problem = Invoke-Integrate -Id $id -State $state
        if ($problem) {
            Write-Trace $id 'PARKED' "$problem; back to Backlog" 'Yellow'
            Invoke-Park -Id $id -Why $problem
            $state = 'Parked'
        } else {
            Write-Trace $id 'push' "integrated into $branch"
        }
    }

    if ($state -eq 'Done') {
        $done++; $failStreak = 0
        Write-Trace $id 'DONE' (Get-Short (Get-LastLogLine $id)) 'Green'
    } elseif ($state -eq 'Blocked') {
        $blocked++
        if ($null -eq $script:RunResult -or $run.TimedOut) { $failStreak++ } else { $failStreak = 0 }
        Write-Trace $id 'BLOCKED' (Get-Short (Get-LastLogLine $id)) 'Yellow'
    } elseif ($state -in 'Backlog', 'Parked') {
        # Waiting on other tasks, a widened touches, or work that would not integrate: back
        # in the queue for a later run, not a stall.
        $requeued++; $failStreak = 0
        Write-Trace $id 'REQUEUE' (Get-Short (Get-LastLogLine $id)) 'Yellow'
    } else {
        $failStreak++
        $stalls += "$id STALLED  ended in $state, exit $($run.ExitCode)"
        Write-Trace $id 'STALL' "ended in $state, exit $($run.ExitCode)" 'Red'
    }
  } catch {
    $stopWhy = 'script error'
    $stalls += "FACTORY SCRIPT ERROR  $($_.Exception.Message)"
    Write-Trace '-' 'ERROR' (Get-Short $_.Exception.Message) 'Red'
    break
  }
}

Write-Trace '-' 'shift' "end ($stopWhy)  done=$done blocked=$blocked requeued=$requeued stalled=$($stalls.Count)" 'Cyan'
if ($stopWhy -eq 'runs failing') { $stalls = @('FACTORY STALLED - two runs in a row failed; check logs\') + $stalls }

if ($Lane) {
    # The coordinator raises one alarm for every lane; a lane only reports.
    Write-LaneSummary (@("SUMMARY lane $Lane ended ($stopWhy): done=$done blocked=$blocked requeued=$requeued stalled=$($stalls.Count)") + $stalls)
    try { $Host.UI.RawUI.WindowTitle = "Dark factory - lane $Lane finished" } catch { }
    exit 0
}

$reasons = @($stalls) + @(Get-WaitingOnStewart)
if ($reasons.Count -gt 0) {
    Invoke-Alarm -Reasons $reasons
    exit 2
}
try { $Host.UI.RawUI.WindowTitle = 'Dark factory - shift complete' } catch { }
exit 0
