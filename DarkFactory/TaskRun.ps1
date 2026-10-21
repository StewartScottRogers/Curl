# One headless Claude run: its result and outcome, the trace of its events, and
# Invoke-TaskRun. Dot-sourced by RunDarkFactory.ps1 in its order; see the loader there.

function Get-ResultText {
    param($Content)
    if ($Content -is [string]) { return $Content }
    return (@($Content) | ForEach-Object { if ($_.PSObject.Properties['text']) { $_.text } }) -join "`n"
}

function Get-Outcome {
    param([string]$Verb, [string]$Text, [bool]$IsError)
    if ($Verb -eq 'test') {
        $t = [regex]::Matches($Text, 'total:\s*(\d+),\s*failed:\s*(\d+)', 'IgnoreCase')
        if ($t.Count -eq 0) { $t = [regex]::Matches($Text, 'Failed:\s*(\d+),\s*Passed:\s*(\d+)') ; $swap = $true } else { $swap = $false }
        if ($t.Count -gt 0) {
            $total = 0; $failed = 0
            foreach ($m in $t) {
                if ($swap) { $failed += [int]$m.Groups[1].Value; $total += [int]$m.Groups[1].Value + [int]$m.Groups[2].Value }
                else { $total += [int]$m.Groups[1].Value; $failed += [int]$m.Groups[2].Value }
            }
            return "$($total - $failed)/$total pass"
        }
    }
    if ($Verb -eq 'commit' -and $Text -match '\[[^\]]*\s([0-9a-f]{7,})\]\s*(.*)') { return "$($Matches[1]) $($Matches[2])" }
    if ($Verb -eq 'build' -and $Text -match '(\d+)\s+Error\(s\)' -and [int]$Matches[1] -gt 0) { return "FAIL $($Matches[1]) error(s)" }
    if ($IsError) { return 'FAIL ' + (Get-Short (($Text -split "`n" | Where-Object { $_.Trim() } | Select-Object -Last 1)) 50) }
    return 'ok'
}

function Write-Event {
    param([string]$Id, $Evt)
    switch ($Evt.type) {
        'assistant' {
            foreach ($c in @($Evt.message.content)) {
                if ($c.type -ne 'tool_use') { continue }
                $label = Get-ToolLabel $c
                if (-not $label) { continue }
                $script:ToolLabels[$c.id] = $label
                Set-HeartbeatStep $label
                if ($label[0] -in 'agent', 'skill', 'edit', 'write') { Write-Trace $Id $label[0] (Get-Short $label[1]) }
            }
        }
        'user' {
            foreach ($c in @($Evt.message.content)) {
                if ($c.type -ne 'tool_result' -or -not $script:ToolLabels.ContainsKey($c.tool_use_id)) { continue }
                $verb, $detail = $script:ToolLabels[$c.tool_use_id]
                if ($verb -in 'agent', 'skill', 'edit', 'write') { continue }
                $isErr = [bool]($c.PSObject.Properties['is_error'] -and $c.is_error)
                $outcome = Get-Outcome $verb (Get-ResultText $c.content) $isErr
                $color = if ($outcome -like 'FAIL*') { 'Red' } else { 'Gray' }
                if ($verb -eq 'move') { $outcome = "$detail $outcome" }
                Write-Trace $Id $verb $outcome $color
            }
        }
        'rate_limit_event' {
            $info = $Evt.rate_limit_info
            if ($info.status -eq 'rejected' -and $info.resetsAt) { $script:LimitResetAt = ConvertFrom-Unix ([long]$info.resetsAt) }
        }
        'result' {
            $mins = [math]::Round($Evt.duration_ms / 60000, 1)
            $script:RunResult = $Evt
            Write-Trace $Id 'end' "$($Evt.num_turns) turns, $mins min$(Format-ModelChoice)"
        }
    }
}

function Invoke-TaskRun {
    # One headless Claude run in this checkout. By default it is the task run; a lane
    # passes its own prompt and deny list, and a log suffix for the resolver's run.
    # -Resume is the task run again after the usage limit cut the last one off.
    # -Note goes in front of the prompt, e.g. Restore-TaskStash's word on an earlier run's work.
    param([string]$Id, [string]$Text = '', [string[]]$Deny = $null, [string]$Suffix = '', [int]$Minutes = 0, [switch]$Resume, [switch]$Overtime, [string]$Note = '')
    if (-not $Text) { $Text = if ($Lane) { $LanePrompt } else { $Prompt } }
    $Text = $Note + $Text
    if ($Overtime) { $Text = $OvertimeNote + $Text; $Suffix += '-overtime' }
    elseif ($Resume) { $Text = $ResumeNote + $Text; $Suffix += '-resumed' }
    if ($null -eq $Deny) { $Deny = if ($Lane) { $LaneForbidden } else { $Forbidden } }
    if ($Minutes -le 0) { $Minutes = $TaskMinutes }
    # The run is told when it will be killed, so it can hand the task back before then (AF-0034).
    $deadline = (Get-Date).AddMinutes($Minutes)
    $Text = $Text.Replace('{ID}', $Id).Replace('{LANE}', "$Lane").Replace('{BRANCH}', $Branch)
    $Text = $Text.Replace('{DEADLINE}', $deadline.ToString('HH:mm')).Replace('{MINUTES}', "$Minutes")
    $raw = Join-Path $LogDir "$Id-$Stamp$LaneTag$Suffix.jsonl"
    $err = Join-Path $LogDir "$Id-$Stamp$LaneTag$Suffix.err.txt"
    $script:RunResult = $null
    $script:LimitResetAt = $null
    $script:ToolLabels = @{}

    $denied = ($Deny | ForEach-Object { "`"Bash($_`:*)`" `"PowerShell($_`:*)`"" }) -join ' '
    # New-ClaudeRunStartInfo gives Bash calls room for a build and the fast tests while six
    # lanes build at once: a tool call past its timeout is moved to the background, and a
    # headless run that then ends its reply to wait for it exits with the task still in
    # Doing (BL-855). It inherits CURL_DARK_FACTORY_LANE.
    # The cost cap (AF-0004, AF-0033): the run stops once it has cost 4 times the median
    # recent run, at most -TaskBudgetUsd, less what the task's runs of the last day cost
    # (AF-0095). Read before this run's log exists, so it never counts itself.
    Close-UnfinishedRunLogs $LogDir $Id $raw
    $script:RunBudgetUsd = Get-RunBudgetUsd $TaskBudgetUsd @(Get-RecentRunCosts $LogDir) (Get-TaskSpentUsd $LogDir $Id)
    $budget = if ($script:RunBudgetUsd -gt 0) { ' --max-budget-usd ' + $script:RunBudgetUsd.ToString([System.Globalization.CultureInfo]::InvariantCulture) } else { '' }
    # The task's model (BL-1705); the resolver's, overtime and resumed runs keep it. The
    # log's first line names it, so cost per model can be read back (Get-ModelCostSummary).
    if ($script:ModelChoice.Id -ne $Id) { Set-ModelChoice $Id }
    $runModel = $script:ModelChoice.Model
    if (-not (Test-Path $raw)) { Add-Content -Path $raw -Value ([pscustomobject][ordered]@{ type = 'factory'; model = $runModel; why = $script:ModelChoice.Why } | ConvertTo-Json -Compress) -Encoding UTF8 }
    $psi = New-ClaudeRunStartInfo "/d /c claude -p --model $runModel --dangerously-skip-permissions --output-format stream-json --verbose$budget --disallowedTools $denied 2>`"$err`""
    $p = [System.Diagnostics.Process]::Start($psi)
    $p.StandardInput.Write($Text)
    $p.StandardInput.Close()

    $timedOut = $false
    $pending = $p.StandardOutput.ReadLineAsync()
    while ($true) {
        Write-HeartbeatIfDue
        if ($pending.Wait(1000)) {
            $line = $pending.Result
            if ($null -eq $line) { break }
            Add-Content -Path $raw -Value $line -Encoding UTF8
            try { $evt = $line | ConvertFrom-Json } catch { $evt = $null }
            # Tracing a run's event never ends the run or the lane (BL-2036).
            if ($evt) { try { Write-Event $Id $evt } catch { Write-Trace $Id 'trace' "could not trace an event: $(Get-Short $_.Exception.Message)" 'DarkYellow' } }
            $pending = $p.StandardOutput.ReadLineAsync()
        } elseif ((Get-Date) -gt $deadline) {
            & taskkill /T /F /PID $p.Id 2>&1 | Out-Null
            $timedOut = $true
            # A killed run sends no result event, so its end is traced here: without it the
            # lane log's last line for the run is whatever step it was in (AF-0034).
            Write-Trace $Id 'end' "killed: timed out after $Minutes min$(Format-ModelChoice)" 'Red'
            break
        }
    }
    $p.WaitForExit()
    # A run that ended with no result event (killed, crashed, stream cut) gets one written
    # for it, so every run log ends with a result and the audit's unfinishedRuns stays empty
    # for runs the factory itself saw end (AF-0151). RunResult stays null: the retry logic
    # still sees "no result". total_cost_usd 0 keeps the cost readers' sums right.
    if ($null -eq $script:RunResult) {
        $why = if ($timedOut) { "killed: timed out after $Minutes min" } else { "ended with no result event, exit $($p.ExitCode)" }
        Add-Content -Path $raw -Value ([pscustomobject][ordered]@{ type = 'result'; subtype = 'factory_no_result'; is_error = $true; exit_code = $p.ExitCode; timed_out = $timedOut; result = $why; total_cost_usd = 0 } | ConvertTo-Json -Compress) -Encoding UTF8
    }
    return @{ ExitCode = $p.ExitCode; TimedOut = $timedOut }
}

# This file ran to its end; RunDarkFactory.ps1 goes on to the next one.
$AreaFileFinished = $true
