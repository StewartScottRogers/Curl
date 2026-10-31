# The machine probe: how many solution builds this PC sustains at once, its self-test, and
# -ProbeMachine. Dot-sourced by RunDarkFactory.ps1 in its order; see the loader there.

# ---- machine probe
# How many concurrent solution builds this PC sustains (ADR-0130 item 7). The pass and cap
# rule is pure, so -TestMachineProbe proves it on recorded steps; -ProbeMachine measures.

# Each step's builds run with MSBuild node reuse off and any worker node still found under them is
# stopped when the step ends, by parentage only (BL-4348).
# Lanes build for minutes of each task and integrate one at a time, so k simultaneous
# builds is a worst case, and how much slower they run swings with the one-build baseline
# (13.6 s one probe, 9.0 s the next). Only memory running out stalls lanes, so memory alone
# sets the cap; wall time and slowdown are recorded for reading (BL-812).
$MachineProbeRule = 'every build succeeds and free memory >= 20%'

function Test-MachineProbeStep {
    # Whether one probe step passes: every build succeeded and the lowest free memory
    # stayed at least 20% of RAM. -OneBuildSeconds is kept for the callers; it decides nothing.
    param($Step, [double]$OneBuildSeconds)
    return ([bool]$Step.Succeeded -and [double]$Step.MinFreeMemoryPercent -ge 20)
}

function Get-MachineLaneCap {
    # The largest passing lane count before the first failing step, at least 1. Complete is
    # false when the steps ran out (at -MaxLanes) before any failed and before 16.
    param([object[]]$Steps, [ValidateRange(1, 16)][int]$MaxLanes = 16)
    $steps = @($Steps | Where-Object { $_ } | Select-Object -First $MaxLanes)
    $cap = 0
    $failed = $false
    foreach ($step in $steps) {
        if (-not (Test-MachineProbeStep -Step $step -OneBuildSeconds $steps[0].Seconds)) { $failed = $true; break }
        $cap = [int]$step.Lanes
    }
    return [pscustomobject]@{ Cap = [math]::Max(1, $cap); Complete = ($failed -or $cap -ge 16) }
}

if ($TestMachineProbe) {
    function New-TestProbeSteps {
        # Recorded steps for k = 1, 2, ...: wall seconds, free memory percent (50 unless
        # given) and whether every build succeeded (unless k is in -FailedAt).
        param([double[]]$Seconds, [double[]]$FreePercent = @(), [int[]]$FailedAt = @())
        $k = 0
        return @($Seconds | ForEach-Object {
            $k++
            $free = if ($FreePercent.Count -ge $k) { $FreePercent[$k - 1] } else { 50 }
            [pscustomobject]@{ Lanes = $k; Seconds = $_; MinFreeMemoryPercent = $free; Succeeded = ($FailedAt -notcontains $k) }
        })
    }
    function Get-TestCapText {
        param([object[]]$Steps, [int]$MaxLanes = 16)
        $result = Get-MachineLaneCap -Steps $Steps -MaxLanes $MaxLanes
        return "cap $($result.Cap), $(if ($result.Complete) { 'complete' } else { 'not complete' })"
    }
    $cases = @(
        ,@('slow-steps-still-pass', 'cap 5, complete', (Get-TestCapText (New-TestProbeSteps 9.0, 33.2, 32.4, 41.4, 60, 70 -FreePercent 75.3, 61.2, 67.5, 63.8, 30, 19)))
        ,@('knee-by-memory', 'cap 2, complete', (Get-TestCapText (New-TestProbeSteps 60, 61, 63, 64 -FreePercent 40, 25, 19, 8)))
        ,@('failed-build', 'cap 2, complete', (Get-TestCapText (New-TestProbeSteps 60, 61, 62 -FailedAt 3)))
        ,@('all-pass-to-16', 'cap 16, complete', (Get-TestCapText (New-TestProbeSteps (@(60) * 16))))
        ,@('cut-short', 'cap 2, not complete', (Get-TestCapText (New-TestProbeSteps 60, 61) 2))
        ,@('first-step-fails', 'cap 1, complete', (Get-TestCapText (New-TestProbeSteps 60 -FailedAt 1))))
    $failed = 0
    foreach ($case in $cases) {
        if ($case[1] -ceq $case[2]) { Write-Host "PASS $($case[0]): $($case[2])" -ForegroundColor Green }
        else { Write-Host "FAIL $($case[0]): expected $($case[1]), got $($case[2])" -ForegroundColor Red; $failed++ }
    }
    exit $(if ($failed) { 1 } else { 0 })
}

function Invoke-ProbeBuilds {
    # Runs -Count concurrent builds of the checkout, each with its own artifacts folder
    # under -ProbeDir, sampling free memory every 2 s. Returns the step's measurements.
    param([string]$ProbeDir, [int]$Count, [switch]$Incremental)
    $totalKB = (Get-FactoryTotalMemoryBytes) / 1KB
    $minFreeKB = (Get-FactoryFreeMemoryBytes) / 1KB
    $clock = [Diagnostics.Stopwatch]::StartNew()
    # Node reuse off, so each build's MSBuild worker nodes exit with it; the nodes seen under a
    # build are stopped when the step ends, so none is left to eat the next step's memory (BL-4348).
    $savedNodeReuse = $env:MSBUILDDISABLENODEREUSE
    $env:MSBUILDDISABLENODEREUSE = '1'
    $builds = foreach ($i in 1..$Count) {
        $artifacts = Join-Path $ProbeDir "$i"
        New-Item -ItemType Directory -Force -Path $artifacts | Out-Null
        $buildArgs = @('build', "`"$Root`"", '-nologo', '-v', 'q', '--artifacts-path', "`"$artifacts`"")
        if (-not $Incremental) { $buildArgs += '--no-incremental' }
        $process = Start-Process dotnet -ArgumentList $buildArgs -PassThru -NoNewWindow `
            -RedirectStandardOutput (Join-Path $ProbeDir "$i.out") -RedirectStandardError (Join-Path $ProbeDir "$i.err")
        # Windows PowerShell only reports ExitCode for a process whose handle was taken early.
        [void]$process.Handle
        $process
    }
    $env:MSBUILDDISABLENODEREUSE = $savedNodeReuse
    $nodeIds = [System.Collections.Generic.HashSet[int]]::new()
    while (@($builds | Where-Object { -not $_.HasExited }).Count) {
        Start-Sleep -Seconds 2
        foreach ($id in @(Get-MsBuildNodeIdsUnder -Table @(Get-FactoryProcessTable) -RootIds @($builds | ForEach-Object { $_.Id }))) { [void]$nodeIds.Add($id) }
        $minFreeKB = [math]::Min($minFreeKB, (Get-FactoryFreeMemoryBytes) / 1KB)
    }
    $clock.Stop()
    $builds | ForEach-Object { $_.WaitForExit() }
    foreach ($id in $nodeIds) { Stop-FactoryProcessTree -ProcessId $id }
    return [pscustomobject]@{
        Lanes = $Count; Seconds = [math]::Round($clock.Elapsed.TotalSeconds, 1)
        MinFreeMemoryPercent = [math]::Round(100 * $minFreeKB / $totalKB, 1)
        Succeeded = (@($builds | Where-Object { $_.ExitCode -ne 0 }).Count -eq 0)
    }
}

$MachineFile = Join-Path $LanesDir 'machine-lanes.json'

function Invoke-MachineProbe {
    # Warm up once, then step k = 1, 2, ... concurrent builds until a step fails, 16, or
    # -ProbeMaxLanes, write the cap to <LanesDir>\machine-lanes.json and return it.
    $probeDir = Join-Path $LanesDir 'probe'
    $machineFile = $MachineFile
    New-Item -ItemType Directory -Force -Path $probeDir | Out-Null
    Write-Trace 'probe' 'warm-up' "one build of $Root, not measured"
    $warmUp = Invoke-ProbeBuilds -ProbeDir $probeDir -Count 1 -Incremental
    if (-not $warmUp.Succeeded) { Write-Trace 'probe' 'warn' 'the warm-up build failed' 'Yellow' }
    $steps = @()
    foreach ($k in 1..$ProbeMaxLanes) {
        $step = Invoke-ProbeBuilds -ProbeDir $probeDir -Count $k
        $steps += $step
        $passed = Test-MachineProbeStep -Step $step -OneBuildSeconds $steps[0].Seconds
        $culture = [Globalization.CultureInfo]::InvariantCulture
        Write-Trace 'probe' $(if ($passed) { 'pass' } else { 'fail' }) ("{0} builds {1}s, {2}x, free memory {3}%{4}" -f $k,
            $step.Seconds.ToString('0.0', $culture), ($step.Seconds / $steps[0].Seconds).ToString('0.00', $culture),
            $step.MinFreeMemoryPercent.ToString('0.0', $culture), $(if ($step.Succeeded) { '' } else { ', a build failed' })) `
            $(if ($passed) { 'Green' } else { 'Yellow' })
        if (-not $passed) { break }
    }
    $result = Get-MachineLaneCap -Steps $steps -MaxLanes $ProbeMaxLanes
    $memoryBytes = Get-FactoryTotalMemoryBytes
    $record = [ordered]@{
        schema = 1; probedAt = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
        logicalProcessors = [Environment]::ProcessorCount; memoryGB = [math]::Round($memoryBytes / 1GB, 1)
        complete = $result.Complete; cap = $result.Cap; rule = $MachineProbeRule
        steps = @($steps | ForEach-Object {
            [ordered]@{ lanes = $_.Lanes; seconds = $_.Seconds; slowdown = [math]::Round($_.Seconds / $steps[0].Seconds, 2)
                minFreeMemoryPercent = $_.MinFreeMemoryPercent; succeeded = $_.Succeeded }
        })
    }
    $temp = "$machineFile.tmp"
    # Windows PowerShell escapes < and > in JSON; the rule reads better as written.
    $json = ($record | ConvertTo-Json -Depth 4) -replace '\\u003c', '<' -replace '\\u003e', '>'
    [IO.File]::WriteAllText($temp, $json, (New-Object Text.UTF8Encoding $false))
    Move-Item -Force -Path $temp -Destination $machineFile
    Remove-Item -Recurse -Force -Path $probeDir -ErrorAction SilentlyContinue
    Write-Trace 'probe' 'done' "machine sustains $($result.Cap)$(if (-not $result.Complete) { ' (incomplete)' }); $machineFile" 'Cyan'
    return $result
}

if ($ProbeMachine) { Invoke-MachineProbe | Out-Null; exit 0 }

# This file ran to its end; RunDarkFactory.ps1 goes on to the next one.
$AreaFileFinished = $true
