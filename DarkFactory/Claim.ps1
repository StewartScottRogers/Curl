# Lanes' git and lock helpers, renumbering duplicate task IDs, claiming a task, and
# -TestClaimDuplicates. Dot-sourced by RunDarkFactory.ps1 in its order; see the loader
# there.

# ---------------------------------------------------------------------------- lanes

function Invoke-Git {
    # git in this checkout; returns $true when it exited 0. Output goes to the trace only
    # on failure, so a quiet lane stays quiet.
    param([string[]]$GitArgs)
    $out = & git -C $Root @GitArgs 2>&1
    if ($LASTEXITCODE -ne 0) {
        $last = ($out | ForEach-Object { "$_" } | Where-Object { $_.Trim() } | Select-Object -Last 1)
        Write-Trace '-' 'git' "FAIL git $($GitArgs[0]): $(Get-Short "$last" 60)" 'DarkYellow'
        return $false
    }
    return $true
}

function Get-LockPriorityPath {
    # The marker a lane waiting with -Priority keeps beside the lock: integrate.priority-L<n>.
    param([int]$ForLane = $Lane)
    return (Join-Path $LanesDir "integrate.priority-L$ForLane")
}

function Test-LockPriorityWaiting {
    # Whether another lane waits for the lock with -Priority: its marker was rewritten in the
    # last minute. A marker a dead lane left goes stale and holds nobody back.
    $mine = Split-Path (Get-LockPriorityPath) -Leaf
    return [bool](@(Get-ChildItem $LanesDir -Filter 'integrate.priority-L*' -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -ne $mine -and $_.LastWriteTime -gt (Get-Date).AddMinutes(-1) }).Count)
}

function Enter-Lock {
    # Claims and integrations take turns across every lane. Holding the file open with no
    # sharing is the lock; the operating system releases it if the lane dies.
    # -Priority (a lane integrating a CI fix) goes before every lane that waits without it:
    # while its marker is fresh they do not try the lock, so it is next once the holder lets
    # go. BL-4308's fix waited 27 minutes behind eight ordinary integrations while CI stayed
    # red (2026-10-29, ADR-1158).
    param([switch]$Priority)
    New-Item -ItemType Directory -Force -Path $LanesDir | Out-Null
    $marker = Get-LockPriorityPath
    try {
        while ($true) {
            if ($Priority) { Set-Content -Path $marker -Value (Get-Date -Format 'yyyy-MM-dd HH:mm:ss') -Encoding ASCII }
            if ($Priority -or -not (Test-LockPriorityWaiting)) {
                try { return [IO.File]::Open($LockFile, 'OpenOrCreate', 'ReadWrite', 'None') }
                catch { }
            }
            Write-HeartbeatIfDue; Start-Sleep -Seconds 3
        }
    } finally {
        if ($Priority) { Remove-Item -LiteralPath $marker -ErrorAction SilentlyContinue }
    }
}

function Test-RemoteReachable {
    # Whether origin answers at all, so a failed fetch or push can be told apart: refused by
    # a remote that answers (another lane pushed first) or never heard (GitHub out of reach).
    & git -C $Root ls-remote -q origin "refs/heads/$Branch" 2>&1 | Out-Null
    return ($LASTEXITCODE -eq 0)
}

function Wait-RemoteReachable {
    # Waits for origin to answer, up to -Minutes, polling every -PollSeconds. An outage at
    # GitHub is no reason to park finished work, or to trace a claim that was never refused
    # as a lost race: on 2026-09-30 a few minutes of "unable to access" parked BL-892, which
    # was finished, and another lane did it again (AF-0025, BL-1281). Returns $true once
    # origin answers, $false when it never did.
    param([string]$Id = '-', [int]$Minutes = 60, [int]$PollSeconds = 30)
    if (Test-RemoteReachable) { return $true }
    Write-Trace $Id 'offline' "origin out of reach; waiting up to $Minutes min for it" 'DarkYellow'
    $deadline = (Get-Date).AddMinutes($Minutes)
    while ((Get-Date) -lt $deadline) {
        Write-HeartbeatIfDue
        Start-Sleep -Seconds $PollSeconds
        if (Test-RemoteReachable) { Write-Trace $Id 'online' 'origin answers again'; return $true }
    }
    return $false
}

function Sync-Lane {
    # Puts this lane's checkout exactly on the shared branch as it is on the remote.
    Save-StrayChanges 'sync'
    if (-not (Invoke-Git @('fetch', '-q', 'origin', $Branch))) { return $false }
    if (-not (Invoke-Git @('checkout', '-q', '-B', "factory/lane-$Lane", "origin/$Branch"))) { return $false }
    return (Invoke-Git @('reset', '-q', '--hard', "origin/$Branch"))
}

function Test-Rebasing {
    foreach ($dir in 'rebase-merge', 'rebase-apply') {
        $path = (& git -C $Root rev-parse --git-path $dir).Trim()
        if (-not [IO.Path]::IsPathRooted($path)) { $path = Join-Path $Root $path }
        if (Test-Path $path) { return $true }
    }
    return $false
}

function Get-AuditPathTest {
    # task-board.ps1's own Test-AuditPath, as a script block taking one touch path, so the
    # factory reads the one list of audit paths and guard files that the board and CI's audit
    # guard keep in step (BL-1209) rather than a copy that could drift. The function's text is
    # parsed out of the script once a process; the board script itself is never run here.
    if (-not $script:AuditPathTest) {
        $boardScript = Join-Path $PSScriptRoot '..\.claude\skills\task-board\task-board.ps1'
        $ast = [Management.Automation.Language.Parser]::ParseFile((Resolve-Path -LiteralPath $boardScript).Path, [ref]$null, [ref]$null)
        $function = $ast.Find({ param($Node) $Node -is [Management.Automation.Language.FunctionDefinitionAst] -and $Node.Name -eq 'Test-AuditPath' }, $true)
        if (-not $function) { throw "$boardScript has no Test-AuditPath function" }
        $script:AuditPathTest = [scriptblock]::Create("$($function.Extent.Text)`nTest-AuditPath `$args[0]")
    }
    return $script:AuditPathTest
}

function Test-InteractiveOnlyTask([string]$Path) {
    # True when the task is interactive only by the board's rule: its file says lane: no, or
    # one of its touches is an audit path or guard file (task-board.ps1's Test-AuditPath). No
    # lane ever claims such a task, so one in Doing is held by an interactive session, not
    # orphaned by a lane (BL-2069; shift start leaves it alone), and no lane waits for it to
    # finish (AF-0435: lane 1 waited 64 minutes on BL-3465, and BL-3472, touching .claude,
    # refused two shift starts as held by no lane).
    $text = Get-Content -Raw -LiteralPath $Path
    if ($text -match '(?m)^lane: no\s*$') { return $true }
    if ($text -notmatch '(?m)^touches:\s*\[(.*)\]\s*$') { return $false }
    $touches = @($Matches[1] -split ',' | ForEach-Object { $_.Trim().Trim('"', "'").Replace([string][char]92, '/').TrimEnd('/') } | Where-Object { $_ })
    $isAuditPath = Get-AuditPathTest
    return [bool](@($touches | Where-Object { & $isAuditPath $_ }).Count)
}

function Get-DoingTasks {
    # The tasks in Doing, split by who can hold them: Lane the lane-eligible ones, which a lane
    # of a shift may be running and whose end can make more work ready this shift; Interactive
    # the interactive-only ones (Test-InteractiveOnlyTask), which only an interactive session
    # holds and finishes, in its own time (AF-0435).
    $lane = @(); $interactive = @()
    foreach ($file in @(Get-ChildItem (Join-Path $Root 'Tasks\Doing') -Filter 'BL-*.md' -ErrorAction SilentlyContinue | Sort-Object Name)) {
        $id = Get-TaskIdFromFileName $file.Name
        if (Test-InteractiveOnlyTask $file.FullName) { $interactive += $id } else { $lane += $id }
    }
    return [pscustomobject]@{ Lane = $lane; Interactive = $interactive }
}

function Get-NothingReadyEndNotice {
    # The trace line a lane writes when it ends because nothing is ready and nothing in Doing
    # is a lane's: the interactive-only work that is left (-InteractiveDoing, the Doing tasks
    # an interactive session holds; -HoldNote, Get-InteractiveHoldNote's sentence on Backlog),
    # so an interactive session knows to take it, or '' when there is none to name.
    param([string[]]$InteractiveDoing = @(), [string]$HoldNote = '')
    $parts = @()
    if ($InteractiveDoing.Count) { $parts += "in Doing with an interactive session: $($InteractiveDoing -join ', ')" }
    if ($HoldNote) { $parts += "Backlog is $HoldNote" }
    if (-not $parts.Count) { return '' }
    return "nothing a lane can claim; ending the lane rather than waiting on interactive work ($($parts -join '; ')). An interactive session takes these; a -Continuous shift polls the board and starts the next shift once a task is claimable"
}

function Get-DuplicateIdSince {
    # The commit to hand `dedupe -Since` so that the first of the live tasks sharing <Id> to
    # appear keeps it: the parent of the commit that added the newest of them. $null when
    # no commit of this checkout added any of them.
    param([string]$Id)
    $added = @()
    foreach ($file in Get-ChildItem (Join-Path $Root 'Tasks') -Directory | Where-Object { $_.Name -ne 'Done' } | ForEach-Object { Get-ChildItem $_.FullName -Filter "$Id-*.md" }) {
        $first = @(& git -C $Root log --diff-filter=A --format=%H -- "Tasks/*/$($file.Name)" 2>$null | Where-Object { $_ }) | Select-Object -Last 1
        if ($first) { $added += $first }
    }
    if (-not $added.Count) { return $null }
    $newest = @(& git -C $Root rev-list --topo-order HEAD 2>$null | Where-Object { $added -contains $_ })[0]
    $parent = (& git -C $Root rev-parse --verify -q "$newest^" 2>$null)
    if ($LASTEXITCODE -ne 0) { return $null }
    return "$parent".Trim()
}

function Invoke-RenumberDuplicates {
    # Two live tasks share <Id>: renumber the later ones, commit and push the renumbering.
    # True when it was pushed, so the claim can go on; false when it could not be.
    param([string]$Id)
    $since = Get-DuplicateIdSince $Id
    if (-not $since) { Write-Trace $Id 'renum' "$Id names several tasks but no commit says which came first" 'Yellow'; return $false }
    $renumbered = @((Invoke-Board @('dedupe', '-Since', $since)) | Where-Object { $_ -match '->' })
    if (-not $renumbered.Count) { return $false }
    Invoke-Git @('add', '-A', 'Tasks') | Out-Null
    Invoke-Git @('commit', '-q', '-m', "chore(tasks): renumber duplicate task IDs`n`n$($renumbered -join "`n")", '-m', $SkipCiNote) | Out-Null
    foreach ($line in $renumbered) { Write-Trace $Id 'renum' "renumbered duplicate task IDs $($line -replace '\s{2,}.*$', '')" 'DarkYellow' }
    return (Move-RenumberedStashesAfterPush $Id $renumbered { Invoke-Git @('push', '-q', 'origin', "HEAD:$Branch") })
}

# ---------------------------------------------------------------------------- readiness

# The pre-claim check (BL-3001): a script-only look at a task's file that keeps a lane from
# claiming a task whose run could only come back without Done or keep the other lanes
# idle. Measured on the 227 lane runs of 2026-10-19 to 2026-10-21: 34 runs ended Blocked
# or requeued ($64 of $328); most of them for reasons no task file shows (a dependency or a
# held file found mid-run), so only two rules are kept, each with no past Done run against it.

# A feature or protocol task whose title names this many upstream case numbers or more is
# too big for one run: BL-2834, BL-2844, BL-2845, BL-2846 and BL-2847 (10 to 12 cases,
# porting or standing in per case) each came back without Done from its first run, three
# of them split for size, at $1.20 to $2.54 a run; of the 9-case tasks, BL-2835 and BL-2842
# were Done in one run and BL-2843 was split, so 9 is the edge and 10 the limit.
$UnreadyTitleCaseLimit = 10

function Get-TitleCaseCount {
    # How many distinct upstream case numbers (two to four digits) a task title names, after
    # dropping task, decision and finding IDs (BL-2843, ADR-0723, AF-0304), versions (8.22.0)
    # and dates, so a title's own references never count as cases.
    param([string]$Title)
    $plain = $Title -replace '\b[A-Z]{2,4}-\d+', ' ' -replace '\d+(\.\d+)+', ' ' -replace '\b\d{4}-\d\d-\d\d\b', ' '
    return @([regex]::Matches($plain, '(?<![\w.])\d{2,4}(?![\w.])') | ForEach-Object { [int]$_.Value } | Select-Object -Unique).Count
}

function Get-TaskTextUnreadyWhy {
    # Why a task file's text says a lane must not claim it yet, or '' when it may. Two rules:
    # - touches empty, missing or '*': the board runs it alone, so every other lane idles
    #   for its whole run (BL-2917 and BL-2998 each held capacity at 1).
    # - a feature or protocol task whose title names $UnreadyTitleCaseLimit or more upstream
    #   cases, unless its title starts with a verb that does one thing to all of them (List,
    #   Record, Report, Check, Mark, Exclude: BL-2826, BL-2849, BL-2858 and BL-2861 named 10
    #   to 18 and were Done) or a lane already handed it back once (that run split it or
    #   filed what it waits on, and what is left fits a run: BL-2844, BL-2845 and BL-2846
    #   were Done on their next claim).
    # Not rules, because the data showed no signal: whole project folders in touches (26 Done
    # tasks named one), many touches or criteria, and a task requeued before.
    param([string]$Text)
    $touches = if ($Text -match '(?m)^touches:[ \t]*(.*)$') { @(($Matches[1].Trim().Trim('[', ']') -split ',') | ForEach-Object { $_.Trim() } | Where-Object { $_ }) } else { @() }
    if (-not $touches.Count -or $touches -contains '*') {
        return 'its touches are empty or *, so it would run alone and idle every other lane; name the folders and files it changes'
    }
    $pipeline = if ($Text -match '(?m)^pipeline:[ \t]*(\S+)') { $Matches[1] } else { '' }
    $title = if ($Text -match '(?m)^title:[ \t]*(.+)$') { $Matches[1].Trim().Trim('"', "'") } else { '' }
    if ($pipeline -notin 'feature', 'protocol') { return '' }
    if ($title -match '^(List|Record|Report|Check|Mark|Exclude)\b') { return '' }
    $log = if ($Text -match '(?s)\n## Log\b(.*)$') { $Matches[1] } else { '' }
    if ($log -match 'Doing -> Backlog') { return '' }
    $cases = Get-TitleCaseCount $title
    if ($cases -lt $UnreadyTitleCaseLimit) { return '' }
    return "its title names $cases upstream cases, more than one run finishes (at most $($UnreadyTitleCaseLimit - 1)); split it"
}

function Get-TaskUnreadyWhy {
    # Get-TaskTextUnreadyWhy for the live task <Id>; '' when no file names it.
    param([string]$Id)
    $file = Get-ChildItem (Join-Path $Root 'Tasks') -Directory | Where-Object { $_.Name -ne 'Done' } |
        ForEach-Object { Get-ChildItem $_.FullName -Filter "$Id-*.md" } | Select-Object -First 1
    if (-not $file) { return '' }
    return (Get-TaskTextUnreadyWhy ([IO.File]::ReadAllText($file.FullName)))
}

# Tasks a lane of this shift passed over as unready, one "BL-###<TAB>why" a line in
# <repo>.logs\lanes-<stamp>\unready.txt, so each is traced once a shift rather than at every
# claim, and a planner or the shift end can list what to fix. Written under the claim lock.
function Get-ShiftUnreadyPath { return (Join-Path (Join-Path $LogDir "lanes-$Stamp") 'unready.txt') }

function Get-ShiftUnready {
    $path = Get-ShiftUnreadyPath
    if (-not (Test-Path $path)) { return @() }
    return @(Get-Content $path | Where-Object { "$_".Trim() })
}

function Add-ShiftUnready {
    # Records <Id> as unready this shift; $true the first time, $false when it already was.
    param([string]$Id, [string]$Why)
    if (@(Get-ShiftUnready | Where-Object { $_ -match "^$Id`t" }).Count) { return $false }
    $path = Get-ShiftUnreadyPath
    New-Item -ItemType Directory -Force -Path (Split-Path $path) | Out-Null
    Add-Content -Path $path -Value "$Id`t$Why" -Encoding ASCII
    return $true
}

function Complete-ClaimSplit {
    # Acts on what Invoke-TaskSplit returned for a capped task the claim offered, as ShiftRunner.ps1
    # does, under the integrate lock. 'failed' moves the task to Blocked with a "Stewart:" reason,
    # then commits and pushes; a refused pull is followed by 'git rebase --abort'. 'skipped' leaves
    # the task in Backlog (another lane's split holds today's marker). 'reset-failed' left the
    # split commit in place: it is pushed (valid, so origin sees the new IDs its stash copies
    # carry), else the stash copies stored under those IDs are dropped, since origin never saw
    # them. Returns 'blocked', 'pushed', 'stash-dropped' or '' for any other result.
    param(
        [string]$Id, [string]$Why, [string]$SplitResult,
        [scriptblock]$Git = { param([string[]]$GitArgs) Invoke-Git $GitArgs })
    # 'skipped' means another lane already offered the task for a split today: the task is still in
    # Backlog, where every lane sees it, so it stays there rather than going to Stewart.
    if ($SplitResult -eq 'failed') {
        $lock = Enter-Lock
        try {
            Invoke-Board @('move', '-Id', $Id, '-To', 'Blocked', '-Reason', "Stewart: dark factory $Why; the automatic split returned '$SplitResult'") | Out-Null
            if ((Get-TaskState $Id) -ne 'Blocked') {
                Write-Trace $Id 'split' "the split returned '$SplitResult'; the task did not move to Blocked" 'Red'
                return ''
            }
            & $Git @('add', '-A', 'Tasks') | Out-Null
            & $Git @('commit', '-q', '-m', "chore(tasks): block $Id - automatic split returned '$SplitResult'", '-m', $SkipCiNote) | Out-Null
            $pushed = [bool](& $Git @('push', '-q', 'origin', "HEAD:$Branch"))
            if (-not $pushed) {
                if (& $Git @('pull', '-q', '--rebase', 'origin', $Branch)) { $pushed = [bool](& $Git @('push', '-q', 'origin', "HEAD:$Branch")) }
                else { & $Git @('rebase', '--abort') | Out-Null }
            }
            Write-Trace $Id 'split' "the split returned '$SplitResult'; blocked$(if (-not $pushed) { ', but the push was refused' })" 'Red'
        } finally { $lock.Dispose() }
        return 'blocked'
    }
    if ($SplitResult -ne 'reset-failed') { return '' }
    $lock = Enter-Lock
    try {
        $subject = "$(& git -C $Root log -1 --format=%s 2>$null)"
        $pushed = [bool](& $Git @('push', '-q', 'origin', "HEAD:$Branch"))
        if (-not $pushed) {
            if (& $Git @('pull', '-q', '--rebase', 'origin', $Branch)) { $pushed = [bool](& $Git @('push', '-q', 'origin', "HEAD:$Branch")) }
            elseif (Test-Rebasing) { & $Git @('rebase', '--abort') | Out-Null }
        }
        if ($pushed) {
            Write-Trace $Id 'split' 'the split commit could not be reset, so it was pushed' 'Yellow'
            return 'pushed'
        }
        if ($subject -notmatch '^chore\(tasks\): split \S+ into (.+?) - over') {
            Write-Trace $Id 'split' "split subject not recognised; dropped no stash copies" 'Red'
            return 'subject-unrecognised'
        }
        $newIds = @($Matches[1] -split ', ')
        $waiting = @(& git -C $Root log --format=%s "origin/$Branch..HEAD" 2>$null) -contains $subject
        if ($waiting) {
            Write-Trace $Id 'split' "split commit still waiting to be integrated; kept stash copies of $($newIds -join ', ')" 'Red'
            return 'stash-kept'
        }
        $removed = @()
        foreach ($newId in $newIds) {
            $sha = Get-TaskStashSha $newId
            if ($sha) {
                Remove-TaskStashEntries @($sha) -Id $newId
                if (-not (Get-TaskStashSha $newId)) { $removed += $newId }
            }
        }
        Write-Trace $Id 'split' "split commit gone; dropped stash copies of $(if ($removed) { $removed -join ', ' } else { 'no task' }) that origin never saw" 'Red'
        return 'stash-dropped'
    } finally { $lock.Dispose() }
}

# ---------------------------------------------------------------------------- CI fixes first

# A red CI on the shift branch is fixed before any other work (ADR-1158). Measured on
# 2026-10-29/30, the red spells over 30 minutes were mostly a CI-fix task that sat ready
# while lanes took other work: BL-4083 was shut out for 86 minutes by its shift's requeued
# list after one run sent it back behind BL-4082 (Done a minute later), then waited 116
# minutes as #6 in next's order behind High feature tasks with more tasks waiting on them;
# BL-4052 and BL-4299 waited 21 and 41 minutes for a lane to come free.

function Get-ClaimSkip {
    # The IDs a lane's claim passes over: the tasks this lane already ran this shift
    # (-Attempted), and those any lane of the shift sent back to Backlog (-ShiftRequeued) but
    # a CI fix, which the next lane claims at once: what sent it back (a task holding a file
    # it needs, a dependency) is gone within minutes, and the red CI is not.
    param([string[]]$Attempted = @(), [string[]]$ShiftRequeued = @())
    $requeued = @($ShiftRequeued | Where-Object { $_ -and $Attempted -notcontains $_ -and -not (Test-CiFixTitle (Get-TaskTitle $_)) })
    return @(@($Attempted) + $requeued | Where-Object { $_ } | Select-Object -Unique)
}

function Get-ReadyCiFixTaskId {
    # The CI-fix task (Test-CiFixFileName) in Backlog a claim takes before anything next
    # offers, the oldest first, or '' when none may be claimed now: none is in -Skip, each of
    # its depends-on is Done (in Tasks\Done or archived under it), and it is neither
    # interactive only nor over its cost cap (Get-TaskNotClaimableWhy). Its touches may
    # overlap a task in Doing: that lane rebases over the fix (ADR-1158).
    param([string[]]$Skip = @())
    $files = @(Get-ChildItem (Join-Path $Root 'Tasks\Backlog') -Filter 'BL-*.md' -ErrorAction SilentlyContinue |
        Where-Object { Test-CiFixFileName $_.Name } | Sort-Object { [int](($_.Name -replace '^BL-(\d+).*$', '$1')) })
    foreach ($file in $files) {
        $id = Get-TaskIdFromFileName $file.Name
        if ($Skip -contains $id) { continue }
        $text = [IO.File]::ReadAllText($file.FullName)
        $depends = if ($text -match '(?m)^depends-on:[ \t]*\[(.*)\]') { @($Matches[1] -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ }) } else { @() }
        if (@($depends | Where-Object { (Get-TaskState $_) -in 'Backlog', 'Doing', 'Blocked', 'Deferred' }).Count) { continue }
        if ((Test-InteractiveOnlyTask $file.FullName) -or (Get-TaskNotClaimableWhy $id)) { continue }
        return $id
    }
    return ''
}

function Invoke-Claim {
    # Returns @{ Id = 'BL-###' } on success, or @{ Wait = $true } when every ready task
    # overlaps work in progress (or nothing is ready but a lane-eligible task in Doing may
    # unlock more, or the only ready tasks are unready for a lane), or @{ None = $true } when
    # the board has nothing left for this shift's lanes - also when the only work left is
    # interactive only, in Backlog or in Doing (AF-0435); Why then names it.
    param([string[]]$Skip, [switch]$CiFixOnly)
    $splitDue = @()
    $ciFixRefused = @()
    $lock = Enter-Lock
    # Every task next offers is checked against its cost cap while this lane holds the lock
    # every claim and integration waits on, and each check read the 40 newest run logs again
    # (about 19 s): the costs are read once for the whole claim (BL-3988's read-once memo).
    $script:RecentRunCostsMemo = @{}
    try {
        foreach ($attempt in 1..5) {
            if (-not (Wait-RemoteReachable)) { return @{ Wait = $true; Why = 'origin out of reach' } }
            if (-not (Sync-Lane)) { Start-Sleep -Seconds 10; continue }
            $requeued = @(Invoke-Requeue)
            if ($requeued.Count) {
                Invoke-Git @('add', '-A', 'Tasks') | Out-Null
                Invoke-Git @('commit', '-q', '-m', "chore(tasks): requeue $($requeued -join ', ') - blockers Done", '-m', $SkipCiNote) | Out-Null
                if (-not (Invoke-Git @('push', '-q', 'origin', "HEAD:$Branch"))) { continue }
            }
            # A task whose runs of the last day already spent its cost cap is left in Backlog
            # rather than claimed only to be Blocked at once, which cost BL-2788 two claims
            # that could only block (AF-0304); the next claim offers it once the window passes.
            # A task the pre-claim check finds unready is passed over the same way, traced once
            # a shift (BL-3001).
            # Only bare task IDs go to -Skip: anything else garbles the argument list.
            $skipNow = @($Skip | ForEach-Object { "$_".Trim() } | Where-Object { $_ -match '^BL-\d+$' })
            $unready = @()
            # A ready CI fix comes before anything next offers, whatever its priority order or
            # an overlap with Doing would say (ADR-1158); one the board refused this claim is not tried again.
            $id = Get-ReadyCiFixTaskId -Skip (@($skipNow) + @($ciFixRefused))
            $overCap = $false; $unreadyWhy = ''
            if ($id) { $next = '' }
            else { do {
                $boardArgs = @('next')
                if ($skipNow.Count) { $boardArgs += @('-Skip', ($skipNow -join ',')) }
                $next = (Invoke-Board $boardArgs) -join "`n"
                $id = Get-NextTaskId $next
                # A task offered again although it is in the skip list would loop here for ever
                # while this lane holds the lock, and every other lane waits on it (shift
                # 20261021-175054: lane 6 offered BL-2970 752 times in 80 minutes). Wait instead.
                if ($id -and ($skipNow -contains $id)) {
                    Write-Trace $id 'claim' "next offered it again although -Skip named it ($($skipNow -join ',')); waiting" 'DarkYellow'
                    return @{ Wait = $true; Why = "next kept offering skipped $id" }
                }
                # The shared claimable-now predicate (BL-3090): over its cost cap or interactive only.
                $notClaimableWhy = if ($id) { Get-TaskNotClaimableWhy $id } else { '' }
                $overCap = [bool]$notClaimableWhy
                if ($overCap) { Write-Trace $id 'unclaimable' "$(Get-Short $notClaimableWhy 80); not claimed" 'Yellow'; $skipNow += $id }
                # One over its cost cap (not one only interactive) is split once the lock is released (BL-3091).
                if ($overCap -and (Get-TaskCapSpentWhy $id) -and $splitDue.Id -notcontains $id) { $splitDue += @{ Id = $id; Why = $notClaimableWhy } }
                # An unready task that overlaps work in Doing is never offered: next holds back
                # every task behind it instead ("No task can start yet ... e.g. BL-### with ...").
                # Skipping it releases them; without that, BL-3026 (touches: []) held lane 1 in
                # a wait while ready tasks sat behind it (BL-3028).
                $candidate = $id
                if (-not $id -and $next -match 'can start yet.*?e\.g\. (BL-\d+) with' -and $skipNow -notcontains $Matches[1]) { $candidate = $Matches[1] }
                $unreadyWhy = if ($candidate -and -not $overCap) { Get-TaskUnreadyWhy $candidate } else { '' }
                if ($unreadyWhy) {
                    if (Add-ShiftUnready $candidate $unreadyWhy) { Write-Trace $candidate 'unready' "not claimed: $unreadyWhy" 'Yellow' }
                    $skipNow += $candidate
                    $unready += $candidate
                }
            } while ($overCap -or $unreadyWhy) }
            if (-not $id) {
                # Nothing is ready: wait only while a lane-eligible task is in Doing, since its
                # end may make more ready. An interactive-only one in Doing is a session's, which
                # may hold it for hours: lane 1 waited 64 minutes on BL-3465 (AF-0435). The lane
                # ends instead, and a -Continuous shift's idle poll starts the next shift once
                # anything is claimable (BL-3092), so no later task is left without a shift.
                $doing = Get-DoingTasks
                if ($next -match 'can start yet' -or $doing.Lane.Count -gt 0) { return @{ Wait = $true; Why = (Get-Short (Get-WaitReason $next) 80) } }
                # Ready tasks this lane already ran this shift (its -Skip) are not claimable by
                # it, but they are not unready: end the lane so a fresh shift, whose lanes have
                # run nothing yet, claims them. Waiting held lane 1 four hours on 2026-10-21
                # with BL-3011 and BL-3023 ready behind the unready BL-3026 (BL-3028).
                if ($unready.Count -and $skipNow.Count -gt $unready.Count) {
                    $behind = Get-NextTaskId ((Invoke-Board @('next', '-Skip', ($unready -join ','))) -join "`n")
                    if ($behind) { return @{ None = $true; Why = "only unready tasks and tasks this lane already ran this shift ($behind) are ready" } }
                }
                # Only unready tasks are ready: wait rather than end the lane, or -Continuous
                # would start shift after shift that each pass them over.
                if ($unready.Count) { return @{ Wait = $true; Why = (Get-Short "only unready tasks are ready ($($unready -join ', ')); see unready.txt" 80) } }
                $notice = Get-NothingReadyEndNotice -InteractiveDoing $doing.Interactive -HoldNote (Get-InteractiveHoldNote (Join-Path $Root 'Tasks\Backlog'))
                if ($notice) { Write-Trace '-' 'idle' $notice 'Yellow' }
                return @{ None = $true; Why = $notice }
            }
            # With the usage stop on, only a CI fix is claimed (AF-0308); any other task waits.
            if ($CiFixOnly -and -not (Test-CiFixTitle (Get-TaskTitle $id))) { return @{ None = $true } }
            $moved = (Invoke-Board @('move', '-Id', $id, '-To', 'Doing')) -join ' '
            # A refusal over a shared ID is a refusal even when the task's twin is already in
            # Doing: Get-TaskState finds the twin, and lane 2 once took that for its own claim
            # and redid lane 6's work (AF-0386, BL-3363).
            $sharedId = $moved -match 'names \d+ tasks'
            if ($sharedId -or (Get-TaskState $id) -ne 'Doing') {
                # The board refuses an ID that names two live tasks, and no claim can win
                # until they are renumbered (BL-2014); any other refusal is reported as it is.
                if ($sharedId -and (Invoke-RenumberDuplicates $id)) { continue }
                if (Test-CiFixTitle (Get-TaskTitle $id)) { $ciFixRefused += $id }
                $refusal = Get-Short $moved 100
                Write-Trace $id 'refused' "the board would not move it to Doing: $refusal" 'DarkYellow'
                continue
            }
            Invoke-Git @('add', '-A', 'Tasks') | Out-Null
            Invoke-Git @('commit', '-q', '-m', "chore(tasks): claim $id on dark factory lane $Lane", '-m', $SkipCiNote) | Out-Null
            if (Invoke-Git @('push', '-q', 'origin', "HEAD:$Branch")) { return @{ Id = $id } }
            # Only a claim that was pushed is traced as 'claim', so the log counts claims
            # truly; a refused push is 'race', and an unheard one is no race at all.
            if (Test-RemoteReachable) { Write-Trace $id 'race' 'another lane pushed first; picking again' 'DarkYellow' }
            $lostRace = $true
        }
        $why = if ($lostRace) { 'claim kept losing races' } else { 'claim kept being refused by the board or origin' }
        return @{ Wait = $true; Why = $why }
    } finally {
        $script:RecentRunCostsMemo = $null
        $lock.Dispose()
        # A split run takes minutes, so it never holds the lock every lane's claim waits on;
        # Invoke-TaskSplit takes it again only to commit and push. At most once a day each.
        # A split that throws is traced and must not replace the claim result being returned.
        foreach ($due in $splitDue) {
            try { Complete-ClaimSplit $due.Id $due.Why (Invoke-TaskSplit $due.Id $due.Why -PullOnRefusal) | Out-Null }
            catch { Write-Trace $due.Id 'split' "the split threw: $(Get-Short $_.Exception.Message 100)" 'Red' }
        }
    }
}

if ($TestClaimDuplicates) {
    # Two lanes filed BL-001 at the same time: the board refuses to move either, so a claim
    # must renumber the later one and carry on, rather than retry and call it a lost race.
    $temp = Join-Path ([IO.Path]::GetTempPath()) "df-claim-dup-$PID"
    $origin = Join-Path $temp 'origin.git'
    $repo = Join-Path $temp 'lane-9'
    New-Item -ItemType Directory -Force -Path $temp | Out-Null
    git init -q --bare -b work $origin 2>&1 | Out-Null
    git clone -q $origin $repo 2>&1 | Out-Null
    git -C $repo config user.name t; git -C $repo config user.email t@t
    git -C $repo checkout -q -b work 2>&1 | Out-Null
    foreach ($state in 'Doing', 'Backlog', 'Blocked', 'Deferred', 'Done') { New-Item -ItemType Directory -Force -Path (Join-Path $repo "Tasks\$state") | Out-Null; Set-Content -Path (Join-Path $repo "Tasks\$state\.gitkeep") -Value '' }
    $file = {
        param($Slug, $Id = 'BL-001', $Priority = 'Normal')
        Set-Content -Path (Join-Path $repo "Tasks\Backlog\$Id-$Slug.md") -Encoding UTF8 -Value @(
            '---', "id: $Id", "title: $Slug", "priority: $Priority", 'assignee: Claude', 'pipeline: direct',
            'depends-on: []', "touches: [$Slug]", 'requirement: none', 'created: 2026-10-01', 'completed:', '---',
            "# $Id - $Slug", '', '## Goal', '', '## Context', '', '## Acceptance criteria', '', '- [ ] Claimed.', '', '## Notes', '', '## Log', '', '- 2026-10-01: Created.')
        git -C $repo add -A 2>&1 | Out-Null
        git -C $repo commit -q -m "file $Slug" 2>&1 | Out-Null
    }
    & $file 'first'
    & $file 'second'
    # A third, High task is offered first, reported over its cost cap, and so split after the claim; the split throws.
    & $file 'capped' 'BL-009' 'High'
    & $file 'splitfails' 'BL-010' 'High'
    & $file 'resetfails' 'BL-011' 'High'
    git -C $repo push -q origin work 2>&1 | Out-Null
    $script:Root = $repo; $script:Branch = 'work'; $script:Lane = 9; $script:Stamp = 'test'
    $script:LanesDir = $temp; $script:LockFile = Join-Path $temp 'integrate.lock'; $script:LogDir = Join-Path $temp 'logs'
    $env:CLAUDE_PROJECT_DIR = $repo
    $failed = 0
    $script:TraceFile = Join-Path $LogDir 'trace.log'; New-Item -ItemType Directory -Force -Path $LogDir | Out-Null
    function Get-TaskNotClaimableWhy { param($Id) if ($Id -in 'BL-009', 'BL-010', 'BL-011') { 'over its cost cap' } else { '' } }
    function Get-TaskCapSpentWhy { param($Id) 'spent' }
    function Invoke-TaskSplit { param($Id) if ($Id -eq 'BL-010') { 'failed' } elseif ($Id -eq 'BL-011') { 'reset-failed' } else { throw 'claude is not on PATH' } }
    try {
        $claim = Invoke-Claim -Skip @()
        git -C $repo fetch -q origin work 2>&1 | Out-Null
        $shared = @(git -C $repo ls-tree -r --name-only origin/work Tasks) | Where-Object { $_ -match 'BL-\d+-' }
        $ids = @($shared | ForEach-Object { if ($_ -match '(BL-\d+)-') { $Matches[1] } })
        $dropGit = { param([string[]]$GitArgs) if ($GitArgs[0] -eq 'push') { $false } else { Invoke-Git $GitArgs } }
        Set-Content -Path (Join-Path $repo 'stashed.txt') -Value 'x'
        git -C $repo add stashed.txt 2>&1 | Out-Null
        git -C $repo stash push -q -m 'darkfactory BL-777 20260101-000000' 2>&1 | Out-Null
        git -C $repo commit -q --allow-empty -m 'chore(tasks): split BL-011 into BL-777 - over its cost cap' 2>&1 | Out-Null
        $pullCalls = [System.Collections.Generic.List[string]]::new()
        $keepGit = { param([string[]]$GitArgs) $pullCalls.Add($GitArgs[0]); if ($GitArgs[0] -eq 'push') { $false } else { Invoke-Git $GitArgs } }
        $kept = Complete-ClaimSplit 'BL-011' 'over its cost cap' 'reset-failed' -Git $keepGit
        $keptStashes = @(git -C $repo stash list).Count
        $goneGit = { param([string[]]$GitArgs) if ($GitArgs[0] -eq 'pull') { git -C $repo reset -q --hard HEAD~1 2>&1 | Out-Null; $false } elseif ($GitArgs[0] -eq 'push') { $false } else { Invoke-Git $GitArgs } }
        $dropped = Complete-ClaimSplit 'BL-011' 'over its cost cap' 'reset-failed' -Git $goneGit
        $droppedStashes = @(git -C $repo stash list).Count
        Set-Content -Path (Join-Path $repo 'stashed2.txt') -Value 'y'
        git -C $repo add stashed2.txt 2>&1 | Out-Null
        git -C $repo stash push -q -m 'darkfactory BL-778 20260101-000000' 2>&1 | Out-Null
        git -C $repo commit -q --allow-empty -m 'something else entirely' 2>&1 | Out-Null
        $unrecognised = Complete-ClaimSplit 'BL-011' 'over its cost cap' 'reset-failed' -Git $dropGit
        $unrecognisedStashes = @(git -C $repo stash list).Count
        $headBefore = "$(git -C $repo rev-parse HEAD)"
        $skipped = Complete-ClaimSplit 'BL-009' 'over its cost cap' 'skipped' -Git $dropGit
        $skippedInBacklog = [bool](Get-ChildItem (Join-Path $repo 'Tasks\Backlog') -Filter 'BL-009-*.md') -and "$(git -C $repo rev-parse HEAD)" -eq $headBefore
        $gitCalls = [System.Collections.Generic.List[string]]::new()
        $refusedGit = { param([string[]]$GitArgs) $gitCalls.Add($GitArgs[0]); if ($GitArgs[0] -in 'push', 'pull', 'rebase') { $false } else { Invoke-Git $GitArgs } }
        $refused = Complete-ClaimSplit 'BL-009' 'over its cost cap' 'failed' -Git $refusedGit
        # AF-0386: a task's twin is already in Doing, so a refused move must not pass for a claim.
        git -C $repo reset -q --hard origin/work 2>&1 | Out-Null
        $reviewed = "$(git -C $repo rev-parse HEAD)".Trim()
        $before = @(Get-ChildItem (Join-Path $repo 'Tasks\Backlog') -Filter 'BL-*.md' | ForEach-Object { Get-TaskIdFromFileName $_.Name })
        & $file 'twin-a' 'BL-020' 'High'
        git -C $repo mv (Join-Path $repo 'Tasks\Backlog\BL-020-twin-a.md') (Join-Path $repo 'Tasks\Doing\BL-020-twin-a.md') 2>&1 | Out-Null
        git -C $repo commit -q -m 'claim twin-a' 2>&1 | Out-Null
        & $file 'twin-b' 'BL-020' 'High'
        git -C $repo push -q origin HEAD:work 2>&1 | Out-Null
        $twinClaim = Invoke-Claim -Skip $before
        git -C $repo fetch -q origin work 2>&1 | Out-Null
        $twinTree = @(git -C $repo ls-tree -r --name-only origin/work Tasks)
        $twinB = "$($twinTree | Where-Object { $_ -match 'twin-b' })"
        # The shift-end review filed BL-020 from the board at the reviewed commit, while a lane
        # had filed BL-020 on the branch: the review's push renumbers its own task.
        $reviewDir = Join-Path $temp 'review'
        git -C $repo worktree add -q --detach $reviewDir $reviewed 2>&1 | Out-Null
        Set-Content -Path (Join-Path $reviewDir 'Tasks\Backlog\BL-020-review-finding.md') -Encoding UTF8 -Value ((Get-Content (Join-Path $repo 'Tasks\Doing\BL-020-twin-a.md') -ErrorAction SilentlyContinue) -replace 'twin-a', 'review-finding')
        if (-not (Test-Path (Join-Path $reviewDir 'Tasks\Backlog\BL-020-review-finding.md'))) { throw 'no review task file' }
        git -C $reviewDir add -A 2>&1 | Out-Null
        git -C $reviewDir commit -q -m 'chore(tasks): file shift-end review findings' 2>&1 | Out-Null
        $reviewPush = Push-ReviewCommitsUnderLock -Dir $reviewDir -Head $reviewed -Branch 'work'
        git -C $repo worktree remove --force $reviewDir 2>&1 | Out-Null
        # BL-3491: a dedupe that fails and a commit that fails each stop the push and say why.
        $failHead = "$(git -C $repo rev-parse origin/work)".Trim()
        $stubBoard = Join-Path $temp 'stub-board.ps1'
        $savedBoard = $Board
        $failingReview = {
            param([string]$Name)
            $dir = Join-Path $temp $Name
            git -C $repo worktree add -q --detach $dir $reviewed 2>&1 | Out-Null
            Set-Content -Path (Join-Path $dir "Tasks\Backlog\BL-090-$Name.md") -Value $Name
            git -C $dir add -A 2>&1 | Out-Null
            git -C $dir commit -q -m 'chore(tasks): file shift-end review findings' 2>&1 | Out-Null
            return $dir
        }
        try {
            Set-Content -Path $stubBoard -Value 'Write-Output "dedupe broke"; exit 1'
            $Board = $stubBoard
            $dedupeDir = & $failingReview 'dedupe-fails'
            $dedupePush = Push-ReviewCommitsUnderLock -Dir $dedupeDir -Head $reviewed -Branch 'work'
            git -C $repo worktree remove --force $dedupeDir 2>&1 | Out-Null
            Set-Content -Path $stubBoard -Value 'Write-Output "BL-090  -> BL-091"; exit 0'
            $hooks = Join-Path $temp 'hooks'
            New-Item -ItemType Directory -Force -Path $hooks | Out-Null
            [IO.File]::WriteAllText((Join-Path $hooks 'pre-commit'), "#!/bin/sh`nexit 1`n")
            $commitDir = & $failingReview 'commit-fails'
            git -C $repo config core.hooksPath $hooks
            $commitPush = Push-ReviewCommitsUnderLock -Dir $commitDir -Head $reviewed -Branch 'work'
            git -C $repo config --unset core.hooksPath
            git -C $repo worktree remove --force $commitDir 2>&1 | Out-Null
        } finally { $Board = $savedBoard }
        git -C $repo fetch -q origin work 2>&1 | Out-Null
        $failPushed = "$(git -C $repo rev-parse origin/work)".Trim() -ne $failHead
        $failTrace = @(Get-Content $TraceFile -ErrorAction SilentlyContinue | Where-Object { $_ -match 'renum' })
        git -C $repo fetch -q origin work 2>&1 | Out-Null
        $reviewTree = @(git -C $repo ls-tree -r --name-only origin/work Tasks)
        $reviewTask = "$($reviewTree | Where-Object { $_ -match 'review-finding' })"
        $liveIds = @($reviewTree | Where-Object { $_ -notmatch '/Done/' } | ForEach-Object { if ($_ -match '(BL-\d+)-') { $Matches[1] } })
        $trace = @(Get-Content $TraceFile -ErrorAction SilentlyContinue)
        git -C $repo fetch -q origin work 2>&1 | Out-Null
        $blockedFile = @(git -C $repo show origin/work:Tasks/Blocked/BL-010-splitfails.md 2>&1) -join "`n"
        $cases = @(
            ,@('a split that returned failed leaves the task Blocked with a Stewart reason on the shared branch', 'True', "$($blockedFile -match 'Stewart: dark factory over its cost cap; the automatic split returned .failed.')")
            ,@('a split that was skipped leaves the task in Backlog with no commit', "True ''", "$skippedInBacklog '$skipped'")
            ,@('a refused pull aborts the rebase and the block is traced as refused', 'blocked push pull rebase True', "$refused $($gitCalls | Where-Object { $_ -in 'push', 'pull', 'rebase' }) $(@($trace | Where-Object { $_ -match 'BL-009.*blocked, but the push was refused' }).Count -eq 1)")
            ,@('a split that returned reset-failed is pushed and traced', 'True', "$(@($trace | Where-Object { $_ -match 'BL-011.*could not be reset, so it was pushed' }).Count -eq 1)")
            ,@('a refused push is retried after pull --rebase, and a split commit still waiting keeps its stash copies', 'stash-kept 1 push pull push True', "$kept $keptStashes $($pullCalls | Where-Object { $_ -in 'push', 'pull', 'rebase' }) $(@($trace | Where-Object { $_ -match 'BL-011.*still waiting to be integrated; kept stash copies of BL-777' }).Count -eq 1)")
            ,@('a split commit gone from the checkout drops its stash copies and names only the ones removed', 'stash-dropped 0 True', "$dropped $droppedStashes $(@($trace | Where-Object { $_ -match 'dropped stash copies of BL-777 that origin never saw' }).Count -eq 1)")
            ,@('an unrecognised split subject drops nothing and says so', 'subject-unrecognised 1 True', "$unrecognised $unrecognisedStashes $(@($trace | Where-Object { $_ -match 'not recognised; dropped no stash copies' }).Count -eq 1)")
            ,@('a duplicate ID is renumbered and the claim goes on', 'claimed', $(if ($claim.Id) { 'claimed' } else { "waited: $($claim.Why)" }))
            ,@('no ID is left naming two tasks on the shared branch', '5 tasks, 5 IDs', "$($ids.Count) tasks, $(@($ids | Select-Object -Unique).Count) IDs")
            ,@('the first task to be filed keeps its ID', 'True', "$(@($shared | Where-Object { $_ -match 'BL-001-first' }).Count -eq 1)")
            ,@('the claimed task is in Doing on the shared branch', 'True', "$(@($shared | Where-Object { $_ -match 'Tasks/Doing/' }).Count -eq 1)")
            ,@('a shared ID whose twin is in Doing is renumbered, not taken for a claim (AF-0386)', 'True True', "$($twinClaim.Id -and $twinClaim.Id -ne 'BL-020') $($twinB -match "^Tasks/Doing/$($twinClaim.Id)-twin-b\.md$")")
            ,@('the shift-end review push renumbers a task ID the branch already had (AF-0386)', "'' True True", "'$reviewPush' $($reviewTask -match '^Tasks/Backlog/BL-0(2[1-9]|[3-9]\d)-review-finding\.md$') $($liveIds.Count -eq @($liveIds | Select-Object -Unique).Count)")
            ,@('a failed dedupe or a failed renumber commit pushes nothing and names the reason (BL-3491)', "True True False True True", "$($dedupePush -match 'dedupe failed') $($commitPush -match 'git commit failed') $failPushed $(@($failTrace | Where-Object { $_ -match 'dedupe failed \(exit 1\): dedupe broke' }).Count -eq 1) $(@($failTrace | Where-Object { $_ -match 'BL-090' }).Count -eq 0)")
        )
        foreach ($case in $cases) {
            if ($case[1] -ceq $case[2]) { Write-Host "PASS $($case[0]): $($case[2])" -ForegroundColor Green }
            else { Write-Host "FAIL $($case[0]): expected $($case[1]), got $($case[2])" -ForegroundColor Red; $failed++ }
        }
    } finally {
        Remove-Item -Recurse -Force -Path $temp -ErrorAction SilentlyContinue
    }
    exit $(if ($failed) { 1 } else { 0 })
}

if ($TestTaskReadiness) {
    # The pre-claim check (BL-3001): each rule on a task file's text, then on a throwaway
    # repository that a claim passes over an unready task, traces it once a shift, and
    # waits rather than ends when only unready tasks are ready.
    $taskText = {
        param([string]$Title, $Touches = '[Curl.Cli.UnitLibrary/Foo.cs]', [string]$Pipeline = 'feature', [string[]]$Log = @('- 2026-10-01: Created.'))
        $front = @('---', 'id: BL-001', "title: $Title", 'priority: Normal', 'assignee: Claude', "pipeline: $Pipeline", 'depends-on: []')
        if ($null -ne $Touches) { $front += "touches: $Touches" }
        return (@($front) + @('requirement: none', 'created: 2026-10-01', 'completed:', '---', "# BL-001 - $Title", '', '## Goal', '', '## Context', '',
            '## Acceptance criteria', '', '- [ ] Claimed.', '', '## Notes', '', '## Log', '') + @($Log)) -join "`n"
    }
    $nine = 'Port upstream unit programs: cases 1394, 1397, 1651, 1656, 1657, 1666, 1667, 1676 and 3205'
    $ten = 'Port upstream unit programs: cases 1395, 1396, 1398, 1399, 1600, 1601, 1602, 1603, 1606 and 1607'
    $requeued = @('- 2026-10-01: Created.', '- 2026-10-02: Backlog -> Doing. Criteria at claim: 1.', '- 2026-10-02: Doing -> Backlog. Split for size: waits on BL-002.')
    $ruleCases = @(
        ,@('a narrow task with few cases is ready', '', (& $taskText 'Make upstream case 1454 pass'))
        ,@('touches: [] is unready', 'touches', (& $taskText 'Fix a thing' '[]'))
        ,@('touches: [*] is unready', 'touches', (& $taskText 'Fix a thing' '[*]'))
        ,@('no touches line is unready', 'touches', (& $taskText 'Fix a thing' $null))
        ,@('whole project folders in touches are ready', '', (& $taskText 'Fix a thing' '[Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]'))
        ,@('a feature title naming 10 cases is unready', 'names 10 upstream cases', (& $taskText $ten))
        ,@('a protocol title naming 10 cases is unready', 'names 10 upstream cases', (& $taskText $ten -Pipeline 'protocol'))
        ,@('a feature title naming 9 cases is ready', '', (& $taskText $nine))
        ,@('a direct task naming 10 cases is ready', '', (& $taskText $ten -Pipeline 'direct'))
        ,@('a docs task naming 10 cases is ready', '', (& $taskText $ten -Pipeline 'docs'))
        ,@('a List title naming 10 cases is ready', '', (& $taskText ($ten -replace '^Port upstream unit programs', 'List the upstream cases')))
        ,@('a Report title naming 10 cases is ready', '', (& $taskText ($ten -replace '^Port upstream unit programs', 'Report the feature so upstream')))
        ,@('10 cases after a lane handed it back once is ready', '', (& $taskText $ten -Log $requeued))
        ,@('IDs, versions and dates in a title are not cases', '', (& $taskText 'Port cases 1394, 1397 (BL-2843, BL-2844, ADR-0723, AF-0304, GF-0012) to curl 8.22.0 as of 2026-10-21 and 8.21.1 with 1651, 1656, 1657, 1666, 1667'))
    )
    $failed = 0
    foreach ($case in $ruleCases) {
        $why = Get-TaskTextUnreadyWhy $case[2]
        $ok = if ($case[1]) { $why -match [regex]::Escape($case[1]) } else { $why -eq '' }
        if ($ok) { Write-Host "PASS $($case[0]): '$why'" -ForegroundColor Green }
        else { Write-Host "FAIL $($case[0]): expected '$($case[1])', got '$why'" -ForegroundColor Red; $failed++ }
    }

    # A High task with empty touches would be offered first; the claim must take the Normal one.
    $temp = Join-Path ([IO.Path]::GetTempPath()) "df-claim-ready-$PID"
    $origin = Join-Path $temp 'origin.git'
    $repo = Join-Path $temp 'lane-9'
    New-Item -ItemType Directory -Force -Path $temp | Out-Null
    git init -q --bare -b work $origin 2>&1 | Out-Null
    git clone -q $origin $repo 2>&1 | Out-Null
    git -C $repo config user.name t; git -C $repo config user.email t@t
    git -C $repo checkout -q -b work 2>&1 | Out-Null
    foreach ($state in 'Doing', 'Backlog', 'Blocked', 'Deferred', 'Done') { New-Item -ItemType Directory -Force -Path (Join-Path $repo "Tasks\$state") | Out-Null; Set-Content -Path (Join-Path $repo "Tasks\$state\.gitkeep") -Value '' }
    Set-Content -Path (Join-Path $repo 'Tasks\Backlog\BL-001-anything.md') -Encoding UTF8 -Value ((& $taskText 'Change anything' '[]') -replace 'priority: Normal', 'priority: High')
    Set-Content -Path (Join-Path $repo 'Tasks\Backlog\BL-002-narrow.md') -Encoding UTF8 -Value ((& $taskText 'Change one file') -replace 'BL-001', 'BL-002')
    # A third ready task, and one in Doing that the unready task overlaps, so next holds
    # back every task behind the unready one rather than offering it (BL-3028).
    Set-Content -Path (Join-Path $repo 'Tasks\Backlog\BL-003-other.md') -Encoding UTF8 -Value ((& $taskText 'Change another file' '[Curl.Cli.UnitLibrary/Bar.cs]') -replace 'BL-001', 'BL-003')
    Set-Content -Path (Join-Path $repo 'Tasks\Doing\BL-004-running.md') -Encoding UTF8 -Value ((& $taskText 'Change a running file' '[Curl.Cli.UnitLibrary/Baz.cs]') -replace 'BL-001', 'BL-004')
    git -C $repo add -A 2>&1 | Out-Null
    git -C $repo commit -q -m 'file four tasks' 2>&1 | Out-Null
    git -C $repo push -q origin work 2>&1 | Out-Null
    $script:Root = $repo; $script:Branch = 'work'; $script:Lane = 9; $script:Stamp = 'test'
    $script:LanesDir = $temp; $script:LockFile = Join-Path $temp 'integrate.lock'; $script:LogDir = Join-Path $temp 'logs'
    $script:TraceFile = Join-Path $LogDir 'trace.log'
    New-Item -ItemType Directory -Force -Path $LogDir | Out-Null
    $env:CLAUDE_PROJECT_DIR = $repo
    try {
        $first = Invoke-Claim -Skip @()
        # BL-002 and BL-004 finish elsewhere; this lane already ran BL-003 this shift.
        git -C $repo rm -q 'Tasks/Doing/BL-002-narrow.md' 'Tasks/Doing/BL-004-running.md' 2>&1 | Out-Null
        git -C $repo commit -q -m 'BL-002 and BL-004 finished' 2>&1 | Out-Null
        git -C $repo push -q origin HEAD:work 2>&1 | Out-Null
        $ranAlready = Invoke-Claim -Skip @('BL-003')
        $third = Invoke-Claim -Skip @()
        # The other task is finished elsewhere: only the unready one is left, with nothing in Doing.
        git -C $repo rm -q 'Tasks/Doing/BL-003-other.md' 2>&1 | Out-Null
        git -C $repo commit -q -m 'BL-003 finished' 2>&1 | Out-Null
        git -C $repo push -q origin HEAD:work 2>&1 | Out-Null
        $second = Invoke-Claim -Skip @()
        # AF-0435: only interactive-only work is left - BL-005 (lane: no) in Backlog, BL-006
        # (lane: no) and BL-007 (touching a guard file) in Doing with an interactive session.
        $unreadyState = Get-TaskState 'BL-001'
        git -C $repo rm -q 'Tasks/Backlog/BL-001-anything.md' 2>&1 | Out-Null
        Set-Content -Path (Join-Path $repo 'Tasks\Backlog\BL-005-interactive.md') -Encoding UTF8 -Value (((& $taskText 'Fix the factory' '[DarkFactory/A.ps1]') -replace 'BL-001', 'BL-005') -replace '(?m)^(touches: .*)$', "`$1`nlane: no")
        Set-Content -Path (Join-Path $repo 'Tasks\Doing\BL-006-session.md') -Encoding UTF8 -Value (((& $taskText 'Fix the factory too' '[DarkFactory/B.ps1]') -replace 'BL-001', 'BL-006') -replace '(?m)^(touches: .*)$', "`$1`nlane: no")
        Set-Content -Path (Join-Path $repo 'Tasks\Doing\BL-007-guard.md') -Encoding UTF8 -Value ((& $taskText 'Change a guard file' '[.claude/settings.json]') -replace 'BL-001', 'BL-007')
        git -C $repo add -A 2>&1 | Out-Null
        git -C $repo commit -q -m 'only interactive work left' 2>&1 | Out-Null
        git -C $repo push -q origin HEAD:work 2>&1 | Out-Null
        $interactiveOnly = Invoke-Claim -Skip @()
        # A lane-eligible task in Doing beside them may still make more ready: the lane waits.
        Set-Content -Path (Join-Path $repo 'Tasks\Doing\BL-008-lane.md') -Encoding UTF8 -Value ((& $taskText 'Change a lane file' '[Curl.Cli.UnitLibrary/Qux.cs]') -replace 'BL-001', 'BL-008')
        git -C $repo add -A 2>&1 | Out-Null
        git -C $repo commit -q -m 'a lane runs BL-008' 2>&1 | Out-Null
        git -C $repo push -q origin HEAD:work 2>&1 | Out-Null
        $laneRunning = Invoke-Claim -Skip @()
        # ADR-1158: a High feature task BL-010 that next offers first; a CI fix BL-012 whose
        # touches overlap BL-008 in Doing; an older CI fix BL-011 that waits on BL-008; and
        # BL-013, a CI fix this lane already ran.
        Set-Content -Path (Join-Path $repo 'Tasks\Backlog\BL-010-feature.md') -Encoding UTF8 -Value (((& $taskText 'Add a feature' '[Curl.Cli.UnitLibrary/New.cs]') -replace 'BL-001', 'BL-010') -replace 'priority: Normal', 'priority: High')
        Set-Content -Path (Join-Path $repo 'Tasks\Backlog\BL-011-fix-ci-failure-waits.md') -Encoding UTF8 -Value ((((& $taskText 'Fix CI failure Waits_OnLinux on Linux' '[Curl.Cli.UnitTests/W.cs]' 'feature') -replace 'BL-001', 'BL-011') -replace 'depends-on: \[\]', 'depends-on: [BL-008]') -replace 'priority: Normal', 'priority: High')
        Set-Content -Path (Join-Path $repo 'Tasks\Backlog\BL-012-fix-ci-failure-overlaps.md') -Encoding UTF8 -Value (((& $taskText 'Fix CI failure Overlaps_OnMac on macOS' '[Curl.Cli.UnitLibrary/Qux.cs]' 'feature') -replace 'BL-001', 'BL-012') -replace 'priority: Normal', 'priority: High')
        Set-Content -Path (Join-Path $repo 'Tasks\Backlog\BL-013-fix-flaky-ci-test-ran.md') -Encoding UTF8 -Value (((& $taskText 'Fix flaky CI test Ran_Once that failed once on Linux' '[Curl.Cli.UnitTests/R.cs]' 'feature') -replace 'BL-001', 'BL-013') -replace 'priority: Normal', 'priority: High')
        git -C $repo add -A 2>&1 | Out-Null
        git -C $repo commit -q -m 'a feature and three CI fixes' 2>&1 | Out-Null
        git -C $repo push -q origin HEAD:work 2>&1 | Out-Null
        $nextFirst = Get-NextTaskId ((Invoke-Board @('next', '-Skip', 'BL-013')) -join "`n")
        $ciFirst = Invoke-Claim -Skip @('BL-013')
        $ciSkip = (Get-ClaimSkip -Attempted @('BL-002') -ShiftRequeued @('BL-010', 'BL-013', 'BL-002')) -join ','
        $traced = @(Get-Content $TraceFile -ErrorAction SilentlyContinue | Where-Object { $_ -match 'BL-001 unready' })
        $idleTraced = @(Get-Content $TraceFile -ErrorAction SilentlyContinue | Where-Object { $_ -match ' idle +nothing a lane can claim' })
        $cases = @(
            ,@('a claim passes over an unready task held back by Doing and takes the next', 'BL-002', "$($first.Id)")
            ,@('with only tasks this lane ran left besides the unready one, the lane ends', 'none', $(if ($ranAlready.Wait) { 'wait' } elseif ($ranAlready.None) { 'none' } else { "claimed $($ranAlready.Id)" }))
            ,@('the end says which task this lane already ran', 'True', "$($ranAlready.Why -match 'already ran this shift \(BL-003\)')")
            ,@('a fresh claim takes the task behind the unready one', 'BL-003', "$($third.Id)")
            ,@('the unready task stays in Backlog', 'Backlog', $unreadyState)
            ,@('with only unready tasks ready the lane waits, not ends', 'wait', $(if ($second.Wait) { 'wait' } elseif ($second.None) { 'none' } else { "claimed $($second.Id)" }))
            ,@('the wait says why', 'True', "$($second.Why -match 'only unready tasks are ready \(BL-001\)')")
            ,@('the unready task is traced once a shift, not at every claim', '1', "$($traced.Count)")
            ,@('unready.txt lists it once with its reason', '1 touches', "$(@(Get-ShiftUnready).Count) $(if ((Get-ShiftUnready) -match 'touches are empty') { 'touches' })")
            ,@('with only interactive-only work left, in Backlog and in Doing, the lane ends rather than waits (AF-0435)', 'none', $(if ($interactiveOnly.Wait) { "wait: $($interactiveOnly.Why)" } elseif ($interactiveOnly.None) { 'none' } else { "claimed $($interactiveOnly.Id)" }))
            ,@('the end names the interactive tasks in Doing and in Backlog, and is traced once as idle, not wait', 'True True 1', "$($interactiveOnly.Why -match 'in Doing with an interactive session: BL-006, BL-007') $($interactiveOnly.Why -match 'Backlog is interactive only, waiting on Stewart or an interactive session: BL-005') $($idleTraced.Count)")
            ,@('a lane-eligible task in Doing beside them keeps the lane waiting', 'wait No task is ready.', $(if ($laneRunning.Wait) { "wait $($laneRunning.Why)" } elseif ($laneRunning.None) { 'none' } else { "claimed $($laneRunning.Id)" }))
            ,@('the end notice is empty when no interactive work is named', "''", "'$(Get-NothingReadyEndNotice)'")
            ,@('next would offer the High feature task first', 'BL-010', "$nextFirst")
            ,@('a claim takes the ready CI fix first, over its overlap with Doing, past one waiting on Doing and one this lane ran (ADR-1158)', 'BL-012 Doing', "$($ciFirst.Id) $(Get-TaskState 'BL-012')")
            ,@('the shift requeued list keeps a lane off a requeued task but not a requeued CI fix (ADR-1158)', 'BL-002,BL-010', $ciSkip)
            ,@('a task is interactive only when it says lane: no or touches an audit path or guard file, and not otherwise', 'lane-no guard-file guard-folder audit-folder', $(
                $kinds = [ordered]@{ 'lane-no' = @('touches: [x]', 'lane: no'); 'guard-file' = @('touches: [Curl.Cli.UnitLibrary, .claude/settings.json]'); 'guard-folder' = @('touches: [.claude]')
                    'audit-folder' = @('touches: ["Audit\Tools"]'); 'lane-task' = @('touches: [DarkFactory/Claim.ps1, .claude/hooks/whisper-milestone.ps1]'); 'no-touches' = @() }
                @(foreach ($kind in $kinds.Keys) {
                    $path = Join-Path $temp "BL-500-$kind.md"
                    Set-Content -Path $path -Encoding UTF8 -Value (@('---', 'id: BL-500', 'title: t', 'depends-on: []') + $kinds[$kind] + @('requirement: none', '---'))
                    if (Test-InteractiveOnlyTask $path) { $kind }
                }) -join ' '))
        )
        foreach ($case in $cases) {
            if ($case[1] -ceq $case[2]) { Write-Host "PASS $($case[0]): $($case[2])" -ForegroundColor Green }
            else { Write-Host "FAIL $($case[0]): expected $($case[1]), got $($case[2])" -ForegroundColor Red; $failed++ }
        }
    } finally {
        Remove-Item -Recurse -Force -Path $temp -ErrorAction SilentlyContinue
    }
    exit $(if ($failed) { 1 } else { 0 })
}

# This file ran to its end; RunDarkFactory.ps1 goes on to the next one.
$AreaFileFinished = $true
