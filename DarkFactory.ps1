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

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File DarkFactory.ps1
    powershell -NoProfile -ExecutionPolicy Bypass -File DarkFactory.ps1 -Hours 4 -MaxTasks 3
    powershell -NoProfile -ExecutionPolicy Bypass -File DarkFactory.ps1 -TestAlarm
    powershell -NoProfile -ExecutionPolicy Bypass -File DarkFactory.ps1 -TestAlarm -AlarmScale 0.1
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
    [switch]$QuietAlarm
)

# Continue, not Stop: native stderr from git or dotnet must never kill an unattended shift.
$ErrorActionPreference = 'Continue'

$Root = $PSScriptRoot
$Board = Join-Path $Root '.claude\skills\task-board\task-board.ps1'
$LogDir = Join-Path $Root 'logs'
$Stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$TraceFile = Join-Path $LogDir "DarkFactory-$Stamp.log"

# ---------------------------------------------------------------------------- trace

function Write-Trace {
    param([string]$Task, [string]$Verb, [string]$Detail = '', [string]$Color = 'Gray')
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
2. Where only Stewart can decide (a new package, a threshold change, a deliberate
   divergence from upstream curl, a truly ambiguous requirement, anything CLAUDE.md
   reserves for him), do not guess: move the task to Blocked with a -Reason that starts
   "Stewart:" and asks the question in one line.
3. When the task reaches Done with dotnet build clean and the fast tests green, commit
   by logical unit (Conventional Commits, including the task file) and push the current
   branch yourself with git, per the standing authorization in CLAUDE.md. Never push to
   master, never force push, never merge.
4. If the task ends Blocked, commit only the task board change and push it. Leave any
   unfinished code uncommitted; the shift stashes it.
5. The task must not be left in Doing.

End your reply with exactly one line, either
FACTORY: DONE {ID} <what now works>
or
FACTORY: BLOCKED {ID} <the blocker>
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
        'result' {
            $mins = [math]::Round($Evt.duration_ms / 60000, 1)
            $script:RunResult = $Evt
            Write-Trace $Id 'end' "$($Evt.num_turns) turns, $mins min"
        }
    }
}

function Invoke-TaskRun {
    param([string]$Id)
    $raw = Join-Path $LogDir "$Id-$Stamp.jsonl"
    $err = Join-Path $LogDir "$Id-$Stamp.err.txt"
    $script:RunResult = $null
    $script:ToolLabels = @{}

    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $env:ComSpec
    $denied = ($Forbidden | ForEach-Object { "`"Bash($_`:*)`" `"PowerShell($_`:*)`"" }) -join ' '
    $psi.Arguments = "/d /c claude -p --model $Model --dangerously-skip-permissions --output-format stream-json --verbose --disallowedTools $denied 2>`"$err`""
    $psi.WorkingDirectory = $Root
    $psi.UseShellExecute = $false
    $psi.RedirectStandardInput = $true
    $psi.RedirectStandardOutput = $true
    $psi.StandardOutputEncoding = [System.Text.Encoding]::UTF8
    $p = [System.Diagnostics.Process]::Start($psi)
    $p.StandardInput.Write($Prompt.Replace('{ID}', $Id))
    $p.StandardInput.Close()

    $deadline = (Get-Date).AddMinutes($TaskMinutes)
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

# ---------------------------------------------------------------------------- shift

New-Item -ItemType Directory -Force -Path $LogDir | Out-Null
try { $Host.UI.RawUI.WindowTitle = 'Dark factory - running' } catch { }

$branch = (git -C $Root rev-parse --abbrev-ref HEAD).Trim()
if ($branch -in 'master', 'main') { Write-Trace '-' 'refuse' "on $branch; switch to a feature branch" 'Red'; exit 1 }
if (Get-Dirty) { Write-Trace '-' 'refuse' 'working tree not clean; commit or stash first' 'Red'; exit 1 }
$stuck = @(Get-ChildItem (Join-Path $Root 'Tasks\Doing') -Filter 'BL-*.md' -ErrorAction SilentlyContinue)
if ($stuck.Count) { Write-Trace '-' 'refuse' "task already in Doing: $($stuck[0].Name)" 'Red'; exit 1 }

$shiftEnd = (Get-Date).AddHours($Hours)
Write-Trace '-' 'shift' "start  branch=$branch model=$Model until $($shiftEnd.ToString('HH:mm'))" 'Cyan'

$done = 0; $blocked = 0; $stalls = @(); $failStreak = 0; $attempted = @{}
$stopWhy = ''

while ($true) {
  try {
    if ((Get-Date) -gt $shiftEnd) { $stopWhy = 'time up'; break }
    if ($MaxTasks -gt 0 -and ($done + $blocked + $stalls.Count) -ge $MaxTasks) { $stopWhy = 'max tasks'; break }
    if ($failStreak -ge 2) { $stopWhy = 'runs failing'; break }

    $next = (Invoke-Board @('next')) -join "`n"
    if ($next -notmatch '(BL-\d{3})') { $stopWhy = 'nothing ready'; break }
    $id = $Matches[1]
    if ($attempted.ContainsKey($id)) { $stopWhy = "$id offered twice"; $stalls += "$id offered again after a run"; break }
    $attempted[$id] = $true

    Write-Trace $id 'claim' (Get-Short (Get-TaskTitle $id)) 'Cyan'
    $run = Invoke-TaskRun $id
    $state = Get-TaskState $id

    if ($state -eq 'Doing') {
        $why = if ($run.TimedOut) { "timed out after $TaskMinutes min" } else { "run ended in Doing, exit $($run.ExitCode)" }
        Save-StrayChanges $id
        Invoke-Board @('move', '-Id', $id, '-To', 'Blocked', '-Reason', "Stewart: dark factory $why; see logs\$id-$Stamp.jsonl") | Out-Null
        $state = Get-TaskState $id
    }
    Save-StrayChanges $id

    if ($state -eq 'Done') {
        $done++; $failStreak = 0
        Write-Trace $id 'DONE' (Get-Short (Get-LastLogLine $id)) 'Green'
    } elseif ($state -eq 'Blocked') {
        $blocked++
        if ($null -eq $script:RunResult -or $run.TimedOut) { $failStreak++ } else { $failStreak = 0 }
        Write-Trace $id 'BLOCKED' (Get-Short (Get-LastLogLine $id)) 'Yellow'
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

Write-Trace '-' 'shift' "end ($stopWhy)  done=$done blocked=$blocked stalled=$($stalls.Count)" 'Cyan'

$reasons = @($stalls) + @(Get-WaitingOnStewart)
if ($stopWhy -eq 'runs failing') { $reasons = @('FACTORY STALLED - two runs in a row failed; check logs\') + $reasons }
if ($reasons.Count -gt 0) {
    Invoke-Alarm -Reasons $reasons
    exit 2
}
try { $Host.UI.RawUI.WindowTitle = 'Dark factory - shift complete' } catch { }
exit 0
