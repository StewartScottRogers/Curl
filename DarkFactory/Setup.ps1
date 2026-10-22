# The shift's settings worked out from the parameters - its checkout, log and lane folders,
# stamp and lock file - and the trace every runner writes. Dot-sourced by RunDarkFactory.ps1
# in its order; see the loader there.

# Continue, not Stop: native stderr from git or dotnet must never kill an unattended shift.
$ErrorActionPreference = 'Continue'

# -Lanes is Auto or a number; $LaneCount is the number, which Auto sets at shift start and
# changes step by step.
$AutoLanes = $Lanes -eq 'auto'
$LaneCount = if ($AutoLanes) { 1 } else { [int]$Lanes }

$Root = if ($LaneDir) { $LaneDir.Trim('"') } else { $FactoryScriptDir }
# The board script and every Claude run use this checkout, never one inherited from a
# Claude Code session that happened to start the shift.
$env:CLAUDE_PROJECT_DIR = $Root
$Board = Join-Path $Root '.claude\skills\task-board\task-board.ps1'
# Logs live beside the checkout, like the lanes: Z:\repos\Curl -> Z:\repos\Curl.logs.
# Lanes are always handed the shift's -LogRoot.
$LogDir = if ($LogRoot) { $LogRoot } else { "$Root.logs" }
$Stamp = if ($ShiftStamp) { $ShiftStamp } else { Get-Date -Format 'yyyyMMdd-HHmmss' }
$LaneTag = if ($Lane) { "-L$Lane" } else { '' }
# The --max-turns 1 probes need no reasoning, so under -Model auto they use sonnet (BL-1705).
$ProbeModel = if ($Model -eq 'auto') { 'sonnet' } else { $Model }
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

# This file ran to its end; RunDarkFactory.ps1 goes on to the next one.
$AreaFileFinished = $true
