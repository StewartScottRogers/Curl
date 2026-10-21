# What waits on Stewart, open audit tasks, and putting tasks back in Backlog once what they
# waited on is Done. Dot-sourced by RunDarkFactory.ps1 in its order; see the loader there.

function Get-LastLogLine {
    param([string]$Id)
    $file = Get-ChildItem (Join-Path $Root 'Tasks') -Recurse -Filter "$Id-*.md" | Select-Object -First 1
    if (-not $file) { return '' }
    $log = Get-Content $file.FullName | Where-Object { $_ -match '^\s*-\s' } | Select-Object -Last 1
    return ("$log" -replace '^\s*-\s*', '')
}

function Get-WaitingOnStewart {
    $reasons = @()
    foreach ($f in Get-ChildItem (Join-Path $Root 'Tasks\Blocked') -Filter 'BL-*.md' -ErrorAction SilentlyContinue) {
        $id = Get-TaskIdFromFileName $f.Name
        $reasons += "$id BLOCKED  $(Get-LastLogLine $id)"
    }
    foreach ($line in Invoke-Board @('status')) {
        $id = Get-NeedsStewartId $line
        if ($id) { $reasons += "$id DECIDE   $(Get-TaskTitle $id)" }
    }
    $reasons += @(Get-OpenAuditTaskLines)
    return $reasons
}

function Get-OpenAuditTaskLines {
    # One line per open audit-guard task (BL-999): the ones the CI watch filed this shift, and
    # any still open in Backlog or Doing of this checkout. Each needs an interactive session.
    $open = [ordered]@{}
    if ($script:CiWatch -and $script:CiWatch.AuditTasks) { foreach ($id in $script:CiWatch.AuditTasks.Keys) { $open[$id] = $script:CiWatch.AuditTasks[$id] } }
    foreach ($state in 'Backlog', 'Doing') {
        foreach ($f in Get-ChildItem (Join-Path $Root "Tasks\$state") -Filter 'BL-*.md' -ErrorAction SilentlyContinue) {
            $title = "$(Select-String -LiteralPath $f.FullName -Pattern '^title:\s*(.*)$' | Select-Object -First 1 | ForEach-Object { $_.Matches[0].Groups[1].Value })"
            if ($title -like "Revert the dark factory's change to *") { $open[(Get-TaskIdFromFileName $f.Name)] = $title }
        }
    }
    return @($open.Keys | ForEach-Object { "$_ AUDIT    needs an interactive session: $($open[$_])" })
}

function Test-TaskDone {
    param([string]$Id)
    return [bool](Get-ChildItem (Join-Path $Root 'Tasks\Done') -Recurse -Filter "$Id-*.md" -ErrorAction SilentlyContinue)
}

function Invoke-Requeue {
    # Moves back to Backlog every Blocked task whose blocker was only other tasks that are
    # now all Done: a last Log line that names BL-### IDs and is not a question for
    # Stewart. Also every task the cost cap's daily rule blocked whose 24-hour spending
    # window has since passed (BL-2117, Test-CapBlockRequeueDue). Returns the IDs it moved.
    $moved = @()
    $cap = $null
    foreach ($f in Get-ChildItem (Join-Path $Root 'Tasks\Blocked') -Filter 'BL-*.md' -ErrorAction SilentlyContinue) {
        $id = Get-TaskIdFromFileName $f.Name
        $reason = Get-LastLogLine $id
        if ($reason -match 'so it was not run again') {
            if ($null -eq $cap) { $cap = Get-RunBudgetUsd $TaskBudgetUsd @(Get-RecentRunCosts $LogDir) }
            if (-not (Test-CapBlockRequeueDue $reason $cap (Get-TaskSpentUsd $LogDir $id))) { continue }
            Invoke-Board @('move', '-Id', $id, '-To', 'Backlog', '-Reason', 'Cost cap window passed: its runs of the last 24 hours now leave 1 US dollar or more of its cost cap, so the cost cap allows a fresh run') | Out-Null
            if ((Get-TaskState $id) -eq 'Backlog') { $moved += $id; Write-Trace $id 'requeue' 'cost cap window passed' 'Cyan' }
            continue
        }
        if ($reason -match 'Stewart') { continue }
        $waits = @(Get-TaskIdsNamed $reason | Where-Object { $_ -ne $id })
        if (-not $waits.Count -or @($waits | Where-Object { -not (Test-TaskDone $_) }).Count) { continue }
        Invoke-Board @('move', '-Id', $id, '-To', 'Backlog', '-Reason', "Unblocked: $($waits -join ', ') now Done") | Out-Null
        if ((Get-TaskState $id) -eq 'Backlog') { $moved += $id; Write-Trace $id 'requeue' "unblocked: $($waits -join ', ') Done" 'Cyan' }
    }
    return $moved
}

# This file ran to its end; RunDarkFactory.ps1 goes on to the next one.
$AreaFileFinished = $true
