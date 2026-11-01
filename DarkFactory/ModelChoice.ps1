# The Claude model per task (BL-1705): the rules, the front-matter override, retries, the
# cost summary per model, and -TestModelChoice. Dot-sourced by RunDarkFactory.ps1 in its
# order; see the loader there.

# ---- the model per task (BL-1705; THE MODEL PER TASK in RunDarkFactory.ps1's help)

$ModelNames = 'haiku', 'sonnet', 'opus'
# The hand-built security and crypto libraries; a task touching one, or its test twin, runs on opus.
$OpusLibraries = 'Curl.Cryptography', 'Curl.Tls', 'Curl.Quic', 'Curl.Kerberos', 'Curl.Ntlm', 'Curl.Original.Protocol.Ssh'
$OpusLibraryPattern = '^(' + (($OpusLibraries | ForEach-Object { [regex]::Escape($_) }) -join '|') + ')(\.(UnitLibrary|UnitTests|IntegrationTests))?([\\/].*)?$'
$script:ModelChoice = @{ Id = ''; Model = ''; Why = '' }

# Routine feature work (BL-3001): a feature task whose title starts with one of these runs on
# sonnet, unless its title or touches name hard work below. Each kind follows a written
# recipe - port an upstream unit program, stand in for a debug variable, interpret an upstream
# .pl check, list cases on the ratchet, split or move a file, add the assertion an audit mutant
# showed missing. "Make upstream case N pass" is not here: finding why Curl differs is
# diagnosis, and those runs came back on opus too. Key: the kind the lane log names.
$RoutineFeatureTitles = [ordered]@{
    'upstream unit program port'          = '^Port upstream unit programs?\b'
    'harness stand-in'                    = '^(Stand in for\b|Add (an? |the )?([\w./-]+ ){0,4}stand-ins?\b)'
    'upstream test script interpretation' = '^Interpret upstream test\d+\.pl\b'
    'upstream case bookkeeping'           = '^(Run|Check|List)\b.*\bupstream\b.*\bcases? \d'
    'harness feature report'              = '^Report the [\w./-]+ feature to the harness\b'
    'file split for parallel lanes'       = '^Split \S+ so tasks touching it can run in parallel\b'
    'code move for unit-program ports'    = '^Move \S+ .*\binto (a library|Curl\.\S+)'
    'test-strength audit fix'             = '^Fix AF-\d+: .*(\bcan become\b.*\bwith no test failing\b|\bhas no assertion\b|\bchecks only\b|\bnever checks\b)'
}
# Hard work that keeps a feature task on opus whatever its title: its title or a touches entry
# names one of these (whole words, any case). Key: the marker the lane log names.
$HardWorkMarkers = [ordered]@{
    'security'    = '\b(security|secure|secrets?|credentials?|passwords?|vulnerab\w*)\b'
    'crypto'      = '\b(crypto\w*|encrypt\w*|decrypt\w*|cipher\w*|hash\w*|HMAC|SHA-?\d*|MD[45]|signing|signatures?)\b'
    'TLS'         = '\b(TLS|SSL|mTLS|https-mtls|X\.?509|ASN\.1|DER|certs?|certificates?)\b'
    'QUIC'        = '\bQUIC\b'
    'HTTP/2'      = '\b(HTTP/2|h2|nghttp2|HPACK)\b'
    'HTTP/3'      = '\b(HTTP/3|h3|QPACK)\b'
    'SSH'         = '\b(SSH|SFTP|SCP)\b'
    'Kerberos'    = '\b(Kerberos|GSS-?API|SPNEGO|Negotiate)\b'
    'NTLM'        = '\bNTLM\b'
    'DoH'         = '\b(DoH|HTTPS-RR|DNS)\b'
    'concurrency' = '\b(concurren\w*|race|races|racing|thread\w*|deadlock\w*|lock-free)\b'
    'framing'     = '\b(fram(e|es|ing)|capsules?|WebSocket|pipelin\w*)\b'
}

function Get-HardWorkMarker {
    # The first hard-work marker any of -Text names, or '' when none does.
    param([string[]]$Text = @())
    $joined = $Text -join "`n"
    foreach ($name in $HardWorkMarkers.Keys) { if ($joined -match $HardWorkMarkers[$name]) { return $name } }
    return ''
}

function Get-RoutineFeatureKind {
    # The routine feature kind a task title starts with, or '' when it starts with none.
    param([string]$Title)
    foreach ($kind in $RoutineFeatureTitles.Keys) { if ($Title -match $RoutineFeatureTitles[$kind]) { return $kind } }
    return ''
}

function Get-FeatureModelChoice {
    # A feature task's model: sonnet when its title is routine and it names no hard work, opus
    # otherwise. Why: "routine feature: <kind>", "feature pipeline, names <marker>" for a
    # routine title the marker overrules, or "feature pipeline".
    param([string]$Title, [string[]]$Touches = @())
    $kind = Get-RoutineFeatureKind $Title
    if (-not $kind) { return [pscustomobject]@{ Model = 'opus'; Why = 'feature pipeline' } }
    $marker = Get-HardWorkMarker (@($Title) + $Touches)
    if ($marker) { return [pscustomobject]@{ Model = 'opus'; Why = "feature pipeline, names $marker" } }
    return [pscustomobject]@{ Model = 'sonnet'; Why = "routine feature: $kind" }
}

function Get-ModelChoice {
    # The model a task runs on and why: a forced -Model, else the task's own model:, else
    # opus for security, crypto, protocol, retried and CI-fix work and for feature work that
    # is not routine (Get-FeatureModelChoice), and sonnet for the rest. The rule never picks
    # haiku. Warning names an ignored model: value.
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
        elseif ($Pipeline -eq 'protocol') { 'protocol pipeline' }
        elseif ($Retried) { 'retry after a run that came back' }
        elseif ($Priority -eq 'High' -and $Title -match '^Fix (CI failure|flaky CI test)') { 'CI failure fix' }
        else { '' }
    if ($why) { return [pscustomobject]@{ Model = 'opus'; Why = $why; Warning = $warning } }
    if ($Pipeline -eq 'feature') {
        $feature = Get-FeatureModelChoice -Title $Title -Touches $Touches
        return [pscustomobject]@{ Model = $feature.Model; Why = $feature.Why; Warning = $warning }
    }
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
        # A killed run's cost is estimated from its usage, as the task budget counts it (BL-3001).
        $cost = Get-RunLogCost $log.FullName
        if ($cost) { $byModel[$name].Usd += $cost.Usd }
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
        ,@('a feature task with no routine title', 'opus: feature pipeline', (Format-Choice (Get-ModelChoice -Forced 'auto' -Pipeline 'feature' -Title 'Reach a SOCKS proxy over a Unix domain socket')))
        ,@('a routine feature title names its kind', 'sonnet: routine feature: upstream unit program port', (Format-Choice (Get-ModelChoice -Forced 'auto' -Pipeline 'feature' -Title 'Port upstream unit program 1304 (netrc parsing) over NetrcFile')))
        ,@('a routine title overruled by a hard marker names it', 'opus: feature pipeline, names DoH', (Format-Choice (Get-ModelChoice -Forced 'auto' -Pipeline 'feature' -Title 'Port upstream unit program 1658: DoH HTTPS resource record decoding')))
        ,@('a hard marker in touches keeps a routine title on opus', 'opus: feature pipeline, names framing', (Format-Choice (Get-ModelChoice -Forced 'auto' -Pipeline 'feature' -Title 'Port upstream unit program 9999' -Touches 'Curl.Conformance.UnitProgram.UnitLibrary/UnitPrograms.Headers.Capsule.cs')))
        ,@('a routine feature title on a retried task', 'opus: retry after a run that came back', (Format-Choice (Get-ModelChoice -Forced 'auto' -Pipeline 'feature' -Title 'Port upstream unit program 1304' -Retried)))
        ,@('a routine feature title touching a crypto library', 'opus', (Get-CaseModel -Pipeline 'feature' -Title 'Port upstream unit program 1610' -Touches 'Curl.Cryptography.UnitTests'))
        ,@('model: opus wins over a routine feature title', 'opus', (Get-CaseModel -TaskModel 'opus' -Pipeline 'feature' -Title 'Port upstream unit program 1304'))
        ,@('-Model opus wins over a routine feature title', 'opus', (Get-CaseModel -Forced 'opus' -Pipeline 'feature' -Title 'Port upstream unit program 1304'))
        ,@('a routine title on the protocol pipeline stays opus', 'opus', (Get-CaseModel -Pipeline 'protocol' -Title 'Port upstream unit program 1304'))
        ,@('an invalid model: on a routine feature falls back to sonnet', 'sonnet (warned)', (Get-CaseModel -TaskModel 'gpt' -Pipeline 'feature' -Title 'Port upstream unit program 1304'))
        ,@('the hard-work marker of a plain title is none', '', (Get-HardWorkMarker 'Port upstream unit program 1304 (netrc parsing)'))
        ,@('a word containing a marker is not the marker', '', (Get-HardWorkMarker 'Move stripcredentials into a library; Curl.TlsSettings.UnitLibrary; shared frameworks'))
        ,@('-Model defaults to auto without CLAUDE_MODEL', 'auto', $(if ($env:CLAUDE_MODEL) { 'auto' } else { $Model }))
        ,@('probes use sonnet under auto', 'sonnet', $(if ($Model -eq 'auto') { $ProbeModel } else { 'sonnet' })))
    # Feature titles from the board (BL-3001): routine ones run on sonnet, hard ones on opus.
    $routineTitles = @(
        'Port upstream unit programs 1660 and 1674 (HSTS parse, check, load, save and the 10000-entry cap) over HstsCache'
        'Stand in for CURL_FAKE_XATTR so upstream cases 644, 687 and 688 run'
        'Add an SMB server stand-in so upstream cases 1451 and 1850 run'
        'Interpret upstream test1177.pl in C# over Libcurl''s CurlVersionFeatures and curl_version_info.3 so case 1177 runs'
        'List the Linux-only upstream cases 307, 1517, 1582, 1587, 2072 and 3207 on the ratchet'
        'Report the codeset-utf8 feature to the harness so upstream cases 165, 955 to 958 run'
        'Split Curl.Engine.UnitLibrary/EngineComposition.cs so tasks touching it can run in parallel'
        'Move create_dir_hierarchy (OutputFileDirectories) into a library the unit-program ports reach, for tool1720'
        'Fix AF-0291: Sample_Create_BuildsAClientOverTheManagedEngineWithoutSendingAnything has no assertion'
        'Fix AF-0297: UpperCasedEscapes'' && can become || with no test failing, upper-casing escapes that are not two hex digits')
    $hardTitles = @(
        'Port upstream unit programs for hashes, encodings and signing: cases 1302, 1600 and 1601'
        'Port upstream unit program unit1651 (x509 parsing) over PeerCertificateText'
        'Port upstream unit program 3400 (capsule protocol encode and decode) over CapsuleDatagramChannel'
        'Stand in for the GSS-API stub (CURL_STUB_GSS_CREDS) so upstream cases 2056, 2057 and 2094 run'
        'Add stand-ins for the https-mtls and socks5unix servers so upstream cases 2088 and 2089 run'
        'Run the HTTP/3 upstream cases 2500, 2501, 2502 and 2503 through the HTTP/3 stand-in'
        'Fix AF-0296: TransferSettingsOf''s ShowError: false can become true with no test failing, so an SSH transfer writes an error line'
        'Make upstream case 1548 pass: TELNET reading stdin with -m 1 ends with exit 28'
        'Run a <command type="perl"> upstream case through a registered C# script interpretation'
        'Fix AF-0300: --limit-rate does not limit the upload: a -F POST at --limit-rate 10 exits 0 where curl exits 56')
    foreach ($title in $routineTitles) { $cases += ,@("routine feature: $title", 'sonnet', (Get-CaseModel -Pipeline 'feature' -Title $title)) }
    foreach ($title in $hardTitles) { $cases += ,@("hard feature: $title", 'opus', (Get-CaseModel -Pipeline 'feature' -Title $title)) }
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
    $cases += ,@('a refused task is requeued once its last day leaves 2 dollars', 'True,True', "$((Test-CapBlockRequeueDue $refused 3.10 0), (Test-CapBlockRequeueDue $refused 3.10 1.10) -join ',')")
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
