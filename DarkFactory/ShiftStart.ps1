# The start of a shift that runs work: the lane marker, the log folder, the refusal while an
# audit runs, and the shift's end time. Dot-sourced by RunDarkFactory.ps1 in its order; see
# the loader there.

# ---------------------------------------------------------------------------- shift

# Every launcher and self-test has exited by now, so this process runs work: mark it and
# everything it starts (LANE MARKER in RunDarkFactory.ps1's help).
$env:CURL_DARK_FACTORY_LANE = Get-LaneMarker -ForLane $Lane
$LaneMarkerTrace = "lane-marker=CURL_DARK_FACTORY_LANE=$env:CURL_DARK_FACTORY_LANE"

New-Item -ItemType Directory -Force -Path $LogDir | Out-Null
# Shifts start between audits (BL-1022): while Audit\RunAudit.ps1 runs, refuse, or with
# -Continuous wait and look again every 5 minutes. Lanes are started by a shift that already
# passed this, so only the coordinator or a single runner checks.
if (-not $Lane) {
    while ($true) {
        $ciFixWaiting = [bool](Get-ChildItem (Join-Path $Root 'Tasks\Backlog') -Filter 'BL-*.md' -ErrorAction SilentlyContinue | Select-String -Pattern '^title: .*(Fix CI failure|Fix flaky CI test)' -List)
        $decision = Get-ShiftStartDecision @(Get-CimInstance Win32_Process -ErrorAction SilentlyContinue | Select-Object CommandLine) $Continuous.IsPresent $ciFixWaiting
        if ($decision -eq 'start') { break }
        if ($decision -eq 'refuse') { Write-Trace '-' 'refuse' 'An audit is running; shifts start between audits.' 'Red'; exit 1 }
        Write-Trace '-' 'audit' 'An audit is running; shifts start between audits. Looking again in 5 minutes.' 'Yellow'
        Start-Sleep -Seconds 300
    }
}
# Logs used to be written to logs\ inside the checkout. Move them beside it, where the
# next shift looks for the lanes it adopts - but never while a lane still writes there.
$oldLogDir = Join-Path $Root 'logs'
if (-not $Lane -and -not $LogRoot -and (Test-Path $oldLogDir)) {
    # Only the newest shift can still be running; a lane is a powershell process.
    $newest = Get-ChildItem $oldLogDir -Directory -Filter 'lanes-*' -ErrorAction SilentlyContinue | Sort-Object Name | Select-Object -Last 1
    $pids = if ($newest) { @(Get-ChildItem $newest.FullName -Filter 'lane-*.pid') } else { @() }
    $writing = @($pids | Where-Object { $p = "$(Get-Content $_.FullName -TotalCount 1)".Trim(); $p -match '^\d+$' -and (Get-Process -Id ([int]$p) -ErrorAction SilentlyContinue | Where-Object ProcessName -eq 'powershell') })
    if (-not $writing.Count) {
        Get-ChildItem $oldLogDir -Force | Move-Item -Destination $LogDir -Force -ErrorAction SilentlyContinue
        if (-not (Get-ChildItem $oldLogDir -Force -ErrorAction SilentlyContinue)) { Remove-Item $oldLogDir -Force -ErrorAction SilentlyContinue }
        Write-Trace '-' 'logs' "moved logs\ to $LogDir"
    }
}
$shiftEnd = (Get-Date).AddHours($Hours)

# ------------------------------------------------ coordinator: start lanes, wait, alarm

# Auto always coordinates lanes, even at one lane.

# This file ran to its end; RunDarkFactory.ps1 goes on to the next one.
$AreaFileFinished = $true
