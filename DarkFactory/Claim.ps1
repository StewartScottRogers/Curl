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

function Enter-Lock {
    # Claims and integrations take turns across every lane. Holding the file open with no
    # sharing is the lock; the operating system releases it if the lane dies.
    New-Item -ItemType Directory -Force -Path $LanesDir | Out-Null
    while ($true) {
        try { return [IO.File]::Open($LockFile, 'OpenOrCreate', 'ReadWrite', 'None') }
        catch { Write-HeartbeatIfDue; Start-Sleep -Seconds 3 }
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

function Get-DoingCount { return @(Get-ChildItem (Join-Path $Root 'Tasks\Doing') -Filter 'BL-*.md' -ErrorAction SilentlyContinue).Count }

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
    return (Invoke-Git @('push', '-q', 'origin', "HEAD:$Branch"))
}

function Invoke-Claim {
    # Returns @{ Id = 'BL-###' } on success, or @{ Wait = $true } when every ready task
    # overlaps work in progress (or nothing is ready but other lanes may unlock more), or
    # @{ None = $true } when the board has nothing left for this shift.
    param([string[]]$Skip, [switch]$CiFixOnly)
    $lock = Enter-Lock
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
            $skipNow = @($Skip)
            do {
                $boardArgs = @('next')
                if ($skipNow.Count) { $boardArgs += @('-Skip', ($skipNow -join ',')) }
                $next = (Invoke-Board $boardArgs) -join "`n"
                $id = Get-NextTaskId $next
                $overCap = [bool]($id -and (Get-TaskCapSpentWhy $id))
                if ($overCap) { Write-Trace $id 'cost cap' 'its runs of the last day already spent its cost cap; not claimed' 'Yellow'; $skipNow += $id }
            } while ($overCap)
            if (-not $id) {
                if ($next -match 'can start yet' -or (Get-DoingCount) -gt 0) { return @{ Wait = $true; Why = (Get-Short (Get-WaitReason $next) 80) } }
                return @{ None = $true }
            }
            # With the usage stop on, only a CI fix is claimed (AF-0308); any other task waits.
            if ($CiFixOnly -and -not (Test-CiFixTitle (Get-TaskTitle $id))) { return @{ None = $true } }
            $moved = (Invoke-Board @('move', '-Id', $id, '-To', 'Doing')) -join ' '
            if ((Get-TaskState $id) -ne 'Doing') {
                # The board refuses an ID that names two live tasks, and no claim can win
                # until they are renumbered (BL-2014); any other refusal is reported as it is.
                if ($moved -match 'names \d+ tasks' -and (Invoke-RenumberDuplicates $id)) { continue }
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
    } finally { $lock.Dispose() }
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
        param($Slug)
        Set-Content -Path (Join-Path $repo "Tasks\Backlog\BL-001-$Slug.md") -Encoding UTF8 -Value @(
            '---', 'id: BL-001', "title: $Slug", 'priority: Normal', 'assignee: Claude', 'pipeline: direct',
            'depends-on: []', "touches: [$Slug]", 'requirement: none', 'created: 2026-10-01', 'completed:', '---',
            "# BL-001 - $Slug", '', '## Goal', '', '## Context', '', '## Acceptance criteria', '', '- [ ] Claimed.', '', '## Notes', '', '## Log', '', '- 2026-10-01: Created.')
        git -C $repo add -A 2>&1 | Out-Null
        git -C $repo commit -q -m "file $Slug" 2>&1 | Out-Null
    }
    & $file 'first'
    & $file 'second'
    git -C $repo push -q origin work 2>&1 | Out-Null
    $script:Root = $repo; $script:Branch = 'work'; $script:Lane = 9; $script:Stamp = 'test'
    $script:LanesDir = $temp; $script:LockFile = Join-Path $temp 'integrate.lock'; $script:LogDir = Join-Path $temp 'logs'
    $env:CLAUDE_PROJECT_DIR = $repo
    $failed = 0
    try {
        $claim = Invoke-Claim -Skip @()
        git -C $repo fetch -q origin work 2>&1 | Out-Null
        $shared = @(git -C $repo ls-tree -r --name-only origin/work Tasks) | Where-Object { $_ -match 'BL-\d+-' }
        $ids = @($shared | ForEach-Object { if ($_ -match '(BL-\d+)-') { $Matches[1] } })
        $cases = @(
            ,@('a duplicate ID is renumbered and the claim goes on', 'claimed', $(if ($claim.Id) { 'claimed' } else { "waited: $($claim.Why)" }))
            ,@('no ID is left naming two tasks on the shared branch', '2 tasks, 2 IDs', "$($ids.Count) tasks, $(@($ids | Select-Object -Unique).Count) IDs")
            ,@('the first task to be filed keeps its ID', 'True', "$(@($shared | Where-Object { $_ -match 'BL-001-first' }).Count -eq 1)")
            ,@('the claimed task is in Doing on the shared branch', 'True', "$(@($shared | Where-Object { $_ -match 'Tasks/Doing/' }).Count -eq 1)")
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
