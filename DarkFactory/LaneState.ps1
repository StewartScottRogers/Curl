# Lane state files, tasks a lane sent back this shift, whether a lane lives, whether tokens
# are available, and -TestHeartbeat. Dot-sourced by RunDarkFactory.ps1 in its order; see the
# loader there.

function Write-LaneSummary {
    param([string[]]$Lines)
    $dir = Join-Path $LogDir "lanes-$Stamp"
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
    Set-Content -Path (Join-Path $dir "lane-$Lane.txt") -Value $Lines -Encoding UTF8
}

# Lane state, in <repo>.logs\lanes-<stamp>\: lane-<n>.pid (the lane's process, so the coordinator
# can tell a dead lane from a busy one) and lane-<n>.task (the task it holds in Doing, so a
# restarted lane - or the next shift - resumes it instead of losing it).
function Get-LaneStatePath {
    param([int]$N, [string]$Kind, [string]$ForStamp = $Stamp)
    return (Join-Path (Join-Path $LogDir "lanes-$ForStamp") "lane-$N.$Kind")
}

function Set-LaneState {
    param([string]$Kind, [string]$Value)
    $path = Get-LaneStatePath $Lane $Kind
    New-Item -ItemType Directory -Force -Path (Split-Path $path) | Out-Null
    if ($Value) { Set-Content -Path $path -Value $Value -Encoding ASCII } else { Remove-Item $path -ErrorAction SilentlyContinue }
}

function Get-LaneState {
    param([int]$N, [string]$Kind, [string]$ForStamp = $Stamp)
    $path = Get-LaneStatePath $N $Kind $ForStamp
    if (Test-Path $path) { return "$(Get-Content $path -TotalCount 1)".Trim() }
    return ''
}

# Tasks a lane of this shift sent back to Backlog (requeued or parked), one ID a line in
# <repo>.logs\lanes-<stamp>\requeued.txt. No lane of the same shift claims one again: a
# task that fell short once - BL-1525 missed its timing target under eight lanes' load -
# would only fall short again on the next lane, handing its work through the stash each
# time (AF-0072). The next shift tries it afresh.
function Get-ShiftRequeuedPath { return (Join-Path (Join-Path $LogDir "lanes-$Stamp") 'requeued.txt') }

function Add-ShiftRequeued {
    param([string]$Id)
    $path = Get-ShiftRequeuedPath
    New-Item -ItemType Directory -Force -Path (Split-Path $path) | Out-Null
    Add-Content -Path $path -Value $Id -Encoding ASCII
}

function Get-ShiftRequeued {
    $path = Get-ShiftRequeuedPath
    if (-not (Test-Path $path)) { return @() }
    return @(Get-Content $path | ForEach-Object { "$_".Trim() } | Where-Object { $_ } | Select-Object -Unique)
}

function Test-LaneAlive {
    # True while lane <n>'s process runs. No pid file yet means it is still starting.
    param([int]$N, [string]$ForStamp = $Stamp)
    $lanePid = Get-LaneState $N 'pid' $ForStamp
    if (-not $lanePid) { return $true }
    $p = Get-Process -Id ([int]$lanePid) -ErrorAction SilentlyContinue
    return [bool]($p -and $p.ProcessName -match 'powershell')
}

function Test-TokensAvailable {
    # Asks Claude for one word. $true when it answers, $false when the usage limit (or any
    # API failure) refuses it. Used where a run failed without saying why.
    try {
        $psi = New-Object System.Diagnostics.ProcessStartInfo
        $psi.FileName = $env:ComSpec
        $psi.Arguments = "/d /c claude -p --model $ProbeModel --output-format stream-json --verbose --max-turns 1 2>nul"
        $psi.WorkingDirectory = $Root
        $psi.UseShellExecute = $false
        $psi.RedirectStandardInput = $true
        $psi.RedirectStandardOutput = $true
        $p = [System.Diagnostics.Process]::Start($psi)
        $p.StandardInput.Write('Reply with the single word OK.')
        $p.StandardInput.Close()
        $read = $p.StandardOutput.ReadToEndAsync()
        if (-not $p.WaitForExit(120000)) { & taskkill /T /F /PID $p.Id 2>&1 | Out-Null; return $false }
        $out = $read.Result
    } catch { return $false }
    return ($out -match '"type":"result"' -and $out -notmatch '"status":"rejected"' -and $out -notmatch '"is_error":true')
}

function Test-ApiFailure {
    # A run that died on the API rather than on the work: no result at all, or an error
    # result the API gave (the usage limit in a form Get-OutOfTokensUntil did not know, an
    # overload, an outage).
    param($Run)
    if ($Run.TimedOut) { return $false }
    $r = $script:RunResult
    if ($null -eq $r) { return ($Run.ExitCode -ne 0) }
    return ([bool]($r.PSObject.Properties['is_error'] -and $r.is_error) -and
        ([bool]$r.PSObject.Properties['api_error_status'] -or "$($r.terminal_reason)" -eq 'api_error'))
}

function Test-BudgetSpent {
    # Whether the last run stopped because it reached the -TaskBudgetUsd cost cap.
    $r = $script:RunResult
    return ($null -ne $r -and "$($r.subtype)" -eq 'error_max_budget_usd')
}

function Wait-ForTokensByProbe {
    # Holds this runner until a one-word probe is answered, checking every 5 minutes, and
    # returns how long it waited.
    param([string]$Id)
    $began = Get-Date
    Write-Trace $Id 'tokens' 'run ended with no result (not necessarily the API); waiting until Claude answers a probe' 'Yellow'
    Write-Heartbeat 'tokens' 'waiting until Claude answers again'
    while (-not (Test-TokensAvailable)) {
        if ($Lane) { try { $Host.UI.RawUI.WindowTitle = "Dark factory - lane $Lane waiting for the API" } catch { } }
        # Refreshed every minute of the wait, so the page never shows this lane as stale.
        foreach ($minute in 1..5) { Write-Heartbeat; Start-Sleep -Seconds 60 }
    }
    Write-Trace $Id 'resume' 'Claude answers again; running the task again' 'Green'
    return ((Get-Date) - $began)
}

if ($TestHeartbeat) {
    # Lane 1 through its phases in a temporary log root, printing its file after each. No
    # board, git or Claude: the task is a made-up BL-000 with a title given here.
    $Lane = 1
    $LogDir = Join-Path ([IO.Path]::GetTempPath()) "DarkFactoryHeartbeat-$Stamp"
    $WritesHeartbeat = $true
    $heartbeatFile = Get-LaneStatePath $Lane 'heartbeat.json'
    try {
        Write-Heartbeat 'starting'
        Get-Content -Raw $heartbeatFile
        Write-Heartbeat 'claim'
        Get-Content -Raw $heartbeatFile
        Set-HeartbeatTask 'BL-000' -Title 'Rehearse the heartbeat file'
        # The model fields Set-ModelChoice fills from the task file (BL-1705).
        $script:Beat.Model = 'sonnet'; $script:Beat.ModelWhy = 'direct pipeline'
        Write-Heartbeat 'run'
        Set-HeartbeatStep @('build', '')
        Get-Content -Raw $heartbeatFile
        Write-Heartbeat 'integrate'
        Get-Content -Raw $heartbeatFile
        Set-HeartbeatTask ''
        Write-Heartbeat 'finished'
        Get-Content -Raw $heartbeatFile
        # Three made-up lanes, written out of order, merged into status.json and built into
        # the board branch's commit. Never pushed: lanes are denied git push, and the first
        # real push is the next shift's.
        foreach ($fake in @(
            @{ Lane = 3; Task = $null; Title = $null; Phase = 'wait'; Step = 'nothing can start yet' },
            @{ Lane = 1; Task = 'BL-001'; Title = 'Rehearse lane one'; Phase = 'run'; Step = 'build' },
            @{ Lane = 2; Task = 'BL-002'; Title = 'Rehearse lane two'; Phase = 'integrate'; Step = '' })) {
            $Lane = $fake.Lane
            $script:Beat.Task = $null
            Set-HeartbeatTask $fake.Task -Title $fake.Title
            if ($fake.Task) { $script:Beat.Model = 'opus'; $script:Beat.ModelWhy = 'feature pipeline' }
            Write-Heartbeat $fake.Phase $fake.Step
        }
        # A made-up Auto step, so the merged file carries an autoLanes object.
        Set-AutoLanesStatus -Lanes 4 -Target 5.24 -Binding 'weekly pace' -Reason 'lanes 3 -> 4 (weekly pace allows 5.2)'
        $json = Get-BoardStatusJson -Branch 'work/dark-factory'
        $json
        New-BoardCommit $json
    } finally { Remove-Item $LogDir -Recurse -Force -ErrorAction SilentlyContinue }
    exit 0
}

# This file ran to its end; RunDarkFactory.ps1 goes on to the next one.
$AreaFileFinished = $true
