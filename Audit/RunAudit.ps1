<#
.SYNOPSIS
    Runs one audit end to end: plants known defects in a throwaway copy of the factory's latest
    work, runs the auditors on it, writes their findings and a scorecard on the audit branch,
    and opens the audit pull request.

.DESCRIPTION
    The audit office's runner (BL-1020, ADR-0267). It lives under Audit/, so the guards that keep
    the dark factory out of Audit/ protect it too. Every step is traced to
    <repo>.audit\<stamp>\audit.log.

      1. Refuse inside a dark factory process (CURL_DARK_FACTORY_LANE set) and while a shift runs
         (a process whose command line holds RunDarkFactory.ps1 without -NewTab or -Restart):
         audits run between shifts, since both spend the same Claude budget.
      2. git fetch origin. Use the worktree that has the audit branch checked out, or create
         <repo>.audit\audit-branch on it (from origin/audit, else origin/master), and merge
         origin/master into it so the audit definitions are current.
      3. Planted tree: a detached worktree <stamp>\planted at -Ref. Overlay Audit/Instructions,
         Audit/Tools, Audit/PlantedDefects and .claude/agents/audit-*.md from the audit branch,
         so the auditors run the definitions the fingerprint describes. Copy the factory logs
         written since the last scorecard (seven days when there is none) from <repo>.logs into
         <stamp>\logs, with the CI runs saved there as ci-runs.json.
      4. audit-seeder plants -Planted defects (at least one per selected auditor) with -Seed and
         writes <stamp>\manifest.json; it commits the planted tree as "Audit baseline". It is
         told the files to avoid - every file in the "Location:" line of an accepted finding on
         the audit branch whose tasks are all in Tasks/Done at the audited commit, so no plant
         sets that finding's re-audit verdict aside (BL-2108) - and writes them to
         <stamp>\avoid-files.txt. When every catalogue entry for an auditor fits only avoided
         files, it draws another entry or lists the auditor under "unseeded" in the manifest.
         The run then checks the manifest and warns, in its log and the scorecard commit's
         message, about any selected auditor left unseeded and any defect in an avoided file.
      5. Each selected auditor in the fixed order, one at a time, in the planted tree, with its
         open findings from the audit branch to re-audit - less any accepted finding about one
         past run's cost (key ...:cost-outlier) whose tasks are all in Tasks/Done at the audited
         commit, which step 6 closes without a re-audit (ADR-0422, amended 2026-10-14) and which
         step 4 does not avoid either; its reply goes to
         <stamp>\reports\<auditor>.md. A planted tree left changed afterwards marks that auditor
         as having written to the audited tree (unreliable), and is reset before the next.
      6. Write-AuditScorecard.ps1 -ReliabilityOnly, then Write-AuditFindings.ps1 -Tree <planted>
         -RerunReproductions, which first reruns each mechanical reproduction the closure rule needs
         on the clean audited commit (never the planted tree; results in <stamp>\reports\reruns),
         then sets aside the verdicts that rerun leaves unanswered and that overlap a planted
         defect, then Write-AuditScorecard.ps1, writing into the audit branch (ADR-0422).
      When -Auditors leaves out an auditor that has accepted findings, the run warns, in its
      output and its log, that they will not be re-audited; the scorecard's Attention section
      says so too.
      7. Commit there ("audit: scorecard <stamp>"), push origin audit, and open a pull request
         audit -> master unless one is open. This script never merges it: an interactive
         session does, once CI is green on all three platforms (Stewart's standing exception,
         2026-09-30).
      8. Remove the planted worktree; keep <stamp> for reading.

    Agents run headless as the factory runs them: claude -p --agent <name> --model <model>
    --dangerously-skip-permissions --output-format stream-json --verbose, prompt on standard
    input; each run's result event gives its cost.

.PARAMETER Ref
    The commit to audit. Default origin/work/dark-factory.

.PARAMETER Auditors
    The auditors to run, in any order; they run in the fixed order quality, security,
    conformance, truthfulness, process. Default all five. (The performance auditor was retired
    on 2026-10-13, BL-2132: Curl does not aim to match curl's speed, memory or size; the
    conformance auditor checks hangs and transfers under load.)

.PARAMETER Planted
    Defects to plant: at least one per selected auditor, at most 10. Default 8.

.PARAMETER Seed
    The seeder's seed. Default: from the clock; recorded in the manifest.

.PARAMETER ReauditOnly
    Re-audit only: no seeding and no auditor method, so no new findings (Stewart, 2026-10-25: "only
    do the reaudit, and do not start a new audit"). Step 3 makes a clean tree at -Ref, step 4 plants
    nothing and writes a manifest with no defects, and step 5 runs only the selected auditors that
    have open findings to re-audit, telling each to run those reproductions and nothing else. The
    runner keeps each auditor's reply as reports\<auditor>.full.md and scores reports\<auditor>.md,
    the same reply with its report block's findings emptied, so a new finding is never filed.
    With no method counts and no planted defect to catch, every auditor is unreliable on the
    scorecard, as ADR-0422 requires of an auditor whose reliability was not measured: a finding
    closes only when the mechanical rerun on the clean commit agrees, or on a second verdict
    running. -Planted is ignored. Decided by Claude under Stewart's delegation.

.PARAMETER AlongsideShift
    Run even while a dark factory shift runs. Shifts started with -Continuous chain into the
    next with no gap, so "between shifts" may never come; this switch lets an interactive session
    audit anyway. The audit then shares the Claude budget with the shift, whose -Lanes Auto adapts
    to the measured burn. The lane-marker refusal still applies. Decided by Claude, 2026-09-30.

.PARAMETER DryRun
    Do step 1, then print what every other step would run, and change nothing. Step 4 lists the
    files the seeder must avoid, each with the findings naming it, read from this copy's
    Audit/Findings and the Tasks folder at -Ref as it stands before the fetch.

.PARAMETER NewTab
    Start the audit in a new herdr tab and return at once. Outside herdr it starts in a new
    console window on Windows, and off Windows as a detached pwsh writing
    <repo>.audit/RunAudit-<stamp>.log (and .log.err).
    Whatever way that run ends, its tab's caption ends in "read" (BL-2133): "done, read" when it
    finishes (a -DryRun included), "FAILED, read" on any error (seeding, an auditor, the scorecard,
    anything thrown), "REFUSED, read" when it refuses to start (a running shift, the lane marker,
    an unknown auditor, too few -Planted). No tab is left captioned "starting" or "seeding".

.PARAMETER SelfTest
    Check the refusals on a faked process list, the -Auditors warnings, that the auditor list,
    the -Auditors default and help, the models, the methods and the agent files agree on the five
    auditors (and that the retired performance auditor is gone and refused), and the seeder's
    avoided files and manifest check on scratch findings and task paths, and the final tab
    caption of a finished run, a thrown error and a refusal (each run as a child process that
    records its captions to a file instead of renaming a tab).

.EXAMPLE
    Audit\RunAudit.cmd -NewTab
.EXAMPLE
    Audit\RunAudit.cmd -Auditors truthfulness,process -Planted 2
.EXAMPLE
    Audit\RunAudit.cmd -NewTab -ReauditOnly -AlongsideShift
.EXAMPLE
    Audit/RunAudit.sh -NewTab -DryRun
    On Linux and macOS: the same script under pwsh, with the same arguments (BL-3927).
#>
param(
    [string]$Ref = 'origin/work/dark-factory',
    [string[]]$Auditors = @('quality', 'security', 'conformance', 'truthfulness', 'process'),
    [ValidateRange(1, 10)][int]$Planted = 8,
    [int]$Seed = -1,
    [switch]$ReauditOnly,
    [switch]$DryRun,
    [switch]$AlongsideShift,
    [switch]$NewTab,
    [switch]$SelfTest
)

$ErrorActionPreference = 'Stop'
# Windows PowerShell 5.1 on Windows, PowerShell 7 elsewhere (BL-3927).
$OnWindows = $PSVersionTable.PSVersion.Major -lt 6 -or $IsWindows
$PowerShellExe = if ($OnWindows) { 'powershell' } else { 'pwsh' }
# <repo> is the main checkout, whichever worktree this copy runs from: the folder holding the
# shared .git, beside which the factory keeps <repo>.logs and the audit keeps <repo>.audit.
$commonGit = "$(& git -C $PSScriptRoot rev-parse --path-format=absolute --git-common-dir 2>$null)".Trim()
if ($OnWindows) { $commonGit = $commonGit -replace '/', '\' }
$repo = if ($commonGit) { Split-Path $commonGit -Parent } else { (Resolve-Path (Join-Path $PSScriptRoot '..')).Path }
$Order = @('quality', 'security', 'conformance', 'truthfulness', 'process')
# quality, truthfulness and process moved from Sonnet to Opus (Stewart, 2026-10-03, BL-1364): on Sonnet they
# cut their methods short in three audits running, even when told to run every step.
$Models = @{ quality = 'opus'; security = 'opus'; conformance = 'opus'; truthfulness = 'opus'; process = 'opus'; seeder = 'sonnet' }
$Method = @{ quality = 'Quality'; security = 'Security'; conformance = 'Conformance'; truthfulness = 'Truthfulness'; process = 'Process' }
$Dot = [char]0x00B7
$Started = Get-Date
$Stamp = $Started.ToString('yyyyMMdd-HHmmss')
$Utf8 = New-Object Text.UTF8Encoding $false
$AuditRoot = "$repo.audit"
$RunDir = Join-Path $AuditRoot $Stamp
$script:CostUsd = 0.0
$script:CaptionsOn = $false

# A terminating error outside the main try (the refusal checks, the dry run) still ends in a caption.
trap {
    Write-Host "audit failed: $($_.Exception.Message)"
    Exit-Audit 'failed' 1
}

# ------------------------------------------------------------------ helpers

function Write-Step([string]$Text) {
    $line = "$(Get-Date -Format 'HH:mm:ss') $Text"
    Write-Host $line
    # The log is a courtesy: a locked audit.log must never turn a step, or the failure handler, into a new failure.
    if (-not $DryRun -and (Test-Path -LiteralPath $RunDir)) { try { [IO.File]::AppendAllText((Join-Path $RunDir 'audit.log'), $line + "`r`n") } catch { $null = $_ } }
}

function Invoke-Git([string[]]$Arguments, [switch]$AllowFailure) {
    $ErrorActionPreference = 'Continue'
    $out = & git @Arguments 2>&1
    if ($LASTEXITCODE -ne 0 -and -not $AllowFailure) { throw "git $($Arguments -join ' ') failed: $out" }
    return @($out | ForEach-Object { "$_" })
}

function Get-HerdrBin {
    # The herdr binary when this process runs inside a herdr pane, otherwise $null (as RunDarkFactory.ps1).
    if ($env:HERDR_ENV -ne '1') { return $null }
    if ($env:HERDR_BIN_PATH -and (Test-Path $env:HERDR_BIN_PATH)) { return $env:HERDR_BIN_PATH }
    $cmd = Get-Command herdr -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    return $null
}

function Set-Caption([string]$Phase) {
    # Captions this run's own tab "Audit HH:mm . <phase>", only when it is a tab this runner opened.
    # It never throws: a caption that cannot be set must not hide the outcome it describes (BL-2133).
    # CURL_RUNAUDIT_CAPTION_FILE (the self-test's) appends each phase to that file instead of renaming a tab.
    if (-not $script:CaptionsOn) { return }
    if ($env:CURL_RUNAUDIT_CAPTION_FILE) { [IO.File]::AppendAllText($env:CURL_RUNAUDIT_CAPTION_FILE, $Phase + "`r`n"); return }
    try { Set-HerdrTabCaption $Phase } catch { $null = $_ }
}

function Set-HerdrTabCaption([string]$Phase) {
    $herdr = Get-HerdrBin
    if (-not $herdr -or -not $env:HERDR_TAB_ID) { return }
    $label = "$(((& $herdr tab get $env:HERDR_TAB_ID) -join "`n" | ConvertFrom-Json).result.tab.label)"
    if ($label -notlike 'Audit *') { return }
    $caption = "Audit $($Started.ToString('HH:mm')) $Dot $Phase"
    if ($caption.Length -gt 48) { $caption = $caption.Substring(0, 47) + '~' }
    & $herdr tab rename $env:HERDR_TAB_ID $caption 2>&1 | Out-Null
}

function Exit-Audit([string]$Outcome, [int]$Code) {
    # Every way a run started with -NewTab ends goes through here (or the trap, or the end of the main
    # try): the tab's last caption always ends in "read", so no tab keeps a "running" caption (BL-2133).
    Set-Caption $(switch ($Outcome) { 'done' { 'done, read' } 'refused' { 'REFUSED, read' } default { 'FAILED, read' } })
    exit $Code
}

function Get-ShiftProcesses([object[]]$Processes) {
    # The dark factory shift processes among $Processes (each with a CommandLine).
    return @($Processes | Where-Object { $_.CommandLine -and $_.CommandLine -match 'RunDarkFactory\.ps1' -and $_.CommandLine -notmatch '\s-(NewTab|Restart)\b' })
}

function Get-RefusalReason([object[]]$Processes, [string]$LaneMarker, [bool]$Alongside) {
    if ($LaneMarker) { return 'Refused: a dark factory process may not run an audit (CURL_DARK_FACTORY_LANE is set). Audits run in an interactive session, between shifts.' }
    if (-not $Alongside -and @(Get-ShiftProcesses $Processes).Count) { return 'Refused: a dark factory shift is running. Audits run between shifts, since both spend the same Claude budget; run this when the shift has ended, or pass -AlongsideShift to share the budget with it.' }
    return ''
}

function Invoke-Agent([string]$Name, [string]$Model, [string]$Prompt, [string]$WorkingDirectory, [string]$ReplyPath) {
    # One headless agent run; writes its final reply to $ReplyPath and adds its cost.
    $stream = [IO.Path]::ChangeExtension($ReplyPath, '.jsonl')
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $claude = "claude -p --agent $Name --model $Model --dangerously-skip-permissions --output-format stream-json --verbose > `"$stream`" 2> `"$stream.err`""
    if ($OnWindows) {
        $psi.FileName = $env:ComSpec
        $psi.Arguments = "/d /c $claude"
    }
    else {
        $psi.FileName = '/bin/sh'
        $psi.ArgumentList.Add('-c')
        $psi.ArgumentList.Add("exec $claude")
    }
    $psi.WorkingDirectory = $WorkingDirectory
    $psi.UseShellExecute = $false
    $psi.RedirectStandardInput = $true
    $psi.EnvironmentVariables['BASH_DEFAULT_TIMEOUT_MS'] = '1800000'
    $psi.EnvironmentVariables['BASH_MAX_TIMEOUT_MS'] = '3600000'
    $p = [System.Diagnostics.Process]::Start($psi)
    $p.StandardInput.Write($Prompt)
    $p.StandardInput.Close()
    $p.WaitForExit()
    $result = $null
    foreach ($line in [IO.File]::ReadLines($stream)) { if ($line.Contains('"type":"result"')) { $result = $line | ConvertFrom-Json } }
    $text = if ($result -and $result.PSObject.Properties['result']) { "$($result.result)" } else { '' }
    if ($result -and $result.PSObject.Properties['total_cost_usd']) { $script:CostUsd += [double]$result.total_cost_usd }
    [IO.File]::WriteAllText($ReplyPath, $text, $Utf8)
    return $p.ExitCode
}

function Get-OpenFindings([string]$Findings, [string]$Auditor, [string[]]$TaskPaths) {
    # The auditor's open findings to re-audit, one prompt line each. A finding about one past run's
    # cost whose tasks are all Done at the audited commit is left out: Write-AuditFindings.ps1 closes
    # it without a re-audit (ADR-0422, amended 2026-10-14).
    $lines = @()
    foreach ($f in @(Get-ChildItem -LiteralPath $Findings -Filter 'AF-*.md' -ErrorAction SilentlyContinue | Sort-Object Name)) {
        $t = [IO.File]::ReadAllText($f.FullName)
        if ($t -notmatch "(?m)^auditor: $Auditor\s*$" -or $t -notmatch '(?m)^status: (proposed|accepted|deferred|blocked)\s*$') { continue }
        if (Test-HistoricalClose $t $TaskPaths) { continue }
        $id = if ($t -match '(?m)^id: (\S+)') { $Matches[1] } else { continue }
        $title = if ($t -match '(?m)^title: (.+)$') { $Matches[1].Trim() } else { '' }
        $command = if ($t -match '(?s)```powershell\r?\n(.*?)```') { $Matches[1].Trim() } else { '' }
        $lines += "- $id - $title. Reproduction: $command"
    }
    return $lines
}

function Get-SkippedAuditorWarnings([string]$Findings, [string[]]$Chosen) {
    # One warning per auditor left out of this audit that has accepted findings: they go
    # un-re-audited, so nothing can close or reopen them this time (ADR-0422).
    $warnings = @()
    foreach ($a in @($Order | Where-Object { $Chosen -notcontains $_ })) {
        $ids = @()
        foreach ($f in @(Get-ChildItem -LiteralPath $Findings -Filter 'AF-*.md' -ErrorAction SilentlyContinue | Sort-Object Name)) {
            $t = [IO.File]::ReadAllText($f.FullName)
            if ($t -match "(?m)^auditor: $a\s*$" -and $t -match '(?m)^status: accepted\s*$' -and $t -match '(?m)^id: (\S+)') { $ids += $Matches[1] }
        }
        if ($ids.Count) { $warnings += "Warning: -Auditors leaves out $a, which has $($ids.Count) accepted findings this audit will not re-audit: $($ids -join ', ')." }
    }
    return $warnings
}

function Get-UnknownAuditorMessage([string]$Name) {
    # Why -Auditors refuses a name; the retired performance auditor says so (BL-2132).
    if ($Name -eq 'performance') { return "The performance auditor was retired on 2026-10-13 (BL-2132): Curl does not aim to match curl's speed, memory or size, and the conformance auditor checks hangs and transfers under load. Auditors: $($Order -join ', ')." }
    return "Unknown auditor '$Name'. Auditors: $($Order -join ', ')."
}

function Get-FindingField([string]$Text, [string]$Name) {
    if ($Text -match "(?m)^$([regex]::Escape($Name)):[ \t]*([^\r\n]*)") { return $Matches[1].Trim() }
    return ''
}

function Test-AllTasksDone([string]$Text, [string[]]$TaskPaths) {
    # True when the finding has at least one task and every one - its task, its tasks list, and any
    # BL-###-fix-/re-fix- task file naming it, as Test-AuditDue.ps1 counts them - is in Tasks/Done
    # (or an archive under it) in $TaskPaths, the audited commit's git paths.
    $paths = @($TaskPaths | Where-Object { $_ } | ForEach-Object { "$_" -replace '\\', '/' })
    $ids = @(@((Get-FindingField $Text 'task'), @((Get-FindingField $Text 'tasks') -split ',\s*')) | Where-Object { $_ -match '^BL-\d+$' })
    $slug = (Get-FindingField $Text 'id').ToLowerInvariant()
    $ids += @($paths | ForEach-Object { $_.Substring($_.LastIndexOf('/') + 1) } | Where-Object { $_ -match '^BL-\d+-(re-)?fix-' -and $_ -match "\b$slug\b" } | ForEach-Object { ($_ -split '-')[0..1] -join '-' })
    $ids = @($ids | Sort-Object -Unique)
    if (-not $ids.Count) { return $false }
    foreach ($taskId in $ids) {
        $path = @($paths | Where-Object { $_.Substring($_.LastIndexOf('/') + 1) -like "$taskId-*" } | Select-Object -First 1)
        if (-not $path.Count -or $path[0] -notmatch '^Tasks/Done/') { return $false }
    }
    return $true
}

function Test-HistoricalClose([string]$Text, [string[]]$TaskPaths) {
    # An accepted finding about one past run's cost (key ...:cost-outlier) whose tasks are all Done:
    # Write-AuditFindings.ps1 closes it task-done-historical, so no auditor re-audits it.
    return (Get-FindingField $Text 'status') -eq 'accepted' -and (Get-FindingField $Text 'key').EndsWith(':cost-outlier') -and (Test-AllTasksDone $Text $TaskPaths)
}

function Get-ReauditAwaitedFiles([string]$Findings, [string[]]$TaskPaths) {
    # The files the seeder must not plant in (BL-2108): those a finding awaiting a re-audit names
    # in its "Location:" line. A finding awaits a re-audit while it is accepted and every one of
    # its tasks - its task, its tasks list, and any BL-###-fix-/re-fix- task file naming it, as
    # Test-AuditDue.ps1 counts them - is in Tasks/Done at the audited commit. Unlike
    # Test-AuditDue's reaudit-pending count, a finding that already has one verdict since its fix
    # still counts: it stays accepted until a second verdict or a clean rerun closes it, and a
    # plant in its file would set that verdict aside too (ADR-0422's overlap rule). A finding about
    # a past run's cost (key ...:cost-outlier) never awaits one: it closes without a re-audit
    # (ADR-0422, amended 2026-10-14).
    # Returns one object per such finding: Id, Auditor, Files (paths with any :line removed).
    $awaited = @()
    foreach ($f in @(Get-ChildItem -LiteralPath $Findings -Filter 'AF-*.md' -ErrorAction SilentlyContinue | Sort-Object Name)) {
        $t = [IO.File]::ReadAllText($f.FullName)
        if ((Get-FindingField $t 'status') -ne 'accepted') { continue }
        $id = Get-FindingField $t 'id'
        if (-not (Test-AllTasksDone $t $TaskPaths)) { continue }
        # A past run's cost closes without a re-audit, so no plant can set one aside.
        if ((Get-FindingField $t 'key').EndsWith(':cost-outlier')) { continue }
        $location = if ($t -match '(?m)^Location: `([^`]*)`') { $Matches[1] } else { '' }
        $files = @($location -split ',\s*' | ForEach-Object { ($_ -replace '\\', '/' -replace ':\d+(-\d+)?$', '').Trim() } | Where-Object { $_ -and $_ -notmatch '[{}]' } | Sort-Object -Unique)
        if ($files.Count) { $awaited += [pscustomobject]@{ Id = $id; Auditor = Get-FindingField $t 'auditor'; Files = $files } }
    }
    return $awaited
}

function Test-AvoidedFile([string]$Planted, [string]$Avoided) {
    # The same file: equal, or one path ending with the other. A process finding names its log as
    # logs/<name>, where the manifest names it relative to the log copy.
    $p = ("$Planted" -replace '\\', '/').Trim().TrimStart('/')
    $a = ("$Avoided" -replace '\\', '/').Trim().TrimStart('/')
    if (-not $p -or -not $a) { return $false }
    return ($p -ieq $a) -or $p.EndsWith('/' + $a, [StringComparison]::OrdinalIgnoreCase) -or $a.EndsWith('/' + $p, [StringComparison]::OrdinalIgnoreCase)
}

function Get-SeedingProblems($Manifest, [string[]]$Chosen, [string[]]$AvoidFiles) {
    # What the seeder's manifest gets wrong against the run's rules (BL-2108): an auditor with no
    # planted defect (with the seeder's reason when its "unseeded" list gives one), and a defect
    # planted in a file a finding awaiting a re-audit names.
    $problems = @()
    $planted = @($Manifest.planted | Where-Object { $_ })
    $reasons = @{}
    foreach ($u in @($Manifest.unseeded | Where-Object { $_ })) { $reasons["$($u.auditor)"] = "$($u.reason)" }
    foreach ($a in $Chosen) {
        if (@($planted | Where-Object { "$($_.auditor)" -eq $a }).Count) { continue }
        $why = if ($reasons[$a]) { ": $($reasons[$a])" } else { ': the manifest names no defect for it' }
        $problems += "Could not seed $a$why. Its catch rate this audit is '-'."
    }
    foreach ($p in $planted) {
        $hit = @($AvoidFiles | Where-Object { Test-AvoidedFile "$($p.file)" $_ } | Select-Object -First 1)
        if ($hit.Count) { $problems += "Planted defect $($p.id) ($($p.auditor)) sits in $($p.file), a file a finding awaiting a re-audit names ($($hit[0])); that finding's verdict may be set aside." }
    }
    return $problems
}

function ConvertFrom-SeederManifest([string]$Text) {
    # The seeder's manifest as an object. A seeder that writes a Windows path inside a string
    # without doubling its backslashes (Curl.Console\bin\Debug, audit 20261030-210336) leaves
    # JSON that does not parse. Only then is every backslash but those of \", \\ and \/ doubled
    # and the text parsed again, so the path's \b and \n stay letters rather than becoming
    # control characters, and one unescaped path no longer fails the whole audit.
    try { return $Text | ConvertFrom-Json }
    catch {
        $repaired = [regex]::Replace($Text, '\\(.)', { param($m) if ('"\/'.Contains($m.Groups[1].Value)) { $m.Value } else { '\\' + $m.Groups[1].Value } })
        return $repaired | ConvertFrom-Json
    }
}

function Get-AvoidPromptLines([object[]]$Awaited) {
    # The seeder prompt's avoid list, one file per line with the findings that name it.
    $byFile = [ordered]@{}
    foreach ($w in $Awaited) { foreach ($file in $w.Files) { if (-not $byFile.Contains($file)) { $byFile[$file] = @() }; $byFile[$file] += $w.Id } }
    return @($byFile.Keys | ForEach-Object { "- $_ ($($byFile[$_] -join ', '))" })
}

function Remove-ReportFindings([string]$Reply) {
    # A -ReauditOnly reply with its last json report block's findings emptied, so a re-audit-only run
    # files no new finding however the auditor answers. A reply with no parseable block is returned
    # as it is: the scorecard marks that auditor unreliable for it.
    $blocks = [regex]::Matches($Reply, '(?s)```json\s*\r?\n(.*?)```')
    if (-not $blocks.Count) { return $Reply }
    $last = $blocks[$blocks.Count - 1]
    try { $report = $last.Groups[1].Value | ConvertFrom-Json } catch { return $Reply }
    if ($report.PSObject.Properties['findings']) { $report.findings = @() } else { $report | Add-Member findings @() }
    $json = $report | ConvertTo-Json -Depth 20
    return $Reply.Substring(0, $last.Index) + "``````json`n$json`n``````" + $Reply.Substring($last.Index + $last.Length)
}

function Get-ReauditOnlyPrompt([string]$Tree, [string]$Commit, [string]$Fingerprint, [string]$LogsLine, [string]$Scratch, [string[]]$Open, [string]$MethodName) {
    # The -ReauditOnly prompt: the listed re-audits and nothing else.
    return "Re-audit only. Do not run your method's steps and report no new findings: this run only re-audits, so write `"findings`": [] and leave out the method counts. The tree is $Tree (commit $Commit; auditor fingerprint $Fingerprint).$LogsLine Your temporary folder is $Scratch. Re-audit these open findings by running each reproduction, and give each a verdict in `"reaudits`":`n$($Open -join "`n")`nFollow Audit/Instructions/Auditor-Rules.md, and the re-audit section of Audit/Instructions/$MethodName.md, in that tree, and end with exactly one json report block."
}

# ------------------------------------------------------------------ self-test

if ($SelfTest) {
    $failed = 0
    function Check([string]$Name, [bool]$Ok, [string]$Detail) { if (-not $Ok) { $script:failed++ }; Write-Host "$(if ($Ok) { 'PASS' } else { 'FAIL' }) ${Name}: $Detail" }
    $shift = [pscustomobject]@{ CommandLine = 'powershell -NoProfile -File Z:\repos\Curl\RunDarkFactory.ps1 -Lanes Auto -Continuous' }
    $lane = [pscustomobject]@{ CommandLine = 'powershell -File Z:\repos\Curl\RunDarkFactory.ps1 -Lane 2 -Branch work/dark-factory' }
    $launcher = [pscustomobject]@{ CommandLine = 'powershell -File Z:\repos\Curl\RunDarkFactory.ps1 -NewTab -Lanes Auto' }
    $restart = [pscustomobject]@{ CommandLine = 'powershell -File Z:\repos\Curl\RunDarkFactory.ps1 -Restart' }
    $other = [pscustomobject]@{ CommandLine = 'pwsh -File C:\x\Other.ps1' }
    $r = Get-RefusalReason @($shift, $other) '' $false
    Check 'a running shift refuses, saying audits run between shifts' ($r -like '*between shifts*') $r
    Check 'a lane alone refuses too' ((Get-RefusalReason @($lane) '' $false) -like 'Refused: a dark factory shift*') 'lane'
    Check 'a -NewTab or -Restart launcher does not count as a shift' ((Get-RefusalReason @($launcher, $restart, $other) '' $false) -eq '') 'none'
    Check 'the lane marker refuses, even alongside a shift' ((Get-RefusalReason @($shift) '1' $true) -like '*CURL_DARK_FACTORY_LANE*') 'marker'
    Check 'nothing running and no marker: no refusal' ((Get-RefusalReason @($other) '' $false) -eq '') 'none'
    $unescaped = ConvertFrom-SeederManifest '{ "planted": [ { "trigger": "planted build (Curl.Console\bin\Debug\net10.0\curl.exe) exits 25", "quote": "a \"b\" \\ c" } ] }'
    Check 'a manifest with an unescaped Windows path parses, the path kept as written' ($unescaped.planted[0].trigger -eq 'planted build (Curl.Console\bin\Debug\net10.0\curl.exe) exits 25' -and $unescaped.planted[0].quote -eq 'a "b" \ c') "$($unescaped.planted[0].trigger) | $($unescaped.planted[0].quote)"
    Check 'a valid manifest parses unchanged, its \n a newline' ((ConvertFrom-SeederManifest '{ "d": "x\ny" }').d -eq "x`ny") 'valid'
    Check '-AlongsideShift runs while a shift runs' ((Get-RefusalReason @($shift, $lane) '' $true) -eq '') 'none'
    $scratch = Join-Path ([IO.Path]::GetTempPath()) ('runaudit-selftest-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Force $scratch | Out-Null
    try {
        [IO.File]::WriteAllText((Join-Path $scratch 'AF-0001-x.md'), "---`nid: AF-0001`nauditor: quality`nstatus: accepted`n---`n")
        [IO.File]::WriteAllText((Join-Path $scratch 'AF-0002-x.md'), "---`nid: AF-0002`nauditor: process`nstatus: proposed`n---`n")
        $warnings = @(Get-SkippedAuditorWarnings $scratch @('security', 'process'))
        Check 'leaving out an auditor with accepted findings warns, naming them' ($warnings.Count -eq 1 -and $warnings[0] -eq 'Warning: -Auditors leaves out quality, which has 1 accepted findings this audit will not re-audit: AF-0001.') "$warnings"
        Check 'running every auditor warns about nothing' (@(Get-SkippedAuditorWarnings $scratch $Order).Count -eq 0) 'none'

        # BL-2132: five auditors; the performance auditor is retired. The list, the default, the
        # -Auditors help, the models, the methods and the agent files agree.
        $five = 'quality,security,conformance,truthfulness,process'
        $paramText = [regex]::Match([IO.File]::ReadAllText($PSCommandPath), "\[string\[\]\]\`$Auditors = @\(([^)]*)\)").Groups[1].Value -replace "[' ]", ''
        Check 'the auditor order and the -Auditors default are the five' (($Order -join ',') -eq $five -and $paramText -eq $five) "order $($Order -join ','); default $paramText"
        $helpText = [regex]::Replace([IO.File]::ReadAllText($PSCommandPath), '\s+', ' ')
        Check 'the -Auditors help names the five in order' ($helpText -match 'they run in the fixed order quality, security, conformance, truthfulness, process\. Default all five\.') 'help'
        $root = Split-Path $PSScriptRoot -Parent
        $missing = @($Order | Where-Object { -not $Models[$_] -or -not (Test-Path -LiteralPath (Join-Path $root ".claude\agents\audit-$_.md")) -or -not (Test-Path -LiteralPath (Join-Path $PSScriptRoot "Instructions\$($Method[$_]).md")) })
        Check 'each auditor has a model, an agent and a method' ($missing.Count -eq 0) "missing: $($missing -join ',')"
        $retiredLeft = @((Join-Path $root '.claude\agents\audit-performance.md'), (Join-Path $PSScriptRoot 'Instructions\Performance.md'), (Join-Path $PSScriptRoot 'Tools\Measure-Performance.ps1')) | Where-Object { Test-Path -LiteralPath $_ }
        Check 'the retired performance auditor has no model, agent, method or tool' (-not $Models.ContainsKey('performance') -and -not $Method.ContainsKey('performance') -and @($retiredLeft).Count -eq 0) "left: $(@($retiredLeft) -join ', ')"
        Check '-Auditors performance is refused, saying it is retired' ((Get-UnknownAuditorMessage 'performance') -like '*retired*' -and (Get-UnknownAuditorMessage 'nonesuch') -like "Unknown auditor 'nonesuch'*") (Get-UnknownAuditorMessage 'performance')

        # BL-2108: files of findings awaiting a re-audit, and the manifest check.
        $awaitDir = Join-Path $scratch 'await'
        New-Item -ItemType Directory -Force $awaitDir | Out-Null
        $finding = { param($Id, $Auditor, $Status, $Task, $Location, $Key) [IO.File]::WriteAllText((Join-Path $awaitDir "$Id-x.md"), "---`nid: $Id`ntitle: Finding $Id`nauditor: $Auditor`nstatus: $Status`nkey: $Key`ntask: $Task`ntasks: $Task`n---`n## Evidence`n`nLocation: ``$Location```n") }
        & $finding 'AF-0010' 'conformance' 'accepted' 'BL-10' 'Curl.Console/CurlCommandRunner.cs:1310'
        & $finding 'AF-0011' 'quality' 'accepted' 'BL-11' 'Curl.Cli.UnitLibrary/A.cs:5'
        & $finding 'AF-0012' 'process' 'accepted' 'BL-12' 'logs/ci-runs.json'
        & $finding 'AF-0013' 'security' 'closed' 'BL-13' 'Curl.Tls.UnitLibrary/B.cs'
        & $finding 'AF-0014' 'truthfulness' 'accepted' 'BL-14' 'README.md:3, Documentation/C.md'
        & $finding 'AF-0015' 'quality' 'accepted' 'none' 'Curl.Core.UnitLibrary/D.cs'
        # ADR-0422, amended 2026-10-14: a past run's cost, Done, closes without a re-audit; open, it is re-audited.
        & $finding 'AF-0016' 'process' 'accepted' 'BL-16' 'logs/BL-800-x.jsonl' 'process:logs:BL-800:cost-outlier'
        & $finding 'AF-0017' 'process' 'accepted' 'BL-17' 'logs/BL-801-x.jsonl' 'process:logs:BL-801:cost-outlier'
        $tasks = @('Tasks/Done/2026-10-10/BL-10-fix-af-0010-x.md', 'Tasks/Doing/BL-11-fix-af-0011-x.md', 'Tasks/Done/BL-12-fix-af-0012-x.md',
            'Tasks/Done/BL-13-fix-af-0013-x.md', 'Tasks/Done/BL-14-fix-af-0014-x.md', 'Tasks/Backlog/BL-20-re-fix-af-0012-y.md',
            'Tasks/Done/BL-16-fix-af-0016-x.md', 'Tasks/Doing/BL-17-fix-af-0017-x.md')
        $awaited = @(Get-ReauditAwaitedFiles $awaitDir $tasks)
        $ids = ($awaited | ForEach-Object { $_.Id }) -join ','
        Check 'an accepted finding whose tasks are all Done awaits a re-audit; an open task, an open Re-fix, closed, task-less or a past run''s cost does not' ($ids -eq 'AF-0010,AF-0014') $ids
        $processOpen = (@(Get-OpenFindings $awaitDir 'process' $tasks) | ForEach-Object { ($_ -split ' ')[1] }) -join ','
        Check 'a past run''s cost with every task Done is not re-audited; with a task open, and any other process finding, it is' ($processOpen -eq 'AF-0012,AF-0017') $processOpen
        $avoid = @($awaited | ForEach-Object { $_.Files } | Sort-Object -Unique)
        Check 'the files to avoid come from Location, without line numbers, every comma-separated path' (($avoid -join ',') -eq 'Curl.Console/CurlCommandRunner.cs,Documentation/C.md,README.md') ($avoid -join ',')
        $lines = @(Get-AvoidPromptLines $awaited)
        Check 'the seeder prompt lists each avoided file with the findings naming it' ($lines[0] -eq '- Curl.Console/CurlCommandRunner.cs (AF-0010)' -and $lines.Count -eq 3) ($lines -join ' / ')
        Check 'a log-copy path matches a process finding''s logs/ path' ((Test-AvoidedFile 'ci-runs.json' 'logs/ci-runs.json') -and -not (Test-AvoidedFile 'a-ci-runs.json' 'logs/ci-runs.json')) 'log'
        $good = [pscustomobject]@{ planted = @([pscustomobject]@{ id = 'PD-201'; auditor = 'conformance'; file = 'Curl.Console/Other.cs' }, [pscustomobject]@{ id = 'PD-101'; auditor = 'quality'; file = 'Curl.Core.UnitTests/T.cs' }) }
        Check 'a manifest that seeds every auditor outside the avoided files has no problems' (@(Get-SeedingProblems $good @('quality', 'conformance') $avoid).Count -eq 0) 'none'
        $bad = [pscustomobject]@{ planted = @([pscustomobject]@{ id = 'PD-201'; auditor = 'conformance'; file = 'Curl.Console\CurlCommandRunner.cs' }); unseeded = @([pscustomobject]@{ auditor = 'quality'; reason = 'every quality entry fits only avoided files' }) }
        $problems = @(Get-SeedingProblems $bad @('quality', 'conformance', 'process') $avoid)
        Check 'an unseeded auditor is reported with the seeder''s reason, a silent gap too, and a plant in an avoided file' ($problems.Count -eq 3 -and $problems[0] -like 'Could not seed quality: every quality entry*' -and $problems[1] -like 'Could not seed process: the manifest names no defect*' -and $problems[2] -like 'Planted defect PD-201 *CurlCommandRunner.cs*') ($problems -join ' / ')

        # -ReauditOnly: a reply's new findings are dropped, its re-audits kept; the prompt asks for re-audits only.
        $reply = "Notes.`n``````json`n{ `"auditor`": `"quality`", `"findings`": [ { `"key`": `"k`" } ], `"reaudits`": [ { `"finding`": `"AF-0010`", `"reproduces`": false } ] }`n```````nAfter."
        $stripped = Remove-ReportFindings $reply
        $block = ([regex]::Match($stripped, '(?s)```json\s*\r?\n(.*?)```').Groups[1].Value | ConvertFrom-Json)
        Check '-ReauditOnly drops a reply''s new findings and keeps its re-audits and text' (@($block.findings).Count -eq 0 -and $block.reaudits[0].finding -eq 'AF-0010' -and $stripped.StartsWith('Notes.') -and $stripped.EndsWith('After.')) "findings $(@($block.findings).Count)"
        Check '-ReauditOnly leaves a reply with no report block as it is' ((Remove-ReportFindings 'no block') -eq 'no block') 'unchanged'
        $onlyPrompt = Get-ReauditOnlyPrompt 'T' 'c' 'f' '' 'S' @('- AF-0010 - x. Reproduction: y') 'Quality'
        Check '-ReauditOnly prompts for the listed re-audits only, with no method and no findings' ($onlyPrompt -like 'Re-audit only. Do not run your method*' -and $onlyPrompt.Contains('"findings": []') -and $onlyPrompt -like '*- AF-0010 - x. Reproduction: y*' -and $onlyPrompt -notlike '*in full*') $onlyPrompt.Substring(0, 40)
    }
    finally { Remove-Item -Recurse -Force -LiteralPath $scratch -ErrorAction SilentlyContinue }

    # BL-2133: the last caption of a run that ends each way. Each child records captions to a file
    # (CURL_RUNAUDIT_CAPTION_FILE) instead of renaming a tab, with the factory lane marker cleared.
    $hostExe = (Get-Process -Id $PID).Path
    $lastCaption = {
        param([string[]]$ScriptArguments, [string]$Fault)
        $file = Join-Path ([IO.Path]::GetTempPath()) ('runaudit-captions-' + [guid]::NewGuid().ToString('N') + '.txt')
        $saved = @{ CAP = $env:CURL_RUNAUDIT_CAPTION_FILE; FAULT = $env:CURL_RUNAUDIT_FAULT; LANE = $env:CURL_DARK_FACTORY_LANE }
        try {
            $env:CURL_RUNAUDIT_CAPTION_FILE = $file; $env:CURL_RUNAUDIT_FAULT = $Fault; $env:CURL_DARK_FACTORY_LANE = ''
            $null = & $hostExe -NoProfile -ExecutionPolicy Bypass -File $PSCommandPath @ScriptArguments 2>&1
            $code = $LASTEXITCODE
            $lines = @(if (Test-Path -LiteralPath $file) { Get-Content -LiteralPath $file | Where-Object { $_ } })
            return [pscustomobject]@{ Last = $(if ($lines.Count) { $lines[-1] } else { '' }); Code = $code }
        }
        finally {
            $env:CURL_RUNAUDIT_CAPTION_FILE = $saved.CAP; $env:CURL_RUNAUDIT_FAULT = $saved.FAULT; $env:CURL_DARK_FACTORY_LANE = $saved.LANE
            Remove-Item -LiteralPath $file -Force -ErrorAction SilentlyContinue
        }
    }
    $done = & $lastCaption @('-DryRun', '-AlongsideShift') ''
    Check 'a run that finishes ends with "done, read"' ($done.Last -eq 'done, read' -and $done.Code -eq 0) "last '$($done.Last)', exit $($done.Code)"
    $thrown = & $lastCaption @('-DryRun', '-AlongsideShift') '1'
    Check 'a run that throws ends with "FAILED, read"' ($thrown.Last -eq 'FAILED, read' -and $thrown.Code -eq 1) "last '$($thrown.Last)', exit $($thrown.Code)"
    $fewPlanted = & $lastCaption @('-Planted', '1', '-AlongsideShift') ''
    Check 'a run that refuses (too few -Planted) ends with "REFUSED, read"' ($fewPlanted.Last -eq 'REFUSED, read' -and $fewPlanted.Code -eq 1) "last '$($fewPlanted.Last)', exit $($fewPlanted.Code)"
    $unknown = & $lastCaption @('-Auditors', 'nonesuch', '-AlongsideShift') ''
    Check 'a run that refuses (unknown auditor) ends with "REFUSED, read"' ($unknown.Last -eq 'REFUSED, read' -and $unknown.Code -eq 1) "last '$($unknown.Last)', exit $($unknown.Code)"
    exit $(if ($failed) { 1 } else { 0 })
}

# ------------------------------------------------------------------ -NewTab

if ($NewTab) {
    $forward = @()
    foreach ($p in $PSBoundParameters.GetEnumerator()) {
        if ($p.Key -eq 'NewTab') { continue }
        if ($p.Value -is [System.Management.Automation.SwitchParameter]) { if ($p.Value) { $forward += "-$($p.Key)" } }
        elseif ($p.Value -is [array]) { $forward += @("-$($p.Key)", ($p.Value -join ',')) }
        else { $forward += @("-$($p.Key)", "`"$($p.Value)`"") }
    }
    $herdr = Get-HerdrBin
    if ($herdr) {
        $create = @('tab', 'create', '--cwd', $repo, '--label', "Audit $($Started.ToString('HH:mm')) $Dot starting", '--no-focus')
        if ($env:HERDR_WORKSPACE_ID) { $create += @('--workspace', $env:HERDR_WORKSPACE_ID) }
        $created = (& $herdr @create) -join "`n" | ConvertFrom-Json
        $pane = $created.result.root_pane.pane_id
        if ($pane) {
            & $herdr pane wait-output $pane --match '>' --timeout 15000 2>&1 | Out-Null
            $line = "$PowerShellExe -NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`" " + ($forward -join ' ')
            & $herdr pane run $pane $line.Replace('"', '\"') 2>&1 | Out-Null
            Write-Host "Audit started in herdr tab $($created.result.tab.tab_id)."
            exit 0
        }
    }
    $arguments = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$PSCommandPath`"") + $forward
    if ($OnWindows) {
        $p = Start-Process -FilePath 'powershell.exe' -ArgumentList $arguments -WorkingDirectory $repo -PassThru
        Write-Host "Audit started in a new console window (pid $($p.Id))."
        exit 0
    }
    # No console windows off Windows: a detached pwsh writing its output beside the run folders.
    New-Item -ItemType Directory -Force $AuditRoot | Out-Null
    $out = Join-Path $AuditRoot "RunAudit-$Stamp.log"
    $p = Start-Process -FilePath 'pwsh' -ArgumentList $arguments -WorkingDirectory $repo -RedirectStandardOutput $out -RedirectStandardError "$out.err" -PassThru
    Write-Host "Audit started as pid $($p.Id), writing $out."
    exit 0
}

# ------------------------------------------------------------------ 1. refusals

$script:CaptionsOn = $true
if ($env:CURL_RUNAUDIT_FAULT -eq '1') { throw 'Injected fault (CURL_RUNAUDIT_FAULT, the self-test''s).' }
$processes = if ($OnWindows) { @(Get-CimInstance Win32_Process -ErrorAction SilentlyContinue | Select-Object CommandLine) }
else { @(& ps -axo 'args=' | ForEach-Object { [pscustomobject]@{ CommandLine = "$_".Trim() } }) }
$refusal = Get-RefusalReason $processes $env:CURL_DARK_FACTORY_LANE $AlongsideShift.IsPresent
if ($refusal) { Write-Host $refusal; Exit-Audit 'refused' 1 }

$Auditors = @($Auditors | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim().ToLowerInvariant() } | Where-Object { $_ })
foreach ($a in $Auditors) { if ($Order -notcontains $a) { Write-Host (Get-UnknownAuditorMessage $a); Exit-Audit 'refused' 1 } }
$Selected = @($Order | Where-Object { $Auditors -contains $_ })
if (-not $ReauditOnly -and $Planted -lt $Selected.Count) { Write-Host "-Planted $Planted is fewer than the $($Selected.Count) auditors selected; each needs at least one."; Exit-Audit 'refused' 1 }
if ($Seed -lt 0) { $Seed = [int]($Started.Ticks % 1000000007) }

# The dry runs' paths, with this platform's separator.
$DryPlanted = Join-Path $RunDir 'planted'
$DryManifest = Join-Path $RunDir 'manifest.json'
$DryReports = Join-Path $RunDir 'reports'
$DryLogs = Join-Path $RunDir 'logs'

if ($DryRun -and $ReauditOnly) {
    Write-Host "Re-audit-only dry run, $Stamp. Would run, from ${repo}:"
    Write-Host "  2. git fetch origin; the audit branch's worktree; git merge origin/master there"
    Write-Host "  3. git worktree add --detach $DryPlanted $Ref (clean: nothing is planted); overlay the audit definitions; copy the logs"
    Write-Host "  4. no seeder: $DryManifest with no planted defects"
    $taskPaths = @(Invoke-Git @('-C', $repo, 'ls-tree', '-r', '--name-only', $Ref, '--', 'Tasks') -AllowFailure)
    foreach ($a in $Selected) {
        $open = @(Get-OpenFindings (Join-Path $PSScriptRoot 'Findings') $a $taskPaths)
        if ($open.Count) { Write-Host "  5. claude -p --agent audit-$a --model $($Models[$a]) ... re-audits only, $($open.Count) findings; findings emptied in $(Join-Path $DryReports "$a.md")" } else { Write-Host "  5. $a skipped: no open findings to re-audit" }
    }
    Write-Host "  6. Write-AuditScorecard.ps1 -ReliabilityOnly; Write-AuditFindings.ps1 -RerunReproductions; Write-AuditScorecard.ps1"
    Write-Host "  7. git commit -m 'audit: re-audit $Stamp'; git push origin audit; gh pr create (unless one is open)"
    Exit-Audit 'done' 0
}

if ($DryRun) {
    Write-Host "Audit dry run, $Stamp. Would run, from ${repo}:"
    Get-SkippedAuditorWarnings (Join-Path $PSScriptRoot 'Findings') $Selected | ForEach-Object { Write-Host "  $_" }
    Write-Host "  2. git fetch origin; the worktree that has branch audit checked out, or git worktree add $(Join-Path $AuditRoot 'audit-branch') audit (from origin/audit, else origin/master); git merge origin/master there"
    Write-Host "  3. git worktree add --detach $DryPlanted $Ref; overlay Audit/Instructions, Audit/Tools, Audit/PlantedDefects and .claude/agents/audit-*.md from the audit branch; copy logs changed since the last scorecard from $repo.logs to $DryLogs; gh run list ... > $(Join-Path $DryLogs 'ci-runs.json')"
    $awaited = @(Get-ReauditAwaitedFiles (Join-Path $PSScriptRoot 'Findings') @(Invoke-Git @('-C', $repo, 'ls-tree', '-r', '--name-only', $Ref, '--', 'Tasks') -AllowFailure))
    Write-Host "  4. claude -p --agent audit-seeder --model sonnet ... (seed $Seed, $Planted defects, manifest $DryManifest), avoiding $(@($awaited | ForEach-Object { $_.Files } | Sort-Object -Unique).Count) files named by $($awaited.Count) findings awaiting a re-audit (findings here, tasks at $Ref before the fetch):"
    Get-AvoidPromptLines $awaited | ForEach-Object { Write-Host "       $_" }
    Write-Host "     then check the manifest: every auditor seeded, no defect in an avoided file"
    foreach ($a in $Selected) { Write-Host "  5. claude -p --agent audit-$a --model $($Models[$a]) ... > $(Join-Path $DryReports "$a.md") (scratch $(Join-Path (Join-Path $RunDir 'scratch') $a))" }
    Write-Host "  6. Write-AuditScorecard.ps1 -ReliabilityOnly; Write-AuditFindings.ps1 -Tree $DryPlanted -RerunReproductions (mechanical reruns on the clean, unplanted $Ref commit); Write-AuditScorecard.ps1 (into the audit branch)"
    Write-Host "  7. git commit -m 'audit: scorecard $Stamp'; git push origin audit; gh pr create --base master --head audit (unless one is open; never merged here)"
    Write-Host "  8. git worktree remove --force $DryPlanted"
    Exit-Audit 'done' 0
}

New-Item -ItemType Directory -Force $RunDir, (Join-Path $RunDir 'reports'), (Join-Path $RunDir 'scratch'), (Join-Path $RunDir 'logs') | Out-Null
Write-Step "audit $Stamp of $Ref; auditors $($Selected -join ', '); $(if ($ReauditOnly) { 're-audit only, nothing planted' } else { "$Planted planted; seed $Seed" })"
Set-Caption 'preparing'
$plantedTree = Join-Path $RunDir 'planted'
try {
    # 2. the audit branch
    Invoke-Git @('-C', $repo, 'fetch', '-q', 'origin') | Out-Null
    $auditTree = $null
    $current = $null
    foreach ($line in (Invoke-Git @('-C', $repo, 'worktree', 'list', '--porcelain'))) {
        if ($line -like 'worktree *') { $current = $line.Substring(9) }
        if ($line -eq 'branch refs/heads/audit') { $auditTree = $current }
    }
    if (-not $auditTree) {
        $auditTree = Join-Path $AuditRoot 'audit-branch'
        $hasRemote = @(Invoke-Git @('-C', $repo, 'ls-remote', '--heads', 'origin', 'audit')).Count -gt 0
        $hasLocal = @(Invoke-Git @('-C', $repo, 'branch', '--list', 'audit')) -join '' -ne ''
        if ($hasLocal) { Invoke-Git @('-C', $repo, 'worktree', 'add', $auditTree, 'audit') | Out-Null }
        else { Invoke-Git @('-C', $repo, 'worktree', 'add', '-b', 'audit', $auditTree, $(if ($hasRemote) { 'origin/audit' } else { 'origin/master' })) | Out-Null }
    }
    $auditTree = (Resolve-Path $auditTree).Path
    Invoke-Git @('-C', $auditTree, '-c', 'user.name=Audit runner', '-c', 'user.email=audit-runner@example.invalid', 'merge', '-q', '--no-edit', 'origin/master') | Out-Null
    Write-Step "audit branch at $auditTree, merged with origin/master"
    Get-SkippedAuditorWarnings (Join-Path $auditTree 'Audit\Findings') $Selected | ForEach-Object { Write-Step $_ }

    # 3. the planted tree, the overlay and the logs
    $auditedCommit = "$(Invoke-Git @('-C', $repo, 'rev-parse', $Ref) | Select-Object -First 1)".Trim()
    Invoke-Git @('-C', $repo, 'worktree', 'add', '--detach', $plantedTree, $auditedCommit) | Out-Null
    foreach ($part in 'Audit\Instructions', 'Audit\Tools', 'Audit\PlantedDefects') {
        $source = Join-Path $auditTree $part
        $target = Join-Path $plantedTree $part
        if (Test-Path -LiteralPath $target) { Remove-Item -Recurse -Force -LiteralPath $target }
        Copy-Item -Recurse -LiteralPath $source -Destination $target
    }
    Get-ChildItem -LiteralPath (Join-Path $plantedTree '.claude\agents') -Filter 'audit-*.md' -ErrorAction SilentlyContinue | Remove-Item -Force
    Copy-Item -Path (Join-Path $auditTree '.claude\agents\audit-*.md') -Destination (Join-Path $plantedTree '.claude\agents')
    $fingerprint = "$(& $PowerShellExe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $auditTree 'Audit\Tools\Get-AuditorFingerprint.ps1') -Root $auditTree)".Trim()
    $scorecards = @(Get-ChildItem -LiteralPath (Join-Path $auditTree 'Audit\Scorecards') -Filter '????-??-??_????.md' -ErrorAction SilentlyContinue | Sort-Object Name)
    $since = if ($scorecards.Count) { [datetime]::ParseExact($scorecards[-1].BaseName, 'yyyy-MM-dd_HHmm', $null) } else { $Started.AddDays(-7) }
    $logRoot = "$repo.logs"
    $logs = Join-Path $RunDir 'logs'
    if (Test-Path -LiteralPath $logRoot) {
        Get-ChildItem -LiteralPath $logRoot -File | Where-Object { $_.LastWriteTime -ge $since } | Copy-Item -Destination $logs
    }
    $ErrorActionPreference = 'Continue'
    & gh run list --workflow CI --branch work/dark-factory --json databaseId,status,conclusion,headSha,createdAt,updatedAt --limit 500 > (Join-Path $logs 'ci-runs.json') 2>$null
    $ErrorActionPreference = 'Stop'
    Write-Step "planted tree $plantedTree at $auditedCommit; fingerprint $fingerprint; logs since $($since.ToString('yyyy-MM-dd HH:mm'))"

    # 4. seed
    Set-Caption 'seeding'
    $manifest = Join-Path $RunDir 'manifest.json'
    $auditedTaskPaths = @(Invoke-Git @('-C', $repo, 'ls-tree', '-r', '--name-only', $auditedCommit, '--', 'Tasks'))
    $awaited = @(Get-ReauditAwaitedFiles (Join-Path $auditTree 'Audit\Findings') $auditedTaskPaths)
    $avoidFiles = @($awaited | ForEach-Object { $_.Files } | Sort-Object -Unique)
    $avoidLines = @(Get-AvoidPromptLines $awaited)
    [IO.File]::WriteAllText((Join-Path $RunDir 'avoid-files.txt'), (($avoidLines | ForEach-Object { "$_`r`n" }) -join ''), $Utf8)
    Write-Step "seeder avoids $($avoidFiles.Count) files named by $($awaited.Count) findings awaiting a re-audit$(if ($avoidFiles.Count) { ': ' + ($avoidFiles -join ', ') })"
    if ($ReauditOnly) {
        [IO.File]::WriteAllText($manifest, '{ "planted": [], "unseeded": [], "reauditOnly": true }', $Utf8)
        # The seeder's "Audit baseline" commit would hold the overlay; with no seeder, commit it here,
        # or the first auditor's clean-tree check sees the overlay as its own change and resets it away.
        Invoke-Git @('-C', $plantedTree, 'add', '-A') | Out-Null
        Invoke-Git @('-C', $plantedTree, '-c', 'user.name=Audit runner', '-c', 'user.email=audit-runner@example.invalid', 'commit', '-q', '--allow-empty', '-m', 'Audit baseline (re-audit only, nothing planted)') | Out-Null
        $seedingProblems = @()
        Write-Step 're-audit only: nothing planted'
    }
    else {
    $avoidPrompt = if ($avoidLines.Count) { " Avoided files - plant nothing in these, because findings awaiting a re-audit name them (a path starting logs/ is that file in the log copy):`n" + ($avoidLines -join "`n") + "`n" } else { ' Avoided files: none.' }
    $seedPrompt = "Seed an audit. Worktree: $plantedTree (detached at $auditedCommit). Log copy: $logs. Manifest path: $manifest. Seed: $Seed. Count: $Planted. Plant at least one defect for each of these auditors: $($Selected -join ', '), and only for them.$avoidPrompt Follow your instructions exactly; change nothing outside the worktree, the log copy and the manifest path."
    $null = Invoke-Agent 'audit-seeder' $Models.seeder $seedPrompt $auditTree (Join-Path $RunDir 'seeder.md')
    if (-not (Test-Path -LiteralPath $manifest)) { throw 'The seeder wrote no manifest.' }
    $seeded = ConvertFrom-SeederManifest (Get-Content -LiteralPath $manifest -Raw)
    Write-Step "seeded: $(@($seeded.planted).Count) defects"
    $seedingProblems = @(Get-SeedingProblems $seeded $Selected $avoidFiles)
    $seedingProblems | ForEach-Object { Write-Step "Warning: $_" }
    }

    # 5. the auditors
    $changedTree = @()
    foreach ($a in $Selected) {
        Set-Caption $a
        $scratch = Join-Path $RunDir "scratch\$a"
        New-Item -ItemType Directory -Force $scratch | Out-Null
        $open = @(Get-OpenFindings (Join-Path $auditTree 'Audit\Findings') $a $auditedTaskPaths)
        if ($ReauditOnly -and -not $open.Count) { Write-Step "$a skipped: no open findings to re-audit"; continue }
        $reaudit = if ($open.Count) { "Re-audit these open findings by running each reproduction:`n" + ($open -join "`n") } else { 'There are no findings to re-audit.' }
        $logsLine = if ($a -eq 'process') { " The factory's log folder is $logs; the previous scorecard's date is $($since.ToString('yyyy-MM-dd'))." } else { '' }
        $prompt = if ($ReauditOnly) { Get-ReauditOnlyPrompt $plantedTree $auditedCommit $fingerprint $logsLine $scratch $open $Method[$a] }
        else { "Audit the tree at $plantedTree (commit $auditedCommit; auditor fingerprint $fingerprint).$logsLine Your temporary folder is $scratch. Do every step of your method in full and report its method counts; the re-audits below come on top of it, never instead. $reaudit Follow Audit/Instructions/Auditor-Rules.md and Audit/Instructions/$($Method[$a]).md in that tree, and end with exactly one json report block." }
        $replyPath = Join-Path $RunDir "reports\$a.md"
        $exit = Invoke-Agent "audit-$a" $Models[$a] $prompt $plantedTree $(if ($ReauditOnly) { Join-Path $RunDir "reports\$a.full.md" } else { $replyPath })
        if ($ReauditOnly) { [IO.File]::WriteAllText($replyPath, (Remove-ReportFindings ([IO.File]::ReadAllText((Join-Path $RunDir "reports\$a.full.md")))), $Utf8) }
        $dirty = @(Invoke-Git @('-C', $plantedTree, 'status', '--porcelain') | Where-Object { $_ })
        if ($dirty.Count) {
            $changedTree += $a
            Write-Step "$a wrote to the audited tree ($($dirty.Count) paths); resetting it"
            Invoke-Git @('-C', $plantedTree, 'reset', '-q', '--hard') | Out-Null
            Invoke-Git @('-C', $plantedTree, 'clean', '-q', '-fdx') | Out-Null
        }
        Write-Step "$a finished (exit $exit)"
    }

    # 6. findings and scorecard
    Set-Caption 'scoring'
    $tools = Join-Path $auditTree 'Audit\Tools'
    $reports = Join-Path $RunDir 'reports'
    $scorecardName = $Started.ToString('yyyy-MM-dd_HHmm') + '.md'
    $common = @('-ReportDirectory', $reports, '-Manifest', $manifest, '-FindingsDirectory', (Join-Path $auditTree 'Audit\Findings'))
    $changedArg = if ($changedTree.Count) { @('-ChangedTree', ($changedTree -join ',')) } else { @() }
    $unreliable = @(& $PowerShellExe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $tools 'Write-AuditScorecard.ps1') @common @changedArg -ReliabilityOnly | Where-Object { $_ })
    $unreliableArg = if ($unreliable.Count) { @('-Unreliable', ($unreliable -join ',')) } else { @() }
    $findingsLine = & $PowerShellExe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $tools 'Write-AuditFindings.ps1') -ReportDirectory $reports -Manifest $manifest -FindingsDirectory (Join-Path $auditTree 'Audit\Findings') -Scorecard $scorecardName -Commit $auditedCommit -Date $Started.ToString('yyyy-MM-dd') -Tree $plantedTree -RerunReproductions @unreliableArg
    Write-Step "$findingsLine; unreliable: $(if ($unreliable.Count) { $unreliable -join ', ' } else { 'none' })"
    $finished = Get-Date
    $modelList = ($Selected | ForEach-Object { "$_=$($Models[$_])" }) -join ','
    $scorecard = & $PowerShellExe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $tools 'Write-AuditScorecard.ps1') @common @changedArg -ScorecardsDirectory (Join-Path $auditTree 'Audit\Scorecards') -Commit $auditedCommit -Branch ($Ref -replace '^origin/', '') -Started $Started -Finished $finished -CostUsd $script:CostUsd -Models $modelList -Fingerprint $fingerprint
    Write-Step "scorecard $scorecard"

    # 7. commit, push, pull request
    Invoke-Git @('-C', $auditTree, 'add', '-A', '--', 'Audit/Findings', 'Audit/Scorecards') | Out-Null
    Invoke-Git @('-C', $auditTree, '-c', 'user.name=Audit runner', '-c', 'user.email=audit-runner@example.invalid', 'commit', '-q', '-m', "audit: $(if ($ReauditOnly) { 're-audit' } else { 'scorecard' }) $Stamp", '-m', "$findingsLine. Audited $auditedCommit; auditors $($Selected -join ', '); $(if ($ReauditOnly) { 're-audit only, nothing planted' } else { "seed $Seed" }).$(if ($seedingProblems.Count) { ' ' + ($seedingProblems -join ' ') })") | Out-Null
    Invoke-Git @('-C', $auditTree, 'push', '-q', 'origin', 'audit') | Out-Null
    $openPr = "$(& gh pr list --head audit --base master --state open --json url --jq '.[0].url' 2>$null)".Trim()
    if (-not $openPr) {
        $table = ((Get-Content -LiteralPath $scorecard -Raw) -split '## Process')[0]
        $body = Join-Path $RunDir 'pr-body.md'
        [IO.File]::WriteAllText($body, "Audit $Stamp of ``$auditedCommit`` ($Ref).`n`n$table`nAn interactive session merges this once CI is green on all three platforms (Stewart's standing exception, 2026-09-30); this runner never merges it. Stewart accepts or rejects each finding (Audit/Triage.md).`n", $Utf8)
        $openPr = "$(& gh pr create --base master --head audit --title "$(if ($ReauditOnly) { 'Re-audit' } else { 'Audit' }) $Stamp" --body-file $body 2>&1)".Trim()
    }
    Write-Step "pull request $openPr"
    Set-Caption 'done, read'
}
catch {
    $failedRun = $true
    Set-Caption 'FAILED, read'
    Write-Step "audit failed: $($_.Exception.Message)"
}
finally {
    # 8. clean up the planted tree; keep the run folder
    if (Test-Path -LiteralPath $plantedTree) { Invoke-Git @('-C', $repo, 'worktree', 'remove', '--force', $plantedTree) -AllowFailure | Out-Null }
    Write-Step "run folder $RunDir; cost $($script:CostUsd.ToString('0.00', [Globalization.CultureInfo]::InvariantCulture)) USD"
}
exit $(if ($failedRun) { 1 } else { 0 })
