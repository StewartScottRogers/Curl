# -Restart: which lanes to stop now, its self-test, and the restart itself. Dot-sourced by
# RunDarkFactory.ps1 in its order; see the loader there.

# ---------------------------------------------------------------------------- restart

function Select-LanesToStop {
    # The lanes -Restart may stop now, from -Phases (lane number -> heartbeat phase, '' when
    # it has none): all but a lane mid-claim, which could leave a task in Doing that no
    # lane holds, or mid-integration, which could leave a half-finished rebase (BL-895). A
    # lane whose run began under two minutes ago (-TaskAgeSeconds: lane number -> seconds
    # since it took its task) is left to get going too: stopping it leaves a log with no
    # result and a run that costs a resume for nothing (AF-0129).
    param([hashtable]$Phases, [hashtable]$TaskAgeSeconds = @{})
    return @($Phases.Keys | Where-Object {
        $Phases[$_] -notin 'claim', 'integrate' -and -not ($Phases[$_] -eq 'run' -and $TaskAgeSeconds.ContainsKey($_) -and $TaskAgeSeconds[$_] -lt 120)
    } | Sort-Object)
}

function Set-ForwardedArgument {
    # The old shift's arguments (-Forward) with each parameter passed beside -Restart
    # (-Override: name -> value, a switch as a SwitchParameter) put in place of the old one,
    # so `-Restart -MaxLanes 12` restarts the same shift with a new lane maximum.
    param([string[]]$Forward, [hashtable]$Override)
    $kept = [Collections.Generic.List[string]]::new()
    for ($i = 0; $i -lt $Forward.Count; $i++) {
        $name = if ($Forward[$i] -match '^-(\w+)$') { $Matches[1] } else { '' }
        if (-not $name -or -not $Override.ContainsKey($name)) { $kept.Add($Forward[$i]); continue }
        if ($i + 1 -lt $Forward.Count -and $Forward[$i + 1] -notmatch '^-\w') { $i++ }
    }
    foreach ($name in $Override.Keys | Sort-Object) {
        $value = $Override[$name]
        if ($value -is [System.Management.Automation.SwitchParameter]) {
            if ($value.IsPresent) { $kept.Add("-$name") }
        } else { $kept.Add("-$name"); $kept.Add("$value") }
    }
    return @($kept)
}

function Add-StoppedShiftEnd {
    # -Restart kills the coordinator before it can log its own "shift end", and the process
    # audit counts any shift log without one as a live shift, so the replaced shift's
    # unfinished runs looked running, not stopped (AF-0313). Writes the line for it, in
    # Write-Trace's format, to the shift's trace log (-Path); returns it.
    param([string]$Path, [datetime]$At = (Get-Date))
    $line = '{0} {1,-6} {2,-7} {3}' -f $At.ToString('HH:mm:ss'), '-', 'shift', 'end (restarted: replaced by a new shift)'
    Add-Content -Path $Path -Value $line.TrimEnd() -Encoding UTF8
    return $line.TrimEnd()
}

function Remove-StoppedCoordinatorStamp {
    # -Restart stops the coordinator on purpose, so its stamp must not read as a coordinator
    # whose loop stopped: the lanes would replace it 9 minutes on, beside the new shift (BL-4396).
    param([string]$StampPath)
    Remove-Item -LiteralPath $StampPath -Force -ErrorAction SilentlyContinue
}

if ($TestRestart) {
    $stampScratch = Join-Path ([IO.Path]::GetTempPath()) "restart-stamp-$PID.stamp"
    Set-Content $stampScratch 'pid=1' -Encoding UTF8
    Remove-StoppedCoordinatorStamp $stampScratch
    Remove-StoppedCoordinatorStamp $stampScratch
    $ok8 = -not (Test-Path $stampScratch)
    Write-Host "$(if ($ok8) { 'PASS' } else { 'FAIL' }) -Restart removes the coordinator stamp, present or not, so no lane replaces the stopped coordinator: $ok8" -ForegroundColor $(if ($ok8) { 'Green' } else { 'Red' })
    $phases = @{ 1 = 'run'; 2 = 'integrate'; 3 = 'claim'; 4 = 'tokens'; 5 = 'wait'; 6 = 'finished'; 7 = '' }
    $got = (Select-LanesToStop $phases) -join ','
    $ok = $got -ceq '1,4,5,6,7'
    Write-Host "$(if ($ok) { 'PASS' } else { 'FAIL' }) stop run, tokens, wait, finished and no heartbeat; wait for claim and integrate: $got" -ForegroundColor $(if ($ok) { 'Green' } else { 'Red' })
    $none = (Select-LanesToStop @{ 1 = 'claim'; 2 = 'integrate' }).Count
    $ok2 = $none -eq 0
    Write-Host "$(if ($ok2) { 'PASS' } else { 'FAIL' }) nothing stops while every lane claims or integrates: $none" -ForegroundColor $(if ($ok2) { 'Green' } else { 'Red' })
    $young = (Select-LanesToStop @{ 1 = 'run'; 2 = 'run'; 3 = 'run' } @{ 1 = 8; 2 = 600 }) -join ','
    $ok3 = $young -ceq '2,3'
    Write-Host "$(if ($ok3) { 'PASS' } else { 'FAIL' }) a run under two minutes old waits; an older one and one of unknown age stop: $young" -ForegroundColor $(if ($ok3) { 'Green' } else { 'Red' })
    $scratch = Join-Path ([IO.Path]::GetTempPath()) "restart-test-$PID.log"
    Set-Content $scratch 'start' -Encoding UTF8
    $endLine = Add-StoppedShiftEnd $scratch (Get-Date '2026-10-19T19:24:24')
    $ok4 = $endLine -match '^19:24:24 - +shift +end \(restarted' -and (Get-Content $scratch)[-1] -ceq $endLine
    Remove-Item $scratch -ErrorAction SilentlyContinue
    Write-Host "$(if ($ok4) { 'PASS' } else { 'FAIL' }) a restart ends the replaced shift's log with a shift end line: $endLine" -ForegroundColor $(if ($ok4) { 'Green' } else { 'Red' })
    $sleeper = if (Test-FactoryOnWindows) { Start-Process -FilePath 'powershell' -ArgumentList '-NoProfile', '-Command', 'Start-Sleep 60' -PassThru -WindowStyle Hidden } else { Start-Process -FilePath 'sleep' -ArgumentList '60' -PassThru }
    Stop-FactoryProcessTree $sleeper.Id
    $ok5 = $sleeper.WaitForExit(10000)
    if (-not $ok5) { $sleeper.Kill() }
    Write-Host "$(if ($ok5) { 'PASS' } else { 'FAIL' }) -Restart stops a process and its tree on this OS: $ok5" -ForegroundColor $(if ($ok5) { 'Green' } else { 'Red' })
    $old = @('-NewTab', '-Lanes', 'Auto', '-MaxLanes', '9', '-Continuous', '-Model', 'auto')
    $set = (Set-ForwardedArgument $old @{ MaxLanes = 12; WeeklyPace = [switch]$true }) -join ' '
    $ok6 = $set -ceq '-NewTab -Lanes Auto -Continuous -Model auto -MaxLanes 12 -WeeklyPace'
    Write-Host "$(if ($ok6) { 'PASS' } else { 'FAIL' }) a parameter passed beside -Restart replaces the old shift's: $set" -ForegroundColor $(if ($ok6) { 'Green' } else { 'Red' })
    $same = (Set-ForwardedArgument $old @{}) -join ' '
    $ok7 = $same -ceq ($old -join ' ')
    Write-Host "$(if ($ok7) { 'PASS' } else { 'FAIL' }) with nothing beside -Restart the old arguments carry over unchanged: $same" -ForegroundColor $(if ($ok7) { 'Green' } else { 'Red' })
    exit $(if ($ok -and $ok2 -and $ok3 -and $ok4 -and $ok5 -and $ok6 -and $ok7 -and $ok8) { 0 } else { 1 })
}

if ($Restart) {
    # This checkout's coordinator only: another repository's factory runs its own copy.
    $mine = [regex]::Escape($FactoryScript)
    $coordinator = Get-FactoryProcessTable | Where-Object {
        $_.ProcessId -ne $PID -and $_.CommandLine -match "-File\s+`"?$mine`"?(\s|$)" -and
        $_.CommandLine -notmatch '\s-Lane\s' -and $_.CommandLine -notmatch '\s-(Restart|NewTab)\b'
    } | Sort-Object CreationDate, ProcessId -Descending | Select-Object -First 1
    # The newest: an ended shift's coordinator can still be running its shift-end review
    # beside the next shift (ADR-0710), and it is not the shift to restart.
    if (-not $coordinator) { Write-Host 'No dark factory shift is running in this checkout.'; exit 1 }
    # The same arguments, so a fixed lane count, -WeeklyPace or -Hours carry over.
    $rest = ($coordinator.CommandLine -split [regex]::Escape((Split-Path $FactoryScript -Leaf)), 2)[1]
    $forward = @([regex]::Matches("$rest".Trim().TrimStart('"').Trim(), '"[^"]*"|\S+') | ForEach-Object { $_.Value })
    # Anything passed beside -Restart (e.g. -MaxLanes 12) replaces the old shift's value.
    $override = @{}
    foreach ($name in $FactoryParameters.Keys | Where-Object { $_ -notin 'Restart', 'NewTab' }) { $override[$name] = $FactoryParameters[$name] }
    $forward = Set-ForwardedArgument $forward $override
    $branchNow = "$(git -C $Root rev-parse --abbrev-ref HEAD)".Trim()
    if ($forward -notcontains '-ShiftBranch' -and $branchNow -notin 'master', 'main', 'HEAD') { $forward += @('-ShiftBranch', $branchNow) }
    $lanesDir = Get-ChildItem $LogDir -Directory -Filter 'lanes-*' -ErrorAction SilentlyContinue | Sort-Object Name | Select-Object -Last 1
    $stamp = if ($lanesDir) { $lanesDir.Name.Substring(6) } else { '' }
    Write-Host "Stopping the shift's coordinator (pid $($coordinator.ProcessId)) first, so it restarts no lane."
    if ($stamp) { Remove-StoppedCoordinatorStamp (Get-CoordinatorStampPath -ForStamp $stamp) }
    Stop-FactoryProcessTree $coordinator.ProcessId
    while ($stamp) {
        $phases = @{}
        foreach ($n in 1..16) {
            $lanePid = Get-LaneState $n 'pid' $stamp
            if (-not $lanePid -or -not (Get-Process -Id ([int]$lanePid) -ErrorAction SilentlyContinue | Where-Object ProcessName -in 'powershell', 'pwsh')) { continue }
            $beat = Get-LaneStatePath $n 'heartbeat.json' $stamp
            $phases[$n] = if (Test-Path $beat) { "$((Get-Content $beat -Raw | ConvertFrom-Json).phase)" } else { '' }
        }
        if (-not $phases.Count) { break }
        $ages = @{}
        foreach ($n in $phases.Keys) {
            $taskFile = Get-LaneStatePath $n 'task' $stamp
            if (Test-Path $taskFile) { $ages[$n] = ((Get-Date) - (Get-Item $taskFile).LastWriteTime).TotalSeconds }
        }
        foreach ($n in Select-LanesToStop $phases $ages) {
            Stop-FactoryProcessTree ([int](Get-LaneState $n 'pid' $stamp))
            Write-Host "$(Get-Date -Format 'HH:mm:ss') lane $n stopped ($(if ($phases[$n]) { $phases[$n] } else { 'no heartbeat' }); $(Get-LaneState $n 'task' $stamp))"
        }
        $waiting = @($phases.Keys | Where-Object { $phases[$_] -in 'claim', 'integrate' -or ($phases[$_] -eq 'run' -and $ages.ContainsKey($_) -and $ages[$_] -lt 120) })
        if ($waiting.Count) { Start-Sleep -Seconds 5 }
    }
    $oldTrace = if ($stamp) { Join-Path $LogDir "DarkFactory-$stamp.log" } else { '' }
    if ($oldTrace -and (Test-Path $oldTrace)) { Add-StoppedShiftEnd $oldTrace | Out-Null }
    $where = Start-Detached -Label 'DF shift starting' -Dir $Root -ScriptArgs $forward
    Write-Host "New shift started ($(if ($where.Tab) { "herdr tab $($where.Tab)" } else { "pid $($where.Process.Id)" })) with: $($forward -join ' ')"
    exit 0
}

# This file ran to its end; RunDarkFactory.ps1 goes on to the next one.
$AreaFileFinished = $true
