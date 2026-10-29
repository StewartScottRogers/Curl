# The alarm: the banner, Core Audio volume, the siren and chimes, the spoken reasons, the
# audio-off switch, and -TestAlarm. Dot-sourced by RunDarkFactory.ps1 in its order; see the
# loader there.

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
    $lines += '  The Curl dark factory is waiting on you.  ' + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss')
    $lines += ''
    $lines += ($Reasons | Select-Object -First 8 | ForEach-Object { '  ' + (Get-Short $_ 74) })
    $lines += ''
    $lines += '  Press any key to silence.  Trace: ' + $TraceFile
    $lines += ''
    foreach ($l in $lines) { Write-Host ('  ' + $l).PadRight($width) -ForegroundColor $fg -BackgroundColor $bg }
}

# Core Audio, for raising and restoring the master volume from stage 3. Windows ships it;
# nothing is installed.
$script:AudioReady = $false
function Initialize-Audio {
    if ($script:AudioReady) { return $true }
    if (-not (Test-FactoryOnWindows)) { return $false }
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

# Off Windows the voice is the first of say (macOS), spd-say or espeak (Linux) found on PATH,
# behind the same members as the System.Speech synthesizer; $null when none is installed, so
# the alarm only prints. The system volume is never touched.
function Get-CommandLineVoice {
    $tool = @('say', 'spd-say', 'espeak') | Where-Object { Get-Command $_ -CommandType Application -ErrorAction SilentlyContinue } | Select-Object -First 1
    if (-not $tool) { return $null }
    $v = [pscustomobject]@{ Volume = 100; Rate = 0; Tool = $tool; Process = $null }
    $v | Add-Member ScriptMethod SpeakAsyncCancelAll {
        if ($this.Process -and -not $this.Process.HasExited) { try { $this.Process.Kill() } catch { } }
        $this.Process = $null
    }
    $v | Add-Member ScriptMethod SpeakAsync {
        param($Text)
        try { $this.Process = Start-Process -FilePath $this.Tool -ArgumentList @($Text) -PassThru -RedirectStandardOutput (Join-Path ([IO.Path]::GetTempPath()) 'curl-factory-voice.out') -RedirectStandardError (Join-Path ([IO.Path]::GetTempPath()) 'curl-factory-voice.err') } catch { }
    }
    return $v
}

function Get-Voice {
    if ($script:Voice) { return $script:Voice }
    if (-not (Test-FactoryOnWindows)) { $script:Voice = Get-CommandLineVoice; return $script:Voice }
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

# What the voice calls this factory. The Surl dark factory runs on the same PC with the same
# voice, so everything said aloud names Curl (Stewart, 2026-09-29, BL-901).
$SpokenName = 'the Curl dark factory'

function Get-AlarmSpeech {
    param([string[]]$Reasons, [int]$Stage)
    if ($Stage -eq 0) { return "Stewart, $SpokenName needs your input." }
    $n = $Reasons.Count
    $what = if ($n -eq 1) { 'One item is' } else { "$n items are" }
    $first = ($Reasons | Select-Object -First 2 | ForEach-Object { Get-Spoken $_ }) -join '. Then, '
    if ($Stage -eq 1) { return "Stewart. $SpokenName. $what waiting on you. $first." }
    return "Stewart! Stewart! $SpokenName has stopped. $what waiting on you. $first. Press any key at the terminal."
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

# Stewart's audio switch (BL-1317, BL-1456): while this file exists every sound Curl makes is
# off - the whisper hook's and the factory's alarm, chimes, speech and volume changes. Read
# at each sound, so creating or deleting it reaches a shift that is already running.
$script:AudioOffFile = Join-Path (Get-FactoryDataDir) 'audio-off'
function Test-AudioOff { return [bool]($script:AudioOffFile -and (Test-Path -LiteralPath $script:AudioOffFile)) }

# True when the factory must stay silent: -QuietAlarm, or the audio-off file. The banner
# and notices still show on screen; nothing plays, speaks or touches the volume.
function Test-AlarmSilent { return ($QuietAlarm -or (Test-AudioOff)) }

function Invoke-AlarmSound {
    param([string[]]$Reasons, [int]$Stage)
    if (Test-AlarmSilent) { return }
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

# This file ran to its end; RunDarkFactory.ps1 goes on to the next one.
$AreaFileFinished = $true
