<#
.SYNOPSIS
    Whispers to Stewart when a task reaches Done, a CI failure is filed, a commit is made, a branch is deleted, or (run by task-board.ps1) the Backlog shrinks.

.DESCRIPTION
    A PostToolUse hook for Bash and PowerShell tool calls, in every Claude Code session in
    this repository - the interactive one and every dark factory lane. It reads the hook's
    JSON from standard input, recognises four milestones from the command and its output,
    and speaks one short phrase very quietly in Windows' Zira voice:

      task-board.ps1 move ... -To Done        "Task done. B L 199, expand variable references in config files."
                                              (the task's whole file name, never shortened)
      task-board.ps1 new, a "Fix CI" task     "CI failure filed. B L 990, fix ci failure ..."
                                              (the dark factory's CI watch, BL-987)
      git commit (that made a commit)         "Committed. Parse the proxy text."
      git branch -d/-D, git push --delete     "Branch factory BL 147 wip, deleted."

    Anything else is ignored. Phrases from several sessions queue behind a named mutex
    instead of talking over each other. The hook runs async, so no session waits for it,
    and it never fails a tool call: every error is swallowed.

    Given -Phrase, it is not a hook: it speaks that phrase in the same voice and queue
    and reads nothing from standard input. task-board.ps1 runs it this way, detached and
    hidden, after every move that takes a task out of Backlog (BL-1182):

      task-board.ps1 move, from Backlog       "Backlog depth, 63."
                                              (the BL-*.md files left in Tasks/Backlog
                                              of the real board, never a scratch copy)

    With -DelaySeconds and -StampFile it first waits, then speaks only if the stamp file
    still starts with -Stamp, so a burst of moves across lanes gives one phrase, with the
    latest depth, a minute after the last move. With -PauseSeconds it holds the queue
    silent that long before speaking, so the phrase never runs on from another one
    (Stewart, 2026-10-02: the depth must be heard apart from everything else).
#>
param(
    # Speak these phrases instead of reading a hook's JSON from standard input.
    [string[]] $Phrase = @(),
    # Wait this long before speaking.
    [int] $DelaySeconds = 0,
    # Speak only if this file still starts with -Stamp after the wait.
    [string] $StampFile = '',
    [string] $Stamp = '',
    # Once the queue is ours, stay silent this long before the first phrase.
    [int] $PauseSeconds = 0
)
$ErrorActionPreference = 'Stop'

# Stewart's off switch (2026-10-03, "turn audio off", BL-1317): while this file exists,
# nothing here speaks - not the hook's phrases, not the Backlog depth, not the factory's
# "CI failure filed". One file outside the repository, so every lane and session obeys it
# at once. Delete it to turn the whispers back on.
if (Test-Path -LiteralPath (Join-Path $env:LOCALAPPDATA 'Curl\audio-off')) { exit 0 }

function Invoke-Whisper([string[]] $Phrases, [int] $PauseSeconds = 0) {
    # "BL-199" reads as "B L 199"; slashes, dashes and colons as pauses.
    function Get-Spoken([string]$Text) {
        $Text = $Text -replace '\bBL-(\d+)', 'B L $1'
        return ($Text -replace '[/_:]', ' ' -replace '\s+', ' ').Trim()
    }
    Add-Type -AssemblyName System.Speech
    $mutex = New-Object System.Threading.Mutex($false, 'Global\CurlWhisper')
    if (-not $mutex.WaitOne(120000)) { return }
    try {
        # Held inside the mutex, so no other phrase can fill the silence.
        if ($PauseSeconds -gt 0) { Start-Sleep -Seconds $PauseSeconds }
        $voice = New-Object System.Speech.Synthesis.SpeechSynthesizer
        $voice.SetOutputToDefaultAudioDevice()
        try { $voice.SelectVoice('Microsoft Zira Desktop') } catch { }
        foreach ($p in $Phrases) {
            # The Surl dark factory whispers on the same PC in the same voice, so every
            # phrase says whose it is (Stewart, 2026-09-29, BL-901).
            $text = [System.Security.SecurityElement]::Escape((Get-Spoken "Curl. $p"))
            $voice.SpeakSsml("<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' xml:lang='en-US'><prosody volume='x-soft' rate='slow' pitch='low'>$text</prosody></speak>")
        }
        $voice.Dispose()
    } finally { $mutex.ReleaseMutex() }
}

if ($Phrase.Count) {
    try {
        if ($DelaySeconds -gt 0) { Start-Sleep -Seconds $DelaySeconds }
        if ($StampFile) {
            $current = [IO.File]::ReadAllText($StampFile)
            if (-not $Stamp -or -not $current.StartsWith("$Stamp|")) { exit 0 }
        }
        Invoke-Whisper $Phrase $PauseSeconds
    } catch { }
    exit 0
}

try {
    $hook = [Console]::In.ReadToEnd() | ConvertFrom-Json
    $command = "$($hook.tool_input.command)"
    $response = $hook.tool_response
    $output = "$($response.stdout)`n$($response.stderr)`n$($response.output)"
    $cwd = if ($hook.cwd) { "$($hook.cwd)" } else { (Get-Location).Path }

    function Get-Words([string]$Text, [int]$Count) {
        return (($Text -split '\s+' | Where-Object { $_ } | Select-Object -First $Count) -join ' ')
    }

    $phrases = @()

    # A task moved to Done: the board script prints "BL-199  Doing -> Done  Tasks\Done\BL-199-<slug>.md".
    if ($command -match 'task-board\.ps1' -and $command -match '\bmove\b' -and $command -match '-To\s+Done') {
        foreach ($m in [regex]::Matches($output, '(BL-\d+)\s+\w+\s+->\s+Done\s+\S*?\1-([a-z0-9-]+)\.md')) {
            # The whole file name, never a shortened one: Stewart asked to hear all of it.
            $phrases += "Task done. $($m.Groups[1].Value), $($m.Groups[2].Value -replace '-', ' ')."
        }
    }

    # A CI failure filed by the dark factory's CI watch (BL-987), whose task titles start
    # "Fix CI" or "Fix flaky CI": "BL-990  Tasks\Backlog\BL-990-fix-ci-failure-<test>.md".
    if ($command -match 'task-board\.ps1' -and $command -match '\bnew\b') {
        foreach ($m in [regex]::Matches($output, '(BL-\d+)\s+\S*?\1-(fix-(?:flaky-)?ci-[a-z0-9-]+)\.md')) {
            $phrases += "CI failure filed. $($m.Groups[1].Value), $($m.Groups[2].Value -replace '-', ' ')."
        }
    }

    # A commit: trust git, not the command line - the newest commit must be under a minute old.
    if ($command -match '\bgit\b[^|;&]*\bcommit\b' -and $command -notmatch '--dry-run') {
        $stamp = & git -C $cwd log -1 --format='%ct|%s' 2>$null
        if ($stamp -match '^(\d+)\|(.*)$') {
            $age = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds() - [long]$Matches[1]
            if ($age -ge 0 -and $age -le 60) {
                $subject = $Matches[2] -replace '^\w+(\([^)]*\))?!?:\s*', ''
                $phrases += "Committed. $(Get-Words $subject 7)."
            }
        }
    }

    # A branch deleted, locally ("Deleted branch x (was abc)") or on GitHub ("- [deleted]  x").
    if ($command -match '\bgit\b[^|;&]*\bbranch\b[^|;&]*\s-[dD]\b' -or $command -match '\bgit\b[^|;&]*\bpush\b[^|;&]*(--delete|\s:\S)') {
        $names = @([regex]::Matches($output, 'Deleted branch (\S+)') | ForEach-Object { $_.Groups[1].Value }) +
                 @([regex]::Matches($output, '-\s+\[deleted\]\s+(\S+)') | ForEach-Object { $_.Groups[1].Value })
        foreach ($name in ($names | Select-Object -Unique)) { $phrases += "Branch $name, deleted." }
    }

    if (-not $phrases.Count) { exit 0 }

    Invoke-Whisper $phrases
} catch { }
exit 0
