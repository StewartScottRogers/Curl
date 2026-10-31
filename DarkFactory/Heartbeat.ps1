# Each runner's heartbeat file, and the board branch's status.json that the coordinator
# builds from them and publishes for the live board page (ADR-0129). Dot-sourced by
# RunDarkFactory.ps1 in its order; see the loader there.

# ---------------------------------------------------------------------------- heartbeat

# Each runner's lane-<n>.heartbeat.json (ADR-0129 items 8 and 9), for the coordinator to
# publish. The coordinator of a multi-lane shift runs no task and writes none, and nor
# does the out-of-tokens rehearsal.
$WritesHeartbeat = ($Lane -or (-not $AutoLanes -and $LaneCount -le 1)) -and -not $TestOutOfTokens
$script:Beat = @{ Task = $null; Title = $null; Model = $null; ModelWhy = $null; Phase = 'starting'; Step = ''; TaskStartedAt = $null; WrittenAt = [datetime]::MinValue; FailureTraced = $false }

function Get-UtcStamp {
    param([datetime]$When = (Get-Date))
    return $When.ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
}

function Set-HeartbeatTask {
    # The task the heartbeat names; '' when the runner holds none. A lane resuming a task it
    # held keeps the time it claimed it, which is when its lane-<n>.task file was written.
    param([string]$Id, [string]$Title = $null)
    if (-not $Id) { $script:Beat.Task = $null; $script:Beat.Title = $null; $script:Beat.Model = $null; $script:Beat.ModelWhy = $null; $script:Beat.TaskStartedAt = $null; return }
    if ($script:Beat.Task -eq $Id) { return }
    $started = Get-Date
    if ($Lane) {
        $taskFile = Get-LaneStatePath $Lane 'task'
        if ((Test-Path $taskFile) -and (Get-LaneState $Lane 'task') -eq $Id) { $started = (Get-Item $taskFile).LastWriteTime }
    }
    $script:Beat.Task = $Id
    $script:Beat.Title = if ($PSBoundParameters.ContainsKey('Title')) { $Title } else { Get-TaskTitle $Id }
    $script:Beat.TaskStartedAt = Get-UtcStamp $started
}

function Write-Heartbeat {
    # Writes the heartbeat now. -Phase moves it to a new phase and sets its step (empty by
    # default); without -Phase it refreshes the current phase and step. A failed write is
    # traced once and never stops the runner.
    param([string]$Phase = '', [string]$Step = '')
    if (-not $WritesHeartbeat) { return }
    if ($Phase) { $script:Beat.Phase = $Phase; $script:Beat.Step = $Step }
    $script:Beat.WrittenAt = Get-Date
    try {
        $path = Get-LaneStatePath $Lane 'heartbeat.json'
        New-Item -ItemType Directory -Force -Path (Split-Path $path) -ErrorAction Stop | Out-Null
        $json = [pscustomobject][ordered]@{
            lane = $Lane
            task = $script:Beat.Task
            title = $script:Beat.Title
            model = $script:Beat.Model
            modelWhy = $script:Beat.ModelWhy
            phase = $script:Beat.Phase
            step = $script:Beat.Step
            taskStartedAt = $script:Beat.TaskStartedAt
            heartbeatAt = Get-UtcStamp $script:Beat.WrittenAt
        } | ConvertTo-Json -Compress
        # Written beside the target and renamed over it, so a reader never sees half a file.
        $temporary = "$path.tmp"
        [System.IO.File]::WriteAllText($temporary, $json)
        Move-Item -LiteralPath $temporary -Destination $path -Force -ErrorAction Stop
    } catch {
        if (-not $script:Beat.FailureTraced) {
            $script:Beat.FailureTraced = $true
            Write-Trace '-' 'beat' "heartbeat not written: $(Get-Short $_.Exception.Message 80)" 'DarkYellow'
        }
    }
    # The single runner publishes its own lane 0, throttled to -HeartbeatMinutes; its last
    # publish, with "state": "ended", is made at shift end.
    if (-not $Lane -and $script:Beat.Phase -ne 'finished') { Publish-BoardStatusIfDue -Branch $branch }
    Invoke-CoordinatorWatchdogIfDue
}

function Write-HeartbeatIfDue {
    # Refreshes the heartbeat once 60 seconds have passed since the last write.
    if (((Get-Date) - $script:Beat.WrittenAt).TotalSeconds -ge 60) { Write-Heartbeat }
}

function Set-HeartbeatStep {
    # The step is the tool label's verb and detail, cut to 80 characters; a new one is
    # written at once.
    param([string[]]$Label)
    $step = ((@($Label) -join ' ') -replace '\s+', ' ').Trim()
    if ($step.Length -gt 80) { $step = $step.Substring(0, 80) }
    if ($step -eq $script:Beat.Step) { return }
    $script:Beat.Step = $step
    Write-Heartbeat
}

# ---------------------------------------------------------------------------- board branch

# The board branch's status.json (ADR-0129 items 7 to 9): the shift's heartbeat files merged
# into one file and force-pushed as a single parentless commit. It has one writer - the
# coordinator of a lane shift, or the single runner for its own lane 0. Lanes never push.
$script:BoardStatus = @{ Lanes = @{}; PublishedAt = [datetime]::MinValue; PushFailing = $false }
# The latest -Lanes Auto step, published as status.json's autoLanes (BL-770): lanes, target,
# binding, reason and changedAt. Only the Auto start and step set it, so a fixed-lane shift
# publishes null.
$script:AutoLanesStatus = $null

function Set-AutoLanesStatus {
    # Records the Auto lane count, its target and what binds it. -Reason, given only when
    # the count starts or changes, replaces the last change's reason and time.
    param([int]$Lanes, $Target, [string]$Binding, [string]$Reason)
    $old = $script:AutoLanesStatus
    $changed = $Reason -or -not $old
    $script:AutoLanesStatus = [pscustomobject][ordered]@{
        lanes = $Lanes
        target = $(if ($null -ne $Target) { [math]::Round([double]$Target, 1) } else { $null })
        binding = $Binding
        reason = $(if ($changed) { $Reason } else { $old.reason })
        changedAt = $(if ($changed) { Get-UtcStamp } else { $old.changedAt })
    }
}

function Get-BoardStatusJson {
    # Merges every lane-<n>.heartbeat.json of this shift into status.json schema 1, lanes
    # sorted by number. A lane whose file cannot be read keeps its last good object.
    # -State ended marks every lane finished.
    param([string]$Branch, [string]$State = 'running')
    $dir = Join-Path $LogDir "lanes-$Stamp"
    foreach ($file in @(Get-ChildItem $dir -Filter 'lane-*.heartbeat.json' -ErrorAction SilentlyContinue)) {
        try {
            $laneObject = [System.IO.File]::ReadAllText($file.FullName) | ConvertFrom-Json -ErrorAction Stop
            if ($null -eq $laneObject.lane) { continue }
            $script:BoardStatus.Lanes[[int]$laneObject.lane] = $laneObject
        } catch { }
    }
    $laneObjects = @($script:BoardStatus.Lanes.Keys | Sort-Object | ForEach-Object { $script:BoardStatus.Lanes[$_] })
    if ($State -eq 'ended') { foreach ($laneObject in $laneObjects) { $laneObject.phase = 'finished' } }
    return [pscustomobject][ordered]@{
        schema = 1
        shift = $Stamp
        branch = $Branch
        state = $State
        publishedAt = Get-UtcStamp
        autoLanes = $script:AutoLanesStatus
        lanes = $laneObjects
    } | ConvertTo-Json -Depth 4
}

function New-BoardCommit {
    # Builds the board branch's commit with plumbing - a blob, a tree holding only
    # status.json, and a commit with no parent - so the checkout's working tree and index
    # never change. Returns the commit id; throws when git fails.
    param([string]$Json)
    # Nothing is piped to git: Windows PowerShell pipes to a native command in its output
    # encoding, which can prepend a byte order mark and mangle a title's non-ASCII
    # characters. The blob is hashed from a UTF-8 file and mktree reads its line through a
    # cmd redirect.
    $utf8 = New-Object System.Text.UTF8Encoding $false
    $file = Join-Path ([IO.Path]::GetTempPath()) "DarkFactoryStatus-$Stamp-$PID.json"
    $treeFile = "$file.tree"
    try {
        [System.IO.File]::WriteAllText($file, $Json, $utf8)
        $blob = "$(git -C $Root hash-object -w -- $file 2>$null)".Trim()
        if ($LASTEXITCODE -ne 0 -or $blob -notmatch '^[0-9a-f]{40,64}$') { throw 'git hash-object failed' }
        [System.IO.File]::WriteAllText($treeFile, "100644 blob $blob`tstatus.json`n", $utf8)
        $mktree = 'git -C "{0}" mktree < "{1}"' -f $Root, $treeFile
        $tree = "$(cmd /s /c $mktree 2>$null)".Trim()
        if ($LASTEXITCODE -ne 0 -or $tree -notmatch '^[0-9a-f]{40,64}$') { throw 'git mktree failed' }
    } finally { Remove-Item $file, $treeFile -ErrorAction SilentlyContinue }
    $commit = "$(git -C $Root commit-tree $tree -m "chore(board): lane status $(Get-UtcStamp)" 2>$null)".Trim()
    if ($LASTEXITCODE -ne 0 -or $commit -notmatch '^[0-9a-f]{40,64}$') { throw 'git commit-tree failed' }
    return $commit
}

function Publish-BoardStatus {
    # Publishes status.json now: builds the commit and force-pushes it to the board branch,
    # the only branch the factory force-pushes. A failure is traced once per failure streak
    # and never stops the shift or raises the alarm. -HeartbeatMinutes 0 publishes nothing.
    param([string]$Branch, [string]$State = 'running')
    if ($HeartbeatMinutes -le 0) { return }
    $script:BoardStatus.PublishedAt = Get-Date
    try {
        $commit = New-BoardCommit (Get-BoardStatusJson -Branch $Branch -State $State)
        git -C $Root push -q --force origin "${commit}:refs/heads/board" 2>&1 | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "git push exited $LASTEXITCODE" }
        if ($script:BoardStatus.PushFailing) { Write-Trace '-' 'board' 'status.json published to board again' }
        $script:BoardStatus.PushFailing = $false
    } catch {
        if (-not $script:BoardStatus.PushFailing) {
            Write-Trace '-' 'board' "status.json not published: $(Get-Short $_.Exception.Message 80)" 'DarkYellow'
        }
        $script:BoardStatus.PushFailing = $true
    }
}

function Publish-BoardStatusIfDue {
    # Publishes once -HeartbeatMinutes have passed since the last publish.
    param([string]$Branch)
    if (((Get-Date) - $script:BoardStatus.PublishedAt).TotalMinutes -ge $HeartbeatMinutes) { Publish-BoardStatus -Branch $Branch }
}

# ---------------------------------------------------------------------------- coordinator watchdog (BL-4353)

# A coordinator that hangs inside a blocking call goes quiet while its lanes keep working
# (2026-10-29: nothing in its log for 55 minutes, no CI watch). Two guards: the coordinator's
# gh and git network calls run with a timeout, and its loop stamps coordinator.stamp each
# pass, which the lanes - independent processes - watch, replacing the coordinator when the
# stamp is 3 heartbeat intervals old.
$script:ExternalCallSeconds = 180
$script:ExternalTools = @{}
$script:CoordinatorWatchedAt = [datetime]::MinValue

function Test-TimedExternalCall {
    # True for the calls that wait on a network: every gh call, and git fetch, push, pull,
    # ls-remote and clone (after any -C <dir> or -c <key=value> options). Others run as they are.
    param([string]$Tool, [string[]]$CallArgs)
    if ($Tool -eq 'gh') { return $true }
    $i = 0
    while ($i -lt $CallArgs.Count -and $CallArgs[$i] -cin '-C', '-c') { $i += 2 }
    return ($i -lt $CallArgs.Count -and $CallArgs[$i] -cin 'fetch', 'push', 'pull', 'ls-remote', 'clone')
}

function ConvertTo-ProcessArgument {
    # One argument as it must appear on a process command line.
    param([string]$Text)
    if ($Text -ne '' -and $Text -notmatch '[\s"]') { return $Text }
    $escaped = ($Text -replace '(\\*)"', '$1$1\"') -replace '(\\+)$', '$1$1'
    return '"' + $escaped + '"'
}

function Invoke-ExternalWithTimeout {
    # Runs $FilePath with $CallArgs and no input; after $Seconds it stops the process and its
    # tree. Returns TimedOut, ExitCode (124 when timed out), Output and Errors (lines).
    param([string]$FilePath, [string[]]$CallArgs, [int]$Seconds)
    $info = New-Object System.Diagnostics.ProcessStartInfo
    $info.FileName = $FilePath
    $info.Arguments = (@($CallArgs | ForEach-Object { ConvertTo-ProcessArgument "$_" }) -join ' ')
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    $info.RedirectStandardInput = $true
    $info.RedirectStandardOutput = $true
    $info.RedirectStandardError = $true
    $process = [System.Diagnostics.Process]::Start($info)
    $process.StandardInput.Close()
    $out = $process.StandardOutput.ReadToEndAsync()
    $err = $process.StandardError.ReadToEndAsync()
    if (-not $process.WaitForExit($Seconds * 1000)) {
        Stop-FactoryProcessTree -ProcessId $process.Id
        return [pscustomobject]@{ TimedOut = $true; ExitCode = 124; Output = @(); Errors = @() }
    }
    $process.WaitForExit()
    $toLines = { param([string]$Text) @(($Text -split "\r?\n") | Where-Object { $_ -ne '' }) }
    return [pscustomobject]@{ TimedOut = $false; ExitCode = $process.ExitCode; Output = (& $toLines $out.Result); Errors = (& $toLines $err.Result) }
}

function ConvertTo-NativeCallArguments {
    # A wrapper function's $args as PowerShell would pass them to the native command: an
    # unquoted a,b,c reaches a function as an array, which a native command gets as "a,b,c".
    # Cast to [string[]] it became "a b c", so every timed `gh run list --json a,b` failed with
    # "Unknown JSON field" and the CI watch and shift-end merge wait were blind (BL-4377).
    param([object[]]$CallArgs)
    return @($CallArgs | ForEach-Object { if ($_ -is [array]) { @($_ | ForEach-Object { "$_" }) -join ',' } else { "$_" } })
}

function Invoke-TimedExternalCall {
    # One timed gh or git call as the command itself would answer: output lines, stderr as
    # errors, $LASTEXITCODE set. A timeout is traced, and exits 124, instead of hanging the loop.
    param([string]$Tool, [object[]]$CallArgs)
    $CallArgs = ConvertTo-NativeCallArguments $CallArgs
    # A pass can make several calls of up to ExternalCallSeconds each: the stamp is refreshed
    # either side of each, so waiting on a slow network never ages it past the threshold (ADR-1160).
    Write-CoordinatorStamp
    $result = Invoke-ExternalWithTimeout -FilePath $script:ExternalTools[$Tool] -CallArgs $CallArgs -Seconds $script:ExternalCallSeconds
    Write-CoordinatorStamp
    if ($result.TimedOut) { Write-Trace '-' 'timeout' "$Tool $(Get-Short ($CallArgs -join ' ') 70) gave no answer in $($script:ExternalCallSeconds)s; stopped" 'Red' }
    $global:LASTEXITCODE = $result.ExitCode
    foreach ($line in $result.Errors) { Write-Error $line -ErrorAction Continue }
    return $result.Output
}

function Enable-ExternalCallTimeouts {
    # From here this process's gh and git calls that wait on a network run with a timeout
    # (-Seconds); only the coordinator calls it, a lane's own calls are never cut short.
    param([int]$Seconds = 180)
    $script:ExternalCallSeconds = $Seconds
    foreach ($tool in 'git', 'gh') {
        $found = Get-Command $tool -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($found) { $script:ExternalTools[$tool] = $found.Source }
    }
    if ($script:ExternalTools.ContainsKey('git')) {
        function script:git {
            if (Test-TimedExternalCall 'git' $args) { return (Invoke-TimedExternalCall 'git' $args) }
            if ($MyInvocation.ExpectingInput) { $input | & $script:ExternalTools['git'] @args } else { & $script:ExternalTools['git'] @args }
        }
    }
    if ($script:ExternalTools.ContainsKey('gh')) {
        function script:gh {
            if (Test-TimedExternalCall 'gh' $args) { return (Invoke-TimedExternalCall 'gh' $args) }
            if ($MyInvocation.ExpectingInput) { $input | & $script:ExternalTools['gh'] @args } else { & $script:ExternalTools['gh'] @args }
        }
    }
}

function Get-CoordinatorStampPath {
    param([string]$ForStamp = $Stamp)
    return (Join-Path (Join-Path $LogDir "lanes-$ForStamp") 'coordinator.stamp')
}

function Get-CoordinatorReplacingPath {
    param([string]$ForStamp = $Stamp)
    return (Join-Path (Join-Path $LogDir "lanes-$ForStamp") 'coordinator.replacing')
}

function ConvertFrom-UtcStamp {
    # Get-UtcStamp's text as a UTC time; $null when it is not one.
    param([string]$Text)
    $when = [datetime]::MinValue
    $styles = [System.Globalization.DateTimeStyles]::AdjustToUniversal -bor [System.Globalization.DateTimeStyles]::AssumeUniversal
    if ([datetime]::TryParseExact("$Text".Trim(), 'yyyy-MM-ddTHH:mm:ssZ', [System.Globalization.CultureInfo]::InvariantCulture, $styles, [ref]$when)) { return $when }
    return $null
}

function Write-CoordinatorStamp {
    # The coordinator's loop calls this each pass: key=value lines - its pid, when the loop
    # last completed a pass, when the shift ends, its script and the arguments to start it
    # again with. Written beside the target and renamed over it. Never stops the loop.
    param([string]$Path = (Get-CoordinatorStampPath), [datetime]$End = $shiftEnd)
    try {
        if ($null -eq $script:CoordinatorArguments) {
            $own = Get-FactoryProcessTable | Where-Object { $_.ProcessId -eq $PID } | Select-Object -First 1
            $leaf = [regex]::Escape((Split-Path $FactoryScript -Leaf))
            $script:CoordinatorArguments = if ($own -and "$($own.CommandLine)" -match "$leaf`"?\s*(.*)$") { $Matches[1].Trim() } else { '' }
        }
        New-Item -ItemType Directory -Force -Path (Split-Path $Path) -ErrorAction Stop | Out-Null
        $text = "pid=$PID`nheartbeat=$HeartbeatMinutes`nat=$(Get-UtcStamp)`nshiftEnd=$(Get-UtcStamp $End)`nscript=$FactoryScript`ndir=$FactoryScriptDir`nargs=$($script:CoordinatorArguments)`n"
        [System.IO.File]::WriteAllText("$Path.tmp", $text)
        Move-Item -LiteralPath "$Path.tmp" -Destination $Path -Force -ErrorAction Stop
        $script:CoordinatorStampFailing = $false
    } catch {
        # Traced on the first failure only: a stamp that cannot be written gets a healthy
        # coordinator replaced within 3 heartbeats, so the reason must be in the log.
        if (-not $script:CoordinatorStampFailing) {
            Write-Trace '-' 'watchdog' "coordinator.stamp not written (lanes will replace this coordinator): $(Get-Short $_.Exception.Message 80)" 'Red'
        }
        $script:CoordinatorStampFailing = $true
    }
}

function Read-CoordinatorStamp {
    # The stamp file's keys as a hashtable; empty when there is no readable file.
    param([string]$Path)
    $fields = @{}
    if (-not (Test-Path -LiteralPath $Path)) { return $fields }
    try {
        foreach ($line in [System.IO.File]::ReadAllLines($Path)) {
            if ($line -match '^(\w+)=(.*)$') { $fields[$Matches[1]] = $Matches[2] }
        }
    } catch { }
    return $fields
}

function Get-CoordinatorWatchdogDecision {
    # Whether a lane should replace the coordinator: 'replace' when its stamp (-StampAt, UTC,
    # $null when there is none) is 3 heartbeat intervals old or older and the shift still has
    # time (-ShiftEnd), unless a replacement began under 10 minutes ago (-ReplacingSince,
    # $null when none), which is still starting. Why is the trace text. Pure.
    param([datetime]$Now, $StampAt, [datetime]$ShiftEnd, [int]$HeartbeatMinutes, $ReplacingSince = $null)
    $interval = if ($HeartbeatMinutes -gt 0) { $HeartbeatMinutes } else { 3 }
    if ($null -eq $StampAt) { return [pscustomobject]@{ Action = 'none'; Why = 'no coordinator stamp' } }
    if ($Now -ge $ShiftEnd) { return [pscustomobject]@{ Action = 'none'; Why = 'the shift is over' } }
    $age = ($Now - $StampAt).TotalMinutes
    if ($age -lt 3 * $interval) { return [pscustomobject]@{ Action = 'none'; Why = 'the coordinator loop is current' } }
    if ($null -ne $ReplacingSince -and ($Now - $ReplacingSince).TotalMinutes -lt 10) { return [pscustomobject]@{ Action = 'none'; Why = 'a replacement coordinator is starting' } }
    return [pscustomobject]@{ Action = 'replace'; Why = "the coordinator's loop has not completed for $([int]$age) minutes (3 heartbeats = $(3 * $interval))" }
}

function Get-CoordinatorOwnProcessIds {
    # The processes to stop beside a hung coordinator (-Table: Get-FactoryProcessTable's
    # rows): what it started - a stuck git or gh - deepest first, but never a lane or anything
    # under one, because the lanes keep working. The coordinator itself is not in the answer.
    param([object[]]$Table, [int]$CoordinatorId)
    $found = [System.Collections.Generic.List[int]]::new()
    $walk = {
        param([int]$ParentId)
        foreach ($row in @($Table | Where-Object { [int]$_.ParentProcessId -eq $ParentId })) {
            if ("$($row.CommandLine)" -match '\s-Lane\s') { continue }
            & $walk ([int]$row.ProcessId)
            $found.Add([int]$row.ProcessId)
        }
    }
    & $walk $CoordinatorId
    return @($found)
}

function Stop-FactoryProcessOnly {
    # Stops process $ProcessId alone, leaving its children running.
    param([int]$ProcessId)
    if (Test-FactoryOnWindows) { & taskkill /PID $ProcessId /F 2>&1 | Out-Null } else { & kill -9 $ProcessId 2>&1 | Out-Null }
}

function Restart-StaleCoordinator {
    # Replaces the coordinator named in its stamp: stops it and what it started, but no lane,
    # then starts a coordinator with its arguments plus -TakeOverShift and this shift's stamp,
    # which keeps this shift's lanes. The first lane to create coordinator.replacing does it.
    param([hashtable]$Fields, [string]$Why)
    try { $lock = [System.IO.File]::Open((Get-CoordinatorReplacingPath), [System.IO.FileMode]::CreateNew) } catch { return }
    $lock.Dispose()
    $coordinatorId = [int]$Fields['pid']
    $table = @(Get-FactoryProcessTable)
    $mine = [regex]::Escape($Fields['script'])
    $alive = $table | Where-Object { $_.ProcessId -eq $coordinatorId -and "$($_.CommandLine)" -match "-File\s+`"?$mine`"?(\s|$)" -and "$($_.CommandLine)" -notmatch '\s-Lane\s' }
    foreach ($id in (Get-CoordinatorOwnProcessIds -Table $table -CoordinatorId $coordinatorId)) { Stop-FactoryProcessOnly $id }
    if ($alive) { Stop-FactoryProcessOnly $coordinatorId }
    $hoursLeft = [math]::Max(0.01, ((ConvertFrom-UtcStamp $Fields['shiftEnd']) - (Get-Date).ToUniversalTime()).TotalHours).ToString([System.Globalization.CultureInfo]::InvariantCulture)
    $forward = @([regex]::Matches("$($Fields['args'])", '"[^"]*"|\S+') | ForEach-Object { $_.Value })
    $forward = Set-ForwardedArgument $forward @{ Hours = $hoursLeft; ShiftStamp = $Stamp; TakeOverShift = [switch]$true }
    if ($forward -notcontains '-ShiftBranch' -and $Branch) { $forward += @('-ShiftBranch', $Branch) }
    $keepScript = $script:FactoryScript; $keepDir = $script:FactoryScriptDir
    try {
        $script:FactoryScript = $Fields['script']; $script:FactoryScriptDir = $Fields['dir']
        $where = Start-Detached -Label 'DF shift taking over' -Dir $Fields['dir'] -ScriptArgs $forward
    } finally { $script:FactoryScript = $keepScript; $script:FactoryScriptDir = $keepDir }
    Write-Trace '-' 'watchdog' "$Why; replaced coordinator pid $coordinatorId with a new one ($(if ($where.Tab) { "herdr tab $($where.Tab)" } else { "pid $($where.Process.Id)" })), lanes left running" 'Red'
}

function Invoke-CoordinatorWatchdogIfDue {
    # A lane's look at the coordinator, once a minute: replaces it when its loop has stopped.
    if (-not $Lane -or ((Get-Date) - $script:CoordinatorWatchedAt).TotalSeconds -lt 60) { return }
    $script:CoordinatorWatchedAt = Get-Date
    try {
        $fields = Read-CoordinatorStamp (Get-CoordinatorStampPath)
        if (-not ($fields.ContainsKey('at') -and $fields.ContainsKey('pid') -and $fields.ContainsKey('script') -and $fields.ContainsKey('shiftEnd'))) { return }
        $replacing = Get-CoordinatorReplacingPath
        $since = $null
        if (Test-Path -LiteralPath $replacing) { $since = (Get-Item -LiteralPath $replacing).LastWriteTimeUtc }
        $decision = Get-CoordinatorWatchdogDecision -Now (Get-Date).ToUniversalTime() -StampAt (ConvertFrom-UtcStamp $fields['at']) -ShiftEnd (ConvertFrom-UtcStamp $fields['shiftEnd']) -HeartbeatMinutes $(if ($fields.ContainsKey('heartbeat')) { [int]$fields['heartbeat'] } else { $HeartbeatMinutes }) -ReplacingSince $since
        if ($decision.Action -ne 'replace') { return }
        # A replacement begun over 10 minutes ago never came up: its lock is cleared first.
        if ($null -ne $since) { Remove-Item -LiteralPath $replacing -ErrorAction SilentlyContinue }
        Restart-StaleCoordinator -Fields $fields -Why $decision.Why
    } catch {
        Write-Trace '-' 'watchdog' "coordinator watch failed: $(Get-Short $_.Exception.Message 80)" 'DarkYellow'
    }
}

# This file ran to its end; RunDarkFactory.ps1 goes on to the next one.
$AreaFileFinished = $true
