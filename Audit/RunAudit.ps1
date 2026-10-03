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
         writes <stamp>\manifest.json; it commits the planted tree as "Audit baseline".
      5. Each selected auditor in the fixed order, one at a time, in the planted tree, with its
         open findings from the audit branch to re-audit; its reply goes to
         <stamp>\reports\<auditor>.md. A planted tree left changed afterwards marks that auditor
         as having written to the audited tree (unreliable), and is reset before the next.
      6. Write-AuditScorecard.ps1 -ReliabilityOnly, then Write-AuditFindings.ps1, then
         Write-AuditScorecard.ps1, writing into the audit branch.
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
    performance, conformance, truthfulness, process. Default all six.

.PARAMETER Planted
    Defects to plant: at least one per selected auditor, at most 10. Default 8.

.PARAMETER Seed
    The seeder's seed. Default: from the clock; recorded in the manifest.

.PARAMETER AlongsideShift
    Run even while a dark factory shift runs. Shifts started with -Continuous chain into the
    next with no gap, so "between shifts" may never come; this switch lets an interactive session
    audit anyway. The audit then shares the Claude budget with the shift, whose -Lanes Auto adapts
    to the measured burn. The lane-marker refusal still applies. Decided by Claude, 2026-09-30.

.PARAMETER DryRun
    Do step 1, then print what every other step would run, and change nothing.

.PARAMETER NewTab
    Start the audit in a new herdr tab (a console window outside herdr) and return at once.

.PARAMETER SelfTest
    Check the refusals on a faked process list.

.EXAMPLE
    Audit\RunAudit.cmd -NewTab
.EXAMPLE
    Audit\RunAudit.cmd -Auditors truthfulness,process -Planted 2
#>
param(
    [string]$Ref = 'origin/work/dark-factory',
    [string[]]$Auditors = @('quality', 'security', 'performance', 'conformance', 'truthfulness', 'process'),
    [ValidateRange(1, 10)][int]$Planted = 8,
    [int]$Seed = -1,
    [switch]$DryRun,
    [switch]$AlongsideShift,
    [switch]$NewTab,
    [switch]$SelfTest
)

$ErrorActionPreference = 'Stop'
# <repo> is the main checkout, whichever worktree this copy runs from: the folder holding the
# shared .git, beside which the factory keeps <repo>.logs and the audit keeps <repo>.audit.
$commonGit = "$(& git -C $PSScriptRoot rev-parse --path-format=absolute --git-common-dir 2>$null)".Trim()
$repo = if ($commonGit) { Split-Path ($commonGit -replace '/', '\') -Parent } else { (Resolve-Path (Join-Path $PSScriptRoot '..')).Path }
$Order = @('quality', 'security', 'performance', 'conformance', 'truthfulness', 'process')
$Models = @{ quality = 'sonnet'; security = 'opus'; performance = 'sonnet'; conformance = 'opus'; truthfulness = 'sonnet'; process = 'sonnet'; seeder = 'sonnet' }
$Method = @{ quality = 'Quality'; security = 'Security'; performance = 'Performance'; conformance = 'Conformance'; truthfulness = 'Truthfulness'; process = 'Process' }
$Dot = [char]0x00B7
$Started = Get-Date
$Stamp = $Started.ToString('yyyyMMdd-HHmmss')
$Utf8 = New-Object Text.UTF8Encoding $false
$AuditRoot = "$repo.audit"
$RunDir = Join-Path $AuditRoot $Stamp
$script:CostUsd = 0.0

# ------------------------------------------------------------------ helpers

function Write-Step([string]$Text) {
    $line = "$(Get-Date -Format 'HH:mm:ss') $Text"
    Write-Host $line
    if (-not $DryRun -and (Test-Path -LiteralPath $RunDir)) { [IO.File]::AppendAllText((Join-Path $RunDir 'audit.log'), $line + "`r`n") }
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
    $herdr = Get-HerdrBin
    if (-not $herdr -or -not $env:HERDR_TAB_ID) { return }
    $label = "$(((& $herdr tab get $env:HERDR_TAB_ID) -join "`n" | ConvertFrom-Json).result.tab.label)"
    if ($label -notlike 'Audit *') { return }
    $caption = "Audit $($Started.ToString('HH:mm')) $Dot $Phase"
    if ($caption.Length -gt 48) { $caption = $caption.Substring(0, 47) + '~' }
    & $herdr tab rename $env:HERDR_TAB_ID $caption 2>&1 | Out-Null
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
    $psi.FileName = $env:ComSpec
    $psi.Arguments = "/d /c claude -p --agent $Name --model $Model --dangerously-skip-permissions --output-format stream-json --verbose > `"$stream`" 2> `"$stream.err`""
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

function Get-OpenFindings([string]$Findings, [string]$Auditor) {
    $lines = @()
    foreach ($f in @(Get-ChildItem -LiteralPath $Findings -Filter 'AF-*.md' -ErrorAction SilentlyContinue | Sort-Object Name)) {
        $t = [IO.File]::ReadAllText($f.FullName)
        if ($t -notmatch "(?m)^auditor: $Auditor\s*$" -or $t -notmatch '(?m)^status: (proposed|accepted|deferred|blocked)\s*$') { continue }
        $id = if ($t -match '(?m)^id: (\S+)') { $Matches[1] } else { continue }
        $title = if ($t -match '(?m)^title: (.+)$') { $Matches[1].Trim() } else { '' }
        $command = if ($t -match '(?s)```powershell\r?\n(.*?)```') { $Matches[1].Trim() } else { '' }
        $lines += "- $id - $title. Reproduction: $command"
    }
    return $lines
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
    Check '-AlongsideShift runs while a shift runs' ((Get-RefusalReason @($shift, $lane) '' $true) -eq '') 'none'
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
            $line = "powershell -NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`" " + ($forward -join ' ')
            & $herdr pane run $pane $line.Replace('"', '\"') 2>&1 | Out-Null
            Write-Host "Audit started in herdr tab $($created.result.tab.tab_id)."
            exit 0
        }
    }
    $p = Start-Process -FilePath 'powershell.exe' -ArgumentList (@('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$PSCommandPath`"") + $forward) -WorkingDirectory $repo -PassThru
    Write-Host "Audit started in a new console window (pid $($p.Id))."
    exit 0
}

# ------------------------------------------------------------------ 1. refusals

$processes = @(Get-CimInstance Win32_Process -ErrorAction SilentlyContinue | Select-Object CommandLine)
$refusal = Get-RefusalReason $processes $env:CURL_DARK_FACTORY_LANE $AlongsideShift.IsPresent
if ($refusal) { Write-Host $refusal; exit 1 }

$Auditors = @($Auditors | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim().ToLowerInvariant() } | Where-Object { $_ })
foreach ($a in $Auditors) { if ($Order -notcontains $a) { Write-Host "Unknown auditor '$a'. Auditors: $($Order -join ', ')."; exit 1 } }
$Selected = @($Order | Where-Object { $Auditors -contains $_ })
if ($Planted -lt $Selected.Count) { Write-Host "-Planted $Planted is fewer than the $($Selected.Count) auditors selected; each needs at least one."; exit 1 }
if ($Seed -lt 0) { $Seed = [int]($Started.Ticks % 1000000007) }

if ($DryRun) {
    Write-Host "Audit dry run, $Stamp. Would run, from ${repo}:"
    Write-Host "  2. git fetch origin; the worktree that has branch audit checked out, or git worktree add $AuditRoot\audit-branch audit (from origin/audit, else origin/master); git merge origin/master there"
    Write-Host "  3. git worktree add --detach $RunDir\planted $Ref; overlay Audit/Instructions, Audit/Tools, Audit/PlantedDefects and .claude/agents/audit-*.md from the audit branch; copy logs changed since the last scorecard from $repo.logs to $RunDir\logs; gh run list ... > $RunDir\logs\ci-runs.json"
    Write-Host "  4. claude -p --agent audit-seeder --model sonnet ... (seed $Seed, $Planted defects, manifest $RunDir\manifest.json)"
    foreach ($a in $Selected) { Write-Host "  5. claude -p --agent audit-$a --model $($Models[$a]) ... > $RunDir\reports\$a.md (scratch $RunDir\scratch\$a)" }
    Write-Host "  6. Write-AuditScorecard.ps1 -ReliabilityOnly; Write-AuditFindings.ps1; Write-AuditScorecard.ps1 (into the audit branch)"
    Write-Host "  7. git commit -m 'audit: scorecard $Stamp'; git push origin audit; gh pr create --base master --head audit (unless one is open; never merged here)"
    Write-Host "  8. git worktree remove --force $RunDir\planted"
    exit 0
}

New-Item -ItemType Directory -Force $RunDir, (Join-Path $RunDir 'reports'), (Join-Path $RunDir 'scratch'), (Join-Path $RunDir 'logs') | Out-Null
Write-Step "audit $Stamp of $Ref; auditors $($Selected -join ', '); $Planted planted; seed $Seed"
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
    $fingerprint = "$(& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $auditTree 'Audit\Tools\Get-AuditorFingerprint.ps1') -Root $auditTree)".Trim()
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
    $seedPrompt = "Seed an audit. Worktree: $plantedTree (detached at $auditedCommit). Log copy: $logs. Manifest path: $manifest. Seed: $Seed. Count: $Planted. Plant at least one defect for each of these auditors: $($Selected -join ', '), and only for them. Follow your instructions exactly; change nothing outside the worktree, the log copy and the manifest path."
    $null = Invoke-Agent 'audit-seeder' $Models.seeder $seedPrompt $auditTree (Join-Path $RunDir 'seeder.md')
    if (-not (Test-Path -LiteralPath $manifest)) { throw 'The seeder wrote no manifest.' }
    Write-Step "seeded: $(@((Get-Content -LiteralPath $manifest -Raw | ConvertFrom-Json).planted).Count) defects"

    # 5. the auditors
    $changedTree = @()
    foreach ($a in $Selected) {
        Set-Caption $a
        $scratch = Join-Path $RunDir "scratch\$a"
        New-Item -ItemType Directory -Force $scratch | Out-Null
        $open = @(Get-OpenFindings (Join-Path $auditTree 'Audit\Findings') $a)
        $reaudit = if ($open.Count) { "Re-audit these open findings by running each reproduction:`n" + ($open -join "`n") } else { 'There are no findings to re-audit.' }
        $logsLine = if ($a -eq 'process') { " The factory's log folder is $logs; the previous scorecard's date is $($since.ToString('yyyy-MM-dd'))." } else { '' }
        $prompt = "Audit the tree at $plantedTree (commit $auditedCommit; auditor fingerprint $fingerprint).$logsLine Your temporary folder is $scratch. Do every step of your method in full and report its method counts; the re-audits below come on top of it, never instead. $reaudit Follow Audit/Instructions/Auditor-Rules.md and Audit/Instructions/$($Method[$a]).md in that tree, and end with exactly one json report block."
        $exit = Invoke-Agent "audit-$a" $Models[$a] $prompt $plantedTree (Join-Path $RunDir "reports\$a.md")
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
    $unreliable = @(& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $tools 'Write-AuditScorecard.ps1') @common @changedArg -ReliabilityOnly | Where-Object { $_ })
    $unreliableArg = if ($unreliable.Count) { @('-Unreliable', ($unreliable -join ',')) } else { @() }
    $findingsLine = & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $tools 'Write-AuditFindings.ps1') -ReportDirectory $reports -Manifest $manifest -FindingsDirectory (Join-Path $auditTree 'Audit\Findings') -Scorecard $scorecardName -Commit $auditedCommit -Date $Started.ToString('yyyy-MM-dd') @unreliableArg
    Write-Step "$findingsLine; unreliable: $(if ($unreliable.Count) { $unreliable -join ', ' } else { 'none' })"
    $finished = Get-Date
    $modelList = ($Selected | ForEach-Object { "$_=$($Models[$_])" }) -join ','
    $scorecard = & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $tools 'Write-AuditScorecard.ps1') @common @changedArg -ScorecardsDirectory (Join-Path $auditTree 'Audit\Scorecards') -Commit $auditedCommit -Branch ($Ref -replace '^origin/', '') -Started $Started -Finished $finished -CostUsd $script:CostUsd -Models $modelList -Fingerprint $fingerprint
    Write-Step "scorecard $scorecard"

    # 7. commit, push, pull request
    Invoke-Git @('-C', $auditTree, 'add', '-A', '--', 'Audit/Findings', 'Audit/Scorecards') | Out-Null
    Invoke-Git @('-C', $auditTree, '-c', 'user.name=Audit runner', '-c', 'user.email=audit-runner@example.invalid', 'commit', '-q', '-m', "audit: scorecard $Stamp", '-m', "$findingsLine. Audited $auditedCommit; auditors $($Selected -join ', '); seed $Seed.") | Out-Null
    Invoke-Git @('-C', $auditTree, 'push', '-q', 'origin', 'audit') | Out-Null
    $openPr = "$(& gh pr list --head audit --base master --state open --json url --jq '.[0].url' 2>$null)".Trim()
    if (-not $openPr) {
        $table = ((Get-Content -LiteralPath $scorecard -Raw) -split '## Performance')[0]
        $body = Join-Path $RunDir 'pr-body.md'
        [IO.File]::WriteAllText($body, "Audit $Stamp of ``$auditedCommit`` ($Ref).`n`n$table`nAn interactive session merges this once CI is green on all three platforms (Stewart's standing exception, 2026-09-30); this runner never merges it. Stewart accepts or rejects each finding (Audit/Triage.md).`n", $Utf8)
        $openPr = "$(& gh pr create --base master --head audit --title "Audit $Stamp" --body-file $body 2>&1)".Trim()
    }
    Write-Step "pull request $openPr"
    Set-Caption 'done, read'
}
catch {
    Write-Step "audit failed: $($_.Exception.Message)"
    Set-Caption 'FAILED, read'
    $failedRun = $true
}
finally {
    # 8. clean up the planted tree; keep the run folder
    if (Test-Path -LiteralPath $plantedTree) { Invoke-Git @('-C', $repo, 'worktree', 'remove', '--force', $plantedTree) -AllowFailure | Out-Null }
    Write-Step "run folder $RunDir; cost $($script:CostUsd.ToString('0.00', [Globalization.CultureInfo]::InvariantCulture)) USD"
}
exit $(if ($failedRun) { 1 } else { 0 })
