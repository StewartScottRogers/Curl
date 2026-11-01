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
            $line = Get-HerdrTabCommandLine -ScriptPath $script -ScriptArgs $ScriptArgs
            # Windows PowerShell passes a native argument's inner quotes through unescaped,
            # and herdr's argument parser would then strip them, splitting any path with a
            # space. \" survives as a literal quote.
            # A refused command is tried once more: it was once swallowed with no sign (BL-3509).
            foreach ($attempt in 1..2) {
                & $herdr pane run $pane $line.Replace('"', '\"') 2>&1 | Out-Null
                if ($LASTEXITCODE -eq 0) { break }
                Write-Trace '-' 'herdr' "pane run in $pane failed (attempt $attempt of 2)" 'Red'
            }
            return @{ Tab = $created.result.tab.tab_id }
        }
        Write-Trace '-' 'herdr' 'could not create a herdr tab; using a console window' 'DarkYellow'
    }
    $start = Get-DetachedStartCommand -ScriptPath $script -ScriptArgs $ScriptArgs -LogDir $LogDir -Stamp $Stamp
    if ($start.LogPath) { Write-Trace '-' 'start' "no herdr: starting $Label detached with pwsh, output to $($start.LogPath)" 'Cyan' }
    return @{ Process = (Start-Process -FilePath $start.FilePath -ArgumentList $start.ArgumentList -WorkingDirectory $Dir -PassThru) }
}

function Get-HerdrTabCommandLine {
    # The line typed into a herdr tab's shell: the platform's PowerShell running the script.
    param([string]$ScriptPath, [string[]]$ScriptArgs, [bool]$OnWindows = (Test-FactoryOnWindows))
    $exe = if ($OnWindows) { 'powershell' } else { 'pwsh' }
    return "$exe -NoProfile -ExecutionPolicy Bypass -File `"$ScriptPath`" " + ($ScriptArgs -join ' ')
}

function Get-DetachedStartCommand {
    # What Start-Detached runs when it has no herdr tab. On Windows: powershell.exe in its
    # own console window (LogPath $null). Elsewhere there is no window to open, so sh starts
    # pwsh detached from the terminal, appending its output to <repo>.logs/DarkFactory-<stamp>-L<n>.log
    # (the lane's own trace log; a shift or launcher gets -start in place of -L<n>).
    param([string]$ScriptPath, [string[]]$ScriptArgs, [string]$LogDir, [string]$Stamp, [bool]$OnWindows = (Test-FactoryOnWindows))
    if ($OnWindows) {
        return @{ FilePath = 'powershell.exe'; LogPath = $null
            ArgumentList = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$ScriptPath`"") + $ScriptArgs }
    }
    $laneAt = [array]::IndexOf(@($ScriptArgs), '-Lane')
    $tag = if ($laneAt -ge 0 -and $laneAt + 1 -lt @($ScriptArgs).Count) { "-L$(@($ScriptArgs)[$laneAt + 1])" } else { '-start' }
    $log = Join-Path $LogDir "DarkFactory-$Stamp$tag.log"
    $quote = { param([string]$s) "'" + ($s -replace "'", "'\''") + "'" }
    $words = @('pwsh', '-NoProfile', '-File', $ScriptPath) + @($ScriptArgs | ForEach-Object { "$_".Trim('"') })
    $command = 'exec nohup ' + ((@($words) | ForEach-Object { & $quote $_ }) -join ' ') + ' >> ' + (& $quote $log) + ' 2>&1 < /dev/null'
    # Start-Process joins ArgumentList into one string that .NET splits again at spaces, so the
    # -c script goes in double quotes or sh would get only 'exec' as its script (BL-4430).
    return @{ FilePath = 'sh'; LogPath = $log; ArgumentList = @('-c', ('"' + $command.Replace('"', '\"') + '"')) }
}

function Get-FactoryDescendantIds {
    # $RootId and every process under it in $Table (ProcessId, ParentProcessId rows), the
    # root first and each level after the one above it.
    param([object[]]$Table, [int]$RootId)
    $order = [System.Collections.Generic.List[int]]::new()
    $queue = [System.Collections.Generic.Queue[int]]::new()
    $queue.Enqueue($RootId)
    while ($queue.Count -gt 0) {
        $parent = $queue.Dequeue()
        $order.Add($parent)
        foreach ($child in @($Table | Where-Object { $_.ParentProcessId -eq $parent -and $_.ProcessId -ne $parent })) { $queue.Enqueue([int]$child.ProcessId) }
    }
    return @($order)
}

function Get-MsBuildNodeIdsUnder {
    # The MSBuild worker nodes (dotnet ... MSBuild.dll /nodemode:N) among $RootIds and their
    # descendants in $Table. Attribution is by parentage alone, never by name, so a node
    # Visual Studio or another checkout's build started is never in the answer (BL-4348).
    param([object[]]$Table, [int[]]$RootIds)
    $under = foreach ($rootId in $RootIds) { Get-FactoryDescendantIds -Table $Table -RootId $rootId }
    $ids = @($under | Select-Object -Unique)
    return @($Table | Where-Object { $ids -contains [int]$_.ProcessId -and "$($_.CommandLine)" -match 'MSBuild' -and "$($_.CommandLine)" -match '/nodemode:' } |
        ForEach-Object { [int]$_.ProcessId })
}

function Stop-FactoryProcessTree {
    # Stops process $ProcessId and everything under it: taskkill /T /F on Windows, else the
    # descendants found through Get-FactoryProcessTable, deepest first, then the process.
    # MSBuild worker nodes under it are found first and stopped by id after, because a
    # reusable node can outlive the tree taskkill walks (BL-4348).
    param([int]$ProcessId)
    $table = @(Get-FactoryProcessTable)
    if (Test-FactoryOnWindows) {
        $nodes = @(Get-MsBuildNodeIdsUnder -Table $table -RootIds @($ProcessId))
        & taskkill /PID $ProcessId /T /F 2>&1 | Out-Null
        foreach ($id in $nodes) { & taskkill /PID $id /F 2>&1 | Out-Null }
        return
    }
    $order = [System.Collections.Generic.List[int]]::new()
    $order.AddRange([int[]]@(Get-FactoryDescendantIds -Table $table -RootId $ProcessId))
    $order.Reverse()
    foreach ($id in $order) { & kill -9 $id 2>&1 | Out-Null }
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
        $processes = @(Get-FactoryProcessTable)
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

function Wait-NextShiftLog {
    # A hand-over is only done when the next shift has written its trace log: waits up to
    # $TimeoutSeconds for a DarkFactory-*.log in $LogDir, other than this shift's own, written
    # at or after $Since. Returns $true when one appears. Otherwise raises the alarm through
    # $Notify, naming $Tab, and returns $false so the caller keeps this shift's tab (BL-3509:
    # a tab traced as started at 05:02 never ran, and nothing said so for 2 hours 44 minutes).
    param(
        [string]$Tab, [datetime]$Since, [string]$LogDir, [string]$OwnLog,
        [int]$TimeoutSeconds = 120,
        [scriptblock]$Sleep = { param($Seconds) Start-Sleep -Seconds $Seconds },
        [scriptblock]$Now = { Get-Date },
        [scriptblock]$Notify = { param([string[]]$Reasons) Invoke-Alarm -Reasons $Reasons })
    $deadline = (& $Now).AddSeconds($TimeoutSeconds)
    while ($true) {
        $written = @(Get-ChildItem -LiteralPath $LogDir -Filter 'DarkFactory-*.log' -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -ne $OwnLog -and $_.LastWriteTime -ge $Since })
        if ($written.Count) { return $true }
        if ((& $Now) -ge $deadline) { break }
        & $Sleep 5
    }
    $where = if ($Tab) { "herdr tab $Tab" } else { 'its process' }
    Write-Trace '-' 'shift' "the next shift ($where) wrote no log within $TimeoutSeconds seconds" 'Red'
    & $Notify -Reasons @("NEXT SHIFT NOT RUNNING  $where wrote no DarkFactory log within $TimeoutSeconds seconds", 'No shift runs until one is started again; this tab was kept.')
    return $false
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
    # The process start info Invoke-TaskRun runs claude -p with: $CommandLine in the
    # platform's shell (Set-FactoryShellCommand) in this checkout, inheriting this process's
    # environment (so CURL_DARK_FACTORY_LANE) plus the Bash tool's time limits.
    param([string]$CommandLine)
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    Set-FactoryShellCommand $psi $CommandLine
    $psi.WorkingDirectory = $Root
    $psi.EnvironmentVariables['BASH_DEFAULT_TIMEOUT_MS'] = '1800000'
    $psi.EnvironmentVariables['BASH_MAX_TIMEOUT_MS'] = '3600000'
    # Builds exit with their MSBuild worker nodes, so stopping the run leaves none behind (BL-4348).
    $psi.EnvironmentVariables['MSBUILDDISABLENODEREUSE'] = '1'
    $psi.UseShellExecute = $false
    $psi.RedirectStandardInput = $true
    $psi.RedirectStandardOutput = $true
    $psi.StandardOutputEncoding = [System.Text.Encoding]::UTF8
    return $psi
}

if ($TestLaneMarker) {
    $results = @(
        @('a herdr tab on Windows runs powershell', (Get-HerdrTabCommandLine -ScriptPath 'C:\r\RunDarkFactory.ps1' -ScriptArgs @('-Lane', '2') -OnWindows $true), 'powershell -NoProfile -ExecutionPolicy Bypass -File "C:\r\RunDarkFactory.ps1" -Lane 2'),
        @('a herdr tab off Windows runs pwsh', (Get-HerdrTabCommandLine -ScriptPath '/r/RunDarkFactory.ps1' -ScriptArgs @('-Lane', '2') -OnWindows $false), 'pwsh -NoProfile -ExecutionPolicy Bypass -File "/r/RunDarkFactory.ps1" -Lane 2'),
        @('a Windows window start is powershell.exe with no log', ((Get-DetachedStartCommand -ScriptPath 'C:\r\x.ps1' -ScriptArgs @('-Lane', '2') -LogDir 'L' -Stamp 'S' -OnWindows $true) | ForEach-Object { "$($_.FilePath)|$($_.LogPath)|$($_.ArgumentList -join ' ')" }), 'powershell.exe||-NoProfile -ExecutionPolicy Bypass -File "C:\r\x.ps1" -Lane 2'),
        @('a lane off Windows without herdr is a detached pwsh logging to its lane log', ((Get-DetachedStartCommand -ScriptPath '/r/x.ps1' -ScriptArgs @('-Lane', '2', '-LaneDir', '"/r/l 2"') -LogDir '/r.logs' -Stamp '20261028-0000' -OnWindows $false) | ForEach-Object { "$($_.FilePath)|$($_.LogPath)|$($_.ArgumentList -join ' ')" }), "sh|$(Join-Path '/r.logs' 'DarkFactory-20261028-0000-L2.log')|-c `"exec nohup 'pwsh' '-NoProfile' '-File' '/r/x.ps1' '-Lane' '2' '-LaneDir' '/r/l 2' >> '$(Join-Path '/r.logs' 'DarkFactory-20261028-0000-L2.log')' 2>&1 < /dev/null`""),
        @('a shift launcher off Windows logs to the -start log', ((Get-DetachedStartCommand -ScriptPath '/r/x.ps1' -ScriptArgs @() -LogDir '/l' -Stamp 'S' -OnWindows $false).LogPath), (Join-Path '/l' 'DarkFactory-S-start.log')),
        @('lane 3 is marked 3', (Get-LaneMarker -ForLane 3), '3'),
        @('a coordinator or single-lane shift is marked 0', (Get-LaneMarker -ForLane 0), '0'),
        @('a -NewTab or -Restart launcher is not marked', (Get-LaneMarker -ForLane 0 -Launcher), ''),
        @('a claude -p run turns MSBuild node reuse off', (New-ClaudeRunStartInfo 'x').EnvironmentVariables['MSBUILDDISABLENODEREUSE'], '1'),
        @('only nodes under the stopped process are found', ((Get-MsBuildNodeIdsUnder -RootIds @(10) -Table @(
            [pscustomobject]@{ ProcessId = 10; ParentProcessId = 1; CommandLine = 'dotnet build' }
            [pscustomobject]@{ ProcessId = 11; ParentProcessId = 10; CommandLine = 'dotnet MSBuild.dll /nodemode:1 /nodeReuse:true' }
            [pscustomobject]@{ ProcessId = 12; ParentProcessId = 11; CommandLine = 'dotnet MSBuild.dll /nodemode:1 /nodeReuse:true' }
            [pscustomobject]@{ ProcessId = 13; ParentProcessId = 10; CommandLine = 'dotnet other.dll' }
            [pscustomobject]@{ ProcessId = 20; ParentProcessId = 2; CommandLine = 'dotnet MSBuild.dll /nodemode:1 /nodeReuse:true' }
            [pscustomobject]@{ ProcessId = 21; ParentProcessId = 20; CommandLine = 'dotnet MSBuild.dll /nodemode:1 /nodeReuse:true' })) -join ','), '11,12')
    )
    $saved = $env:CURL_DARK_FACTORY_LANE
    try {
        $env:CURL_DARK_FACTORY_LANE = Get-LaneMarker -ForLane 3
        $probe = [System.Diagnostics.Process]::Start((New-ClaudeRunStartInfo $(if (Test-FactoryOnWindows) { 'echo %CURL_DARK_FACTORY_LANE%' } else { 'echo $CURL_DARK_FACTORY_LANE' })))
        $probe.StandardInput.Close()
        $seen = $probe.StandardOutput.ReadToEnd().Trim()
        $probe.WaitForExit()
        $results += , @('a claude -p run started like Invoke-TaskRun sees the marker', $seen, '3')
    } finally { $env:CURL_DARK_FACTORY_LANE = $saved }
    $logs = Join-Path ([IO.Path]::GetTempPath()) "df-nextlog-$PID"
    New-Item -ItemType Directory -Force -Path $logs | Out-Null
    $clock = @{ T = [datetime]'2026-10-25 05:02:31'; Alarms = @() }
    $sleep = { param($Seconds) $clock.T = $clock.T.AddSeconds($Seconds) }
    $now = { $clock.T }
    $notify = { param([string[]]$Reasons) $clock.Alarms += $Reasons[0] }
    $own = Join-Path $logs 'DarkFactory-own.log'
    Set-Content -Path $own -Value 'own'
    (Get-Item $own).LastWriteTime = $clock.T.AddSeconds(10)
    $silent = Wait-NextShiftLog -Tab 'w4:t10' -Since $clock.T -LogDir $logs -OwnLog $own -Sleep $sleep -Now $now -Notify $notify 6>$null
    $results += , @('a next shift that writes no log in 2 minutes raises the alarm naming its tab after 120 s', "$silent $($clock.Alarms.Count) $(($clock.Alarms[0] -split '  ')[1] -replace ' wrote.*') $(($clock.T - [datetime]'2026-10-25 05:02:31').TotalSeconds)", 'False 1 herdr tab w4:t10 120')
    $clock.Alarms = @()
    $other = Join-Path $logs 'DarkFactory-next.log'
    Set-Content -Path $other -Value 'next'
    (Get-Item $other).LastWriteTime = $clock.T.AddSeconds(5)
    $found = Wait-NextShiftLog -Tab 'w4:t10' -Since $clock.T -LogDir $logs -OwnLog $own -Sleep $sleep -Now $now -Notify $notify
    $results += , @('a next shift that wrote its log is found with no alarm', "$found $($clock.Alarms.Count)", 'True 0')
    Remove-Item (Join-Path $logs 'DarkFactory-next.log')
    # The coordinator's hand-over passes a Notify that only stores the reasons (BL-3510): the
    # call returns $false with the alarm still unraised, to be raised after the merge and review.
    $script:lateAlarmReasons = $null
    $deferred = Wait-NextShiftLog -Tab 'w4:t10' -Since $clock.T.AddSeconds(1000) -LogDir $logs -OwnLog $own -Sleep $sleep -Now $now -Notify { param([string[]]$Reasons) $script:lateAlarmReasons = $Reasons } 6>$null
    $results += , @('a hand-over whose Notify defers returns without an acknowledgement, the alarm left pending', "$deferred $(@($script:lateAlarmReasons).Count -gt 0) $($clock.Alarms.Count)", 'False True 0')
    Remove-Item -Recurse -Force -Path $logs -ErrorAction SilentlyContinue
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
