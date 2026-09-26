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
    assigned to him, a run that stalled - it fills the screen with an ASCII banner and
    beeps every 30 seconds until a key is pressed.

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File DarkFactory.ps1
    powershell -NoProfile -ExecutionPolicy Bypass -File DarkFactory.ps1 -Hours 4 -MaxTasks 3
    powershell -NoProfile -ExecutionPolicy Bypass -File DarkFactory.ps1 -TestAlarm
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
    [switch]$TestAlarm
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

function Invoke-Alarm {
    param([string[]]$Reasons)
    try { $Host.UI.RawUI.WindowTitle = '!!! STEWART - INPUT NEEDED !!!' } catch { }
    $frame = 0
    while ($true) {
        Show-Banner -Reasons $Reasons -Frame $frame
        try { [Console]::Beep(880, 400); [Console]::Beep(660, 400); [Console]::Beep(880, 600) } catch { }
        $frame++
        # Wait 30 s for a key, flashing the banner every second. Non-interactive: show once.
        foreach ($tick in 1..30) {
            try {
                if ([Console]::KeyAvailable) { [void][Console]::ReadKey($true); return }
            } catch { return }
            Start-Sleep -Seconds 1
            Show-Banner -Reasons $Reasons -Frame ($frame + $tick)
        }
    }
}

if ($TestAlarm) {
    Invoke-Alarm -Reasons @('TEST - BL-000 Stewart: this is what a real question looks like')
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
    $psi.Arguments = "/d /c claude -p --model $Model --dangerously-skip-permissions --output-format stream-json --verbose 2>`"$err`""
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
