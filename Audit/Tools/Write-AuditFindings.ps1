<#
.SYNOPSIS
    Turns the auditors' reports from one audit into finding files: new ones filed, repeats noted,
    re-audits recorded, and findings closed only by a confirming re-audit - or, for a finding
    about a past run's cost, once its tasks are Done.

.DESCRIPTION
    Called by RunAudit.ps1 (BL-1020) after the auditors reply. Formats and rules:
    Audit/Findings/README.md, Audit/Findings/FINDING-TEMPLATE.md and
    Audit/Instructions/Report-Format.md (BL-1001).

    For each auditor's report, in the fixed order quality, security, conformance, truthfulness,
    process (so new IDs are deterministic), it reads the last fenced json block of
    <auditor>.md in -ReportDirectory and applies these rules:

      catch       A reported finding from the same auditor as a planted defect in -Manifest,
                  whose location names the planted file and whose title, key or evidence holds
                  the manifest's catch text, is a catch, not a finding. It is never filed; it is
                  written to catches.json in -ReportDirectory for the scorecard (BL-1017).
      repeat      A reported finding whose key equals an open (proposed, accepted, deferred or
                  blocked) finding's key is the same finding: that file gains a Re-audits line
                  "still reported", its status is kept, and no new file is filed.
      rejected    A key equal to a rejected finding's key gains the same "still reported" line,
                  and its status stays rejected (Decided by Claude, 2026-09-30: otherwise every
                  audit re-files what Stewart turned down).
      reappeared  A key equal only to a closed finding's key is filed new, and its Summary says
                  "reappeared; previously AF-####". The closed file is not touched.
      new         Anything else is filed as the next AF-#### with status proposed. A finding from
                  an auditor in -Unreliable says so in its Summary.
      mutant key  A finding with reproduction.mutation (<file>:<line>:<operator>, from
                  Invoke-MutationTest.ps1) is matched and filed under the mechanical key
                  quality:<file>:<member>-<operator word>:surviving-mutant, the member read from
                  -Tree at that line, with "reproduction: mutation <file>:<line>:<operator>" and
                  the targeted -Site command as its reproduction (ADR-0422).
      clean new   A finding that would be filed new and has a mutation reproduction first gets the
                  runner's targeted rerun on the clean audited -Commit (-RerunReproductions). If
                  the mutant is killed there, it survived only in the planted tree - a plant's
                  effect, wherever the finding's location is - so it is set aside: listed in
                  set-aside.json with the reason (and the overlapping plant, if one is found) and
                  no AF-#### is written (BL-3502). One with no mutation reproduction whose
                  reproduction command is exactly one dotnet test <project> --filter "<filter>"
                  (a project folder in the tree; no other command is ever run) has that test rerun
                  on the clean -Commit, checked out in a throwaway worktree <repo>.audit\testrerun-*
                  that is removed afterwards; if the tests run and none fails, it failed only in
                  the planted tree and is set aside the same way (BL-3503: AF-0426). Survived,
                  still failing, unverified or any other command: filed as before.
      duplicate  Before anything else, open findings that share a key are one finding: all but
                  the lowest ID are closed with closed-how duplicate and duplicate-of, and their
                  tasks join the original's tasks list.
      historical  Next, before any report is read, an accepted finding whose key ends in
                  :cost-outlier (one past run's cost, which no fix can change) closes with
                  closed-how task-done-historical, closed-by this -Scorecard and a reason, once it
                  has at least one task and every one - its task, its tasks list, and every
                  BL-###-fix-/re-fix- task file naming it, as Test-AuditDue.ps1 counts them - is in
                  Tasks/Done at the audited -Commit (read with git ls-tree). It is not re-audited:
                  a re-audit of it in this audit's reports finds it closed and is skipped
                  (ADR-0422, amended 2026-10-14).
      rerun      A finding with a mutation reproduction re-audited by its own auditor has the
                  runner's targeted rerun on the clean audited -Commit (-RerunReproductions) first,
                  when the auditor says "no" or a planted defect overlaps it. No plant can touch
                  that rerun, so its answer stands: an own "no" with the mutant killed closes
                  (mechanical, below) and is never set aside; a survivor is recorded "reproduces:
                  yes (runner rerun on clean commit)". Its JSON and output stay in
                  <ReportDirectory>\reruns.
      set aside   A re-audit or repeat the clean rerun did not answer is recorded "not re-audited |
                  overlaps planted defect PD-###" and counts as neither yes nor no when the
                  auditor's evidence cites a planted file by name, or the finding's location is the
                  planted file, or - for a finding with a mutation reproduction only - its location
                  is in a project that depends on a planted defect's project (its .csproj references
                  in -Tree, transitively): auditors run their reproductions in the planted tree.
                  (ADR-0422, amended 2026-10-07: a dependency alone set aside every re-audit of
                  audit 20261007-133608.)
      re-audit    Each other reaudits entry appends a Re-audits line to the finding it names (one
                  from another auditor marked "(re-audited by <auditor>)"). An open finding whose
                  own auditor says it no longer reproduces is closed (status, reason, closed,
                  closed-how, closed-by and a Log line) by the first of:
                    mechanical        it has a mutation reproduction and the runner's targeted
                                      rerun on the clean audited -Commit kills the mutant
                                      (-RerunReproductions), whatever -Unreliable says and
                                      whatever plant overlaps it;
                    reliable-reaudit  no mechanical answer (none, or the rerun could not tell)
                                      and the auditor is not in -Unreliable;
                    consecutive       no mechanical answer and the finding's previous Re-audits
                                      line is its own auditor's "no" from a different audit;
                                      closed-by names both scorecards.
                  A rerun in which the mutant survives closes nothing. Nothing else closes a
                  finding - not its task's state (outside the historical rule above), and not an
                  audit that did not report it. Closed
                  findings are never changed. Re-audits lines go at the end of Re-audits, before
                  Log (BL-1183).

    Prints one summary line: new, still open, closed, catches.

.PARAMETER ReportDirectory
    Holds <auditor>.md for each auditor that ran: its full reply.

.PARAMETER Manifest
    The seeder's manifest (BL-1015). Optional: without it there are no catches.

.PARAMETER Unreliable
    Auditor names flagged unreliable on this audit's scorecard (BL-1017 computes them).

.PARAMETER Scorecard
    This audit's scorecard file name, e.g. 2026-10-14_0930.md.

.PARAMETER Commit
    The audited commit. Its Tasks folder, read with git, is the board the historical rule checks.

.PARAMETER Date
    The audit's date, yyyy-MM-dd. Default today.

.PARAMETER FindingsDirectory
    Default: Audit/Findings in this repository.

.PARAMETER Tree
    The audited (planted) tree: where a mutant's member and the projects' references are read.

.PARAMETER RerunReproductions
    Rerun each mechanical reproduction the closure rule needs, with Invoke-MutationTest.ps1 -Site
    on -Commit (the clean audited commit, not the planted tree), and each new finding's single
    dotnet test --filter command on -Commit (the clean new rule). Without it each rerun reads as
    unverified: the other closure paths apply, and new findings are filed.

.PARAMETER SelfTest
    Run against Audit/Tools/Fixtures/findings and Fixtures/closure, copied to a temporary folder,
    and check every rule.
#>
param(
    [string]$ReportDirectory,
    [string]$Manifest,
    [string[]]$Unreliable = @(),
    [string]$Scorecard,
    [string]$Commit,
    [string]$Date = (Get-Date -Format 'yyyy-MM-dd'),
    [string]$FindingsDirectory,
    [string]$Tree,
    [switch]$RerunReproductions,
    [switch]$SelfTest
)
# Finding ID -> a mechanical outcome to use instead of rerunning; only the self-test sets it.
$MechanicalOutcomes = @{}
# Finding key -> a failing-test rerun outcome to use instead of rerunning; only the self-test sets it.
$TestOutcomes = @{}

$ErrorActionPreference = 'Stop'
# Windows PowerShell 5.1 on Windows, PowerShell 7 elsewhere (BL-3927).
$PowerShellExe = if ($PSVersionTable.PSVersion.Major -lt 6 -or $IsWindows) { 'powershell' } else { 'pwsh' }
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path

function ConvertTo-NameList([string[]]$Names) {
    # RunAudit.ps1 passes the list through powershell -File as one comma-joined argument,
    # which arrives as a single string: split it, or 'quality,process' matches neither
    # auditor and an unreliable one closes findings (BL-1244).
    return @($Names | ForEach-Object { "$_" -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ })
}
$Unreliable = ConvertTo-NameList $Unreliable
$AuditorOrder = @('quality', 'security', 'conformance', 'truthfulness', 'process')
$Utf8 = New-Object Text.UTF8Encoding $false

function Get-ReportBlock([string]$Path) {
    $text = [IO.File]::ReadAllText($Path)
    $blocks = [regex]::Matches($text, '(?s)```json\r?\n(.*?)```')
    if ($blocks.Count -eq 0) { return $null }
    return $blocks[$blocks.Count - 1].Groups[1].Value | ConvertFrom-Json
}

function Read-Finding([string]$Path) {
    $text = [IO.File]::ReadAllText($Path)
    $fields = @{}
    if ($text -match '(?s)^---\r?\n(.*?)\r?\n---') {
        foreach ($line in ($Matches[1] -split '\r?\n')) {
            if ($line -match '^([a-z-]+):\s?(.*)$') { $fields[$Matches[1]] = $Matches[2].Trim() }
        }
    }
    $location = if ($text -match '(?m)^Location: `([^`]*)`') { $Matches[1] } else { '' }
    return [pscustomobject]@{ Path = $Path; Id = $fields['id']; Key = $fields['key']; Status = $fields['status']; Auditor = $fields['auditor']; Reproduction = "$($fields['reproduction'])"; Tasks = "$($fields['tasks'])"; Location = $location }
}

# --- planted defects overlapping a finding (ADR-0422) -----------------------------------------

function Get-ProjectClosure([string]$Project) {
    # The project, its .UnitLibrary/.UnitTests twin, and every project they reference, transitively,
    # as the audited tree (-Tree) has them: a planted defect in any of them can change what the
    # project's tests and reproductions show.
    $seen = @{}
    $queue = New-Object System.Collections.Queue
    foreach ($p in @($Project, ($Project -replace '\.UnitTests$', '.UnitLibrary'), ($Project -replace '\.UnitLibrary$', '.UnitTests'))) { $queue.Enqueue($p) }
    while ($queue.Count) {
        $p = $queue.Dequeue()
        if ($seen.ContainsKey($p)) { continue }
        $seen[$p] = $true
        $csproj = if ($Tree) { Join-Path $Tree "$p\$p.csproj" } else { '' }
        if (-not $csproj -or -not (Test-Path -LiteralPath $csproj)) { continue }
        foreach ($m in [regex]::Matches([IO.File]::ReadAllText($csproj), 'ProjectReference\s+Include="[^"]*?([^"\\/]+)\.csproj"')) { $queue.Enqueue($m.Groups[1].Value) }
    }
    return @($seen.Keys)
}

function Get-OverlappingPlant($Target, [string]$Evidence, [object[]]$Planted, [bool]$ProjectClosure) {
    # The planted defect that may have produced this audit's verdict on the finding, or $null: one
    # whose file name the auditor's evidence cites, or one in the very file the finding's location
    # names. Auditors re-audit in the planted tree (BL-1597's audit: AF-0009 and AF-0026
    # "reproduced" because of PD-303 and PD-103). With -ProjectClosure (a finding with a mechanical
    # reproduction, whose mutant any test in the project's closure can kill or miss) also one in a
    # project the finding's location depends on (Get-ProjectClosure); the caller then lets the
    # runner's clean rerun, which no plant can touch, decide instead (ADR-0422, amended 2026-10-07).
    foreach ($p in $Planted) {
        $file = "$($p.file)" -replace '\\', '/'
        $name = $file.Substring($file.LastIndexOf('/') + 1)
        if ($name -and $Evidence.IndexOf($name, [StringComparison]::OrdinalIgnoreCase) -ge 0) { return $p }
        if ($file -notmatch '/') { continue }
        $plantProject = ($file -split '/')[0]
        foreach ($part in ("$($Target.Location)" -split ',\s*')) {
            if (Test-SamePath (ConvertTo-Normalised $part) $file) { return $p }
            if (-not $ProjectClosure) { continue }
            $project = (($part -replace '\\', '/').Trim() -split '/')[0]
            if ($project -and (@(Get-ProjectClosure $project) -contains $plantProject)) { return $p }
        }
    }
    return $null
}

function Add-SetAsideLine($Target, $Plant, [string]$Verdict, [string]$Evidence) {
    Add-ReauditLine $Target.Path "- $Date | $Scorecard | not re-audited | overlaps planted defect $($Plant.id) in $($Plant.file), so the auditor's verdict (reproduces $Verdict) is set aside: $Evidence"
}

# The front matter keys in template order (Findings/README.md). Findings filed before ADR-0422 lack
# reproduction, tasks, duplicate-of and closed-how; Set-FrontMatter adds one in its place when set.
$FrontMatterOrder = @('id', 'title', 'auditor', 'severity', 'status', 'reason', 'key', 'reproduction', 'task', 'tasks', 'found', 'found-at', 'scorecard', 'duplicate-of', 'closed', 'closed-how', 'closed-by')

function Set-FrontMatter([string]$Text, [string]$Name, [string]$Value) {
    $line = "${Name}: $Value".TrimEnd()
    $existing = [regex]::new("(?m)^$([regex]::Escape($Name)):[^\r\n]*")
    if ($existing.IsMatch($Text)) { return $existing.Replace($Text, $line.Replace('$', '$$'), 1) }
    $newline = if ($Text.Contains("`r`n")) { "`r`n" } else { "`n" }
    $before = @($FrontMatterOrder[0..([array]::IndexOf($FrontMatterOrder, $Name) - 1)])
    [array]::Reverse($before)
    foreach ($previous in $before) {
        $at = [regex]::Match($Text, "(?m)^$([regex]::Escape($previous)):[^\r\n]*")
        if ($at.Success) { return $Text.Insert($at.Index + $at.Length, $newline + $line) }
    }
    return $Text
}

# --- surviving mutants: a mechanical key and reproduction (ADR-0422) ---------------------------

# Operator names from Invoke-MutationTest.ps1, as words a key may hold.
$OperatorWords = @{ '==' = 'eq'; '!=' = 'ne'; '<' = 'lt'; '>' = 'gt'; '<=' = 'le'; '>=' = 'ge'; '&&' = 'and'; '||' = 'or'; '+1' = 'plus1'; '-1' = 'minus1'; 'true' = 'true'; 'false' = 'false'; '!(' = 'not' }

# A member declaration; the same rule as Invoke-MutationTest.ps1's $MemberPattern.
$MemberPattern = '^\s*(?:(?:public|private|protected|internal|static|async|override|virtual|sealed|abstract|extern|unsafe|new|partial|readonly|required|file)\s+)+[^=;(){}]*?\b([A-Za-z_]\w*)\s*(?:<[^<>]*(?:<[^<>]*>[^<>]*)*>)?\s*(?:\(|\{|=>|=(?![=>]))'

function Get-MutationSite([string]$Text) {
    # "<file>:<line>:<operator>" (a report's reproduction.mutation) or "mutation <file>:<line>:<operator>"
    # (a finding's reproduction field) -> File, Line, Operator; $null for anything else.
    if ("$Text".Trim() -notmatch '^(?:mutation\s+)?(?<file>[^\s:]+\.cs):(?<line>\d+):(?<op>\S+)$') { return $null }
    if (-not $OperatorWords.ContainsKey($Matches['op'])) { return $null }
    return [pscustomobject]@{ File = ($Matches['file'] -replace '\\', '/'); Line = [int]$Matches['line']; Operator = $Matches['op'] }
}

function Get-SiteMember($Site, [string]$ReportedKey) {
    # The member the site is in: from the audited tree (-Tree) when it has the file, otherwise
    # the <what> of the auditor's key without an operator suffix, letters and digits only.
    $path = if ($Tree) { Join-Path $Tree ($Site.File -replace '/', '\') } else { '' }
    if ($path -and (Test-Path -LiteralPath $path)) {
        $lines = [IO.File]::ReadAllLines($path)
        for ($i = [math]::Min($Site.Line, $lines.Length) - 1; $i -ge 0; $i--) { if ($lines[$i] -match $MemberPattern) { return $Matches[1] } }
        return '-'
    }
    $what = @("$ReportedKey" -split ':')
    $name = if ($what.Count -ge 4) { $what[$what.Count - 2] } else { '-' }
    return (($name -split '-')[0] -replace '[^A-Za-z0-9_]', '')
}

function Get-MutantKey([string]$Auditor, $Site, [string]$Member) {
    return "${Auditor}:$($Site.File):$Member-$($OperatorWords[$Site.Operator]):surviving-mutant"
}

function Get-MutantCommand($Site, [string]$Member) {
    return "powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site $($Site.File):$($Site.Line):$($Site.Operator) -Member $Member -ExcludeBaselineFailures -TimeoutSeconds 600"
}

function ConvertTo-FiledFinding($Finding, [string]$Auditor) {
    # A reported finding as it is matched and filed. One with reproduction.mutation gets the
    # mechanical key, the targeted reproduction command and a reproduction field; so the same
    # mutant reported in other words is the same finding (ADR-0422).
    $site = Get-MutationSite "$($Finding.reproduction.mutation)"
    $filed = [pscustomobject]@{ Key = "$($Finding.key)"; Reproduction = 'none'; Command = "$($Finding.reproduction.command)" }
    if (-not $site) { return $filed }
    $member = Get-SiteMember $site "$($Finding.key)"
    $filed.Key = Get-MutantKey $Auditor $site $member
    $filed.Reproduction = "mutation $($site.File):$($site.Line):$($site.Operator)"
    $filed.Command = Get-MutantCommand $site $member
    return $filed
}

# --- closing (ADR-0422) -----------------------------------------------------------------------

function Get-ReauditLines([string]$Text) {
    # The Re-audits lines, oldest first: Date, Scorecard, Reproduces, Evidence, Own (written for the
    # finding's own auditor: every line not marked "(re-audited by <auditor>)"). A verdict may carry
    # a note: "reproduces: yes (runner rerun on clean commit)".
    $section = if ($Text -match '(?s)## Re-audits[ \t]*\r?\n(.*?)(\r?\n## |\z)') { $Matches[1] } else { '' }
    $lines = @()
    foreach ($line in ($section -split '\r?\n')) {
        if ($line -notmatch '^- (\S+) \| (\S+) \| reproduces: (yes|no)(?: \([^)|]*\))? \| ?(.*)$') { continue }
        $lines += [pscustomobject]@{ Date = $Matches[1]; Scorecard = $Matches[2]; Reproduces = ($Matches[3] -eq 'yes'); Evidence = $Matches[4]; Own = ($Matches[4] -notlike '(re-audited by *') }
    }
    return $lines
}

function Get-ConsecutiveNo([string]$Text, [string]$ScorecardName) {
    # The scorecard of the previous Re-audits line when it, too, is the finding's own auditor saying
    # "reproduces: no" on a different audit than this one; otherwise ''. Called after this audit's
    # line was added, so that line is the last.
    $lines = @(Get-ReauditLines $Text)
    if ($lines.Count -lt 2) { return '' }
    $previous = $lines[$lines.Count - 2]
    if ($previous.Own -and -not $previous.Reproduces -and $previous.Scorecard -ne $ScorecardName) { return $previous.Scorecard }
    return ''
}

function Invoke-MechanicalReproduction($Target) {
    # Reruns a finding's mechanical reproduction on the clean audited commit (Invoke-MutationTest.ps1
    # checks -Commit out in a throwaway worktree; the planted tree is never used): 'killed' (the
    # targeted mutant is killed or timed out: fixed), 'survived' (still reproduces), 'unverified'
    # (not run, or it could not tell), or 'none' (the finding has no mechanical reproduction). The
    # tool's JSON and output stay in <ReportDirectory>\reruns\<id>.json and .log, and one line per
    # rerun in reruns\outcomes.txt.
    $site = Get-MutationSite $Target.Reproduction
    if (-not $site) { return 'none' }
    foreach ($stub in @($Target.Id, $Target.Key)) { if ($stub -and $MechanicalOutcomes.ContainsKey($stub)) { return $MechanicalOutcomes[$stub] } }
    if (-not $RerunReproductions) { return 'unverified' }
    $member = if ($Target.Key -match ':([A-Za-z0-9_]+)-[a-z0-9]+:surviving-mutant$') { $Matches[1] } else { '' }
    $reruns = Join-Path $ReportDirectory 'reruns'
    New-Item -ItemType Directory -Force $reruns | Out-Null
    $out = Join-Path $reruns "$($Target.Id).json"
    $log = Join-Path $reruns "$($Target.Id).log"
    Remove-Item -LiteralPath $out -ErrorAction SilentlyContinue
    $arguments = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $PSScriptRoot 'Invoke-MutationTest.ps1'), '-Site', "$($site.File):$($site.Line):$($site.Operator)", '-Commit', $Commit, '-ExcludeBaselineFailures', '-TimeoutSeconds', '600', '-OutFile', $out)
    if ($member) { $arguments += @('-Member', $member) }
    $ErrorActionPreference = 'Continue'
    & $PowerShellExe @arguments *> $log
    $outcome = 'unverified'
    $raw = ''
    if (Test-Path -LiteralPath $out) {
        $result = Get-Content -LiteralPath $out -Raw | ConvertFrom-Json
        $raw = "$($result.outcome)"
        $outcome = switch ($raw) { 'killed' { 'killed' } 'timedOut' { 'killed' } 'survived' { 'survived' } default { 'unverified' } }
    }
    # Not to the console: RunAudit.ps1 reads this script's output as its one summary line.
    [IO.File]::AppendAllText((Join-Path $reruns 'outcomes.txt'), "$($Target.Id) $($site.File):$($site.Line):$($site.Operator) on $Commit`: $outcome$(if ($raw -and $raw -ne $outcome) { " ($raw)" })`r`n", $Utf8)
    return $outcome
}

# --- failing-test reproductions on the clean commit (BL-3503) ---------------------------------

function Get-TestCommand([string]$Command) {
    # A reproduction command that is exactly one `dotnet test <project> --filter "<filter>"` ->
    # Project, Filter; $null for anything else, so no other command an auditor wrote is ever run.
    # The project is a folder name (optionally <name>/<name>.csproj): letters, digits, _ and dots,
    # never a path out of the tree. The filter is one double-quoted string with no quote in it.
    $pattern = '^dotnet\s+test\s+(?<project>[A-Za-z0-9_]+(?:\.[A-Za-z0-9_]+)*)(?:[\\/](?:\k<project>\.csproj)?)?\s+--filter\s+"(?<filter>[^"\r\n`$]+)"$'
    if ("$Command".Trim() -notmatch $pattern) { return $null }
    return [pscustomobject]@{ Project = $Matches['project']; Filter = $Matches['filter'] }
}

function Get-TrxCounts([string]$TrxPath) {
    # Executed and failed counts from a trx log's ResultSummary, or $null when there is none.
    if (-not (Test-Path -LiteralPath $TrxPath)) { return $null }
    [xml]$trx = [IO.File]::ReadAllText($TrxPath)
    $counters = $trx.TestRun.ResultSummary.Counters
    if (-not $counters) { return $null }
    return [pscustomobject]@{ Executed = [int]$counters.executed; Failed = [int]$counters.failed + [int]$counters.error + [int]$counters.timeout }
}

function Invoke-TestReproduction($Target) {
    # Reruns a new finding's failing-test command on the clean audited commit, checked out in a
    # throwaway worktree <repo>.audit\testrerun-<stamp> beside the repository and removed afterwards
    # (as Invoke-MutationTest.ps1 does; the planted tree is never used): 'passed' (the tests ran and
    # none failed: the failure was the planted tree's), 'failed' (still fails), 'unverified' (not
    # run, or it could not tell: no such project, a build failure, a timeout, no test matched), or
    # 'none' (the command is not one `dotnet test <project> --filter "..."`). Its output and trx
    # stay in <ReportDirectory>\reruns\<id>.log and .trx, one line per rerun in reruns\outcomes.txt.
    $test = Get-TestCommand $Target.Command
    if (-not $test) { return 'none' }
    if ($Target.Key -and $TestOutcomes.ContainsKey($Target.Key)) { return $TestOutcomes[$Target.Key] }
    if (-not $RerunReproductions) { return 'unverified' }
    $reruns = Join-Path $ReportDirectory 'reruns'
    New-Item -ItemType Directory -Force $reruns | Out-Null
    $log = Join-Path $reruns "$($Target.Id).log"
    $trxPath = Join-Path $reruns "$($Target.Id).trx"
    Remove-Item -LiteralPath $trxPath -ErrorAction SilentlyContinue
    $outcome = 'unverified'
    $detail = ''
    $ErrorActionPreference = 'Continue'
    $sha = "$(git -C $repo rev-parse --verify "$Commit^{commit}" 2>$null)".Trim()
    $workRoot = "$repo.audit"
    $worktree = Join-Path $workRoot "testrerun-$(Get-Date -Format 'yyyyMMdd-HHmmss')-$([guid]::NewGuid().ToString('N').Substring(0, 6))"
    try {
        if (-not $sha) { $detail = "$Commit is not a commit"; return $outcome }
        New-Item -ItemType Directory -Force $workRoot | Out-Null
        & git -C $repo worktree add --detach $worktree $sha *> $log
        if ($LASTEXITCODE -ne 0) { $detail = 'git worktree add failed'; return $outcome }
        $project = Join-Path $worktree "$($test.Project)\$($test.Project).csproj"
        if (-not (Test-Path -LiteralPath $project)) { $detail = "no $($test.Project) at $sha"; return $outcome }
        $out = [IO.Path]::GetTempFileName(); $err = [IO.Path]::GetTempFileName()
        $arguments = @('test', "`"$project`"", '-p:TreatWarningsAsErrors=false', '--filter', "`"$($test.Filter)`"", '--logger', "`"trx;LogFileName=$trxPath`"", '-nologo')
        $p = Start-Process -FilePath 'dotnet' -ArgumentList $arguments -WorkingDirectory $worktree -NoNewWindow -PassThru -RedirectStandardOutput $out -RedirectStandardError $err
        $null = $p.Handle
        if (-not $p.WaitForExit(1800 * 1000)) {
            if ($env:OS -eq 'Windows_NT') { & taskkill /PID $p.Id /T /F 2>&1 | Out-Null } else { $p.Kill() }
            $p.WaitForExit()
            $detail = 'timed out'
        }
        [IO.File]::AppendAllText($log, "$(Get-Content $out -Raw)`n$(Get-Content $err -Raw)", $Utf8)
        Remove-Item $out, $err -ErrorAction SilentlyContinue
        $counts = if ($detail) { $null } else { Get-TrxCounts $trxPath }
        if ($counts -and $counts.Failed -gt 0) { $outcome = 'failed' }
        elseif ($counts -and $counts.Executed -gt 0 -and $p.ExitCode -eq 0) { $outcome = 'passed' }
        elseif (-not $detail) { $detail = if ($counts) { "executed $($counts.Executed), exit $($p.ExitCode)" } else { "no test results, exit $($p.ExitCode)" } }
        return $outcome
    }
    finally {
        & git -C $repo worktree remove --force $worktree *> $null
        if (Test-Path -LiteralPath $worktree) { Remove-Item -Recurse -Force -LiteralPath $worktree -ErrorAction SilentlyContinue; & git -C $repo worktree prune *> $null }
        # Not to the console: RunAudit.ps1 reads this script's output as its one summary line.
        [IO.File]::AppendAllText((Join-Path $reruns 'outcomes.txt'), "$($Target.Id) dotnet test $($test.Project) --filter `"$($test.Filter)`" on $Commit`: $outcome$(if ($detail) { " ($detail)" })`r`n", $Utf8)
    }
}

function Get-ClosePath($Target, [string]$Auditor, [bool]$Flagged, [string]$Mechanical) {
    # How a "reproduces: no" re-audit closes an open finding, or $null when it does not: its own
    # auditor's re-audit, then, in this order, the runner's mechanical rerun agreeing (whatever the
    # auditor's reliability), a reliable auditor (findings with no mechanical answer), or two
    # consecutive "no" re-audits on two different audits. A mechanical "survived" closes nothing.
    if ($Target.Auditor -ne $Auditor -or $Target.Status -notin 'proposed', 'accepted', 'deferred', 'blocked') { return $null }
    if ($Mechanical -eq 'killed') { return [pscustomobject]@{ How = 'mechanical'; By = $Scorecard } }
    if ($Mechanical -eq 'survived') { return $null }
    if (-not $Flagged) { return [pscustomobject]@{ How = 'reliable-reaudit'; By = $Scorecard } }
    $previous = Get-ConsecutiveNo ([IO.File]::ReadAllText($Target.Path)) $Scorecard
    if ($previous) { return [pscustomobject]@{ How = 'consecutive'; By = "$previous, $Scorecard" } }
    return $null
}

function Close-Finding($Target, $Path, [string]$Reason) {
    $text = [IO.File]::ReadAllText($Target.Path)
    $text = Set-FrontMatter $text 'status' 'closed'
    $text = Set-FrontMatter $text 'reason' $Reason
    $text = Set-FrontMatter $text 'closed' $Date
    $text = Set-FrontMatter $text 'closed-how' $Path.How
    $text = Set-FrontMatter $text 'closed-by' $Path.By
    [IO.File]::WriteAllText($Target.Path, $text, $Utf8)
    Add-LogLine $Target.Path "- ${Date}: $($Target.Status) -> closed. $Reason"
    $Target.Status = 'closed'
}

function Close-Duplicates([object[]]$Findings) {
    # Open findings with the same key are one finding: every one but the lowest ID is closed as a
    # duplicate of it (closed-how: duplicate, duplicate-of), and its tasks join the original's
    # tasks list. Returns how many were closed.
    $closed = 0
    $open = @($Findings | Where-Object { $_.Status -in 'proposed', 'accepted', 'deferred', 'blocked' } | Sort-Object Id)
    foreach ($group in @($open | Group-Object Key | Where-Object { $_.Count -gt 1 })) {
        $original = $group.Group[0]
        foreach ($dup in @($group.Group | Select-Object -Skip 1)) {
            $text = Set-FrontMatter ([IO.File]::ReadAllText($dup.Path)) 'duplicate-of' $original.Id
            [IO.File]::WriteAllText($dup.Path, $text, $Utf8)
            Add-FindingTasks $original (Get-FindingTasks $dup)
            Close-Finding $dup ([pscustomobject]@{ How = 'duplicate'; By = $Scorecard }) "Duplicate of $($original.Id): the same key."
            $closed++
        }
    }
    return $closed
}

# --- historical findings: a past run's cost (ADR-0422, amended 2026-10-14) --------------------

# A key ending in this suffix names one past run's cost, which no later change can alter.
$HistoricalKeySuffix = ':cost-outlier'
# The board's task files at -Commit, as git paths; read once. Only the self-test sets it first.
$BoardTaskPaths = $null

function Get-BoardTaskPaths {
    # Every BL-*.md task file under Tasks/ at the audited -Commit (archives included), from git, so
    # the board is the audited commit's whatever the planted tree holds. Empty when git cannot tell.
    if ($null -ne $script:BoardTaskPaths) { return $script:BoardTaskPaths }
    $ErrorActionPreference = 'Continue'
    $out = @(& git -C $repo ls-tree -r --name-only $Commit -- Tasks 2>$null)
    if ($LASTEXITCODE -ne 0) { $out = @() }
    $script:BoardTaskPaths = @($out | ForEach-Object { "$_" -replace '\\', '/' } | Where-Object { $_ -match '/BL-\d+-[^/]*\.md$' })
    return $script:BoardTaskPaths
}

function Get-DoneHistoricalTasks($Finding, [string[]]$TaskPaths) {
    # The tasks of an accepted finding whose key ends in :cost-outlier, when there is at least one
    # and every one is in Tasks/Done (or an archive under it) at the audited commit; otherwise an
    # empty list. Its tasks are counted as Test-AuditDue.ps1 counts them: its task, its tasks list,
    # and every BL-###-fix-... or BL-###-re-fix-... task file naming it.
    if ($Finding.Status -ne 'accepted' -or -not "$($Finding.Key)".EndsWith($HistoricalKeySuffix)) { return @() }
    $names = @($TaskPaths | ForEach-Object { $_.Substring($_.LastIndexOf('/') + 1) })
    $slug = "$($Finding.Id)".ToLowerInvariant()
    $ids = @(Get-FindingTasks $Finding)
    $ids += @($names | Where-Object { $_ -match '^BL-\d+-(re-)?fix-' -and $_ -match "\b$slug\b" } | ForEach-Object { ($_ -split '-')[0..1] -join '-' })
    $ids = @($ids | Where-Object { $_ } | Sort-Object { [int]($_ -replace '\D', '') } -Unique)
    if (-not $ids.Count) { return @() }
    foreach ($id in $ids) {
        $path = @($TaskPaths | Where-Object { $_.Substring($_.LastIndexOf('/') + 1) -like "$id-*" } | Select-Object -First 1)
        if (-not $path.Count -or $path[0] -notmatch '^Tasks/Done/') { return @() }
    }
    return $ids
}

function Close-HistoricalFindings([object[]]$Findings) {
    # A finding about one past run's cost (key ...:cost-outlier) describes history, so every re-audit
    # would say it still reproduces and file a Re-fix task with nothing to fix. Its task is the only
    # evidence there can be: once every task is Done at the audited commit, it closes here, before
    # any re-audit, with closed-how task-done-historical (Stewart, 2026-10-14). Returns how many.
    $closed = 0
    $paths = @(Get-BoardTaskPaths)
    foreach ($finding in @($Findings | Sort-Object Id)) {
        $ids = @(Get-DoneHistoricalTasks $finding $paths)
        if (-not $ids.Count) { continue }
        Add-FindingTasks $finding $ids
        $at = if ("$Commit".Length -gt 12) { "$Commit".Substring(0, 12) } else { "$Commit" }
        Close-Finding $finding ([pscustomobject]@{ How = 'task-done-historical'; By = $Scorecard }) "Audit $Scorecard`: a cost-outlier finding records a past run's cost, which no fix can change; its tasks ($($ids -join ', ')) are Done at $at, so it closes without a re-audit (ADR-0422, amended 2026-10-14)."
        $closed++
    }
    return $closed
}

function Get-FindingTasks($Finding) {
    # Every task filed for the finding: its tasks list, and its task when that is not in it.
    $text = [IO.File]::ReadAllText($Finding.Path)
    $all = @()
    if ($text -match '(?m)^tasks:[ \t]*([^\r\n]*)') { $all += @($Matches[1] -split ',\s*' | Where-Object { $_ -match '^BL-\d+$' }) }
    if ($text -match '(?m)^task:[ \t]*(BL-\d+)') { $all += $Matches[1] }
    return @($all | Select-Object -Unique)
}

function Add-FindingTasks($Finding, [string[]]$Ids) {
    $all = @(@(Get-FindingTasks $Finding) + @($Ids) | Where-Object { $_ } | Sort-Object { [int]($_ -replace '\D', '') } -Unique)
    if (-not $all.Count) { return }
    $text = Set-FrontMatter ([IO.File]::ReadAllText($Finding.Path)) 'tasks' ($all -join ', ')
    [IO.File]::WriteAllText($Finding.Path, $text, $Utf8)
}

function Add-ReauditLine([string]$Path, [string]$Line) {
    # Re-audits comes before Log (BL-1183), so the line goes at the end of Re-audits, not
    # the end of the file. A finding written before Log existed has no Log to step over.
    $text = [IO.File]::ReadAllText($Path).TrimEnd()
    $newline = if ($text.Contains("`r`n")) { "`r`n" } else { "`n" }
    $log = ''
    $logHeading = [regex]::Match($text, '(?m)^## Log[ \t]*\r?$')
    if ($logHeading.Success) { $log = $text.Substring($logHeading.Index); $text = $text.Substring(0, $logHeading.Index).TrimEnd() }
    if ($text -match '## Re-audits$') { $text += $newline + $newline + $Line } else { $text += $newline + $Line }
    if ($log) { $text += $newline + $newline + $log }
    [IO.File]::WriteAllText($Path, $text + $newline, $Utf8)
}

function Add-LogLine([string]$Path, [string]$Line) {
    # Log is the last section, append-only, one dated line per status move (BL-1183). A
    # finding written before Log existed gains the section with this line.
    $text = [IO.File]::ReadAllText($Path).TrimEnd()
    $newline = if ($text.Contains("`r`n")) { "`r`n" } else { "`n" }
    if ($text -notmatch '(?m)^## Log[ \t]*\r?$') { $text += $newline + $newline + '## Log' }
    if ($text -match '## Log$') { $text += $newline + $newline + $Line } else { $text += $newline + $Line }
    [IO.File]::WriteAllText($Path, $text + $newline, $Utf8)
}

function Get-Slug([string]$Title) {
    $slug = ($Title.ToLowerInvariant() -replace '[^a-z0-9]+', '-').Trim('-')
    if ($slug.Length -gt 50) { $slug = $slug.Substring(0, 50).TrimEnd('-') }
    return $slug
}

function ConvertTo-Normalised([string]$Path) { return ("$Path" -replace '\\', '/' -replace ':\d+(-\d+)?$', '').Trim() }

function Test-SamePath([string]$Location, [string]$Planted) {
    # The finding's location names the planted file: the same path, or one ending with it (a
    # process defect's file is relative to the log copy, which auditors cite as logs/...).
    if (-not $Location -or -not $Planted) { return $false }
    # Or the planted file's base name alone: an ADR or script cited by its file name (BL-1370).
    return ($Location -ieq $Planted) -or $Location.EndsWith('/' + $Planted, [StringComparison]::OrdinalIgnoreCase) -or
        ($Planted.Contains('/') -and $Location -ieq $Planted.Substring($Planted.LastIndexOf('/') + 1))
}

function Test-AtPlantedLine([string]$Location, $Planted) {
    # The finding's location (file:line or file:first-last) reaches within 2 lines of the
    # planted line. A defect reported twice for two symptoms - slow and memory-hungry, say -
    # names its catch text in one report only; the other still points at the planted line
    # (BL-1316). 2 lines, not more, so a real finding beside a planted one stays a finding.
    if (-not "$($Planted.line)" -or "$Location" -notmatch ':(\d+)(?:-(\d+))?$') { return $false }
    $first = [int]$Matches[1]
    $last = if ($Matches[2]) { [int]$Matches[2] } else { $first }
    $line = [int]$Planted.line
    return ($line -ge $first - 2) -and ($line -le $last + 2)
}

function Test-LogPlant($Planted) {
    # A process plant edits one file of the log copy (a lane trace, a run log, ci-runs.json),
    # named bare, with no project folder. A finding about it names the task or run it shows,
    # rarely the one file the seeder edited, so its catch text alone identifies it (BL-1365).
    return ("$($Planted.auditor)" -eq 'process') -and ("$($Planted.file)" -notmatch '[\\/]')
}
function Test-Catch($Finding, [string]$Auditor, [object[]]$Planted) {
    # A catch: the planted defect's own auditor, in the defect's file, with the manifest's catch
    # fragment in the finding's title, key or evidence, or with a location at the planted line.
    # The same rule as Write-AuditScorecard.ps1.
    foreach ($p in $Planted) {
        if ($p.auditor -ne $Auditor) { continue }
        $sameFile = Test-SamePath (ConvertTo-Normalised $Finding.location) (ConvertTo-Normalised $p.file)
        $haystack = "$($Finding.title) $($Finding.key) $($Finding.evidence)"
        $catchText = "$($p.catch)".Trim()
        $named = $catchText -and $haystack.IndexOf($catchText, [StringComparison]::OrdinalIgnoreCase) -ge 0
        if (Test-LogPlant $p) { if ($named) { return $p } else { continue } }
        if ($sameFile -and ($named -or (Test-AtPlantedLine "$($Finding.location)" $p))) { return $p }
    }
    return $null
}

function Get-PlantInFindingFile($Finding, [string]$Auditor, [object[]]$Planted) {
    # A plant of another auditor in the very file the finding's location names, or $null. The
    # caller has already ruled out a catch for the finding's own auditor (Test-Catch). Another
    # auditor's report of that file describes the planted defect itself, not a defect of Curl, so
    # it is set aside rather than filed (BL-3465: AF-0416). A log plant has no code file to name.
    foreach ($p in $Planted) {
        if ($p.auditor -eq $Auditor -or (Test-LogPlant $p)) { continue }
        if ("$($p.file)" -notmatch '[\/]') { continue }
        if (Test-SamePath (ConvertTo-Normalised $Finding.location) (ConvertTo-Normalised $p.file)) { return $p }
    }
    return $null
}

function New-FindingFile($Finding, $Filed, [string]$Auditor, [string]$Id, [string]$Note) {
    $template = Join-Path $FindingsDirectory 'FINDING-TEMPLATE.md'
    if (-not (Test-Path -LiteralPath $template)) { $template = Join-Path $repo 'Audit\Findings\FINDING-TEMPLATE.md' }
    $summary = "$($Finding.severity) finding from the $Auditor auditor at ``$($Finding.location)``: $($Finding.title)."
    if ($Note) { $summary += " $Note" }
    $values = [ordered]@{
        '{{ID}}' = $Id; '{{TITLE}}' = "$($Finding.title)"; '{{AUDITOR}}' = $Auditor; '{{SEVERITY}}' = "$($Finding.severity)"
        '{{KEY}}' = $Filed.Key; '{{REPRODUCTION}}' = $Filed.Reproduction; '{{FOUND}}' = $Date; '{{FOUND_AT}}' = $Commit; '{{SCORECARD}}' = $Scorecard
        '{{SUMMARY}}' = $summary; '{{LOCATION}}' = "$($Finding.location)"; '{{EVIDENCE}}' = "$($Finding.evidence)"
        '{{REPRODUCTION_COMMAND}}' = $Filed.Command; '{{REPRODUCTION_EXPECTED}}' = "$($Finding.reproduction.expected)"
        '{{REPRODUCTION_ACTUAL}}' = "$($Finding.reproduction.actual)"
    }
    $text = [IO.File]::ReadAllText($template)
    foreach ($k in $values.Keys) { $text = $text.Replace($k, $values[$k]) }
    $path = Join-Path $FindingsDirectory "$Id-$(Get-Slug $Finding.title).md"
    [IO.File]::WriteAllText($path, $text, $Utf8)
    return $path
}

function Invoke-WriteFindings {
    if (-not (Test-Path -LiteralPath $FindingsDirectory)) { New-Item -ItemType Directory -Force $FindingsDirectory | Out-Null }
    $existing = @(Get-ChildItem -LiteralPath $FindingsDirectory -Filter 'AF-*.md' | ForEach-Object { Read-Finding $_.FullName })
    $next = 1 + [int](@($existing | ForEach-Object { [int]($_.Id -replace '\D', '') } | Measure-Object -Maximum).Maximum)
    $planted = @()
    if ($Manifest -and (Test-Path -LiteralPath $Manifest)) { $planted = @((Get-Content -LiteralPath $Manifest -Raw | ConvertFrom-Json).planted) }
    $counts = @{ new = 0; open = 0; closed = 0; catches = 0; setAside = 0 }
    $catches = @()
    $setAside = @()
    $newCount = 0
    $counts.closed += Close-Duplicates $existing
    $counts.closed += Close-HistoricalFindings $existing

    foreach ($auditor in $AuditorOrder) {
        $reportPath = Join-Path $ReportDirectory "$auditor.md"
        if (-not (Test-Path -LiteralPath $reportPath)) { continue }
        $report = Get-ReportBlock $reportPath
        if (-not $report) { continue }
        $flagged = $Unreliable -contains $auditor

        foreach ($f in @($report.findings | Where-Object { $_ })) {
            $planted1 = Test-Catch $f $auditor $planted
            if ($planted1) {
                $catches += [ordered]@{ auditor = $auditor; planted = $planted1.id; key = "$($f.key)"; title = "$($f.title)" }
                $counts.catches++
                continue
            }
            $inPlantedFile = Get-PlantInFindingFile $f $auditor $planted
            if ($inPlantedFile) {
                $setAside += [ordered]@{ auditor = $auditor; planted = $inPlantedFile.id; key = "$($f.key)"; title = "$($f.title)"; reason = "its location is $($inPlantedFile.id)'s planted file $($inPlantedFile.file)" }
                $counts.setAside++
                continue
            }
            $filed = ConvertTo-FiledFinding $f $auditor
            $same = @($existing | Where-Object { $_.Key -ceq $filed.Key })
            $live = @($same | Where-Object { $_.Status -in 'proposed', 'accepted', 'deferred', 'blocked', 'rejected' })[0]
            if ($live) {
                $plant = Get-OverlappingPlant $live "$($f.evidence)" $planted ([bool](Get-MutationSite $live.Reproduction))
                if ($plant) { Add-SetAsideLine $live $plant 'yes' 'still reported' }
                else { Add-ReauditLine $live.Path "- $Date | $Scorecard | reproduces: yes | still reported" }
                if ($live.Status -ne 'rejected') { $counts.open++ }
                continue
            }
            # A new finding with a mechanical reproduction is rerun on the clean audited commit first:
            # the auditor found it in the planted tree, so a mutant the clean commit kills is a plant's
            # effect, wherever the finding's location is (BL-3502: AF-0426 was PD-303 seen from
            # another file). One with no mutation whose command is a single `dotnet test <project>
            # --filter "..."` has that test rerun there the same way, and one that passes there failed
            # only in the planted tree (BL-3503: AF-0426's command). Survived, failed, unverified or
            # any other command: filed as before.
            $newCount++
            $candidate = [pscustomobject]@{ Id = "new-$auditor-$newCount"; Key = $filed.Key; Reproduction = $filed.Reproduction; Command = $filed.Command; Location = "$($f.location)" }
            $mechanical = Invoke-MechanicalReproduction $candidate
            $reason = ''
            if ($mechanical -eq 'killed') {
                $site = Get-MutationSite $filed.Reproduction
                $reason = "its mutant $($site.File):$($site.Line):$($site.Operator) is killed on the clean audited commit $Commit, so it survived only in the planted tree"
            }
            elseif ($mechanical -eq 'none' -and (Invoke-TestReproduction $candidate) -eq 'passed') {
                $test = Get-TestCommand $filed.Command
                $reason = "its failing test (dotnet test $($test.Project) --filter `"$($test.Filter)`") passes on the clean audited commit $Commit, so it failed only in the planted tree"
            }
            if ($reason) {
                $plant = Get-OverlappingPlant $candidate "$($f.evidence)" $planted $true
                $setAside += [ordered]@{ auditor = $auditor; planted = $(if ($plant) { $plant.id } else { $null }); key = $filed.Key; title = "$($f.title)"; reason = $reason }
                $counts.setAside++
                continue
            }
            $closedBefore = @($same | Where-Object { $_.Status -eq 'closed' })[0]
            $notes = @()
            if ($closedBefore) { $notes += "Reappeared; previously $($closedBefore.Id)." }
            if ($flagged) { $notes += "Reported by an auditor flagged unreliable in $Scorecard." }
            $id = 'AF-{0:D4}' -f $next
            $next++
            $path = New-FindingFile $f $filed $auditor $id ($notes -join ' ')
            $existing += Read-Finding $path
            $counts.new++
        }

        foreach ($r in @($report.reaudits | Where-Object { $_ })) {
            $target = @($existing | Where-Object { $_.Id -eq "$($r.finding)" })[0]
            if (-not $target -or $target.Status -eq 'closed') { continue }
            if ($null -eq $r.reproduces) {
                # The auditor could not run the reproduction (a site its sample missed, a red baseline):
                # not a "no" (Quality.md, ADR-0422).
                Add-ReauditLine $target.Path "- $Date | $Scorecard | not re-audited | $($r.evidence)"
                continue
            }
            $reproduces = [bool]$r.reproduces
            $verdict = if ($reproduces) { 'yes' } else { 'no' }
            $own = $target.Auditor -eq $auditor
            $hasMechanical = [bool](Get-MutationSite $target.Reproduction)
            $plant = Get-OverlappingPlant $target "$($r.evidence)" $planted $hasMechanical
            # The runner's rerun on the clean audited commit comes first: no plant can touch it, so
            # it decides an own "no" (to close) and an own verdict a plant may have produced.
            $mechanical = if ($own -and $hasMechanical -and ((-not $reproduces) -or $plant)) { Invoke-MechanicalReproduction $target } else { 'none' }
            $evidence = "$($r.evidence)"
            if (-not $own) { $evidence = "(re-audited by $auditor) $evidence" }
            if ($mechanical -ne 'none') { $evidence += " Runner's targeted mutation rerun on the clean audited commit: $mechanical." }
            # Set aside only what the clean rerun did not answer: no rerun, one that could not tell,
            # or an own "yes" the rerun contradicts (killed) - a "yes" closes nothing either way.
            $answered = ($mechanical -eq 'survived') -or ($mechanical -eq 'killed' -and -not $reproduces)
            if ($plant -and -not $answered) { Add-SetAsideLine $target $plant $verdict $evidence; continue }
            $line = "reproduces: $verdict"
            if ($mechanical -eq 'survived' -and ($plant -or -not $reproduces)) { $line = 'reproduces: yes (runner rerun on clean commit)' }
            Add-ReauditLine $target.Path "- $Date | $Scorecard | $line | $evidence"
            $path = if ($reproduces) { $null } else { Get-ClosePath $target $auditor $flagged $mechanical }
            if ($path) {
                $because = switch ($path.How) {
                    'mechanical' { "the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit" }
                    'consecutive' { "a second consecutive re-audit by its own auditor found the reproduction no longer reproduces ($($path.By))" }
                    default { 'the reproduction no longer reproduces' }
                }
                Close-Finding $target $path "Re-audit $Scorecard`: $because."
                $counts.closed++
            }
        }
    }

    [IO.File]::WriteAllText((Join-Path $ReportDirectory 'catches.json'), (ConvertTo-Json @($catches) -Depth 4), $Utf8)
    [IO.File]::WriteAllText((Join-Path $ReportDirectory 'set-aside.json'), (ConvertTo-Json @($setAside) -Depth 4), $Utf8)
    Write-Output ("findings: new {0}, still open {1}, closed {2}, catches {3}, set aside {4}" -f $counts.new, $counts.open, $counts.closed, $counts.catches, $counts.setAside)
}

if ($SelfTest) {
    $fixture = Join-Path $PSScriptRoot 'Fixtures\findings'
    $work = Join-Path ([IO.Path]::GetTempPath()) ('findings-selftest-' + [guid]::NewGuid().ToString('N'))
    Copy-Item -Recurse -LiteralPath $fixture -Destination $work
    try {
        $FindingsDirectory = Join-Path $work 'findings'
        Copy-Item -LiteralPath (Join-Path $repo 'Audit\Findings\FINDING-TEMPLATE.md') -Destination $FindingsDirectory
        $ReportDirectory = Join-Path $work 'reports'
        $Manifest = Join-Path $work 'manifest.json'
        # One comma-joined string, as RunAudit.ps1 passes it through powershell -File (BL-1244).
        $Unreliable = ConvertTo-NameList @('process,truthfulness-not-run'); $Scorecard = '2026-10-14_0930.md'; $Commit = 'abc1234'; $Date = '2026-10-14'
        # An empty board, not git: the fixtures' commit does not exist.
        $script:BoardTaskPaths = @()
        $before = @{}
        foreach ($f in Get-ChildItem -LiteralPath $FindingsDirectory -Filter 'AF-*.md') { $before[$f.Name] = [IO.File]::ReadAllText($f.FullName) }
        $line = Invoke-WriteFindings
        $failed = 0
        function Check([string]$Name, [bool]$Ok, [string]$Detail) {
            if (-not $Ok) { $script:failed++ }
            Write-Host "$(if ($Ok) { 'PASS' } else { 'FAIL' }) ${Name}: $Detail"
        }
        function Text([string]$Prefix) { $f = Get-ChildItem -LiteralPath $FindingsDirectory -Filter "$Prefix-*.md" | Select-Object -First 1; if ($f) { [IO.File]::ReadAllText($f.FullName) } else { '' } }
        $af7 = Text 'AF-0007'
        Check 'new finding filed as proposed with the next ID' ($af7 -match '(?m)^status: proposed\r?$' -and $af7 -match '(?m)^key: quality:Curl.Cli.UnitTests/ParserTests.cs:Parse_Empty_Throws:weak-assertion\r?$') 'AF-0007'
        $af1 = Text 'AF-0001'
        Check 'a repeated key on a deferred finding keeps it deferred, its line inside Re-audits, before Log' ($af1 -match '(?m)^status: deferred\r?$' -and $af1 -match '(?s)## Re-audits\r?\n\r?\n- 2026-10-14 \| 2026-10-14_0930.md \| reproduces: yes \| still reported\r?\n\r?\n## Log\r?\n\r?\n- 2026-10-01: filed proposed\.\r?\n- 2026-10-02: proposed -> deferred\.[^\r\n]*\r?\n?$') 'AF-0001'
        Check 'a repeated key adds a re-audit line and files nothing' ((Text 'AF-0001') -match 'reproduces: yes \| still reported' -and -not ((Get-ChildItem -LiteralPath $FindingsDirectory -Filter 'AF-*.md' | ForEach-Object { [IO.File]::ReadAllText($_.FullName) }) -match 'key: quality:Curl.Core.UnitTests/UrlTests.cs:Parse_Port_Rejects:name-lies' | Measure-Object).Count -ne 1) 'AF-0001'
        # Windows PowerShell 5.1's ConvertFrom-Json emits a JSON array as one object; unroll it.
        $catchesJson = @((Get-Content -LiteralPath (Join-Path $ReportDirectory 'catches.json') -Raw | ConvertFrom-Json) | ForEach-Object { $_ })
        Check 'a planted-defect match is skipped and in catches.json' ($catchesJson.Count -eq 2 -and @($catchesJson | Where-Object { $_.planted -eq 'PD-101' }).Count -eq 2 -and -not ((Get-ChildItem -LiteralPath $FindingsDirectory -Filter 'AF-*.md' | ForEach-Object { [IO.File]::ReadAllText($_.FullName) }) -match 'FixedTimeEquals replaced')) "catches $($catchesJson.Count)"
        $af6 = Text 'AF-0006'
        Check 'reproduces false from a reliable auditor closes it, blocked or not' ($af6 -match '(?m)^status: closed\r?$' -and $af6 -match '(?m)^closed: 2026-10-14\r?$' -and $af6 -match '(?m)^closed-by: 2026-10-14_0930.md\r?$') 'AF-0006'
        Check 'closing writes the reason and a last Log line' ($af6 -match '(?m)^reason: Re-audit 2026-10-14_0930.md: the reproduction no longer reproduces\.\r?$' -and $af6 -match '(?s)reproduces: no \|[^\n]*\n\r?\n## Log\r?\n\r?\n- 2026-10-01: filed proposed\.\r?\n- 2026-10-02: proposed -> blocked\.[^\n]*\n- 2026-10-14: blocked -> closed\. Re-audit 2026-10-14_0930.md: the reproduction no longer reproduces\.\r?\n$') 'AF-0006'
        $af5 = Text 'AF-0005'
        Check 'reproduces false from an unreliable auditor does not close it' ($af5 -match '(?m)^status: proposed\r?$' -and $af5 -match 'reproduces: no') 'AF-0005'
        $af2 = Text 'AF-0002'
        Check 'a finding whose task is Done, with no re-audit, stays open' ($af2 -ceq $before[(Split-Path (Get-ChildItem -LiteralPath $FindingsDirectory -Filter 'AF-0002-*.md').FullName -Leaf)] -and $af2 -match '(?m)^status: accepted\r?$') 'AF-0002 unchanged'
        $af3 = Text 'AF-0003'
        Check 'a rejected finding stays rejected and gains only a still-reported line' ($af3 -match '(?m)^status: rejected\r?$' -and $af3 -match 'still reported') 'AF-0003'
        $af4 = Text 'AF-0004'
        $af8 = Text 'AF-0008'
        Check 'a key matching a closed finding files a new one naming it' ($af4 -ceq $before[(Split-Path (Get-ChildItem -LiteralPath $FindingsDirectory -Filter 'AF-0004-*.md').FullName -Leaf)] -and $af8 -match 'Reappeared; previously AF-0004') 'AF-0008'
        Check 'an unreliable auditor''s new finding says so' ((Text 'AF-0009') -match 'flagged unreliable in 2026-10-14_0930.md') 'AF-0009'
        Check 'a comma-joined -Unreliable is split into names' (((ConvertTo-NameList @('quality, process', 'security')) -join '|') -eq 'quality|process|security') 'quality|process|security'
        $templateFields = @([regex]::Matches([IO.File]::ReadAllText((Join-Path $repo 'Audit\Findings\FINDING-TEMPLATE.md')), '(?m)^([a-z-]+):') | ForEach-Object { $_.Groups[1].Value }) -join ','
        $newFields = @([regex]::Matches(($af7 -split '\r?\n---')[0], '(?m)^([a-z-]+):') | ForEach-Object { $_.Groups[1].Value }) -join ','
        $sections = @([regex]::Matches($af7, '(?m)^## (.+)$') | ForEach-Object { $_.Groups[1].Value.Trim() }) -join ','
        Check 'front matter and sections match the template' ($newFields -eq $templateFields -and $sections -eq 'Summary,Evidence,Reproduction,Re-audits,Log' -and $af7 -match '(?m)^- 2026-10-14: filed proposed\.\r?$' -and $af7 -notmatch '\{\{') "$newFields | $sections"
        Check 'a process defect cited under logs/ is the same file' ((Test-SamePath 'logs/ci-runs.json' 'ci-runs.json') -and -not (Test-SamePath 'logs/other-ci-runs.json' 'ci-runs.json') -and (Test-SamePath 'Curl.Tls.UnitLibrary/TlsMac.cs' 'Curl.Tls.UnitLibrary/TlsMac.cs'))
        Check 'a file cited by its base name is the planted file' ((Test-SamePath 'ADR-0401-x.md' 'Documentation/Planning/Decisions/ADR-0401-x.md') -and -not (Test-SamePath 'ADR-0402-x.md' 'Documentation/Planning/Decisions/ADR-0401-x.md') -and -not (Test-SamePath 'TlsMac.cs' 'TlsMac.cs.bak')) 'ADR-0401-x.md' 'logs/ci-runs.json'
        Check 'a differently worded report at the planted line is a catch, not a finding' (-not ((Get-ChildItem -LiteralPath $FindingsDirectory -Filter 'AF-*.md' | ForEach-Object { [IO.File]::ReadAllText($_.FullName) }) -match 'returns at the first differing byte')) 'TlsMac.cs:41'
        $plantedAt40 = [pscustomobject]@{ line = 40 }
        Check 'the planted-line window is 2 lines' ((Test-AtPlantedLine 'x.cs:42' $plantedAt40) -and -not (Test-AtPlantedLine 'x.cs:43' $plantedAt40) -and (Test-AtPlantedLine 'x.cs:30-38' $plantedAt40) -and -not (Test-AtPlantedLine 'x.cs:30-37' $plantedAt40) -and -not (Test-AtPlantedLine 'x.cs' $plantedAt40) -and -not (Test-AtPlantedLine 'x.cs:40' ([pscustomobject]@{ line = $null }))) '42 yes, 43 no, 30-38 yes, 30-37 no, no line no'
        # The two real process reports of planted log defects (2026-10-02 and 2026-10-03, BL-1365).
        $plant1 = [pscustomobject]@{ id = 'PD-501'; auditor = 'process'; file = 'DarkFactory-20261001-120001-L9.log'; line = $null; catch = 'BL-1121' }
        $plant2 = [pscustomobject]@{ id = 'PD-501'; auditor = 'process'; file = 'DarkFactory-20261002-231500-L9.log'; line = 2; catch = 'BL-1289' }
        $report1 = [pscustomobject]@{ key = 'process:logs:BL-1121:redone-work'; title = 'BL-1121 claimed 5 times'; location = 'logs/BL-1121'; evidence = 'claimed 5 times' }
        $report2 = [pscustomobject]@{ key = 'process:logs:BL-1289:redone-work'; title = 'BL-1289 was claimed 6 times (134 minutes) before reaching Done'; location = 'logs/BL-1289-20261002-211047-L1.jsonl:1'; evidence = 'six claims' }
        $other = [pscustomobject]@{ key = 'process:logs:BL-1300:redone-work'; title = 'BL-1300 claimed 3 times'; location = 'logs/DarkFactory-20261001-120001-L9.log'; evidence = 'three claims' }
        Check 'a process report naming a planted log defect''s catch text is a catch, whatever file it cites' ((Test-Catch $report1 'process' @($plant1)) -and (Test-Catch $report2 'process' @($plant2)) -and -not (Test-Catch $other 'process' @($plant1)) -and -not (Test-Catch $report1 'security' @($plant1))) 'both PD-501 reports; not another task, not another auditor'
        $setAsideJson = @((Get-Content -LiteralPath (Join-Path $ReportDirectory 'set-aside.json') -Raw | ConvertFrom-Json) | ForEach-Object { $_ })
        $allFindingText = (Get-ChildItem -LiteralPath $FindingsDirectory -Filter 'AF-*.md' | ForEach-Object { [IO.File]::ReadAllText($_.FullName) }) -join "`n"
        Check 'another auditor''s new finding in a planted file is set aside, listed, and files no AF' ($setAsideJson.Count -eq 1 -and $setAsideJson[0].auditor -eq 'quality' -and $setAsideJson[0].planted -eq 'PD-101' -and $setAsideJson[0].title -eq 'Verify compares before checking lengths' -and $setAsideJson[0].reason -like 'its location is PD-101''s planted file *' -and $allFindingText -notmatch 'Verify compares before checking lengths') "set aside $($setAsideJson.Count)"
        Check 'a new finding with no mechanical reproduction is filed without a rerun' ($af7 -match '(?m)^reproduction: none\r?$' -and -not (Test-Path -LiteralPath (Join-Path $ReportDirectory 'reruns'))) 'AF-0007'
        Check 'a new finding in another file of the planted file''s project is still filed' ($allFindingText -match 'Read slices before its length check' -and (Text 'AF-0010') -match 'Read slices before its length check') 'AF-0010'
        Check 'summary line' ($line -eq 'findings: new 4, still open 1, closed 1, catches 2, set aside 1') $line

        # ADR-0422: closing on mechanical evidence, two consecutive "no"s, duplicates, mechanical keys.
        $closure = Join-Path $work 'closure'
        Copy-Item -Recurse -LiteralPath (Join-Path $PSScriptRoot 'Fixtures\closure') -Destination $closure
        $FindingsDirectory = Join-Path $closure 'findings'
        Copy-Item -LiteralPath (Join-Path $repo 'Audit\Findings\FINDING-TEMPLATE.md') -Destination $FindingsDirectory
        $ReportDirectory = Join-Path $closure 'reports'; $Manifest = Join-Path $closure 'manifest.json'; $Tree = Join-Path $closure 'tree'
        $Unreliable = @('quality')
        # New findings are stubbed by their mechanical key: the C.cs mutant survives on the clean
        # commit (filed as AF-0024), the D.cs one is killed there (set aside, BL-3502).
        $script:MechanicalOutcomes = @{ 'AF-0010' = 'killed'; 'AF-0011' = 'survived'; 'AF-0020' = 'killed'; 'AF-0021' = 'survived'; 'AF-0023' = 'unverified'
            'quality:Curl.Net.UnitLibrary/C.cs:Check-eq:surviving-mutant' = 'survived'; 'quality:Curl.Net.UnitLibrary/D.cs:Flag-true:surviving-mutant' = 'killed' }
        # New failing-test findings are stubbed by key (BL-3503): Ratchet_Holds passes on the clean
        # commit (set aside), Parse_Holds still fails (AF-0025), Chain_Holds's command chains another
        # one, so it is never rerun though its stub says passed (AF-0026), and Slow_Holds's rerun
        # cannot tell (AF-0027).
        $script:TestOutcomes = @{ 'quality:Curl.Net.UnitTests/RatchetTests.cs:Ratchet_Holds:failing-test' = 'passed'; 'quality:Curl.Net.UnitTests/ParseTests.cs:Parse_Holds:failing-test' = 'failed'
            'quality:Curl.Net.UnitTests/ChainTests.cs:Chain_Holds:failing-test' = 'passed'; 'quality:Curl.Net.UnitTests/SlowTests.cs:Slow_Holds:failing-test' = 'unverified' }
        $line2 = Invoke-WriteFindings
        # LF throughout, whatever line endings the checkout gave the fixture.
        function Lf([string]$Prefix) { (Text $Prefix) -replace "`r`n", "`n" }
        $c10 = Lf 'AF-0010'; $c11 = Lf 'AF-0011'; $c12 = Lf 'AF-0012'; $c13 = Lf 'AF-0013'; $c14 = Lf 'AF-0014'; $c15 = Lf 'AF-0015'; $c16 = Lf 'AF-0024'; $c17 = Lf 'AF-0017'; $c18 = Lf 'AF-0018'
        $c20 = Lf 'AF-0020'; $c21 = Lf 'AF-0021'; $c22 = Lf 'AF-0022'; $c23 = Lf 'AF-0023'
        Check 'an unreliable auditor''s "no" closes when the runner''s targeted mutant is killed' ($c10 -match '(?m)^status: closed$' -and $c10 -match '(?m)^closed-how: mechanical$' -and $c10 -match '(?m)^closed-by: 2026-10-14_0930.md$' -and $c10 -match "rerun on the clean audited commit: killed\.") 'AF-0010'
        Check 'a "no" the runner''s rerun contradicts (survived) stays open, recorded as the rerun''s "yes"' ($c11 -match '(?m)^status: accepted$' -and $c11 -match "\| reproduces: yes \(runner rerun on clean commit\) \| not sampled Runner's targeted mutation rerun on the clean audited commit: survived\.") 'AF-0011'
        Check 'a plant in another file of the project does not set aside a "no" the clean rerun confirms (killed): closes mechanical' ($c20 -match '(?m)^status: closed$' -and $c20 -match '(?m)^closed-how: mechanical$' -and $c20 -match '\| reproduces: no \| killed in the planted tree Runner''s targeted mutation rerun on the clean audited commit: killed\.' -and $c20 -notmatch 'overlaps planted defect') 'AF-0020'
        Check 'a plant in another file of the project, and the clean rerun''s mutant survives: open, "yes"' ($c21 -match '(?m)^status: accepted$' -and $c21 -match '\| reproduces: yes \(runner rerun on clean commit\) \| killed in the planted tree Runner''s targeted mutation rerun on the clean audited commit: survived\.' -and $c21 -notmatch 'overlaps planted defect') 'AF-0021'
        Check 'a re-audit of a finding in the planted file itself is set aside' ($c22 -match 'overlaps planted defect PD-103 in Curl\.Dep\.UnitLibrary/Reader\.cs, so the auditor''s verdict \(reproduces no\) is set aside: asserts the length now' -and $c22 -match '(?m)^status: accepted$') 'AF-0022'
        Check 'a mutation finding overlapping a plant whose clean rerun cannot tell is set aside' ($c23 -match 'overlaps planted defect PD-103 in Curl\.Dep\.UnitLibrary/Reader\.cs, so the auditor''s verdict \(reproduces yes\) is set aside: survived in the planted tree Runner''s targeted mutation rerun on the clean audited commit: unverified\.' -and $c23 -match '(?m)^status: accepted$') 'AF-0023'
        Check 'a runner''s "yes" line parses as a yes' (@(Get-ReauditLines $c21).Count -eq 1 -and @(Get-ReauditLines $c21)[0].Reproduces) 'AF-0021'
        Check 'another auditor''s re-audit is marked and closes nothing' ($c11 -match '\| reproduces: no \| \(re-audited by security\) looked fine') 'AF-0011'
        Check 'two consecutive "no"s on two audits close an unreliable auditor''s finding' ($c12 -match '(?m)^status: closed$' -and $c12 -match '(?m)^closed-how: consecutive$' -and $c12 -match '(?m)^closed-by: 2026-10-07_0844.md, 2026-10-14_0930.md$') 'AF-0012'
        Check 'closed-how is inserted in template order into a finding from before ADR-0422' ($c12 -match '(?s)\nclosed: 2026-10-14\nclosed-how: consecutive\nclosed-by: ') 'AF-0012'
        Check 'a "no" after a "yes" from an unreliable auditor stays open' ($c13 -match '(?m)^status: accepted$') 'AF-0013'
        Check 'open findings with one key: the later is closed as a duplicate of the earlier' ($c15 -match '(?m)^status: closed$' -and $c15 -match '(?m)^closed-how: duplicate$' -and $c15 -match '(?m)^duplicate-of: AF-0014$' -and $c14 -match '(?m)^tasks: BL-6, BL-7$' -and $c14 -match '(?m)^status: accepted$') 'AF-0015'
        Check 'the same mutant in other words is a repeat of the open finding' ($c14 -match 'reproduces: yes \| still reported' -and -not ((Get-ChildItem -LiteralPath $FindingsDirectory -Filter 'AF-*.md' | ForEach-Object { [IO.File]::ReadAllText($_.FullName) }) -match 'title: NoDelay can become false')) 'AF-0014'
        Check 'a new surviving mutant gets the mechanical key, reproduction and command' ($c16 -match '(?m)^key: quality:Curl\.Net\.UnitLibrary/C\.cs:Check-eq:surviving-mutant\r?$' -and $c16 -match '(?m)^reproduction: mutation Curl\.Net\.UnitLibrary/C\.cs:6:==\r?$' -and $c16 -match 'Invoke-MutationTest\.ps1 -Site Curl\.Net\.UnitLibrary/C\.cs:6:== -Member Check -ExcludeBaselineFailures') 'AF-0024'
        Check 'a re-audit with no mechanical reproduction, in a project that only depends on a planted defect''s project, counts' ($c17 -match '\| reproduces: yes \| fails with IndexOutOfRange' -and $c17 -notmatch 'overlaps planted defect') 'AF-0017'
        Check 'a re-audit whose evidence cites a planted file is set aside' ($c18 -match 'overlaps planted defect PD-104' -and $c18 -match '(?m)^status: accepted\r?$') 'AF-0018'
        Check 'a set-aside line is neither yes nor no' (@(Get-ReauditLines $c22).Count -eq 1) "$(@(Get-ReauditLines $c22).Count) parsed"
        Check 'a re-audit with reproduces null is "not re-audited", not a no' ($c14 -match '\| 2026-10-14_0930\.md \| not re-audited \| site not sampled' -and $c14 -match '(?m)^status: accepted\r?$') 'AF-0014'
        $closureSetAside = @((Get-Content -LiteralPath (Join-Path $ReportDirectory 'set-aside.json') -Raw | ConvertFrom-Json) | ForEach-Object { $_ })
        $closureText = (Get-ChildItem -LiteralPath $FindingsDirectory -Filter 'AF-*.md' | ForEach-Object { [IO.File]::ReadAllText($_.FullName) }) -join "`n"
        $mutantSetAside = @($closureSetAside | Where-Object { $_.key -eq 'quality:Curl.Net.UnitLibrary/D.cs:Flag-true:surviving-mutant' })
        Check 'a new mutant the clean commit kills is set aside with its reason and files no AF' ($closureSetAside.Count -eq 2 -and $mutantSetAside.Count -eq 1 -and $mutantSetAside[0].planted -eq 'PD-103' -and $mutantSetAside[0].reason -eq 'its mutant Curl.Net.UnitLibrary/D.cs:3:true is killed on the clean audited commit abc1234, so it survived only in the planted tree' -and $closureText -notmatch 'Flag can stay true' -and -not (Get-ChildItem -LiteralPath $FindingsDirectory -Filter 'AF-0028-*.md')) "set aside $($closureSetAside.Count)"
        Check 'a new mutant that survives on the clean commit is filed' ($c16 -match '(?m)^title: a == 0 can become a != 0\r?$' -and $c16 -match '(?m)^status: proposed\r?$') 'AF-0024'
        # BL-3503: a new failing-test finding whose test passes on the clean commit is set aside.
        $testSetAside = @($closureSetAside | Where-Object { $_.key -eq 'quality:Curl.Net.UnitTests/RatchetTests.cs:Ratchet_Holds:failing-test' })
        Check 'a new failing-test finding whose test passes on the clean commit is set aside with its reason and files no AF' ($testSetAside.Count -eq 1 -and $testSetAside[0].auditor -eq 'quality' -and $testSetAside[0].planted -eq 'PD-103' -and $testSetAside[0].title -eq 'Ratchet_Holds fails unmutated' -and $testSetAside[0].reason -eq 'its failing test (dotnet test Curl.Net.UnitTests --filter "FullyQualifiedName~Ratchet_Holds&TestCategory!=Integration") passes on the clean audited commit abc1234, so it failed only in the planted tree' -and $closureText -notmatch 'Ratchet_Holds fails unmutated') "set aside $($testSetAside.Count)"
        $c25 = Lf 'AF-0025'; $c26 = Lf 'AF-0026'; $c27 = Lf 'AF-0027'
        Check 'a new failing-test finding whose test still fails on the clean commit is filed' ($c25 -match '(?m)^title: Parse_Holds fails on the clean commit too$' -and $c25 -match '(?m)^status: proposed$' -and $c25 -match '(?m)^reproduction: none$') 'AF-0025'
        Check 'a new finding whose command is anything but one dotnet test --filter is filed, never rerun' ($c26 -match '(?m)^title: Chain_Holds fails behind a chained command$' -and $c26 -match '(?m)^status: proposed$') 'AF-0026'
        Check 'a new failing-test finding whose rerun cannot tell is filed' ($c27 -match '(?m)^title: Slow_Holds fails and its rerun cannot tell$' -and $c27 -match '(?m)^status: proposed$') 'AF-0027'
        $accepted = @('dotnet test Curl.Conformance.UnitTests --filter "FullyQualifiedName~UpstreamCase_RunThroughCurl_HoldsTheRatchet&TestCategory!=Integration"', 'dotnet test Curl.X.UnitTests/Curl.X.UnitTests.csproj --filter "Name=A"', 'dotnet test Curl.X.UnitTests\ --filter "Name=A"')
        $refused = @('dotnet test Curl.X.UnitTests --filter "A"; Remove-Item x', 'dotnet test Curl.X.UnitTests --filter "A" && echo x', 'dotnet test ..\Other --filter "A"', 'dotnet test C:\x\Curl.X.UnitTests --filter "A"', 'dotnet test Curl.X.UnitTests --filter "A" --logger x', 'dotnet test Curl.X.UnitTests', 'dotnet test Curl.X.UnitTests --filter "$(Remove-Item x)"', 'dotnet test Curl.X.UnitTests/Other.csproj --filter "A"', 'dotnet build Curl.X.UnitTests --filter "A"', 'Write-Output x')
        $badAccept = @($accepted | Where-Object { -not (Get-TestCommand $_) }); $badRefuse = @($refused | Where-Object { Get-TestCommand $_ })
        Check 'only exactly one dotnet test <project> --filter "..." is a rerunnable test command' ($badAccept.Count -eq 0 -and $badRefuse.Count -eq 0 -and (Get-TestCommand $accepted[1]).Project -eq 'Curl.X.UnitTests' -and (Get-TestCommand $accepted[0]).Filter -eq 'FullyQualifiedName~UpstreamCase_RunThroughCurl_HoldsTheRatchet&TestCategory!=Integration') "wrongly refused: $($badAccept -join ' / '); wrongly accepted: $($badRefuse -join ' / ')"
        Check 'closure summary line' ($line2 -eq 'findings: new 4, still open 1, closed 4, catches 0, set aside 2') $line2

        # ADR-0422, amended 2026-10-14: a finding about a past run's cost closes once its tasks are Done.
        $historical = Join-Path $work 'historical'
        $FindingsDirectory = Join-Path $historical 'findings'; $ReportDirectory = Join-Path $historical 'reports'
        New-Item -ItemType Directory -Force $FindingsDirectory, $ReportDirectory | Out-Null
        Copy-Item -LiteralPath (Join-Path $repo 'Audit\Findings\FINDING-TEMPLATE.md') -Destination $FindingsDirectory
        $Manifest = ''; $Tree = ''; $Unreliable = @(); $script:MechanicalOutcomes = @{}; $script:TestOutcomes = @{}
        $newHistorical = { param($Id, $Key, $Task, $Tasks, $Status)
            $t = [IO.File]::ReadAllText((Join-Path $repo 'Audit\Findings\FINDING-TEMPLATE.md'))
            $t = $t.Replace('{{ID}}', $Id).Replace('{{TITLE}}', "Finding $Id").Replace('{{AUDITOR}}', 'process').Replace('{{SEVERITY}}', 'Low').Replace('{{KEY}}', $Key).Replace('{{REPRODUCTION}}', 'none').Replace('{{FOUND}}', '2026-10-13').Replace('{{FOUND_AT}}', 'abc').Replace('{{SCORECARD}}', '2026-10-13_1822.md').Replace('{{SUMMARY}}', 's').Replace('{{LOCATION}}', 'logs/x.jsonl').Replace('{{EVIDENCE}}', 'e').Replace('{{REPRODUCTION_COMMAND}}', 'c').Replace('{{REPRODUCTION_EXPECTED}}', 'x').Replace('{{REPRODUCTION_ACTUAL}}', 'y')
            $t = $t.Replace('status: proposed', "status: $Status").Replace('task: none', "task: $Task").Replace("tasks:`n", "tasks: $Tasks`n").Replace("tasks:`r`n", "tasks: $Tasks`r`n")
            [IO.File]::WriteAllText((Join-Path $FindingsDirectory "$Id-x.md"), $t, $Utf8)
        }
        & $newHistorical 'AF-0030' 'process:logs:BL-10:cost-outlier' 'BL-10' 'BL-10' 'accepted'
        & $newHistorical 'AF-0031' 'process:logs:BL-11:cost-outlier' 'BL-11' 'BL-11' 'accepted'
        & $newHistorical 'AF-0032' 'process:logs:BL-13:redone-work' 'BL-13' 'BL-13' 'accepted'
        & $newHistorical 'AF-0033' 'process:logs:BL-14:cost-outlier' 'none' '' 'accepted'
        & $newHistorical 'AF-0034' 'process:logs:BL-15:cost-outlier' 'BL-15' 'BL-15' 'accepted'
        & $newHistorical 'AF-0035' 'process:logs:BL-17:cost-outlier' 'BL-17' 'BL-17' 'accepted'
        & $newHistorical 'AF-0036' 'process:logs:BL-19:cost-outlier' 'BL-19' 'BL-19' 'proposed'
        # BL-12 is a Fix task for AF-0031 still in Backlog, found by its file name; BL-15 is not on
        # the board; BL-18 is a Re-fix task for AF-0035 found by its file name, Done in an archive.
        $script:BoardTaskPaths = @('Tasks/Done/BL-10-fix-af-0030-x.md', 'Tasks/Done/BL-11-fix-af-0031-x.md', 'Tasks/Backlog/BL-12-fix-af-0031-again.md', 'Tasks/Done/2026-10-10/BL-13-fix-af-0032-x.md', 'Tasks/Done/BL-17-fix-af-0035-x.md', 'Tasks/Done/2026-10-12/BL-18-re-fix-af-0035-x.md', 'Tasks/Done/BL-19-fix-af-0036-x.md')
        [IO.File]::WriteAllText((Join-Path $ReportDirectory 'process.md'), "Fixture reply.`n`n``````json`n{ `"auditor`": `"process`", `"findings`": [], `"reaudits`": [ { `"finding`": `"AF-0030`", `"reproduces`": true, `"evidence`": `"BL-10 still cost 4 times the median`" }, { `"finding`": `"AF-0032`", `"reproduces`": true, `"evidence`": `"requeued again`" } ], `"metrics`": {} }`n```````n", $Utf8)
        $beforeHistorical = @{}; foreach ($f in Get-ChildItem -LiteralPath $FindingsDirectory -Filter 'AF-*.md') { $beforeHistorical[$f.Name] = [IO.File]::ReadAllText($f.FullName) }
        $Scorecard = '2026-10-15_0900.md'; $Commit = '0123456789abcdef0123'; $Date = '2026-10-15'
        $line3 = Invoke-WriteFindings
        $h30 = Lf 'AF-0030'; $h35 = Lf 'AF-0035'; $h32 = Lf 'AF-0032'
        Check 'a cost-outlier finding whose tasks are all Done closes task-done-historical, with a reason and a Log line' ($h30 -match '(?m)^status: closed$' -and $h30 -match '(?m)^closed-how: task-done-historical$' -and $h30 -match '(?m)^closed-by: 2026-10-15_0900\.md$' -and $h30 -match '(?m)^closed: 2026-10-15$' -and $h30 -match '(?m)^reason: Audit 2026-10-15_0900\.md: a cost-outlier finding records a past run''s cost, which no fix can change; its tasks \(BL-10\) are Done at 0123456789ab, so it closes without a re-audit \(ADR-0422, amended 2026-10-14\)\.$' -and $h30 -match '(?m)^- 2026-10-15: accepted -> closed\. Audit 2026-10-15_0900\.md: a cost-outlier') 'AF-0030'
        Check 'a closed cost-outlier finding is not re-audited: its re-audit in the reports adds no line' ($h30 -notmatch 'still cost 4 times') 'AF-0030'
        Check 'a Re-fix task found by its file name, Done in an archive, joins the tasks and the close' ($h35 -match '(?m)^status: closed$' -and $h35 -match '(?m)^tasks: BL-17, BL-18$' -and $h35 -match 'its tasks \(BL-17, BL-18\) are Done') 'AF-0035'
        $openHistorical = @('AF-0031-x.md', 'AF-0033-x.md', 'AF-0034-x.md', 'AF-0036-x.md' | Where-Object { [IO.File]::ReadAllText((Join-Path $FindingsDirectory $_)) -cne $beforeHistorical[$_] })
        Check 'a cost-outlier finding with an open task, no task, a task not on the board, or not accepted is untouched' ($openHistorical.Count -eq 0) "changed: $($openHistorical -join ',')"
        Check 'any other key whose task is Done follows ADR-0422 unchanged: re-audited, still open' ($h32 -match '(?m)^status: accepted$' -and $h32 -match '\| 2026-10-15_0900\.md \| reproduces: yes \| requeued again' -and $h32 -notmatch 'closed-how: task') 'AF-0032'
        Check 'historical summary line' ($line3 -eq 'findings: new 0, still open 0, closed 2, catches 0, set aside 0') $line3
        $Tree = ''
        Check 'with no tree the member is the key''s <what> without its operator' ((Get-SiteMember ([pscustomobject]@{ File = 'X.UnitLibrary/Y.cs'; Line = 3; Operator = 'true' }) 'quality:X.UnitLibrary/Y.cs:Turn-true:surviving-mutant') -eq 'Turn') 'Turn'
        exit $(if ($failed) { 1 } else { 0 })
    }
    finally { Remove-Item -Recurse -Force -LiteralPath $work -ErrorAction SilentlyContinue }
}

if (-not $ReportDirectory -or -not $Scorecard -or -not $Commit) { throw 'Give -ReportDirectory, -Scorecard and -Commit (and -Manifest, -Unreliable as the audit has them).' }
if (-not $FindingsDirectory) { $FindingsDirectory = Join-Path $repo 'Audit\Findings' }
Invoke-WriteFindings
