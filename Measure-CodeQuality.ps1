<#
.SYNOPSIS
    Measures line coverage, branch coverage, cyclomatic complexity and CRAP score for
    every production library in the Curl solution, and fails if any member is outside
    the thresholds.

.DESCRIPTION
    Everything here comes out of tooling the solution already has, because Curl takes
    no packages beyond MSTest (CLAUDE.md):

      * Coverage - Microsoft.Testing.Extensions.CodeCoverage, which the MSTest
        meta-package already pulls into every .UnitTests project. It is asked for
        Cobertura, whose <method> elements carry line-rate, branch-rate and a
        complexity attribute.
      * Complexity - that same Cobertura complexity attribute. The build itself is
        gated separately by the SDK's CA1502 analyzer at the threshold in
        CodeMetricsConfig.txt. CA1502 measures the source and Cobertura measures the
        compiled method, so on a method full of pattern matching the two can differ by
        one or two. CA1502 is the one that breaks the build; this is the one that
        feeds CRAP, because it arrives in the same file as the coverage it pairs with.
      * CRAP - produced by no tool at all, so it is computed here:
            CRAP(m) = comp(m)^2 * (1 - cov(m))^3 + comp(m)
        At full coverage the middle term vanishes and CRAP is just the complexity,
        which is why the default CRAP limit of 30 only ever bites on a method that is
        both complex and undertested.

    A library covered by more than one test project is merged line by line, taking the
    best hit count and the best condition count seen for each line, so a helper in
    Curl.Protocol.Abstractions.UnitLibrary exercised from another protocol's tests is
    credited for it.

    A whole-solution run builds Curl.slnx once and then runs dotnet test --no-build on
    each *.UnitTests project in turn, one Cobertura report per project. It never runs
    dotnet test on Curl.slnx itself, whose single merged report carried no branch counts
    on CI (BL-2466).

    A whole-solution run that leaves any member short of line or branch coverage runs
    the *.UnitTests projects that reach that member's library again, one at a time, into
    a recheck folder under ResultsDirectory, and merges their reports in the same way
    before scoring again. A whole-solution run once lost one project's coverage although
    its tests passed (BL-2269); the recheck restores it, and a real gap still fails.

.PARAMETER Library
    Assembly names to report on, wildcards allowed. Defaults to every production
    assembly - anything named *.UnitLibrary, plus Curl.Console, Curl.Api, Curl.Mcp and Libcurl. Curl.Console's
    assembly is named curl, and is reported and matched here as Curl.Console.

    With -Library, the tests that run are only the *.UnitTests projects whose project
    references reach a named library, directly or through other projects - the only
    tests that can execute its code - one dotnet test per project into the same
    results directory (BL-1318). With -IncludeIntegration as well, the
    *.IntegrationTests projects that reach a named library run too; without it they
    are left out, since the Integration filter would skip every test they hold
    (ADR-0421, BL-1605). Without -Library, or when no test project reaches the names,
    the whole solution runs.

    Test assemblies of both kinds, *.UnitTests and *.IntegrationTests, are never
    reported as production code, and no file under either kind of project is listed
    among the coverage exclusions in production code.

.PARAMETER Shard
    With -ShardCount, collect coverage for one shard of a whole-solution run and stop:
    the *.UnitTests projects (all of them, as a whole-solution run measures) are split
    into -ShardCount shards by Get-ShardTestProjects, and this run builds and tests only
    shard -Shard (1-based) into ResultsDirectory, one folder per test project, beside a
    manifest named shard-<Shard>-of-<ShardCount>.txt that lists the projects it ran. It
    scores nothing, because a library's coverage is the merge of every test project that
    reaches it and those may sit in other shards; a -FromShards run scores the shards'
    reports together. A shard with no test project, or whose tests wrote no Cobertura
    report, fails rather than passing having measured nothing. CI's quality-shard jobs
    run the shards in parallel (BL-3464). The unit of a shard is a whole test project,
    never part of one: the merge keeps the best condition count per line, not a union of
    branches, so splitting one project's tests would change what it measures.

.PARAMETER ShardCount
    How many shards -Shard counts from. Splits the test projects by their measured
    test-run seconds (Get-ShardTestProjects), so the shards are roughly even.

.PARAMETER FromShards
    Score the reports that every shard of a -Shard run left under ResultsDirectory (in
    any folder layout, such as one folder per downloaded CI artifact), instead of running
    tests. Fails unless the shards' manifests name every shard of one -ShardCount and
    every *.UnitTests project exactly once, and unless every gated library - every
    production project in the checkout - is measured, so a library is never silently
    dropped. A member short of line or branch coverage gets the same recheck as a
    whole-solution run's.

.PARAMETER SelfTest
    Check Get-ShardTestProjects against this checkout and exit: for every shard count
    from 1 to 8 the shards hold every *.UnitTests project exactly once, none is empty,
    the split does not depend on the order projects are listed in, and every gated
    library is reached by at least one test project, so by some shard. Exits 1 on a
    failed check.

.PARAMETER SkipTestRun
    Reuse the Cobertura files already in ResultsDirectory instead of running tests.
    Without -ResultsDirectory these are the reports this checkout's last run left in
    its own default directory (see ResultsDirectory).

.PARAMETER ResultsDirectory
    Where the Cobertura reports are written and read. Unless -SkipTestRun is given,
    this directory is deleted and recreated before the tests run, and every
    *.cobertura.xml under it is merged into the report.

    Defaults to a directory that belongs to this checkout alone:
    $env:TEMP\CurlCodeQuality\<checkout folder name>-<first 16 hex digits of the
    SHA-256 of the checkout's full path>. Each git worktree (for example each dark
    factory lane under <repo>.lanes\lane-<n>) therefore gets its own directory, so two
    runs from different checkouts at the same time never delete or read each other's
    reports, and -SkipTestRun still finds the reports of this checkout's last run.
    Two runs from the same checkout at the same time still share it.

    An explicit value is used exactly as given.

.PARAMETER IncludeIntegration
    Run the integration tests too: drop the TestCategory!=Integration filter and, with
    -Library, also run the *.IntegrationTests projects that reach the named libraries.
    Off by default, matching the fast test command in CLAUDE.md, which is the one
    coverage is measured from (ADR-0421).

.PARAMETER ReportPath
    Also write the Markdown report to this file.

.PARAMETER JsonPath
    Also write the measurements as JSON - totals, one row per library, every failing
    member and every coverage exclusion - for the published coverage report that
    .github/coverage/make-coverage-report.cs renders.

.EXAMPLE
    .\Measure-CodeQuality.ps1

.EXAMPLE
    .\Measure-CodeQuality.ps1 -Library Curl.Original.Protocol.File.UnitLibrary -ReportPath report.md

.EXAMPLE
    .\Measure-CodeQuality.ps1 -Shard 2 -ShardCount 3 -ResultsDirectory $env:TEMP\q\shard-2
    .\Measure-CodeQuality.ps1 -FromShards -ResultsDirectory $env:TEMP\q -JsonPath q.json
#>
[CmdletBinding()]
param(
    [string[]] $Library,
    [int] $Shard,
    [int] $ShardCount,
    [switch] $FromShards,
    [switch] $SelfTest,
    [switch] $SkipTestRun,
    [switch] $IncludeIntegration,
    [string] $ResultsDirectory,
    [double] $LineThreshold = 100,
    [double] $BranchThreshold = 100,
    [int] $ComplexityThreshold = 10,
    [double] $CrapThreshold = 30,
    [int] $MaxMissingLinesShown = 12,
    [string] $ReportPath,
    [string] $JsonPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = $PSScriptRoot

# Through powershell -File, "-Library A,B" arrives as the one string "A,B", which matches
# no project, so the run fell back to the whole solution and the report named nothing.
# Split it into names (BL-1360, the same trap as the audit tools' BL-1245).
$Library = @($Library | ForEach-Object { "$_" -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ })

function Get-CheckoutResultsDirectory {
    param([string] $CheckoutRoot)
    $fullPath = [System.IO.Path]::GetFullPath($CheckoutRoot).TrimEnd([char] '\', [char] '/').ToLowerInvariant()
    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        $hash = $sha256.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($fullPath))
    } finally {
        $sha256.Dispose()
    }
    $hashText = -join ($hash[0..7] | ForEach-Object { $_.ToString('x2') })
    $checkoutName = Split-Path $fullPath -Leaf
    return Join-Path (Join-Path $env:TEMP 'CurlCodeQuality') "$checkoutName-$hashText"
}

if ([string]::IsNullOrWhiteSpace($ResultsDirectory)) {
    $ResultsDirectory = Get-CheckoutResultsDirectory -CheckoutRoot $repositoryRoot
}

function Get-TestProjectsReaching {
    # The test project files whose ProjectReference graph reaches a project whose name
    # matches one of -Library (wildcards allowed; Curl.Console is the console project).
    # Test projects are the *.UnitTests ones and, with -IncludeIntegration, the
    # *.IntegrationTests ones too; without it those are left out, because every test in
    # them carries the Integration category the filter skips (ADR-0421, BL-1605).
    param([string] $Root, [string[]] $Library, [switch] $IncludeIntegration)
    $projects = @{}
    foreach ($file in Get-ChildItem -Path $Root -Filter '*.csproj' -Recurse -Depth 1 -File) {
        $projects[$file.BaseName] = $file.FullName
    }
    $references = @{}
    foreach ($name in $projects.Keys) {
        [xml] $xml = Get-Content -LiteralPath $projects[$name] -Raw
        $references[$name] = @($xml.SelectNodes('//ProjectReference') | ForEach-Object {
            [System.IO.Path]::GetFileNameWithoutExtension(($_.GetAttribute('Include') -replace '\\', '/'))
        })
    }
    $reaching = New-Object System.Collections.Generic.List[string]
    $testProjects = @($projects.Keys | Where-Object {
        ($_ -like '*.UnitTests') -or ($IncludeIntegration -and $_ -like '*.IntegrationTests')
    } | Sort-Object)
    foreach ($test in $testProjects) {
        $seen = New-Object System.Collections.Generic.HashSet[string]
        $queue = New-Object System.Collections.Generic.Queue[string]
        $queue.Enqueue($test)
        while ($queue.Count) {
            $current = $queue.Dequeue()
            if (-not $seen.Add($current)) { continue }
            foreach ($next in @($references[$current])) { if ($next -and $references.ContainsKey($next)) { $queue.Enqueue($next) } }
        }
        $hit = @($seen | Where-Object { $_ -ne $test } | Where-Object { $name = $_; @($Library | Where-Object { $name -like $_ }).Count -gt 0 })
        if ($hit.Count) { $reaching.Add($projects[$test]) }
    }
    return $reaching
}

function Get-TestProjects {
    # Every *.UnitTests project file and, with -IncludeIntegration, every
    # *.IntegrationTests one: the test projects a whole-solution run measures.
    param([string] $Root, [switch] $IncludeIntegration)
    return @(Get-ChildItem -Path $Root -Filter '*.csproj' -Recurse -Depth 1 -File |
        Where-Object { ($_.BaseName -like '*.UnitTests') -or ($IncludeIntegration -and $_.BaseName -like '*.IntegrationTests') } |
        Sort-Object BaseName |
        ForEach-Object { $_.FullName })
}

function Invoke-CoverageTestRun {
    # Runs dotnet test with Cobertura coverage for each target, writing into Directory,
    # and throws if any test failed. With -NoBuild the targets must already be built.
    param([string[]] $Targets, [string] $Directory, [switch] $NoBuild)

    Write-Host "Running tests with coverage into $Directory ..." -ForegroundColor Cyan
    $testOutput = New-Object System.Collections.Generic.List[string]
    $testExitCode = 0
    foreach ($target in $Targets) {
        # One folder per test project, so each project's reports sit apart when shards'
        # results are gathered; the reports are merged from every folder alike.
        $projectDirectory = Join-Path $Directory ([System.IO.Path]::GetFileNameWithoutExtension($target))
        $testArguments = @(
            'test', $target,
            '--collect:Code Coverage;Format=cobertura',
            '--results-directory', $projectDirectory
        )
        if ($NoBuild) { $testArguments += '--no-build' }
        if (-not $IncludeIntegration) { $testArguments += @('--filter', 'TestCategory!=Integration') }
        Write-Host "dotnet test $(Split-Path $target -Leaf)" -ForegroundColor DarkCyan
        & dotnet @testArguments | ForEach-Object {
            $line = [string]$_
            $testOutput.Add($line)
            Write-Host $line
        }
        if ($LASTEXITCODE -ne 0) { $testExitCode = $LASTEXITCODE }
    }
    if ($testExitCode -ne 0) {
        # A scaffolded .UnitTests project with no tests yet prints "No test matches the
        # given testcase filter" and can make dotnet test exit non-zero although nothing
        # failed. Measure anyway, but only when the output shows no real failure: no
        # "Failed!" summary (it can land mid-line when projects' output interleaves), no
        # aborted run and no build error.
        $realFailurePatterns = @(
            'Failed!\s+-\s+Failed:',
            'Test Run Aborted',
            'test run was aborted',
            ':\s+error\s+[A-Za-z]+\d+',
            'Build FAILED'
        )
        $realFailure = $testOutput | Where-Object {
            $line = $_
            @($realFailurePatterns | Where-Object { $line -match $_ }).Count -gt 0
        } | Select-Object -First 1
        $emptyTestProject = $testOutput | Where-Object { $_ -match 'No test matches the given testcase filter' } | Select-Object -First 1
        if ($null -ne $realFailure -or $null -eq $emptyTestProject) {
            throw "dotnet test failed with exit code $testExitCode. Fix the tests before measuring quality."
        }
        Write-Warning "dotnet test exited with code $testExitCode, but no test failed: a test project has no tests matching the filter yet. Measuring anyway."
    }
}

# Production code is *.UnitLibrary, Curl.Console, Curl.Api, Curl.Mcp and Libcurl only. Test assemblies of both kinds,
# *.UnitTests and *.IntegrationTests, are never production code (ADR-0421).
function Test-IsProductionAssembly {
    param([string] $Name)

    if ($Name -like '*.UnitTests' -or $Name -like '*.IntegrationTests') { return $false }
    return ($Name -like '*.UnitLibrary') -or ($Name -in 'Curl.Console', 'Curl.Api', 'Curl.Mcp', 'Libcurl')
}

function Get-GatedLibraries {
    # The gated libraries: every production project in the checkout, by project name.
    param([string] $Root)
    return @(Get-ChildItem -Path $Root -Filter '*.csproj' -Recurse -Depth 1 -File |
        Where-Object { Test-IsProductionAssembly $_.BaseName } |
        ForEach-Object { $_.BaseName } | Sort-Object -Unique)
}

# --- Shards (BL-3464) -------------------------------------------------------------

# Seconds from one test project's coverage run finishing to the next one's, in CI run
# 41239174672's shard jobs on CI's Windows runner: what Get-ShardTestProjects balances. Each
# includes the 4 to 6 seconds every project's run costs before its first test, which is why
# a project not listed counts as $defaultTestProjectSeconds. Remeasure when a shard runs
# minutes longer than the others: the 707 seconds the SSH protocol's tests once took (run
# 40108510840) kept them alone in shard 1 long after they fell to 25, and shard 1 finished
# in 2 minutes while shards 2 and 3 took 10 and 13 (BL-3512). Only the balance depends on
# these numbers; every project lands in exactly one shard whatever they say.
$measuredTestProjectSeconds = @{
    'Curl.Conformance.UnitTests' = 75; 'Curl.Ssh.UnitTests' = 67; 'Curl.Cryptography.UnitTests' = 57
    'Curl.Tls.UnitTests' = 25; 'Curl.Console.UnitTests' = 21; 'Curl.Networking.UnitTests' = 17
    'Curl.Engine.UnitTests' = 17; 'Curl.Cli.UnitTests' = 12; 'Curl.Mcp.UnitTests' = 11
    'Curl.Extension.WireGuard.UnitTests' = 10; 'Curl.Output.UnitTests' = 8; 'Curl.Conformance.LibTest.UnitTests' = 8
    'Curl.Conformance.SshServer.UnitTests' = 8
}
$defaultTestProjectSeconds = 5

function Get-ShardTestProjects {
    # The test project paths in shard -Shard (1-based) of -ShardCount, sorted by name. The
    # projects are dealt longest first, by $measuredTestProjectSeconds (ties by name), each
    # to the shard with the fewest seconds so far (ties to the lowest shard), so the split
    # depends only on the set of projects, never on the order -Projects lists them in.
    param([string[]] $Projects, [int] $Shard, [int] $ShardCount)
    $load = New-Object 'double[]' $ShardCount
    $assigned = @{}
    $ordered = @($Projects | Sort-Object -Unique |
        Sort-Object @{ Expression = { $name = [System.IO.Path]::GetFileNameWithoutExtension($_); if ($measuredTestProjectSeconds.ContainsKey($name)) { $measuredTestProjectSeconds[$name] } else { $defaultTestProjectSeconds } }; Descending = $true },
                    @{ Expression = { [System.IO.Path]::GetFileNameWithoutExtension($_) } })
    foreach ($project in $ordered) {
        $name = [System.IO.Path]::GetFileNameWithoutExtension($project)
        $seconds = if ($measuredTestProjectSeconds.ContainsKey($name)) { $measuredTestProjectSeconds[$name] } else { $defaultTestProjectSeconds }
        $lightest = 0
        for ($i = 1; $i -lt $ShardCount; $i++) { if ($load[$i] -lt $load[$lightest]) { $lightest = $i } }
        $load[$lightest] += $seconds
        $assigned[$project] = $lightest + 1
    }
    return @($ordered | Where-Object { $assigned[$_] -eq $Shard } | Sort-Object { [System.IO.Path]::GetFileNameWithoutExtension($_) })
}

function Invoke-ShardSelfTest {
    # -SelfTest: the shards partition this checkout's test projects exactly, and every
    # gated library is reached by some test project. Returns the number of failed checks.
    $failed = 0
    $all = @(Get-TestProjects -Root $repositoryRoot)
    $reversed = @($all); [array]::Reverse($reversed)
    foreach ($count in 1..8) {
        $seen = @{}
        $empty = 0
        $orderDependent = 0
        foreach ($index in 1..$count) {
            $shardProjects = @(Get-ShardTestProjects -Projects $all -Shard $index -ShardCount $count)
            if ($shardProjects.Count -eq 0) { $empty++ }
            $fromReversed = @(Get-ShardTestProjects -Projects $reversed -Shard $index -ShardCount $count)
            if (($shardProjects -join '|') -cne ($fromReversed -join '|')) { $orderDependent++ }
            foreach ($project in $shardProjects) { $seen[$project] = 1 + $(if ($seen.ContainsKey($project)) { $seen[$project] } else { 0 }) }
        }
        $twice = @($seen.Keys | Where-Object { $seen[$_] -ne 1 })
        $missing = @($all | Where-Object { -not $seen.ContainsKey($_) })
        $ok = ($twice.Count -eq 0) -and ($missing.Count -eq 0) -and ($empty -eq 0) -and ($orderDependent -eq 0)
        if (-not $ok) { $failed++ }
        $sizes = @(1..$count | ForEach-Object { @(Get-ShardTestProjects -Projects $all -Shard $_ -ShardCount $count).Count }) -join ', '
        Write-Host ('{0} {1} shard(s) of {2} test projects: sizes {3}; in more than one {4}, in none {5}, empty {6}, order-dependent {7}' -f
            $(if ($ok) { 'PASS' } else { 'FAIL' }), $count, $all.Count, $sizes, $twice.Count, $missing.Count, $empty, $orderDependent)
    }
    $unreached = @(Get-GatedLibraries -Root $repositoryRoot | Where-Object {
            @(Get-TestProjectsReaching -Root $repositoryRoot -Library @($_)).Count -eq 0 })
    if ($unreached.Count) { $failed++ }
    Write-Host ('{0} every gated library is reached by a test project; unreached: {1}' -f
        $(if ($unreached.Count) { 'FAIL' } else { 'PASS' }), $(if ($unreached.Count) { $unreached -join ', ' } else { 'none' }))
    return $failed
}

if ($SelfTest) {
    $failedChecks = Invoke-ShardSelfTest
    if ($failedChecks) { Write-Host "$failedChecks shard check(s) failed." -ForegroundColor Red; exit 1 }
    Write-Host 'Every shard check passed.' -ForegroundColor Green
    exit 0
}

if ($ShardCount -gt 0 -or $Shard -gt 0) {
    if ($ShardCount -lt 1 -or $Shard -lt 1 -or $Shard -gt $ShardCount) { throw "-Shard must be from 1 to -ShardCount; got -Shard $Shard -ShardCount $ShardCount." }
    if ($Library -or $FromShards -or $SkipTestRun -or $IncludeIntegration) { throw '-Shard measures a shard of a whole-solution run; it takes none of -Library, -FromShards, -SkipTestRun or -IncludeIntegration.' }
    $shardProjects = @(Get-ShardTestProjects -Projects (Get-TestProjects -Root $repositoryRoot) -Shard $Shard -ShardCount $ShardCount)
    if ($shardProjects.Count -eq 0) { throw "Shard $Shard of $ShardCount holds no test project, so it would measure nothing." }
    if (Test-Path $ResultsDirectory) { Remove-Item $ResultsDirectory -Recurse -Force }
    New-Item -ItemType Directory -Path $ResultsDirectory -Force | Out-Null
    Write-Host "Shard $Shard of ${ShardCount}: $(@($shardProjects | ForEach-Object { [System.IO.Path]::GetFileNameWithoutExtension($_) }) -join ', ')" -ForegroundColor Cyan

    # Build only what this shard tests: a solution filter of its test projects, whose
    # project references build with them.
    $filterPath = Join-Path $ResultsDirectory "shard-$Shard-of-$ShardCount.slnf"
    $filter = [ordered]@{ solution = [ordered]@{
            path     = (Join-Path $repositoryRoot 'Curl.slnx')
            projects = @($shardProjects | ForEach-Object { $_.Substring($repositoryRoot.Length).TrimStart('\', '/') })
        } }
    $filter | ConvertTo-Json -Depth 4 | Out-File -FilePath $filterPath -Encoding utf8
    Write-Host "Building shard $Shard of $ShardCount ..." -ForegroundColor Cyan
    & dotnet build $filterPath | ForEach-Object { Write-Host ([string]$_) }
    if ($LASTEXITCODE -ne 0) { throw "dotnet build failed with exit code $LASTEXITCODE. Fix the build before measuring quality." }

    Invoke-CoverageTestRun -Targets $shardProjects -Directory $ResultsDirectory -NoBuild
    $shardReports = @(Get-ChildItem -Path $ResultsDirectory -Filter '*.cobertura.xml' -Recurse -ErrorAction SilentlyContinue)
    if ($shardReports.Count -eq 0) { throw "Shard $Shard of $ShardCount wrote no Cobertura report, so it measured nothing." }
    $manifest = @("shard $Shard of $ShardCount") + @($shardProjects | ForEach-Object { [System.IO.Path]::GetFileNameWithoutExtension($_) })
    $manifest | Out-File -FilePath (Join-Path $ResultsDirectory "shard-$Shard-of-$ShardCount.txt") -Encoding utf8
    Write-Host "Shard $Shard of $ShardCount wrote $($shardReports.Count) Cobertura report(s) for $($shardProjects.Count) test project(s)." -ForegroundColor Cyan
    exit 0
}

if ($FromShards) {
    if ($Library -or $SkipTestRun -or $IncludeIntegration) { throw '-FromShards scores a whole-solution run; it takes none of -Library, -SkipTestRun or -IncludeIntegration.' }
    if (-not (Test-Path $ResultsDirectory)) { throw "-FromShards found no $ResultsDirectory." }
    # Every shard of one count reported, and together they ran every test project once.
    $manifests = @(Get-ChildItem -Path $ResultsDirectory -Filter 'shard-*-of-*.txt' -Recurse -File)
    $counts = @($manifests | ForEach-Object { if ($_.Name -match '^shard-(\d+)-of-(\d+)\.txt$') { [int] $Matches[2] } } | Sort-Object -Unique)
    if ($counts.Count -ne 1) { throw "-FromShards needs the manifests of one shard count under $ResultsDirectory; found $($manifests.Count) manifest(s) of count(s) '$($counts -join ', ')'." }
    $shardNumbers = @($manifests | ForEach-Object { if ($_.Name -match '^shard-(\d+)-of-') { [int] $Matches[1] } } | Sort-Object)
    if (($shardNumbers -join ',') -ne ((1..$counts[0]) -join ',')) { throw "-FromShards needs shards 1 to $($counts[0]) once each; found shard(s) $($shardNumbers -join ', ')." }
    $ran = @($manifests | ForEach-Object { Get-Content -LiteralPath $_.FullName | Select-Object -Skip 1 | ForEach-Object { $_.Trim() } | Where-Object { $_ } })
    $expected = @(Get-TestProjects -Root $repositoryRoot | ForEach-Object { [System.IO.Path]::GetFileNameWithoutExtension($_) })
    $repeated = @($ran | Group-Object | Where-Object { $_.Count -gt 1 } | ForEach-Object { $_.Name })
    $notRun = @($expected | Where-Object { $ran -notcontains $_ })
    $unknown = @($ran | Where-Object { $expected -notcontains $_ } | Sort-Object -Unique)
    if ($repeated.Count -or $notRun.Count -or $unknown.Count) {
        throw "The shards did not run every test project exactly once. Run twice: $($repeated -join ', '). Not run: $($notRun -join ', '). Not in this checkout: $($unknown -join ', ')."
    }
    Write-Host "Scoring $($counts[0]) shard(s) that ran all $($expected.Count) test projects." -ForegroundColor Cyan
}

# --- 1. Collect coverage ----------------------------------------------------------

if (-not $SkipTestRun -and -not $FromShards) {
    if (Test-Path $ResultsDirectory) { Remove-Item $ResultsDirectory -Recurse -Force }
    New-Item -ItemType Directory -Path $ResultsDirectory -Force | Out-Null

    # With -Library, run only the test projects whose project references reach a named
    # library, directly or through other projects, rather than the whole solution. Those
    # are the only tests that can execute its code, so its coverage, merged across them,
    # is what a whole-solution run would report; on nine busy lanes that is minutes, not
    # the 40-58 a whole-solution run took (BL-1318). Without -Library, or when no test
    # project reaches the names given, every test project runs.
    $testTargets = @()
    if ($Library) {
        $testTargets = @(Get-TestProjectsReaching -Root $repositoryRoot -Library $Library -IncludeIntegration:$IncludeIntegration)
    }
    $wholeSolution = $testTargets.Count -eq 0

    if ($wholeSolution) {
        # A whole-solution run builds Curl.slnx once and then tests each project on its
        # own, one report per project, rather than running dotnet test on Curl.slnx: that
        # merges every project's coverage into one Cobertura file, and on CI that merged
        # file carried no branch counts, so the branch gate passed without measuring a
        # branch until a recheck added one project's counts and 663 members failed (CI run
        # 38876049958, BL-2466). It also lost a whole project's coverage once (BL-2269).
        Write-Host 'Building Curl.slnx ...' -ForegroundColor Cyan
        & dotnet build (Join-Path $repositoryRoot 'Curl.slnx') | ForEach-Object { Write-Host ([string]$_) }
        if ($LASTEXITCODE -ne 0) { throw "dotnet build failed with exit code $LASTEXITCODE. Fix the build before measuring quality." }
        $testTargets = Get-TestProjects -Root $repositoryRoot -IncludeIntegration:$IncludeIntegration
    }

    Invoke-CoverageTestRun -Targets $testTargets -Directory $ResultsDirectory -NoBuild:$wholeSolution
}

$coverageFiles = @(Get-ChildItem -Path $ResultsDirectory -Filter '*.cobertura.xml' -Recurse -ErrorAction SilentlyContinue)
if ($coverageFiles.Count -eq 0) {
    throw "No Cobertura report found under $ResultsDirectory."
}

# --- 2. Merge every report into one table of production methods -------------------

# Curl.Console.csproj sets <AssemblyName>curl</AssemblyName> so the binary is a drop-in
# for curl, which makes its Cobertura package "curl". Report it under its project name,
# so the table, the failing-member heading and -Library all see Curl.Console.
function Get-ReportedAssemblyName {
    param([string] $PackageName)

    if ($PackageName -ceq 'curl') { return 'Curl.Console' }
    return $PackageName
}

# The merge is C#, compiled by Add-Type, because a whole-solution run's reports are about
# 1.5 GB of XML: every test project's report lists every production assembly it loaded.
# Walked node by node in PowerShell they took CI's quality job 5 minutes 40 seconds (run
# 41239174672), which kept work/dark-factory's CI over 15 minutes (BL-3512). C# 5 only, so
# Windows PowerShell's compiler takes it as well as PowerShell 7's.
if (-not ('CurlCoverage.CoberturaMerge' -as [type])) {
    # Windows PowerShell compiles against System.dll alone unless told; PowerShell 7 already
    # references the whole runtime, and naming an assembly there replaces that set.
    $mergeReferences = @{}
    if ($PSVersionTable.PSEdition -ne 'Core') { $mergeReferences['ReferencedAssemblies'] = @('System.Xml') }
    Add-Type @mergeReferences -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Xml;

namespace CurlCoverage
{
    public sealed class MergedLine
    {
        public int Hits;
        public int ConditionsCovered;
        public int ConditionsTotal;
    }

    public sealed class MergedMethod
    {
        public string Assembly;
        public string Class;
        public string Member;
        public string SourceFile;
        public int Complexity;
        public Dictionary<int, MergedLine> Lines = new Dictionary<int, MergedLine>();
    }

    public static class CoberturaMerge
    {
        private static readonly Regex ConditionCoverage = new Regex(@"\((\d+)/(\d+)\)", RegexOptions.CultureInvariant);

        // Merges one Cobertura file into methods, keeping the best hit and condition counts
        // seen for each line. reportedName maps a package name to the assembly name to report
        // (curl becomes Curl.Console); isProduction says which assemblies to keep.
        public static void AddReport(string path, Dictionary<string, MergedMethod> methods, Func<string, string> reportedName, Func<string, bool> isProduction)
        {
            XmlReaderSettings settings = new XmlReaderSettings();
            settings.DtdProcessing = DtdProcessing.Ignore;
            settings.IgnoreComments = true;
            settings.IgnoreWhitespace = true;
            using (XmlReader reader = XmlReader.Create(path, settings))
            {
                string assembly = null;
                bool keepPackage = false;
                string className = null;
                string sourceFile = null;
                MergedMethod method = null;
                while (reader.Read())
                {
                    if (reader.NodeType == XmlNodeType.EndElement)
                    {
                        if (reader.LocalName == "method") { method = null; }
                        continue;
                    }
                    if (reader.NodeType != XmlNodeType.Element) { continue; }
                    switch (reader.LocalName)
                    {
                        case "package":
                            assembly = reportedName(reader.GetAttribute("name") ?? string.Empty);
                            keepPackage = isProduction(assembly);
                            method = null;
                            break;
                        case "class":
                            className = reader.GetAttribute("name");
                            sourceFile = reader.GetAttribute("filename");
                            method = null;
                            break;
                        case "method":
                            method = keepPackage ? FindOrAddMethod(reader, methods, assembly, className, sourceFile) : null;
                            if (reader.IsEmptyElement) { method = null; }
                            break;
                        case "line":
                            // Lines outside a method (a class's own lines list) repeat the
                            // methods' lines, so only a method's lines are merged.
                            if (method != null) { MergeLine(reader, method); }
                            break;
                    }
                }
            }
        }

        private static MergedMethod FindOrAddMethod(XmlReader reader, Dictionary<string, MergedMethod> methods, string assembly, string className, string sourceFile)
        {
            string member = reader.GetAttribute("name") + reader.GetAttribute("signature");
            string key = assembly + "|" + className + "|" + member;
            MergedMethod entry;
            if (!methods.TryGetValue(key, out entry))
            {
                entry = new MergedMethod();
                entry.Assembly = assembly;
                entry.Class = className;
                entry.Member = member;
                entry.SourceFile = sourceFile;
                methods.Add(key, entry);
            }
            // Complexity is a property of the code, not of the run, so every report that saw
            // this method agrees on it and the maximum is simply the value.
            int complexity = ParseInt(reader.GetAttribute("complexity"));
            if (complexity > entry.Complexity) { entry.Complexity = complexity; }
            return entry;
        }

        private static void MergeLine(XmlReader reader, MergedMethod method)
        {
            int number = ParseInt(reader.GetAttribute("number"));
            int hits = ParseInt(reader.GetAttribute("hits"));
            int covered = 0;
            int total = 0;
            string condition = reader.GetAttribute("condition-coverage");
            if (condition != null)
            {
                Match match = ConditionCoverage.Match(condition);
                if (match.Success)
                {
                    covered = ParseInt(match.Groups[1].Value);
                    total = ParseInt(match.Groups[2].Value);
                }
            }
            MergedLine line;
            if (!method.Lines.TryGetValue(number, out line))
            {
                line = new MergedLine();
                line.Hits = hits;
                line.ConditionsCovered = covered;
                line.ConditionsTotal = total;
                method.Lines.Add(number, line);
                return;
            }
            if (hits > line.Hits) { line.Hits = hits; }
            if (covered > line.ConditionsCovered) { line.ConditionsCovered = covered; }
            if (total > line.ConditionsTotal) { line.ConditionsTotal = total; }
        }

        // As PowerShell's [int] cast: a missing attribute is 0. A hit count past Int32 is
        // capped rather than thrown on, since only zero against nonzero matters.
        private static int ParseInt(string text)
        {
            if (string.IsNullOrEmpty(text)) { return 0; }
            long value;
            if (!long.TryParse(text, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out value)) { return 0; }
            return value > int.MaxValue ? int.MaxValue : (int)value;
        }
    }
}
'@
}

$methods = New-Object 'System.Collections.Generic.Dictionary[string,CurlCoverage.MergedMethod]'
$reportedAssemblyName = [Func[string, string]] { param($name) Get-ReportedAssemblyName $name }
$isProductionAssembly = [Func[string, bool]] { param($name) Test-IsProductionAssembly $name }

function Add-CoberturaReports {
    # Merges each Cobertura file into $methods, keeping the best hit and condition counts
    # seen for each line.
    param([System.IO.FileInfo[]] $Files)

    foreach ($file in $Files) {
        [CurlCoverage.CoberturaMerge]::AddReport($file.FullName, $methods, $reportedAssemblyName, $isProductionAssembly)
    }
}

Add-CoberturaReports -Files $coverageFiles

# --- 3. Score each method ---------------------------------------------------------

function Get-ScoredMembers {
    # Scores every merged method against the thresholds.
    $scored = New-Object System.Collections.Generic.List[object]

    foreach ($entry in $methods.Values) {
        $lineNumbers = @($entry.Lines.Keys)
        $lineTotal = $lineNumbers.Count
        if ($lineTotal -eq 0) { continue }

        $missing = @($lineNumbers | Where-Object { $entry.Lines[$_].Hits -eq 0 } | Sort-Object)
        $lineRate = ($lineTotal - $missing.Count) / $lineTotal

        $branchTotal = 0
        $branchCovered = 0
        foreach ($number in $lineNumbers) {
            $branchTotal += $entry.Lines[$number].ConditionsTotal
            $branchCovered += $entry.Lines[$number].ConditionsCovered
        }
        if ($branchTotal -eq 0) { $branchRate = 1.0 } else { $branchRate = $branchCovered / $branchTotal }

        $complexity = $entry.Complexity
        $crap = ($complexity * $complexity) * [math]::Pow(1 - $lineRate, 3) + $complexity

        $failures = New-Object System.Collections.Generic.List[string]
        if (($lineRate * 100) -lt $LineThreshold) { $failures.Add('line') }
        if (($branchRate * 100) -lt $BranchThreshold) { $failures.Add('branch') }
        if ($complexity -gt $ComplexityThreshold) { $failures.Add('complexity') }
        if ($crap -gt $CrapThreshold) { $failures.Add('crap') }

        $relativeFile = $entry.SourceFile
        if ($relativeFile -and $relativeFile.StartsWith($repositoryRoot, [StringComparison]::OrdinalIgnoreCase)) {
            $relativeFile = $relativeFile.Substring($repositoryRoot.Length).TrimStart('\', '/')
        }

        $scored.Add([pscustomobject]@{
                Assembly      = $entry.Assembly
                Class         = $entry.Class
                Member        = $entry.Member
                SourceFile    = $relativeFile
                FirstLine     = ($lineNumbers | Measure-Object -Minimum).Minimum
                LinesCovered  = $lineTotal - $missing.Count
                LinesTotal    = $lineTotal
                BranchCovered = $branchCovered
                BranchTotal   = $branchTotal
                LinePercent   = [math]::Round($lineRate * 100, 2)
                BranchPercent = [math]::Round($branchRate * 100, 2)
                Complexity    = $complexity
                Crap          = [math]::Round($crap, 2)
                MissingLines  = $missing
                Failures      = $failures
            })
    }
    # The comma keeps PowerShell from unrolling the list on return.
    return ,$scored
}

$scored = Get-ScoredMembers

# Once in a while a whole-solution run lost one project's coverage although its tests
# all passed: CI run 38700283564 lost Curl.Cryptography.UnitTests' and reported 108
# members uncovered that the same commit's other run covered in full (BL-2269). So when a
# whole-solution run leaves a member short of line or branch coverage, run the test
# projects that reach its library again, one at a time, and merge their reports in. A
# real gap stays a gap, since merging only ever adds lines some test executed.
if ($FromShards -or (-not $SkipTestRun -and $wholeSolution)) {
    $shortAssemblies = @($scored |
        Where-Object { $_.Failures -contains 'line' -or $_.Failures -contains 'branch' } |
        Select-Object -ExpandProperty Assembly -Unique)
    if ($shortAssemblies.Count) {
        $recheckTargets = @(Get-TestProjectsReaching -Root $repositoryRoot -Library $shortAssemblies -IncludeIntegration:$IncludeIntegration)
        if ($recheckTargets.Count) {
            Write-Host "Coverage short in $($shortAssemblies -join ', '); measuring the test projects that reach them again." -ForegroundColor Yellow
            $recheckDirectory = Join-Path $ResultsDirectory 'recheck'
            Invoke-CoverageTestRun -Targets $recheckTargets -Directory $recheckDirectory
            Add-CoberturaReports -Files @(Get-ChildItem -Path $recheckDirectory -Filter '*.cobertura.xml' -Recurse -ErrorAction SilentlyContinue)
            $scored = Get-ScoredMembers
        }
    }
}

# A -FromShards run fails when a gated library is not in the merged reports at all, so a
# shard that lost a library can never pass by leaving it out (BL-3464).
$missingLibraries = @()
if ($FromShards) {
    $measured = @($scored | Select-Object -ExpandProperty Assembly -Unique)
    $missingLibraries = @(Get-GatedLibraries -Root $repositoryRoot | Where-Object { $measured -notcontains $_ })
}

# ToArray rather than @(...): Windows PowerShell 5.1 throws "Argument types do not
# match" when @() wraps a List[object] whose elements are PSCustomObjects.
$selected = $scored.ToArray()
if ($Library) {
    $selected = @($scored | Where-Object {
            $name = $_.Assembly
            @($Library | Where-Object { $name -like $_ }).Count -gt 0
        })
}

# --- 4. Report --------------------------------------------------------------------

$report = New-Object System.Collections.Generic.List[string]
$report.Add('# Curl code-quality audit')
$report.Add('')
$report.Add(('Measured {0}. Thresholds: line {1}%, branch {2}%, cyclomatic complexity {3}, CRAP {4}.' -f
    (Get-Date -Format 'yyyy-MM-dd HH:mm'), $LineThreshold, $BranchThreshold, $ComplexityThreshold, $CrapThreshold))
$report.Add('')

$assemblies = @($selected | Select-Object -ExpandProperty Assembly -Unique | Sort-Object)
$totalViolations = 0

$report.Add('## Libraries')
$report.Add('')
$report.Add('| Library | Line % | Branch % | Members | Failing | Worst CRAP |')
$report.Add('| --- | ---: | ---: | ---: | ---: | ---: |')

$jsonLibraries = New-Object System.Collections.Generic.List[object]
$jsonFailing = New-Object System.Collections.Generic.List[object]

foreach ($assembly in $assemblies) {
    $members = @($selected | Where-Object { $_.Assembly -eq $assembly })
    $failing = @($members | Where-Object { $_.Failures.Count -gt 0 })
    $totalViolations += $failing.Count

    # Weighted by line, not an average of per-member percentages: a one-line property
    # left uncovered should not cost as much as a forty-line method left uncovered.
    $linesTotal = ($members | Measure-Object -Property LinesTotal -Sum).Sum
    $linesCovered = ($members | Measure-Object -Property LinesCovered -Sum).Sum
    $branchTotal = ($members | Measure-Object -Property BranchTotal -Sum).Sum
    $branchCovered = ($members | Measure-Object -Property BranchCovered -Sum).Sum

    if ($linesTotal -eq 0) { $linePercent = 100 } else { $linePercent = [math]::Round(100 * $linesCovered / $linesTotal, 2) }
    if ($branchTotal -eq 0) { $branchPercent = 100 } else { $branchPercent = [math]::Round(100 * $branchCovered / $branchTotal, 2) }
    $worstCrap = ($members | Measure-Object -Property Crap -Maximum).Maximum

    $report.Add(('| {0} | {1} | {2} | {3} | {4} | {5} |' -f
            $assembly, $linePercent, $branchPercent, $members.Count, $failing.Count, $worstCrap))
    $jsonLibraries.Add([ordered]@{
            name = $assembly; linePercent = $linePercent; branchPercent = $branchPercent
            linesCovered = [int]$linesCovered; linesTotal = [int]$linesTotal
            branchesCovered = [int]$branchCovered; branchesTotal = [int]$branchTotal
            members = $members.Count; failingMembers = $failing.Count; worstCrap = $worstCrap
        })
}

$report.Add('')

foreach ($assembly in $assemblies) {
    $failing = @($selected |
        Where-Object { $_.Assembly -eq $assembly -and $_.Failures.Count -gt 0 } |
        Sort-Object -Property Crap -Descending)
    if ($failing.Count -eq 0) { continue }

    $report.Add(('## {0} - {1} failing member(s)' -f $assembly, $failing.Count))
    $report.Add('')
    $report.Add('| Member | Where | Line % | Branch % | Cx | CRAP | Fails | Uncovered lines |')
    $report.Add('| --- | --- | ---: | ---: | ---: | ---: | --- | --- |')

    foreach ($member in $failing) {
        $shown = @($member.MissingLines | Select-Object -First $MaxMissingLinesShown)
        $missingText = ''
        if ($shown.Count -gt 0) {
            $missingText = ($shown -join ', ')
            if ($member.MissingLines.Count -gt $shown.Count) {
                $missingText += (' (+{0} more)' -f ($member.MissingLines.Count - $shown.Count))
            }
        }

        # An async method is compiled into a state-machine class, so coverage arrives as
        # Owner.<TheMethod>d__14.MoveNext(). Reported literally, nobody can find it;
        # reported as Owner.TheMethod (async), it is where the developer left it.
        $segments = @($member.Class -split '\.')
        $shortClass = $segments[-1]
        $displayMember = $member.Member
        if ($shortClass -match '^<(?<method>[^>]+)>d__\d+$') {
            $displayMember = $Matches['method'] + ' (async)'
            if ($segments.Count -ge 2) { $shortClass = $segments[-2] }
        }

        $report.Add(('| `{0}.{1}` | {2}:{3} | {4} | {5} | {6} | {7} | {8} | {9} |' -f
                $shortClass, $displayMember, $member.SourceFile, $member.FirstLine,
                $member.LinePercent, $member.BranchPercent, $member.Complexity, $member.Crap,
                ($member.Failures -join ' '), $missingText))
        $jsonFailing.Add([ordered]@{
                library = $assembly; member = "$shortClass.$displayMember"
                file = "$($member.SourceFile)"; line = [int]$member.FirstLine
                linePercent = $member.LinePercent; branchPercent = $member.BranchPercent
                complexity = [int]$member.Complexity; crap = $member.Crap
                fails = @($member.Failures); uncoveredLines = $missingText
            })
    }
    $report.Add('')
}

# An excluded member is not in the coverage report at all, so exclusions are invisible
# unless they are listed on purpose. An [ExcludeFromCodeCoverage] with no justifying
# comment above it is a finding in its own right. Files in test projects of both kinds,
# *.UnitTests and *.IntegrationTests, are not production code and are skipped. Both
# separator classes match / as well as \, so the scan is right off Windows too.
$excludeHits = @(Get-ChildItem -Path $repositoryRoot -Filter '*.cs' -Recurse |
    Where-Object { $_.FullName -notmatch '[\\/](bin|obj|data)[\\/]' -and $_.FullName -notmatch '\.(UnitTests|IntegrationTests)[\\/]' } |
    Select-String -Pattern 'ExcludeFromCodeCoverage' -SimpleMatch)

$report.Add('## Coverage exclusions in production code')
$report.Add('')
if ($excludeHits.Count -eq 0) {
    $report.Add('None. Every production member is measured.')
}
else {
    foreach ($hit in $excludeHits) {
        $path = $hit.Path
        if ($path.StartsWith($repositoryRoot, [StringComparison]::OrdinalIgnoreCase)) {
            $path = $path.Substring($repositoryRoot.Length).TrimStart('\', '/')
        }
        $report.Add(('- `{0}:{1}` - {2}' -f $path, $hit.LineNumber, $hit.Line.Trim()))
    }
}

$report.Add('')
if ($missingLibraries.Count) {
    $report.Add('## Gated libraries missing from the shards'' reports')
    $report.Add('')
    foreach ($name in $missingLibraries) { $report.Add("- $name") }
    $report.Add('')
}
$report.Add(('**{0} failing member(s) across {1} production assembly/assemblies.**' -f $totalViolations, $assemblies.Count))

$text = $report -join [Environment]::NewLine
Write-Output $text

if ($ReportPath) {
    $text | Out-File -FilePath $ReportPath -Encoding utf8
    Write-Host "Report written to $ReportPath" -ForegroundColor Cyan
}

# The same numbers as data, for the published coverage report (.github/coverage).
if ($JsonPath) {
    $allLines = ($selected | Measure-Object -Property LinesTotal -Sum).Sum
    $allLinesCovered = ($selected | Measure-Object -Property LinesCovered -Sum).Sum
    $allBranches = ($selected | Measure-Object -Property BranchTotal -Sum).Sum
    $allBranchesCovered = ($selected | Measure-Object -Property BranchCovered -Sum).Sum
    $exclusions = @($excludeHits | ForEach-Object {
            $path = $_.Path
            if ($path.StartsWith($repositoryRoot, [StringComparison]::OrdinalIgnoreCase)) { $path = $path.Substring($repositoryRoot.Length).TrimStart('\', '/') }
            [ordered]@{ file = $path; line = [int]$_.LineNumber; text = $_.Line.Trim() }
        })
    $data = [ordered]@{
        measuredAt = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
        thresholds = [ordered]@{ linePercent = $LineThreshold; branchPercent = $BranchThreshold; complexity = $ComplexityThreshold; crap = $CrapThreshold }
        totals = [ordered]@{
            linePercent = $(if ($allLines) { [math]::Round(100 * $allLinesCovered / $allLines, 2) } else { 100 })
            branchPercent = $(if ($allBranches) { [math]::Round(100 * $allBranchesCovered / $allBranches, 2) } else { 100 })
            linesCovered = [int]$allLinesCovered; linesTotal = [int]$allLines
            branchesCovered = [int]$allBranchesCovered; branchesTotal = [int]$allBranches
            libraries = $assemblies.Count
            librariesAtGate = @($jsonLibraries | Where-Object { $_.failingMembers -eq 0 }).Count
            failingMembers = $totalViolations
        }
        libraries = $jsonLibraries.ToArray()
        failing = $jsonFailing.ToArray()
        exclusions = $exclusions
        missingLibraries = @($missingLibraries)
    }
    $data | ConvertTo-Json -Depth 6 | Out-File -FilePath $JsonPath -Encoding utf8
    Write-Host "Data written to $JsonPath" -ForegroundColor Cyan
}

if ($totalViolations -gt 0 -or $missingLibraries.Count -gt 0) { exit 1 }
exit 0
