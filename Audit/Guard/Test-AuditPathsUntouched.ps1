<#
.SYNOPSIS
    Fails when the dark factory's branch carries a change of its own to an audit path or
    to one of the guards that protect them.

.DESCRIPTION
    Audit guard layer 3 (ADR-0267). The audit office's files reach master only through the
    audit branch's pull request, which Stewart approves; the dark factory's branch may
    receive them from master, by merging it or by being cut from it, but may never change
    them itself. CI runs this on work/dark-factory, and a red CI run blocks the shift-end
    merge to master.

    With B the base (origin/master) and H the commit under test:

      mine     = git diff --name-only B...H   what H changed since the merge base, so a
                                              change master made and H merged in is not H's
      differs  = git diff --name-only B H     paths whose content differs from master now

    A guarded path in both lists is offending. Requiring differs passes a path the branch
    changed to exactly what master has (a cherry-picked audit commit); requiring mine
    passes a path only master changed.

    Guarded paths: Audit/ (this script included, so the factory cannot weaken it),
    .claude/agents/audit-*, the gap analysis office's Gap/ and .claude/agents/gap-*
    (ADR-0433), and the guards themselves - .claude/hooks/guard-audit-paths.ps1,
    .claude/settings.json (which registers that hook), .github/workflows/ci.yml (which runs
    this) and .claude/skills/task-board/task-board.ps1 (which refuses audit work to lanes).
    A change to any of them is interactive-only work that reaches master through the
    audit branch.

    Prints one line and a GitHub ::error:: annotation per offending path and exits 1, or
    prints "Audit guard: no audit path changed" and exits 0. -SelfTest proves the rule on a
    scratch repository and exits 0 only when every case passes.

.PARAMETER Base
    The branch the factory's own changes are measured against. Default origin/master.

.PARAMETER Head
    The commit under test. Default HEAD. For a pull request, its head commit, not
    GitHub's merge commit.

.PARAMETER SelfTest
    Build a scratch git repository in a temporary folder, check each case, print PASS or
    FAIL per case, and exit.
#>
param(
    [string]$Base = 'origin/master',
    [string]$Head = 'HEAD',
    [switch]$SelfTest
)

$ErrorActionPreference = 'Stop'

$GuardedPatterns = @(
    '^Audit/',
    '^\.claude/agents/audit-',
    '^Gap/',
    '^\.claude/agents/gap-',
    '^\.claude/hooks/guard-audit-paths\.ps1$',
    '^\.claude/settings\.json$',
    '^\.github/workflows/ci\.yml$',
    '^\.claude/skills/task-board/task-board\.ps1$'
)

function Test-GuardedPath([string]$Path) {
    foreach ($pattern in $GuardedPatterns) { if ($Path -match $pattern) { return $true } }
    return $false
}

function Get-ChangedPaths([string]$Repo, [string[]]$Revisions) {
    $paths = & git -C $Repo diff --name-only --no-renames @Revisions
    if ($LASTEXITCODE -ne 0) { throw "git diff $($Revisions -join ' ') failed in $Repo" }
    return @($paths | Where-Object { $_ })
}

function Get-OffendingPaths([string]$BaseRef, [string]$HeadRef, [string]$Repo) {
    $mine = Get-ChangedPaths $Repo @("$BaseRef...$HeadRef")
    $differs = Get-ChangedPaths $Repo @($BaseRef, $HeadRef)
    return @($mine | Where-Object { ($differs -contains $_) -and (Test-GuardedPath $_) } | Sort-Object -Unique)
}

function Get-BoardAuditPathTest([string]$BoardScript) {
    # task-board.ps1's Test-AuditPath as a script block taking one path, or $null when
    # the board script has none.
    $ast = [Management.Automation.Language.Parser]::ParseFile($BoardScript, [ref]$null, [ref]$null)
    $function = $ast.Find({ param($n) $n -is [Management.Automation.Language.FunctionDefinitionAst] -and $n.Name -eq 'Test-AuditPath' }, $true)
    if (-not $function) { return $null }
    return [scriptblock]::Create("$($function.Extent.Text)`nTest-AuditPath `$args[0]")
}

function Get-BoardMisses([string]$BoardScript) {
    # The guarded paths task-board.ps1's Test-AuditPath does not call audit paths. Each
    # pattern gives one sample path: a prefix pattern (ending / or -) a file under it,
    # an exact one the path itself. A guarded path the board misses lets a lane claim a
    # task this guard will fail (BL-1209).
    $test = Get-BoardAuditPathTest $BoardScript
    if (-not $test) { return @('(no Test-AuditPath in the board script)') }
    $misses = @()
    foreach ($pattern in $GuardedPatterns) {
        $sample = $pattern.TrimStart('^').TrimEnd('$') -replace '\\(.)', '$1'
        if ($sample.EndsWith('/') -or $sample.EndsWith('-')) { $sample += 'x.md' }
        if (-not (& $test $sample)) { $misses += $sample }
    }
    return $misses
}

function Invoke-SelfTest {
    $failures = 0
    $scratch = Join-Path ([IO.Path]::GetTempPath()) ('audit-guard-' + [guid]::NewGuid().ToString('N'))

    function Invoke-ScratchGit([string]$Repo) {
        # git with a fixed identity and no line-ending conversion; output is discarded,
        # failures throw. Continue, so Windows PowerShell 5.1 does not turn git's stderr
        # into a terminating error.
        $ErrorActionPreference = 'Continue'
        $gitArgs = @('-C', $Repo, '-c', 'user.name=Audit Guard', '-c', 'user.email=audit-guard@example.invalid',
            '-c', 'commit.gpgsign=false', '-c', 'core.autocrlf=false', '-c', 'core.safecrlf=false') + $args
        $out = & git @gitArgs 2>&1
        if ($LASTEXITCODE -ne 0) { throw "git $($args -join ' ') failed: $out" }
    }

    function Set-File([string]$Repo, [string]$Path, [string]$Text) {
        $full = Join-Path $Repo $Path
        New-Item -ItemType Directory -Force (Split-Path $full) | Out-Null
        [IO.File]::WriteAllText($full, $Text)
    }

    function New-ScratchRepo([string]$Name) {
        # master with one commit holding ordinary and guarded files, and a factory branch
        # cut from it.
        $repo = Join-Path $scratch $Name
        New-Item -ItemType Directory -Force $repo | Out-Null
        Invoke-ScratchGit $repo init -q -b master
        foreach ($p in 'Curl.Core.UnitLibrary/x.cs', '.claude/agents/code-reviewer.md', '.claude/agents/audit-quality.md',
            '.claude/agents/gap-options.md', 'Gap/README.md', '.claude/hooks/guard-audit-paths.ps1', '.claude/settings.json', '.github/workflows/ci.yml',
            '.claude/skills/task-board/task-board.ps1', 'Audit/Guard/Test-AuditPathsUntouched.ps1') {
            Set-File $repo $p "base $p`n"
        }
        Invoke-ScratchGit $repo add -A
        Invoke-ScratchGit $repo commit -q -m base
        Invoke-ScratchGit $repo branch factory
        return $repo
    }

    function Invoke-FactoryCommit([string]$Repo, [string[]]$Paths) {
        Invoke-ScratchGit $Repo checkout -q factory
        foreach ($p in $Paths) { Set-File $Repo $p "factory changed $p`n" }
        Invoke-ScratchGit $Repo add -A
        Invoke-ScratchGit $Repo commit -q -m 'factory change'
    }

    function Invoke-MasterCommit([string]$Repo, [string]$Path) {
        Invoke-ScratchGit $Repo checkout -q master
        Set-File $Repo $Path "master changed $Path`n"
        Invoke-ScratchGit $Repo add -A
        Invoke-ScratchGit $Repo commit -q -m 'master change'
    }

    function Assert-Case([string]$Name, [string]$Repo, [bool]$ExpectFail) {
        $offending = @(Get-OffendingPaths 'master' 'factory' $Repo)
        $ok = ($offending.Count -gt 0) -eq $ExpectFail
        if (-not $ok) { $script:selfTestFailures++ }
        $verdict = if ($ExpectFail) { 'fails' } else { 'passes' }
        Write-Host "$(if ($ok) { 'PASS' } else { 'FAIL' }) ($Name) $verdict; offending: $(if ($offending) { $offending -join ', ' } else { 'none' })"
    }

    $script:selfTestFailures = 0
    try {
        $r = New-ScratchRepo 'a'; Invoke-FactoryCommit $r @('Audit/Findings/x.md')
        Assert-Case 'a: factory adds Audit/Findings/x.md' $r $true

        $r = New-ScratchRepo 'b'; Invoke-FactoryCommit $r @('.claude/agents/audit-quality.md')
        Assert-Case 'b: factory changes .claude/agents/audit-quality.md' $r $true

        $r = New-ScratchRepo 'c'; Invoke-MasterCommit $r 'Audit/x.md'
        Invoke-FactoryCommit $r @('Curl.Core.UnitLibrary/x.cs')
        Assert-Case 'c: Audit/x.md changed only on master' $r $false

        $r = New-ScratchRepo 'd'; Invoke-MasterCommit $r 'Audit/x.md'
        Invoke-ScratchGit $r checkout -q factory
        Invoke-ScratchGit $r merge -q --no-edit master
        Assert-Case 'd: master change merged into factory' $r $false

        $r = New-ScratchRepo 'e'; Invoke-MasterCommit $r 'Audit/x.md'
        $sha = "$(& git -C $r rev-parse master)".Trim()
        Invoke-ScratchGit $r checkout -q factory
        Invoke-ScratchGit $r cherry-pick $sha
        Invoke-FactoryCommit $r @('Curl.Core.UnitLibrary/x.cs')
        Assert-Case 'e: master change cherry-picked onto factory' $r $false

        $r = New-ScratchRepo 'f'; Invoke-FactoryCommit $r @('Curl.Core.UnitLibrary/x.cs', '.claude/agents/code-reviewer.md')
        Assert-Case 'f: factory changes x.cs and code-reviewer.md' $r $false

        $r = New-ScratchRepo 'g'; Invoke-FactoryCommit $r @('Audit/Guard/Test-AuditPathsUntouched.ps1')
        Assert-Case 'g: factory changes this guard script' $r $true

        foreach ($p in '.claude/hooks/guard-audit-paths.ps1', '.claude/settings.json', '.github/workflows/ci.yml', '.claude/skills/task-board/task-board.ps1') {
            $r = New-ScratchRepo ('h' + [guid]::NewGuid().ToString('N').Substring(0, 6)); Invoke-FactoryCommit $r @($p)
            Assert-Case "h: factory changes $p" $r $true
        }

        # The gap analysis office's paths are guarded too (ADR-0433), and only they:
        # Gaps.md and gapper.md merely start with the same letters.
        $r = New-ScratchRepo 'i'; Invoke-FactoryCommit $r @('Gap/README.md')
        Assert-Case 'i: factory changes Gap/README.md' $r $true

        $r = New-ScratchRepo 'j'; Invoke-FactoryCommit $r @('.claude/agents/gap-options.md')
        Assert-Case 'j: factory changes .claude/agents/gap-options.md' $r $true

        $r = New-ScratchRepo 'k'; Invoke-FactoryCommit $r @('Gaps.md', '.claude/agents/gapper.md')
        Assert-Case 'k: factory adds Gaps.md and .claude/agents/gapper.md' $r $false

        # The board must refuse lanes every guarded path, or a lane builds work this
        # guard then fails (BL-1209). Checked on the real board script, then on a copy
        # with ci.yml taken out of its list, which must be caught.
        $board = Join-Path $PSScriptRoot '../../.claude/skills/task-board/task-board.ps1'
        $misses = @(Get-BoardMisses $board)
        $ok = $misses.Count -eq 0
        if (-not $ok) { $script:selfTestFailures++ }
        Write-Host "$(if ($ok) { 'PASS' } else { 'FAIL' }) (l: task-board.ps1 calls every guarded path an audit path) missed: $(if ($misses) { $misses -join ', ' } else { 'none' })"
        New-Item -ItemType Directory -Force $scratch | Out-Null
        $weakened = Join-Path $scratch 'task-board.ps1'
        [IO.File]::WriteAllText($weakened, ([IO.File]::ReadAllText($board) -replace "(?m)^\s*'\.github/workflows/ci\.yml',\r?\n", ''))
        $misses = @(Get-BoardMisses $weakened)
        $ok = ($misses -join ',') -eq '.github/workflows/ci.yml'
        if (-not $ok) { $script:selfTestFailures++ }
        Write-Host "$(if ($ok) { 'PASS' } else { 'FAIL' }) (m: a board missing ci.yml is caught) missed: $(if ($misses) { $misses -join ', ' } else { 'none' })"

        # The board agrees with this guard on the gap office's paths: a copy without the
        # Gap/ prefix is caught, and the look-alikes are not audit paths to the board.
        $weakened = Join-Path $scratch 'task-board-no-gap.ps1'
        [IO.File]::WriteAllText($weakened, ([IO.File]::ReadAllText($board) -replace "(?m)^.*StartsWith\('Gap/'.*\r?\n", ''))
        $misses = @(Get-BoardMisses $weakened)
        $ok = ($misses -join ',') -eq 'Gap/x.md'
        if (-not $ok) { $script:selfTestFailures++ }
        Write-Host "$(if ($ok) { 'PASS' } else { 'FAIL' }) (n: a board missing Gap/ is caught) missed: $(if ($misses) { $misses -join ', ' } else { 'none' })"
        $test = Get-BoardAuditPathTest $board
        $claimed = @('Gaps.md', '.claude/agents/gapper.md' | Where-Object { & $test $_ })
        $ok = $claimed.Count -eq 0
        if (-not $ok) { $script:selfTestFailures++ }
        Write-Host "$(if ($ok) { 'PASS' } else { 'FAIL' }) (o: task-board.ps1 does not call Gaps.md or .claude/agents/gapper.md audit paths) claimed: $(if ($claimed) { $claimed -join ', ' } else { 'none' })"
    }
    finally {
        Remove-Item -Recurse -Force $scratch -ErrorAction SilentlyContinue
    }
    return $script:selfTestFailures
}

if ($SelfTest) {
    $failed = Invoke-SelfTest
    exit $(if ($failed) { 1 } else { 0 })
}

$offending = @(Get-OffendingPaths $Base $Head (Get-Location).Path)
if ($offending.Count -eq 0) {
    Write-Host 'Audit guard: no audit path changed'
    exit 0
}
foreach ($path in $offending) {
    Write-Host "Audit guard: $path changed on work/dark-factory since its merge base with master"
    Write-Host "::error file=$path::Audit guard: $path changed on work/dark-factory since its merge base with master. Audit paths and their guards change only through the audit branch (ADR-0267, ADR-0433)."
}
exit 1
