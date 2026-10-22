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

# This file ran to its end; RunDarkFactory.ps1 goes on to the next one.
$AreaFileFinished = $true
