# Starting a shift or lane in a herdr tab or console window, tab captions, the lane marker
# (CURL_DARK_FACTORY_LANE) and its self-test, and -NewTab. Dot-sourced by RunDarkFactory.ps1
# in its order; see the loader there.

# ---------------------------------------------------------------------------- herdr

function Get-HerdrBin {
    # The herdr binary when this process runs inside a herdr pane, otherwise $null.
    if ($env:HERDR_ENV -ne '1') { return $null }
    if ($env:HERDR_BIN_PATH -and (Test-Path $env:HERDR_BIN_PATH)) { return $env:HERDR_BIN_PATH }
    $cmd = Get-Command herdr -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    return $null
}

function Start-Detached {
    # Runs RunDarkFactory.ps1 from $Dir with $ScriptArgs somewhere the user can watch: a
    # new tab in the same herdr workspace when inside herdr, else a new console window.
    # Returns @{ Process = <Process> } or @{ Tab = '<tab id>' }.
    param([string]$Label, [string]$Dir, [string[]]$ScriptArgs)
    # Always this copy of the script; a lane is told its worktree with -LaneDir.
    $script = $FactoryScript
    if ((Resolve-Path $Dir).Path -ne (Resolve-Path $FactoryScriptDir).Path) { $ScriptArgs = @($ScriptArgs) + @('-LaneDir', "`"$Dir`"") }
    $herdr = Get-HerdrBin
    if ($herdr) {
        $create = @('tab', 'create', '--cwd', $Dir, '--label', $Label, '--no-focus')
        if ($env:HERDR_WORKSPACE_ID) { $create += @('--workspace', $env:HERDR_WORKSPACE_ID) }
        $created = (& $herdr @create) -join "`n" | ConvertFrom-Json
        $pane = $created.result.root_pane.pane_id
        if ($pane) {
            & $herdr pane wait-output $pane --match '>' --timeout 15000 2>&1 | Out-Null
            $line = "powershell -NoProfile -ExecutionPolicy Bypass -File `"$script`" " + ($ScriptArgs -join ' ')
            # Windows PowerShell passes a native argument's inner quotes through unescaped,
            # and herdr's argument parser would then strip them, splitting any path with a
            # space. \" survives as a literal quote.
            & $herdr pane run $pane $line.Replace('"', '\"') 2>&1 | Out-Null
            return @{ Tab = $created.result.tab.tab_id }
        }
        Write-Trace '-' 'herdr' 'could not create a herdr tab; using a console window' 'DarkYellow'
    }
    $argList = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$script`"") + $ScriptArgs
    return @{ Process = (Start-Process -FilePath 'powershell.exe' -ArgumentList $argList -WorkingDirectory $Dir -PassThru) }
}

# Tab captions are all Stewart sees of a shift, so each says whose it is and whether it can
# go: "DF 09:07 L1 $Dot BL-670 Create Curl.Cryptography", "DF 09:07 L2 $Dot BLOCKED, read",
# "DF 09:07 shift $Dot done, close". DF and the shift's start time come first, so a shift's
# tabs group together; a finished tab ends in "close" or "read"; anything else is working.
# A lane holding no task is "DF 09:07 L1 $Dot empty": after each task it clears its screen to
# one line saying what it finished, and a lane that ends cleanly reads "empty, close".
# $Dot is U+00B7, the middle dot. Finished tabs are swept at shift end: Close-FinishedHerdrTabs
# closes every such tab, "read" ones included, that has no live run under it (BL-3340).
$Dot = [char]0x00B7
$ShiftTime = if ($Stamp -match '^\d{8}-(\d\d)(\d\d)') { "$($Matches[1]):$($Matches[2])" } else { (Get-Date).ToString('HH:mm') }
$OwnTabPrefix = if ($Lane) { "DF $ShiftTime L$Lane" } else { "DF $ShiftTime shift" }

function Get-LaneTabLabel {
    # The caption the coordinator gives lane $N's tab: its prefix, then $Text if any.
    param([int]$N, [string]$Text)
    $label = "DF $ShiftTime L$N"
    if ($Text) { $label += " $Dot $Text" }
    return $label
}

function Set-HerdrTabLabel {
    # Renames a tab the factory opened; captions past 48 characters are cut with "~".
    param([string]$Tab, [string]$Label)
    $herdr = Get-HerdrBin
    if (-not $herdr -or -not $Tab) { return }
    if ($Label.Length -gt 48) { $Label = $Label.Substring(0, 47) + '~' }
    & $herdr tab rename $Tab $Label 2>&1 | Out-Null
}

function Test-FactoryTab {
    # True when $Tab is one the factory opened ("DF ..." or the older "Dark factory ..."),
    # so a shift Stewart started by hand in his own tab never renames or closes it.
    param([string]$Tab)
    $herdr = Get-HerdrBin
    if (-not $herdr -or -not $Tab) { return $false }
    $label = "$(((& $herdr tab get $Tab) -join "`n" | ConvertFrom-Json).result.tab.label)"
    return ($label -like 'DF *' -or $label -like 'Dark factory*')
}

function Set-OwnTabLabel {
    # Captions this process's own tab: "DF 09:07 L1 $Dot $Text", or just the prefix.
    param([string]$Text)
    if (-not (Test-FactoryTab $env:HERDR_TAB_ID)) { return }
    Set-HerdrTabLabel $env:HERDR_TAB_ID $(if ($Text) { "$OwnTabPrefix $Dot $Text" } else { $OwnTabPrefix })
}

function Show-LaneEmpty {
    # A lane that holds no task says so: the screen is cleared of the last run's output,
    # $Line says what it last did, and the caption reads "empty" ($Caption overrides it).
    param([string]$Line, [string]$Caption = 'empty')
    if (-not $Lane) { return }
    try { Clear-Host } catch { }
    Write-Host $Line
    Set-OwnTabLabel $Caption
}

function Close-HerdrTab {
    # Closes a herdr tab the factory opened, once nothing in it is worth reading: a tab
    # left behind only says "idle" and looks like a lane that is still working. Its
    # trace and summary are in <repo>.logs\ either way. Closing this process's own tab
    # ends it. A tab herdr will not close is captioned "done, close" instead.
    param([string]$Tab, [string]$Why, [string]$DoneLabel)
    $herdr = Get-HerdrBin
    if (-not $herdr -or -not $Tab) { return }
    Write-Trace '-' 'herdr' "closing tab $Tab ($Why)"
    & $herdr tab close $Tab 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0 -and $DoneLabel) { Set-HerdrTabLabel $Tab "$DoneLabel $Dot done, close" }
}

function Test-HerdrPaneRunAlive {
    # True when a factory or sibling-office run is a descendant of the pane's shell: the
    # foreground process is always the pane's own pwsh.exe, so it cannot say. $Processes
    # is Win32_Process rows (ProcessId, ParentProcessId, CommandLine).
    param([int]$ShellPid, [object[]]$Processes)
    $children = @{}
    foreach ($p in $Processes) {
        $key = "$($p.ParentProcessId)"
        if (-not $children.ContainsKey($key)) { $children[$key] = [System.Collections.Generic.List[object]]::new() }
        $children[$key].Add($p)
    }
    $queue = [System.Collections.Generic.Queue[int]]::new()
    $seen = [System.Collections.Generic.HashSet[int]]::new()
    $queue.Enqueue($ShellPid)
    while ($queue.Count -gt 0) {
        $parent = $queue.Dequeue()
        if (-not $children.ContainsKey("$parent")) { continue }
        foreach ($child in $children["$parent"]) {
            if (-not $seen.Add([int]$child.ProcessId)) { continue }
            if ("$($child.CommandLine)" -match 'Run(DarkFactory|Audit|GapAnalysis)\.ps1') { return $true }
            $queue.Enqueue([int]$child.ProcessId)
        }
    }
    return $false
}

function Close-FinishedHerdrTabs {
    # Sweeps the herdr workspace at shift end: closes every factory, audit and gap tab
    # ("DF *", "Dark factory*", "Audit *", "Gap *", the ones ending in "read" too) that has
    # no live run under its pane's shell. Never closes this process's own tab or a tab with
    # another caption. Returns how many it closed; outside herdr, or when herdr fails, it
    # returns what it closed so far after one trace line.
    $herdr = Get-HerdrBin
    if (-not $herdr) { return 0 }
    $closed = 0
    try {
        $tabs = @(((& $herdr tab list) -join "`n" | ConvertFrom-Json).result.tabs)
        $panes = @(((& $herdr pane list) -join "`n" | ConvertFrom-Json).result.panes)
        $processes = @(Get-CimInstance Win32_Process -Property ProcessId, ParentProcessId, CommandLine)
        foreach ($tab in $tabs) {
            if ($env:HERDR_WORKSPACE_ID -and $tab.workspace_id -ne $env:HERDR_WORKSPACE_ID) { continue }
            if ($tab.tab_id -eq $env:HERDR_TAB_ID) { continue }
            if ("$($tab.label)" -notmatch '^(DF |Dark factory|Audit |Gap )') { continue }
            $alive = $false
            foreach ($pane in @($panes | Where-Object { $_.tab_id -eq $tab.tab_id })) {
                $info = ((& $herdr pane process-info --pane $pane.pane_id) -join "`n" | ConvertFrom-Json).result.process_info
                if (-not $info.shell_pid -or (Test-HerdrPaneRunAlive -ShellPid ([int]$info.shell_pid) -Processes $processes)) { $alive = $true; break }
            }
            if ($alive) { continue }
            & $herdr tab close $tab.tab_id 2>&1 | Out-Null
            if ($LASTEXITCODE -eq 0) { $closed++ }
        }
    } catch {
        Write-Trace '-' 'herdr' "could not sweep finished tabs: $($_.Exception.Message)" 'Yellow'
    }
    return $closed
}

function Close-FinishedHerdrTabsAndTrace {
    # The shift-end sweep: Close-FinishedHerdrTabs, then one trace line saying how many.
    $closed = Close-FinishedHerdrTabs
    if (Get-HerdrBin) { Write-Trace '-' 'herdr' "closed $closed finished tabs" 'Cyan' }
}

function Get-LaneMarker {
    # The CURL_DARK_FACTORY_LANE value for this process (LANE MARKER in RunDarkFactory.ps1's
    # help): the lane number, 0 for a coordinator or single runner, '' for a launcher that
    # only starts another shift and exits.
    param([int]$ForLane, [switch]$Launcher)
    if ($Launcher) { return '' }
    return "$ForLane"
}

function New-ClaudeRunStartInfo {
    # The process start info Invoke-TaskRun runs claude -p with: cmd.exe in this checkout,
    # inheriting this process's environment (so CURL_DARK_FACTORY_LANE) plus the Bash
    # tool's time limits.
    param([string]$Arguments)
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $env:ComSpec
    $psi.Arguments = $Arguments
    $psi.WorkingDirectory = $Root
    $psi.EnvironmentVariables['BASH_DEFAULT_TIMEOUT_MS'] = '1800000'
    $psi.EnvironmentVariables['BASH_MAX_TIMEOUT_MS'] = '3600000'
    $psi.UseShellExecute = $false
    $psi.RedirectStandardInput = $true
    $psi.RedirectStandardOutput = $true
    $psi.StandardOutputEncoding = [System.Text.Encoding]::UTF8
    return $psi
}

if ($TestLaneMarker) {
    $results = @(
        @('lane 3 is marked 3', (Get-LaneMarker -ForLane 3), '3'),
        @('a coordinator or single-lane shift is marked 0', (Get-LaneMarker -ForLane 0), '0'),
        @('a -NewTab or -Restart launcher is not marked', (Get-LaneMarker -ForLane 0 -Launcher), '')
    )
    $saved = $env:CURL_DARK_FACTORY_LANE
    try {
        $env:CURL_DARK_FACTORY_LANE = Get-LaneMarker -ForLane 3
        $probe = [System.Diagnostics.Process]::Start((New-ClaudeRunStartInfo '/d /c echo %CURL_DARK_FACTORY_LANE%'))
        $probe.StandardInput.Close()
        $seen = $probe.StandardOutput.ReadToEnd().Trim()
        $probe.WaitForExit()
        $results += , @('a claude -p run started like Invoke-TaskRun sees the marker', $seen, '3')
    } finally { $env:CURL_DARK_FACTORY_LANE = $saved }
    $failed = 0
    foreach ($r in $results) {
        $ok = $r[1] -ceq $r[2]
        if (-not $ok) { $failed++ }
        Write-Host "$(if ($ok) { 'PASS' } else { 'FAIL' }) $($r[0]): '$($r[1])'" -ForegroundColor $(if ($ok) { 'Green' } else { 'Red' })
    }
    exit $(if ($failed) { 1 } else { 0 })
}

function Close-OwnHerdrTab {
    # Closes the tab this shift runs in, but only one the factory opened.
    param([string]$Why)
    if (Test-FactoryTab $env:HERDR_TAB_ID) { Close-HerdrTab $env:HERDR_TAB_ID $Why $OwnTabPrefix }
}

if ($NewTab) {
    # Hand this exact shift, minus -NewTab, to a tab or window of its own, and return.
    $forward = @()
    foreach ($p in $FactoryParameters.GetEnumerator()) {
        if ($p.Key -eq 'NewTab') { continue }
        if ($p.Value -is [System.Management.Automation.SwitchParameter]) { if ($p.Value) { $forward += "-$($p.Key)" } }
        else { $forward += @("-$($p.Key)", "`"$($p.Value)`"") }
    }
    $where = Start-Detached -Label "DF shift starting" -Dir $Root -ScriptArgs $forward
    if ($where.Tab) { Write-Host "Dark factory started in herdr tab $($where.Tab)." }
    else { Write-Host "Dark factory started in a new console window (pid $($where.Process.Id))." }
    exit 0
}

# This file ran to its end; RunDarkFactory.ps1 goes on to the next one.
$AreaFileFinished = $true
