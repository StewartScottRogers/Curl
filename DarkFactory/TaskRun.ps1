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

function Get-LaneContextArguments {
    # The flags that keep a headless run's fixed context small (BL-3001): every call
    # re-reads it, and 227 lane runs measured it at 55K tokens. --setting-sources
    # project,local leaves out ~\.claude\settings.json - its hooks (the SessionStart ones
    # too), its enabled plugins with their skills, agents and hooks, and its permission
    # rules - while the project's .claude\settings.json still loads, so the PreToolUse
    # audit-path guard and the whisper hook still run. --strict-mcp-config with an empty
    # --mcp-config loads no MCP server, and disableClaudeAiConnectors no claude.ai one.
    # claudeMdExcludes drops only this checkout's root CLAUDE.md, which LaneRules.md
    # replaces; a project folder's CLAUDE.md still loads when the run works there.
    # --exclude-dynamic-system-prompt-sections moves the checkout's path and git status
    # out of the system prompt, so every lane's system prompt is the same and cached once.
    # The two small files are written to the given folder, named after the checkout.
    param([string]$CheckoutRoot, [string]$Folder)
    $leaf = Split-Path $CheckoutRoot -Leaf
    $mcp = Join-Path $Folder "claude-$leaf-mcp.json"
    $settings = Join-Path $Folder "claude-$leaf-settings.json"
    $rootClaudeMd = (Join-Path $CheckoutRoot 'CLAUDE.md').Replace('\', '/')
    Set-Content -Path $mcp -Value '{"mcpServers":{}}' -Encoding ASCII
    $json = [pscustomobject][ordered]@{ claudeMdExcludes = @($rootClaudeMd, "**/$leaf/CLAUDE.md"); disableClaudeAiConnectors = $true } | ConvertTo-Json -Compress
    Set-Content -Path $settings -Value $json -Encoding ASCII
    $rules = Join-Path $PSScriptRoot 'LaneRules.md'
    return "--setting-sources project,local --strict-mcp-config --mcp-config `"$mcp`" --settings `"$settings`" --append-system-prompt-file `"$rules`" --exclude-dynamic-system-prompt-sections"
}

function Get-TurnCapArgument {
    # claude's --max-turns for -MaxRunTurns; 0 means no turn cap.
    param([int]$MaxTurns)
    if ($MaxTurns -gt 0) { return " --max-turns $MaxTurns" }
    return ''
}

function Get-TurnCapNote {
    # The prompt's {TURNCAP}: the run is told its turn cap as well as its deadline, so it can
    # hand back what is left before the cap stops it (AF-0482); empty with no turn cap.
    param([int]$MaxTurns)
    if ($MaxTurns -le 0) { return '' }
    $handBack = [Math]::Max(1, $MaxTurns - 15)
    return "`r`n   The run is also stopped after $MaxTurns turns (each reply that calls tools is one), and" +
        "`r`n   its task is then split as too big for one run. By turn $handBack, if the task is not" +
        "`r`n   finished, hand back what is left the same way (AF-0482: BL-3563's third run had its" +
        "`r`n   fix passing when the cap stopped it mid quality run, and a split and a fourth claim" +
        "`r`n   followed)."
}

function Test-TurnCapReached {
    # Whether the last run stopped because it reached the -MaxRunTurns turn cap: the task
    # is too big for one run and wants splitting (BL-3001).
    $r = $script:RunResult
    return ($null -ne $r -and "$($r.subtype)" -eq 'error_max_turns')
}

function Open-RunLog {
    # The run's .jsonl log, held open for the whole run and shared with readers. Written line
    # by line with Add-Content, a write that met the file open elsewhere was dropped with only
    # a console error, and BL-3895's log lost its last two minutes and its result (AF-0486).
    # Held open, no later opener can deny the writes. $null when it cannot be opened: each
    # line then falls back to Add-Content.
    param([string]$Path)
    try {
        $stream = [System.IO.FileStream]::new($Path, [System.IO.FileMode]::Append, [System.IO.FileAccess]::Write, [System.IO.FileShare]'ReadWrite, Delete')
        $writer = [System.IO.StreamWriter]::new($stream, [System.Text.UTF8Encoding]::new($false))
        $writer.AutoFlush = $true
        return $writer
    } catch { return $null }
}

function Write-RunLogLine {
    # One line of the run log, through the open -Writer, else appended. A line that still
    # cannot be written is traced, once a run, instead of being lost silently (AF-0486).
    param($Writer, [string]$Path, [string]$Line, [string]$Id)
    try {
        if ($Writer) { $Writer.WriteLine($Line) } else { Add-Content -Path $Path -Value $Line -Encoding UTF8 -ErrorAction Stop }
    } catch {
        if (-not $script:RunLogWriteFailed) { Write-Trace $Id 'trace' "run log $(Split-Path $Path -Leaf) missed a line: $(Get-Short $_.Exception.Message)" 'DarkYellow' }
        $script:RunLogWriteFailed = $true
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
    $Text = $Text.Replace('{TURNCAP}', (Get-TurnCapNote $MaxRunTurns))
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
    # The turn cap (BL-3001): a run past -MaxRunTurns stops, and its task is too big for one run.
    $turns = Get-TurnCapArgument $MaxRunTurns
    # The task's model (BL-1705); the resolver's, overtime and resumed runs keep it. The
    # log's first line names it, so cost per model can be read back (Get-ModelCostSummary).
    if ($script:ModelChoice.Id -ne $Id) { Set-ModelChoice $Id }
    $runModel = $script:ModelChoice.Model
    $isNewLog = -not (Test-Path $raw)
    $script:RunLogWriteFailed = $false
    $log = Open-RunLog $raw
    try {
        if ($isNewLog) { Write-RunLogLine $log $raw ([pscustomobject][ordered]@{ type = 'factory'; model = $runModel; why = $script:ModelChoice.Why } | ConvertTo-Json -Compress) $Id }
        $context = Get-LaneContextArguments $Root $LogDir
        $psi = New-ClaudeRunStartInfo "claude -p --model $runModel --dangerously-skip-permissions --output-format stream-json --verbose $context$budget$turns --disallowedTools $denied 2>`"$err`""
        # No claude.ai connectors and no auto memory either: the operator's memory is not a lane's.
        $psi.EnvironmentVariables['ENABLE_CLAUDEAI_MCP_SERVERS'] = 'false'
        $psi.EnvironmentVariables['CLAUDE_CODE_DISABLE_AUTO_MEMORY'] = '1'
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
                Write-RunLogLine $log $raw $line $Id
                try { $evt = $line | ConvertFrom-Json } catch { $evt = $null }
                # Tracing a run's event never ends the run or the lane (BL-2036).
                if ($evt) { try { Write-Event $Id $evt } catch { Write-Trace $Id 'trace' "could not trace an event: $(Get-Short $_.Exception.Message)" 'DarkYellow' } }
                $pending = $p.StandardOutput.ReadLineAsync()
            } elseif ((Get-Date) -gt $deadline) {
                Stop-FactoryProcessTree -ProcessId $p.Id
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
            Write-RunLogLine $log $raw ([pscustomobject][ordered]@{ type = 'result'; subtype = 'factory_no_result'; is_error = $true; exit_code = $p.ExitCode; timed_out = $timedOut; result = $why; total_cost_usd = 0 } | ConvertTo-Json -Compress) $Id
        }
        return @{ ExitCode = $p.ExitCode; TimedOut = $timedOut }
    } finally { if ($log) { $log.Dispose() } }
}

if ($TestLaneFlags) {
    $failed = 0
    $check = { param($Name, $Expected, $Got)
        if ($Expected -ceq $Got) { Write-Host "PASS ${Name}: $Got" -ForegroundColor Green }
        else { Write-Host "FAIL ${Name}: expected $Expected, got $Got" -ForegroundColor Red; $script:failed++ } }
    $folder = Join-Path ([System.IO.Path]::GetTempPath()) "lane-flags-$PID"
    New-Item -ItemType Directory -Force $folder | Out-Null
    try {
        $context = Get-LaneContextArguments 'Z:\repos\Curl.lanes\lane-3' $folder
        foreach ($flag in '--setting-sources project,local', '--strict-mcp-config', '--mcp-config', '--settings', '--append-system-prompt-file', '--exclude-dynamic-system-prompt-sections') {
            & $check "context has $flag" 'True' "$($context.Contains($flag))"
        }
        & $check 'the lane rules file exists' 'True' "$(Test-Path (Join-Path $PSScriptRoot 'LaneRules.md'))"
        & $check 'the MCP config names no server' '0' "$(@((Get-Content (Join-Path $folder 'claude-lane-3-mcp.json') -Raw | ConvertFrom-Json).mcpServers.PSObject.Properties).Count)"
        $settings = Get-Content (Join-Path $folder 'claude-lane-3-settings.json') -Raw | ConvertFrom-Json
        & $check 'the root CLAUDE.md is excluded' 'Z:/repos/Curl.lanes/lane-3/CLAUDE.md|**/lane-3/CLAUDE.md' ($settings.claudeMdExcludes -join '|')
        & $check 'claude.ai connectors are off' 'True' "$($settings.disableClaudeAiConnectors)"
        & $check 'turn cap 80' ' --max-turns 80' (Get-TurnCapArgument 80)
        & $check '-MaxRunTurns 0 is no turn cap' '' (Get-TurnCapArgument 0)
        & $check 'the prompt names the turn cap' 'True' "$((Get-TurnCapNote 80).Contains('stopped after 80 turns') -and (Get-TurnCapNote 80).Contains('By turn 65'))"
        & $check '-MaxRunTurns 0 adds no turn-cap note' '' (Get-TurnCapNote 0)
        & $check 'both task prompts carry the turn-cap note' 'True' "$($Prompt.Contains('(AF-0094).{TURNCAP}') -and $LanePrompt.Contains('(AF-0094).{TURNCAP}'))"
        & $check 'the run fills in the turn-cap note' 'True' "$((Get-Command Invoke-TaskRun).Definition.Contains('{TURNCAP}'))"
        $launch = (Get-Command Invoke-TaskRun).Definition
        & $check 'the run is launched with the context, cost cap and turn cap' 'True' "$($launch.Contains('--verbose $context$budget$turns --disallowedTools'))"
        $script:RunResult = [pscustomobject]@{ type = 'result'; subtype = 'error_max_turns'; is_error = $true }
        & $check 'a run stopped at the turn cap is seen' 'True' "$(Test-TurnCapReached)"
        $script:RunResult = [pscustomobject]@{ type = 'result'; subtype = 'error_max_budget_usd'; is_error = $true }
        & $check 'a run stopped at the cost cap is not a turn-cap stop' 'False' "$(Test-TurnCapReached)"
        $script:RunResult = $null
        & $check 'a run with no result is not a turn-cap stop' 'False' "$(Test-TurnCapReached)"
        # AF-0486: the run log is held open, so a reader that would deny writes cannot open it,
        # every line lands, and readers sharing the file still read it while the run writes.
        $runLog = Join-Path $folder 'BL-1-20261030-080000-L1.jsonl'
        $writer = Open-RunLog $runLog
        & $check 'the run log opens' 'True' "$($null -ne $writer)"
        Write-RunLogLine $writer $runLog '{"type":"factory","model":"opus"}' 'BL-1'
        $denied = try { [System.IO.File]::Open($runLog, 'Open', 'Read', 'Read').Dispose(); 'opened' } catch { 'refused' }
        & $check 'a reader that denies writes cannot open the run log' 'refused' $denied
        Write-RunLogLine $writer $runLog '{"type":"result","total_cost_usd":1.5}' 'BL-1'
        & $check 'a sharing reader reads every line while the run writes' '2' "$(@(Get-Content $runLog).Count)"
        $writer.Dispose()
        & $check 'the run log has no byte-order mark' '123' "$([System.IO.File]::ReadAllBytes($runLog)[0])"
        $script:traced = @()
        function Write-Trace { param($Id, $Kind, $Text) $script:traced += "$Kind $Text" }
        $script:RunLogWriteFailed = $false
        $holder = [System.IO.File]::Open($runLog, 'Open', 'ReadWrite', 'None')
        try {
            Write-RunLogLine $null $runLog 'lost one' 'BL-1'
            Write-RunLogLine $null $runLog 'lost two' 'BL-1'
        } finally { $holder.Dispose() }
        & $check 'a line that cannot be written is traced once' '1' "$(@($script:traced | Where-Object { $_ -like 'trace run log*missed a line*' }).Count)"
        & $check 'the run notes a missed line' 'True' "$script:RunLogWriteFailed"
    } finally { Remove-Item -Recurse -Force $folder -ErrorAction SilentlyContinue }
    exit $(if ($failed) { 1 } else { 0 })
}

# This file ran to its end; RunDarkFactory.ps1 goes on to the next one.
$AreaFileFinished = $true
