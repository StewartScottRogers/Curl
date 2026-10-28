# -Lanes Auto's reads and writes: the saved auto-lanes.json, the machine cap, the board's
# capacity, one step, and -AutoLanesReport. Dot-sourced by RunDarkFactory.ps1 in its order;
# see the loader there.

# ---- auto lanes: the shift's reads and writes (ADR-0130 items 1, 5, 7 and 8)

$AutoLanesFile = Join-Path $LanesDir 'auto-lanes.json'
# A detached worktree the coordinator reads the board's capacity from, so its own checkout
# is pulled only at shift end: lanes run its copy of the factory's code.
$AutoBoardDir = Join-Path $LanesDir 'auto-board'
# The samples of the last 3 hours, the rates last metered, the saved auto-lanes.json, the
# machine cap, and whether a capacity failure has been traced yet.
$script:Auto = @{ Samples = @(); FiveHourRate = $null; WeeklyRate = $null; Saved = $null; MachineCap = 16; CapacityTraced = $false; LastLow = $false
    CapacityStreak = 0; SplitFiled = @() }

function Read-JsonFile {
    # The file's JSON, or $null when it is missing or does not parse.
    param([string]$Path)
    if (-not (Test-Path $Path)) { return $null }
    try { return (Get-Content -Raw -Path $Path | ConvertFrom-Json) } catch { return $null }
}

function Get-MachineProbeNeed {
    # Why the machine cap must be measured again - no probe yet, an incomplete one, or other
    # hardware - or '' while machine-lanes.json still describes this PC.
    param($Record)
    if (-not $Record) { return 'no probe yet' }
    if (-not $Record.complete) { return 'the last probe was incomplete' }
    if ("$($Record.rule)" -ne $MachineProbeRule) { return 'the probe rule changed' }
    if ([int]$Record.logicalProcessors -ne [Environment]::ProcessorCount) { return 'the processor count changed' }
    $memoryGB = (Get-FactoryTotalMemoryBytes) / 1GB
    if ([math]::Abs([double]$Record.memoryGB - $memoryGB) -gt 1) { return 'the memory size changed' }
    return ''
}

function Get-MachineCap {
    # This PC's lane cap and the line saying where it came from. -Probe measures it when
    # machine-lanes.json is missing, incomplete or from other hardware; without -Probe the
    # cap is then unknown and does not limit the lanes.
    param([switch]$Probe)
    $record = Read-JsonFile $MachineFile
    $need = Get-MachineProbeNeed $record
    if ($need -and -not $Probe) { return [pscustomobject]@{ Cap = 16; Text = "machine cap unknown ($need)" } }
    if ($need) {
        Write-Trace '-' 'probe' "measuring the machine cap first: $need" 'Cyan'
        Invoke-MachineProbe | Out-Null
        $record = Read-JsonFile $MachineFile
        if (-not $record) { return [pscustomobject]@{ Cap = 16; Text = 'machine cap unknown (the probe wrote no file)' } }
    }
    $cap = [math]::Max(1, [int]$record.cap)
    return [pscustomobject]@{ Cap = $cap; Text = "machine cap $cap lanes (probed $(([datetime]$record.probedAt).ToString('yyyy-MM-dd')))" }
}

function Get-BoardCapacity {
    # How many tasks the board at -Ref could run at once, from the capacity command of the
    # task board in the $AutoBoardDir worktree checked out at -Ref. Created returns whether
    # this call made the worktree; Capacity is $null, with Why, when it could not be read.
    param([string]$Ref)
    $created = $false
    $saved = $env:CLAUDE_PROJECT_DIR
    try {
        if (-not (Test-Path (Join-Path $AutoBoardDir '.git'))) {
            New-Item -ItemType Directory -Force -Path $LanesDir | Out-Null
            git -C $Root worktree add -q --detach $AutoBoardDir $Ref 2>&1 | Out-Null
            if ($LASTEXITCODE) { return [pscustomobject]@{ Capacity = $null; Created = $false; Why = "cannot add the worktree $AutoBoardDir" } }
            $created = $true
        }
        git -C $AutoBoardDir checkout -q --detach $Ref 2>&1 | Out-Null
        if ($LASTEXITCODE) { return [pscustomobject]@{ Capacity = $null; Created = $created; Why = "cannot check out $Ref in $AutoBoardDir" } }
        $env:CLAUDE_PROJECT_DIR = $AutoBoardDir
        $out = (& (Get-FactoryPowerShellExe) -NoProfile -ExecutionPolicy Bypass -File (Join-Path $AutoBoardDir '.claude\skills\task-board\task-board.ps1') capacity 2>&1) -join "`n"
        $line = @($out -split "`r?`n" | Where-Object { $_ -match '^Capacity \d+:' }) | Select-Object -First 1
        if ($line) {
            # A ready task over its cost cap or interactive only gets no lane (BL-3090).
            $tasksDir = Join-Path $AutoBoardDir 'Tasks'
            $notClaimable = @(Get-CapacityReadyIds $line | Where-Object { Get-TaskNotClaimableWhy $_ -TasksDir $tasksDir })
            return [pscustomobject]@{ Capacity = (Get-ClaimableCapacity $line $notClaimable); Created = $created; Why = '' }
        }
        return [pscustomobject]@{ Capacity = $null; Created = $created; Why = "the board printed no capacity: $(Get-Short $out 60)" }
    } catch {
        return [pscustomobject]@{ Capacity = $null; Created = $created; Why = $_.Exception.Message }
    } finally { $env:CLAUDE_PROJECT_DIR = $saved }
}

function Remove-AutoBoard {
    # Deletes the $AutoBoardDir worktree.
    git -C $Root worktree remove --force $AutoBoardDir 2>&1 | Out-Null
    git -C $Root worktree prune 2>&1 | Out-Null
}

function Get-AutoLaneStep {
    # One Auto step's computation from -Samples, newest last: both burn rates (the saved
    # ones while the samples give none), the pace target and the next lane count.
    param([object[]]$Samples, [int]$Current, [int]$Capacity, [int]$MachineCap, $Saved, [bool]$PreviousLow = $false)
    $fiveHourRate = Get-BurnRate -Samples $Samples -Window FiveHour
    if ($null -eq $fiveHourRate -and $Saved -and $null -ne $Saved.fiveHourRatePerLane) { $fiveHourRate = [double]$Saved.fiveHourRatePerLane }
    $weeklyRate = Get-BurnRate -Samples $Samples -Window Week
    if ($null -eq $weeklyRate -and $Saved -and $null -ne $Saved.weeklyRatePerLane) { $weeklyRate = [double]$Saved.weeklyRatePerLane }
    $pace = $null
    if (@($Samples).Count) {
        $pace = Get-PaceTarget -Sample @($Samples)[-1] -FiveHourRate $fiveHourRate -WeeklyRate $weeklyRate -WeeklyPace $WeeklyPace.IsPresent `
            -StopAtUsage $StopAtUsage -StopAtWeeklyUsage $StopAtWeeklyUsage
    }
    $next = Get-NextLaneCount -Current $Current -Pace $pace -Capacity $Capacity -MachineCap $MachineCap -MaxLanes $MaxLanes -PreviousLow $PreviousLow
    return [pscustomobject]@{ FiveHourRate = $fiveHourRate; WeeklyRate = $weeklyRate; Pace = $pace; Next = $next }
}

function Save-AutoLanes {
    # Writes auto-lanes.json: -Count lanes and the rates last metered, for the next shift's
    # cold start. A temporary file and a rename, so a reader never sees half of one.
    param([int]$Count)
    $record = [ordered]@{
        schema = 1; lanes = $Count; savedAt = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
        fiveHourRatePerLane = $(if ($null -ne $script:Auto.FiveHourRate) { [math]::Round([double]$script:Auto.FiveHourRate, 3) } else { $null })
        weeklyRatePerLane = $(if ($null -ne $script:Auto.WeeklyRate) { [math]::Round([double]$script:Auto.WeeklyRate, 3) } else { $null })
    }
    try {
        New-Item -ItemType Directory -Force -Path $LanesDir | Out-Null
        $temp = "$AutoLanesFile.tmp"
        [IO.File]::WriteAllText($temp, ($record | ConvertTo-Json), (New-Object Text.UTF8Encoding $false))
        Move-Item -Force -Path $temp -Destination $AutoLanesFile
    } catch { Write-Trace '-' 'lanes' "cannot save $($AutoLanesFile): $($_.Exception.Message)" 'Yellow' }
}

function Format-Rate {
    # A rate or target with one decimal, invariant culture; 'none yet' for $null, 'unbounded' for infinity.
    param($Value)
    if ($null -eq $Value) { return 'none yet' }
    if ([double]::IsPositiveInfinity([double]$Value)) { return 'unbounded' }
    return ([double]$Value).ToString('0.0', [Globalization.CultureInfo]::InvariantCulture)
}

if ($AutoLanesReport) {
    # The shift-start reads and one step's computation from the newest readings in $LogDir,
    # printed; never the probe, a lane, a claim or a push. The board is read at origin/-Branch
    # when -Branch is given, otherwise at this checkout's commit.
    $machine = Get-MachineCap
    Write-Host $machine.Text
    $ref = (git -C $Root rev-parse HEAD).Trim()
    if ($Branch) {
        git -C $Root fetch -q origin $Branch 2>&1 | Out-Null
        $ref = "origin/$Branch"
    }
    $board = Get-BoardCapacity -Ref $ref
    if ($board.Created) { Remove-AutoBoard }
    $saved = Read-JsonFile $AutoLanesFile
    $capacity = $board.Capacity
    if ($null -eq $capacity) { Write-Host "capacity unknown ($($board.Why)); counted as $MaxLanes"; $capacity = $MaxLanes }
    else { Write-Host "capacity $capacity (board at $ref)" }
    $start = Get-AutoStartCount -Saved $saved -Capacity $capacity -MachineCap $machine.Cap
    $reading = Get-UsageReading
    $samples = @()
    if ($reading) { $samples = @(New-UsageSample -At (Get-Date) -Reading $reading -ActiveLanes 0) }
    $step = Get-AutoLaneStep -Samples $samples -Current $start.Count -Capacity $capacity -MachineCap $machine.Cap -Saved $saved
    $savedNote = if ($saved) { " (saved $($saved.savedAt))" } else { '' }
    Write-Host "5-hour rate $(Format-Rate $step.FiveHourRate)$(if ($null -ne $step.FiveHourRate) { ' points/hour/lane' })$savedNote"
    Write-Host "weekly rate $(Format-Rate $step.WeeklyRate)$(if ($null -ne $step.WeeklyRate) { ' points/hour/lane' })$savedNote"
    if ($reading) {
        Write-Host ("usage 5-hour {0}% (reset {1}), weekly {2}% (reset {3})" -f [math]::Round($reading.FiveHour * 100),
            $reading.FiveHourResets.ToString('HH:mm'), [math]::Round($reading.Week * 100), $reading.WeekResets.ToString('ddd HH:mm'))
    } else { Write-Host "usage unknown (no reading in $LogDir)" }
    if ($step.Pace) { Write-Host "5-hour target $(Format-Rate $step.Pace.FiveHour) lanes, weekly target $(Format-Rate $step.Pace.Weekly) lanes$(if (-not $WeeklyPace) { ' (not pacing weekly)' })" }
    else { Write-Host '5-hour target none yet, weekly target none yet' }
    Write-Host "start count $($start.Count) ($($start.Why))"
    Write-Host $step.Next.Reason
    exit 0
}

# This file ran to its end; RunDarkFactory.ps1 goes on to the next one.
$AreaFileFinished = $true
