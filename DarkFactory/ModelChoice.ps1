# The Claude model per task (BL-1705): the rules, the front-matter override, retries, the
# cost summary per model, and -TestModelChoice. Dot-sourced by RunDarkFactory.ps1 in its
# order; see the loader there.

# ---- the model per task (BL-1705; THE MODEL PER TASK in RunDarkFactory.ps1's help)

$ModelNames = 'haiku', 'sonnet', 'opus'
# The hand-built security and crypto libraries; a task touching one, or its test twin, runs on opus.
$OpusLibraries = 'Curl.Cryptography', 'Curl.Tls', 'Curl.Quic', 'Curl.Kerberos', 'Curl.Ntlm', 'Curl.Protocol.Ssh'
$OpusLibraryPattern = '^(' + (($OpusLibraries | ForEach-Object { [regex]::Escape($_) }) -join '|') + ')(\.(UnitLibrary|UnitTests|IntegrationTests))?([\\/].*)?$'
$script:ModelChoice = @{ Id = ''; Model = ''; Why = '' }

function Get-ModelChoice {
    # The model a task runs on and why: a forced -Model, else the task's own model:, else
    # opus for security, crypto, feature, protocol, retried and CI-fix work and sonnet for
    # the rest. The rule never picks haiku. Warning names an ignored model: value.
    param([string]$Forced, [string]$TaskModel = '', [string]$Pipeline = '', [string]$Priority = '',
        [string[]]$Touches = @(), [string]$Title = '', [switch]$Retried)
    if ($Forced -and $Forced -ne 'auto') { return [pscustomobject]@{ Model = $Forced; Why = '-Model forces it'; Warning = '' } }
    $warning = ''
    if ($TaskModel) {
        if ($TaskModel -in $ModelNames) { return [pscustomobject]@{ Model = $TaskModel.ToLowerInvariant(); Why = 'task model field'; Warning = '' } }
        $warning = "model: $TaskModel is not haiku, sonnet or opus; ignored"
    }
    $library = @($Touches | ForEach-Object { "$_".Trim() } | Where-Object { $_ -match $OpusLibraryPattern }) | Select-Object -First 1
    $why = if ($library) { "touches $library" }
        elseif ($Pipeline -in 'feature', 'protocol') { "$Pipeline pipeline" }
        elseif ($Retried) { 'retry after a run that came back' }
        elseif ($Priority -eq 'High' -and $Title -match '^Fix (CI failure|flaky CI test)') { 'CI failure fix' }
        else { '' }
    if ($why) { return [pscustomobject]@{ Model = 'opus'; Why = $why; Warning = $warning } }
    $why = if ($Pipeline) { "$Pipeline pipeline" } else { 'no pipeline' }
    return [pscustomobject]@{ Model = 'sonnet'; Why = $why; Warning = $warning }
}

function Test-TaskRetried {
    # Whether a task's Log shows an earlier claim that came back, as task-board.ps1 writes
    # it: "Doing -> Backlog" (a requeue, a park, a hand-back) or "Doing -> Blocked" (a
    # failed, stalled or killed run).
    param([string[]]$Lines)
    return [bool](@($Lines | Where-Object { $_ -match '^\s*-\s.*\bDoing -> (Backlog|Blocked)\b' }).Count)
}

function Get-TaskModelChoice {
    # Get-ModelChoice for a task file's lines: its front matter and its Log.
    param([string]$Forced, [string[]]$Lines)
    $fields = @{}
    $dashes = 0
    foreach ($line in $Lines) {
        if ($line -match '^---\s*$') { if (++$dashes -ge 2) { break }; continue }
        if ($dashes -eq 1 -and $line -match '^([A-Za-z-]+):\s*(.*)$') { $fields[$Matches[1]] = $Matches[2].Trim().Trim('"', "'") }
    }
    $touches = @("$($fields['touches'])".Trim('[', ']', ' ') -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ })
    return Get-ModelChoice -Forced $Forced -TaskModel "$($fields['model'])" -Pipeline "$($fields['pipeline'])" -Priority "$($fields['priority'])" `
        -Touches $touches -Title "$($fields['title'])" -Retried:(Test-TaskRetried $Lines)
}

function Set-ModelChoice {
    # Chooses the model for the task about to run and puts it in the heartbeat.
    param([string]$Id)
    $file = Get-ChildItem (Join-Path $Root 'Tasks') -Recurse -Filter "$Id-*.md" | Select-Object -First 1
    $choice = Get-TaskModelChoice -Forced $Model -Lines $(if ($file) { @(Get-Content $file.FullName) } else { @() })
    if ($choice.Warning) { Write-Trace $Id 'model' $choice.Warning 'DarkYellow' }
    $script:ModelChoice = @{ Id = $Id; Model = $choice.Model; Why = $choice.Why }
    $script:Beat.Model = $choice.Model
    $script:Beat.ModelWhy = $choice.Why
}

function Format-ModelChoice {
    # The model and why, for the claim and end trace lines: [model sonnet: docs pipeline].
    if (-not $script:ModelChoice.Model) { return '' }
    return "  [model $($script:ModelChoice.Model): $($script:ModelChoice.Why)]"
}

function Get-ModelCostSummary {
    # Runs, tasks and US dollars per model over the run logs in -Dir whose names match
    # -NamePattern: each log's first line names its model, its result event its cost.
    param([string]$Dir, [string]$NamePattern)
    $byModel = @{}
    foreach ($log in @(Get-ChildItem $Dir -Filter 'BL-*.jsonl' -ErrorAction SilentlyContinue | Where-Object { $_.Name -match $NamePattern })) {
        $head = try { Get-Content $log.FullName -TotalCount 1 | ConvertFrom-Json } catch { $null }
        if (-not $head -or $head.type -ne 'factory' -or -not $head.model) { continue }
        $name = "$($head.model)"
        if (-not $byModel.ContainsKey($name)) { $byModel[$name] = @{ Runs = 0; Tasks = @{}; Usd = 0.0 } }
        $byModel[$name].Runs++
        $byModel[$name].Tasks[(Get-TaskIdFromFileName $log.Name)] = $true
        $line = Get-Content $log.FullName -Tail 5 -ErrorAction SilentlyContinue | Where-Object { $_ -match '"total_cost_usd"' } | Select-Object -Last 1
        $evt = try { $line | ConvertFrom-Json } catch { $null }
        if ($evt -and $evt.type -eq 'result') { $byModel[$name].Usd += [double]$evt.total_cost_usd }
    }
    if (-not $byModel.Count) { return 'models: none' }
    return 'models: ' + ((@($byModel.Keys | Sort-Object) | ForEach-Object {
        "$_ $($byModel[$_].Runs) runs $($byModel[$_].Tasks.Count) tasks `$" + $byModel[$_].Usd.ToString('0.00', [System.Globalization.CultureInfo]::InvariantCulture)
    }) -join '; ')
}

if ($TestModelChoice) {
    function Get-CaseModel {
        param([string]$Forced = 'auto', [string]$TaskModel = '', [string]$Pipeline = 'direct', [string]$Priority = 'Normal', [string[]]$Touches = @('Curl.Cli.UnitLibrary'), [string]$Title = 'Do a thing', [switch]$Retried)
        $c = Get-ModelChoice -Forced $Forced -TaskModel $TaskModel -Pipeline $Pipeline -Priority $Priority -Touches $Touches -Title $Title -Retried:$Retried
        return "$($c.Model)$(if ($c.Warning) { ' (warned)' })"
    }
    function Format-Choice { param($Choice) return "$($Choice.Model): $($Choice.Why)" }
    $cases = @(
        ,@('forced -Model beats the task model field', 'sonnet', (Get-CaseModel -Forced 'sonnet' -TaskModel 'opus' -Pipeline 'feature'))
        ,@('forced haiku is honoured', 'haiku', (Get-CaseModel -Forced 'haiku'))
        ,@('model: haiku wins over the rule', 'haiku', (Get-CaseModel -TaskModel 'haiku' -Pipeline 'feature'))
        ,@('model: sonnet wins over the rule', 'sonnet', (Get-CaseModel -TaskModel 'sonnet' -Touches 'Curl.Tls.UnitLibrary'))
        ,@('model: opus wins over the rule', 'opus', (Get-CaseModel -TaskModel 'Opus' -Pipeline 'docs'))
        ,@('an invalid model: falls back to the rule', 'sonnet (warned)', (Get-CaseModel -TaskModel 'gpt'))
        ,@('an invalid model: on feature work falls back to opus', 'opus (warned)', (Get-CaseModel -TaskModel 'fable' -Pipeline 'feature'))
        ,@('feature pipeline', 'opus', (Get-CaseModel -Pipeline 'feature'))
        ,@('protocol pipeline', 'opus', (Get-CaseModel -Pipeline 'protocol'))
        ,@('a retried task', 'opus', (Get-CaseModel -Retried))
        ,@('a High Fix CI failure task', 'opus', (Get-CaseModel -Priority 'High' -Title 'Fix CI failure Foo_Bar on Linux and macOS'))
        ,@('a High Fix flaky CI test task', 'opus', (Get-CaseModel -Priority 'High' -Title 'Fix flaky CI test Foo_Bar'))
        ,@('a Normal Fix CI failure task', 'sonnet', (Get-CaseModel -Title 'Fix CI failure Foo_Bar'))
        ,@('docs pipeline', 'sonnet', (Get-CaseModel -Pipeline 'docs'))
        ,@('a direct diagnostic-output task', 'sonnet', (Get-CaseModel -Title 'Print the lane diagnostics in the trace' -Touches 'RunDarkFactory.ps1'))
        ,@('a Normal ordinary direct task', 'sonnet', (Get-CaseModel))
        ,@('a lookalike library is not a crypto library', 'sonnet', (Get-CaseModel -Touches 'Curl.TlsSettings.UnitLibrary'))
        ,@('-Model defaults to auto without CLAUDE_MODEL', 'auto', $(if ($env:CLAUDE_MODEL) { 'auto' } else { $Model }))
        ,@('probes use sonnet under auto', 'sonnet', $(if ($Model -eq 'auto') { $ProbeModel } else { 'sonnet' })))
    foreach ($library in $OpusLibraries) {
        $cases += ,@("touches $library.UnitLibrary", 'opus', (Get-CaseModel -Touches 'Curl.Cli.UnitLibrary', "$library.UnitLibrary"))
        $cases += ,@("touches $library.UnitTests", 'opus', (Get-CaseModel -Touches "$library.UnitTests"))
    }
    # The task file reading: front matter, touches list and the Log's retry test.
    $front = @('---', 'id: BL-9', 'title: "Tidy names"', 'priority: Normal', 'pipeline: direct', 'touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]', '---', '## Log', '', '- 2026-10-07: Created.', '- 2026-10-07: Backlog -> Doing.')
    $requeued = $front + @('- 2026-10-07: Doing -> Backlog: depends on BL-8.', '- 2026-10-07: Backlog -> Doing.')
    $blockedOnce = $front + @('- 2026-10-07: Doing -> Blocked: Stewart: dark factory timed out.', '- 2026-10-07: Blocked -> Doing.')
    $cases += ,@('a task file with an ordinary first claim', 'sonnet: direct pipeline', (Format-Choice (Get-TaskModelChoice -Forced 'auto' -Lines $front)))
    $cases += ,@('a task file requeued once', 'opus: retry after a run that came back', (Format-Choice (Get-TaskModelChoice -Forced 'auto' -Lines $requeued)))
    $cases += ,@('a task file blocked by a run', 'opus', (Get-TaskModelChoice -Forced 'auto' -Lines $blockedOnce).Model)
    $cases += ,@('a task file touching Curl.Quic.UnitTests', 'opus', (Get-TaskModelChoice -Forced 'auto' -Lines ($front -replace 'Curl\.Cli\.UnitTests', 'Curl.Quic.UnitTests')).Model)
    $cases += ,@('a task file with model: haiku', 'haiku', (Get-TaskModelChoice -Forced 'auto' -Lines ($front[0..5] + @('model: haiku') + $front[6..10])).Model)
    $cases += ,@('a Log line in the body text is not a retry', 'False', "$(Test-TaskRetried 'Notes say Doing -> Backlog happens.')")
    # -Model is forwarded as given by the shift start and the -Continuous hand-over, and
    # -Restart reuses the coordinator's own command line, so auto stays auto. The source read is
    # RunDarkFactory.ps1 and every file in DarkFactory\.
    $own = (@($FactoryScript) + @(Get-ChildItem (Join-Path $FactoryScriptDir 'DarkFactory') -Filter '*.ps1' | ForEach-Object { $_.FullName }) | ForEach-Object { Get-Content -Raw $_ }) -join "`n"
    $cases += ,@('shift start and hand-over forward -Model as given', '2', "$(([regex]::Matches($own, "'-Model', \`$Model\b")).Count)")
    # Cost per model from a made-up shift's logs.
    $dir = Join-Path ([IO.Path]::GetTempPath()) "DarkFactoryModels-$PID"
    New-Item -ItemType Directory -Force $dir | Out-Null
    Set-Content (Join-Path $dir 'BL-1-20261007-100000-L1.jsonl') '{"type":"factory","model":"sonnet","why":"docs pipeline"}', '{"type":"result","total_cost_usd":1.25}'
    Set-Content (Join-Path $dir 'BL-1-20261007-100000-L1-resumed.jsonl') '{"type":"factory","model":"sonnet","why":"docs pipeline"}', '{"type":"result","total_cost_usd":0.5}'
    Set-Content (Join-Path $dir 'BL-2-20261007-100000-L2.jsonl') '{"type":"factory","model":"opus","why":"feature pipeline"}', '{"type":"result","total_cost_usd":4}'
    Set-Content (Join-Path $dir 'BL-3-20261006-100000-L1.jsonl') '{"type":"factory","model":"opus","why":"feature pipeline"}', '{"type":"result","total_cost_usd":9}'
    $cases += ,@('runs, tasks and dollars per model for a shift', 'models: opus 1 runs 1 tasks $4.00; sonnet 2 runs 1 tasks $1.75', (Get-ModelCostSummary $dir '^BL-\d+-20261007-100000(-L\d+)?[-.]'))
    $cases += ,@('the same for one lane', 'models: sonnet 2 runs 1 tasks $1.75', (Get-ModelCostSummary $dir '^BL-\d+-20261007-100000-L1[-.]'))
    $cases += ,@('the model line leaves costs readable', '0.5,1.25,4,9', ((@(Get-RecentRunCosts $dir) | Sort-Object) -join ','))
    # A task the daily rule blocked goes back to Backlog once its 24-hour window has passed;
    # one blocked because a run stopped at the cap stays Blocked (BL-2117).
    $refused = '2026-10-11: Doing -> Blocked. Stewart: dark factory its runs of the last day already cost 1.16 of its 2.10 US dollar cost cap, so it was not run again; split the task; see x'
    $stopped = '2026-10-11: Doing -> Blocked. Stewart: dark factory stopped at its 3.2 US dollar cost cap (4 times the median run, at least $3, at most -TaskBudgetUsd, less its runs of the last day), so split the task; see x'
    $cases += ,@('a refused task is requeued once its last day leaves 1 dollar', 'True,True', "$((Test-CapBlockRequeueDue $refused 2.10 0), (Test-CapBlockRequeueDue $refused 2.10 1.10) -join ',')")
    $cases += ,@('a refused task stays Blocked while its cap is still spent', 'False', "$(Test-CapBlockRequeueDue $refused 2.10 1.16)")
    $cases += ,@('a run that stopped at the cap is never requeued', 'False,False', "$((Test-CapBlockRequeueDue $stopped 3.2 0), (Test-CapBlockRequeueDue $stopped 0 0) -join ',')")
    $cases += ,@('a task blocked for another reason is not requeued by the cap', 'False', "$(Test-CapBlockRequeueDue '2026-10-11: Doing -> Blocked. Stewart: which package?' 3 0)")
    Remove-Item -Recurse -Force $dir
    $failed = 0
    foreach ($case in $cases) {
        if ($case[1] -ceq $case[2]) { Write-Host "PASS $($case[0]): $($case[2])" -ForegroundColor Green }
        else { Write-Host "FAIL $($case[0]): expected $($case[1]), got $($case[2])" -ForegroundColor Red; $failed++ }
    }
    # No case may give haiku unless haiku was asked for.
    $unasked = @($cases | Where-Object { "$($_[2])" -like 'haiku*' -and $_[0] -notmatch 'haiku' })
    if ($unasked.Count) { Write-Host "FAIL haiku chosen unasked: $($unasked[0][0])" -ForegroundColor Red; $failed++ }
    exit $(if ($failed) { 1 } else { 0 })
}

# This file ran to its end; RunDarkFactory.ps1 goes on to the next one.
$AreaFileFinished = $true
