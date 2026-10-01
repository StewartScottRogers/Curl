<#
.SYNOPSIS
    Says whether an audit is due, and why.

.DESCRIPTION
    The audit office's cadence (ADR-0267): audits run on demand, before each roadmap-milestone
    merge to master, and after changes to RunDarkFactory.ps1. This script reports the last two
    from git alone; it never fetches (fetch first) and never writes.

    The last audit is the newest scorecard Audit/Scorecards/yyyy-MM-dd_HHmm.md at -ScorecardsRef,
    read with git ls-tree and git show so the script works from any checkout; its header's
    "Audited commit" is the last audited commit. Reasons, each reported when it holds:

      no-audit        no scorecard exists at -ScorecardsRef
      factory-script  RunDarkFactory.ps1 differs between the last audited commit and -Ref
      milestone:<N>   in Documentation/Planning/Roadmap.md, the "- **Status:**" line under
                      "## Milestone <N>" does not start with Done at the last audited commit and
                      does at -Ref

    Output: "Audit due: <reason>[, <reason>]" or "No audit due: last audit <scorecard> covers
    <commit>". -Json prints { due, reasons, lastScorecard, lastCommit } instead.

    Exit codes: 0 whether or not an audit is due (that is information, not an error); 2 when git
    fails.

.PARAMETER Ref
    The commit the factory has reached. Default origin/work/dark-factory.

.PARAMETER ScorecardsRef
    Where scorecards are read from. Default origin/master.

.PARAMETER Repository
    The repository to read. Default: the one this script is in.

.PARAMETER Json
    Print JSON instead of a line.

.PARAMETER SelfTest
    Check each reason on a scratch repository.
#>
param(
    [string]$Ref = 'origin/work/dark-factory',
    [string]$ScorecardsRef = 'origin/master',
    [string]$Repository,
    [switch]$Json,
    [switch]$SelfTest
)

$ErrorActionPreference = 'Stop'

function Invoke-Git([string]$Repo, [string[]]$Arguments, [switch]$AllowFailure) {
    $ErrorActionPreference = 'Continue'
    $out = & git -C $Repo @Arguments 2>$null
    if ($LASTEXITCODE -ne 0 -and -not $AllowFailure) { throw "git $($Arguments -join ' ') failed" }
    if ($LASTEXITCODE -ne 0) { return $null }
    return @($out)
}

function Get-MilestoneStatuses([string]$Repo, [string]$Commit) {
    # Milestone number -> its Status text, from Roadmap.md at $Commit.
    $lines = Invoke-Git $Repo @('show', "${Commit}:Documentation/Planning/Roadmap.md") -AllowFailure
    $statuses = @{}
    $current = $null
    foreach ($line in @($lines)) {
        if ($line -match '^## Milestone (\d+)') { $current = $Matches[1]; continue }
        if ($line -match '^## ') { $current = $null; continue }
        if ($current -and -not $statuses.ContainsKey($current) -and $line -match '^- \*\*Status:\*\*\s*(.*)$') { $statuses[$current] = $Matches[1] }
    }
    return $statuses
}

function Get-AuditDue([string]$Repo, [string]$At, [string]$ScorecardsAt) {
    $names = @(Invoke-Git $Repo @('ls-tree', '--name-only', "${ScorecardsAt}:Audit/Scorecards") -AllowFailure | Where-Object { $_ -match '^\d{4}-\d\d-\d\d_\d{4}\.md$' } | Sort-Object)
    if (-not $names.Count) { return [ordered]@{ due = $true; reasons = @('no-audit'); lastScorecard = $null; lastCommit = $null } }
    $last = $names[-1]
    $text = (Invoke-Git $Repo @('show', "${ScorecardsAt}:Audit/Scorecards/$last")) -join "`n"
    $commit = if ($text -match '\| Audited commit \| ([0-9a-f]{7,40}) \|') { $Matches[1] } else { throw "$last has no Audited commit" }
    $reasons = @()
    $diff = @(Invoke-Git $Repo @('diff', '--name-only', $commit, $At, '--', 'RunDarkFactory.ps1'))
    if (@($diff | Where-Object { $_ }).Count) { $reasons += 'factory-script' }
    $before = Get-MilestoneStatuses $Repo $commit
    $after = Get-MilestoneStatuses $Repo $At
    foreach ($n in @($after.Keys | Sort-Object { [int]$_ })) {
        $wasDone = $before.ContainsKey($n) -and $before[$n] -like 'Done*'
        if (-not $wasDone -and $after[$n] -like 'Done*') { $reasons += "milestone:$n" }
    }
    return [ordered]@{ due = [bool]$reasons.Count; reasons = $reasons; lastScorecard = $last; lastCommit = $commit }
}

function Format-Due($Result) {
    if ($Result.due) { return "Audit due: $($Result.reasons -join ', ')" }
    return "No audit due: last audit $($Result.lastScorecard) covers $($Result.lastCommit)"
}

if ($SelfTest) {
    $repo = Join-Path ([IO.Path]::GetTempPath()) ('audit-due-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Force (Join-Path $repo 'Documentation\Planning'), (Join-Path $repo 'Audit\Scorecards') | Out-Null
    $failed = 0
    function Check([string]$Name, [string]$Expected, [string]$Got) { if ($Expected -ceq $Got) { Write-Host "PASS ${Name}: $Got" } else { $script:failed++; Write-Host "FAIL ${Name}: expected '$Expected', got '$Got'" } }
    function Commit-All([string]$Message) {
        $null = Invoke-Git $repo @('add', '-A')
        $null = Invoke-Git $repo @('-c', 'user.name=t', '-c', 'user.email=t@t', '-c', 'commit.gpgsign=false', 'commit', '-q', '-m', $Message)
        return "$(Invoke-Git $repo @('rev-parse', 'HEAD') | Select-Object -First 1)".Trim()
    }
    function Set-Roadmap([string]$Status1) {
        [IO.File]::WriteAllText((Join-Path $repo 'Documentation\Planning\Roadmap.md'), "# Roadmap`n`n## Milestone 0 - Foundations`n`n- **Status:** Done`n`n## Milestone 1 - Phase 1`n`n- **Status:** $Status1`n")
    }
    try {
        $null = Invoke-Git $repo @('init', '-q', '-b', 'main')
        Set-Roadmap 'In progress'
        [IO.File]::WriteAllText((Join-Path $repo 'RunDarkFactory.ps1'), "# v1`n")
        [IO.File]::WriteAllText((Join-Path $repo 'Audit\Scorecards\README.md'), "x`n")
        $base = Commit-All 'base'
        Check 'no scorecard gives no-audit' 'Audit due: no-audit' (Format-Due (Get-AuditDue $repo $base $base))
        [IO.File]::WriteAllText((Join-Path $repo 'Audit\Scorecards\2026-10-14_0930.md'), "| Field | Value |`n| Audited commit | $base |`n")
        $scored = Commit-All 'scorecard'
        Check 'neither gives No audit due' "No audit due: last audit 2026-10-14_0930.md covers $base" (Format-Due (Get-AuditDue $repo $scored $scored))
        [IO.File]::WriteAllText((Join-Path $repo 'RunDarkFactory.ps1'), "# v2`n")
        $script = Commit-All 'factory script'
        Check 'a RunDarkFactory.ps1 change gives factory-script' 'Audit due: factory-script' (Format-Due (Get-AuditDue $repo $script $scored))
        Set-Roadmap 'Done. Everything shipped.'
        $both = Commit-All 'milestone 1 done'
        Check 'both at once give both' 'Audit due: factory-script, milestone:1' (Format-Due (Get-AuditDue $repo $both $scored))
        $null = Invoke-Git $repo @('checkout', '-q', '-b', 'only-milestone', $scored)
        Set-Roadmap 'Done.'
        $milestone = Commit-All 'milestone only'
        Check 'a milestone moving to Done gives milestone:1' 'Audit due: milestone:1' (Format-Due (Get-AuditDue $repo $milestone $scored))
        $parsed = [pscustomobject](Get-AuditDue $repo $both $scored) | ConvertTo-Json -Compress | ConvertFrom-Json
        Check '-Json has the four fields' 'due,reasons,lastScorecard,lastCommit' (($parsed.PSObject.Properties | ForEach-Object { $_.Name }) -join ',')
    }
    finally { Remove-Item -Recurse -Force -LiteralPath $repo -ErrorAction SilentlyContinue }
    exit $(if ($failed) { 1 } else { 0 })
}

if (-not $Repository) { $Repository = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path }
try { $result = Get-AuditDue $Repository $Ref $ScorecardsRef }
catch { Write-Host "Cannot tell whether an audit is due: $($_.Exception.Message)"; exit 2 }
if ($Json) { Write-Output ([pscustomobject]$result | ConvertTo-Json -Compress) } else { Write-Output (Format-Due $result) }
exit 0
