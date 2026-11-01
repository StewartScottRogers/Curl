# The checkout's sync with origin, failed-test names and projects, which test projects a
# change affects, and -TestAffectedTests and -TestFlakyTests. Dot-sourced by
# RunDarkFactory.ps1 in its order; see the loader there.

# ---------------------------------------------------------------------------- git

function Get-Dirty { return @(git -C $Root status --porcelain) | Where-Object { $_ } }

function Sync-CheckoutWithOrigin {
    # Brings a clean, freshly fetched checkout level with origin/<Branch> before a shift
    # starts. Only behind is fast-forwarded (BL-1141): a lane pushed after the last shift
    # synced. Ahead or diverged needs a person, so it returns the refusal text; $null
    # means the checkout now matches origin.
    param([string]$Repo, [string]$Branch)
    $head = "$(git -C $Repo rev-parse HEAD)".Trim()
    $remote = "$(git -C $Repo rev-parse "origin/$Branch")".Trim()
    if ($head -eq $remote) { return $null }
    git -C $Repo merge-base --is-ancestor $head $remote 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) {
        git -C $Repo merge-base --is-ancestor $remote $head 2>&1 | Out-Null
        $how = if ($LASTEXITCODE -eq 0) { 'is ahead of' } else { 'has diverged from' }
        return "$Branch $how origin/$Branch; push or reconcile first"
    }
    $behind = "$(git -C $Repo rev-list --count "$head..$remote")".Trim()
    git -C $Repo merge -q --ff-only "origin/$Branch" 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) { return "$Branch is behind origin/$Branch and git merge --ff-only failed; pull first" }
    Write-Trace '-' 'sync' "fast-forwarded $Branch $behind commit(s) to origin/$Branch"
    return $null
}

function Get-FailedTestNames {
    # What a dotnet test run's -Output says failed, as one short line for a trace or a park
    # reason: "Curl.Quic.UnitTests: Loop_A, Loop_B", "a test host aborted", or "no test
    # named" when the run was red without naming one (BL-898).
    param([string[]]$Output)
    $tests = @($Output | ForEach-Object { if ($_ -match '^\s+Failed\s+(\S+)\s+\[') { $Matches[1] } } | Select-Object -Unique)
    $assemblies = @($Output | ForEach-Object { if ($_ -match '^Failed!\s.*?-\s+([\w.]+)\.dll') { $Matches[1] } } | Select-Object -Unique)
    $aborted = [bool]($Output | Where-Object { $_ -match 'test run was aborted|Test host process crashed|hang timeout' })
    $parts = @()
    if ($tests.Count) {
        $shown = ($tests | Select-Object -First 4) -join ', '
        if ($tests.Count -gt 4) { $shown += " and $($tests.Count - 4) more" }
        $parts += $(if ($assemblies.Count) { "$($assemblies -join ', '): $shown" } else { $shown })
    } elseif ($assemblies.Count) { $parts += "$($assemblies -join ', ') failed" }
    if ($aborted) { $parts += 'a test host aborted' }
    if (-not $parts.Count) {
        # Red without a name parked BL-1649 and BL-1588 with nothing to go on (AF-0093), so
        # the last line that looks like an error says why, when there is one.
        $why = @($Output | Where-Object { $_ -match '(?i)\berror\b|exited with|exception' } | Select-Object -Last 1)
        if (-not $why.Count) { return 'no test named' }
        $line = "$($why[0])".Trim()
        if ($line.Length -gt 120) { $line = $line.Substring(0, 117) + '...' }
        return "no test named: $line"
    }
    return $parts -join '; '
}

function Get-FailedTestProjects {
    # The test projects a red dotnet test run's -Output gives no reason to trust, so it can be
    # run again with only them (AF-0092): those it names as failed, and those of -TestProjects
    # it never reports as passed - a host that crashed, or a run red without naming a test,
    # leaves no summary line for its project (AF-0093). Empty when more than -Most are in
    # doubt: then the run broke as a whole, and nothing smaller can stand in for it.
    param([string[]]$Output, [string[]]$TestProjects = @(), [int]$Most = 10)
    $failed = @($Output | ForEach-Object { if ($_ -match '^Failed!\s.*?-\s+([\w.]+)\.dll') { $Matches[1] } })
    $passed = @($Output | ForEach-Object { if ($_ -match '^Passed!\s.*?-\s+([\w.]+)\.dll') { $Matches[1] } })
    $unreported = @($TestProjects | Where-Object { $_ -notin $passed -and $_ -notin $failed })
    $doubt = @(@($failed) + @($unreported) | Select-Object -Unique)
    if ($doubt.Count -gt $Most) { return @() }
    return $doubt
}

function Get-UnitTestProjectNames {
    # The *.UnitTests projects a fast-test run of -Repo reports on: each prints a Passed! or
    # Failed! summary. *.IntegrationTests print none under the fast filter, so they are left out.
    param([string]$Repo)
    return @(Get-ChildItem -Path $Repo -Directory -Filter '*.UnitTests' | Where-Object { Test-Path (Join-Path $_.FullName "$($_.Name).csproj") } | ForEach-Object Name)
}

function Get-AffectedTestProjects {
    # The *.UnitTests projects of -Repo whose fast tests the commits in <Base>...HEAD can change
    # (BL-2967): those of the projects the commits change, and of every project that depends on
    # one of them, however indirectly. Returns All (every test project must run: a shared build
    # file or a .csproj changed, or git could not say what changed), Projects (the names, sorted)
    # and Why, one line for the trace. The repository is flat, so a path's project is its first
    # folder when that folder holds <folder>.csproj; Tasks, Documentation, .claude, .github and
    # scripts at the root are in no project and select nothing.
    param([string]$Repo, [string]$Base)
    $all = Get-UnitTestProjectNames $Repo
    # The three-dot form diffs HEAD against its merge base with -Base: after the rebase that is
    # -Base itself, and before one it is still only this lane's own commits. --no-renames lists
    # both ends of a move, so a file moved out of a project counts for it too.
    $changed = @(git -C $Repo diff --name-only --no-renames "$Base...HEAD" 2>$null | Where-Object { $_ })
    if ($LASTEXITCODE -ne 0) { return [pscustomobject]@{ All = $true; Projects = $all; Why = "all $($all.Count) test projects: git diff $Base...HEAD failed" } }
    $projects = @(Get-ChildItem -Path $Repo -Directory | Where-Object { Test-Path (Join-Path $_.FullName "$($_.Name).csproj") } | ForEach-Object Name)
    # Every project builds against these, and a .csproj can change any project's references
    # (and some tests read the .csproj files themselves), so any of them means every test.
    $shared = '(?i)^(Directory\.Build\.[^/]+|Directory\.Packages\.props|global\.json|NuGet\.config|[^/]+\.slnx?|[^/]+\.props|[^/]+\.targets|CodeMetricsConfig\.txt|\.editorconfig|MSTestSettings\.cs)$'
    $projectBuild = '(?i)(^|/)(Directory\.Build\.[^/]+|[^/]+\.props|[^/]+\.targets|[^/]+\.csproj|\.editorconfig)$'
    $sharedHit = @($changed | Where-Object { $_ -match $shared -or ($_ -match $projectBuild -and ($_.Split('/')[0] -in $projects -or $_ -match '^([^/]+)/\1\.csproj$')) })
    if ($sharedHit.Count) { return [pscustomobject]@{ All = $true; Projects = $all; Why = "all $($all.Count) test projects: $(($sharedHit | Select-Object -First 3) -join ', ') changed" } }
    $touched = @($changed | Where-Object { $_.Contains('/') } | ForEach-Object { $_.Split('/')[0] } | Where-Object { $_ -in $projects } | Sort-Object -Unique)
    # Who depends on whom. A ProjectReference passes a change on to every project above it. An
    # item that links a file in from another project's folder (Compile
    # Include="..\Curl.Kerberos.UnitTests\FakeKdc.cs") passes on only a change to that folder
    # itself: what the linked file uses, its linker must reference on its own.
    $dependents = @{}
    $linkers = @{}
    foreach ($project in $projects) {
        $text = Get-Content -Raw -LiteralPath (Join-Path $Repo "$project\$project.csproj")
        foreach ($m in [regex]::Matches("$text", '<(\w+)\s[^>]*?Include="\.\.[\\/]([^\\/"]+)[\\/]')) {
            $used = $m.Groups[2].Value
            if ($used -eq $project -or $used -notin $projects) { continue }
            $edges = if ($m.Groups[1].Value -eq 'ProjectReference') { $dependents } else { $linkers }
            if (-not $edges.ContainsKey($used)) { $edges[$used] = New-Object System.Collections.Generic.List[string] }
            $edges[$used].Add($project)
        }
    }
    $reached = New-Object System.Collections.Generic.HashSet[string]
    $queue = New-Object System.Collections.Generic.Queue[string]
    $seeds = @($touched) + @($touched | Where-Object { $linkers.ContainsKey($_) } | ForEach-Object { $linkers[$_] })
    foreach ($project in $seeds) { if ($reached.Add($project)) { $queue.Enqueue($project) } }
    while ($queue.Count) {
        $project = $queue.Dequeue()
        if (-not $dependents.ContainsKey($project)) { continue }
        foreach ($dependent in $dependents[$project]) { if ($reached.Add($dependent)) { $queue.Enqueue($dependent) } }
    }
    $selected = @($reached | Where-Object { $_ -in $all } | Sort-Object)
    if (-not $touched.Count) { return [pscustomobject]@{ All = $false; Projects = @(); Why = 'no test projects: the commits change no project' } }
    $names = ($touched | Select-Object -First 3) -join ', '
    if ($touched.Count -gt 3) { $names += " and $($touched.Count - 3) more" }
    return [pscustomobject]@{ All = $false; Projects = $selected; Why = "$($selected.Count) of $($all.Count) test projects: $names changed" }
}

if ($TestAffectedTests) {
    # A throwaway repository with made-up projects, never built: Lib.B.UnitLibrary uses
    # Lib.A.UnitLibrary, each library has its tests, and Lib.D.UnitTests links a fake in from
    # Lib.C.UnitTests' folder. Each case commits one change on top of the same base.
    $repo = Join-Path ([IO.Path]::GetTempPath()) "df-affected-tests-$PID"
    New-Item -ItemType Directory -Force -Path $repo | Out-Null
    git init -q -b work $repo 2>&1 | Out-Null
    git -C $repo config user.name t; git -C $repo config user.email t@t
    $uses = @{
        'Lib.A.UnitLibrary' = @(); 'Lib.A.UnitTests' = @('<ProjectReference Include="..\Lib.A.UnitLibrary\Lib.A.UnitLibrary.csproj" />')
        'Lib.B.UnitLibrary' = @('<ProjectReference Include="..\Lib.A.UnitLibrary\Lib.A.UnitLibrary.csproj" />')
        'Lib.B.UnitTests' = @('<ProjectReference Include="..\Lib.B.UnitLibrary\Lib.B.UnitLibrary.csproj" />')
        'Lib.C.UnitLibrary' = @(); 'Lib.C.UnitTests' = @('<ProjectReference Include="..\Lib.C.UnitLibrary\Lib.C.UnitLibrary.csproj" />')
        'Lib.D.UnitTests' = @('<Compile Include="..\Lib.C.UnitTests\Fake.cs" Link="Fake.cs" />')
    }
    foreach ($project in $uses.Keys) {
        New-Item -ItemType Directory -Force -Path (Join-Path $repo $project) | Out-Null
        Set-Content -Path (Join-Path $repo "$project\$project.csproj") -Value (@('<Project Sdk="Microsoft.NET.Sdk">', '  <ItemGroup>') + @($uses[$project] | ForEach-Object { "    $_" }) + @('  </ItemGroup>', '</Project>'))
        Set-Content -Path (Join-Path $repo "$project\Code.cs") -Value '// code'
    }
    New-Item -ItemType Directory -Force -Path (Join-Path $repo 'Tasks'), (Join-Path $repo 'Lib.C.UnitLibrary\Build') | Out-Null
    Set-Content -Path (Join-Path $repo 'Directory.Build.props') -Value '<Project />'
    Set-Content -Path (Join-Path $repo 'Lib.C.UnitTests\Fake.cs') -Value '// fake'
    Set-Content -Path (Join-Path $repo 'Tasks\BL-001-x.md') -Value 'task'
    git -C $repo add -A 2>&1 | Out-Null
    git -C $repo commit -q -m base 2>&1 | Out-Null
    $base = "$(git -C $repo rev-parse HEAD)".Trim()
    $select = {
        param([string[]]$Paths, [string]$From = $base)
        git -C $repo checkout -q --detach $base 2>&1 | Out-Null
        foreach ($path in $Paths) { Add-Content -Path (Join-Path $repo $path) -Value '// changed' }
        git -C $repo add -A 2>&1 | Out-Null
        git -C $repo commit -q -m change 2>&1 | Out-Null
        $r = Get-AffectedTestProjects -Repo $repo -Base $From
        if ($r.All) { 'all' } elseif ($r.Projects.Count) { $r.Projects -join ',' } else { 'none' }
    }
    $cases = @(
        ,@('a library change selects its tests and its dependents'' tests, not others', 'Lib.A.UnitTests,Lib.B.UnitTests', (& $select @('Lib.A.UnitLibrary\Code.cs')))
        ,@('a change with no dependents selects only its own tests', 'Lib.C.UnitTests', (& $select @('Lib.C.UnitLibrary\Code.cs')))
        ,@('a test project whose file another links in selects both', 'Lib.C.UnitTests,Lib.D.UnitTests', (& $select @('Lib.C.UnitTests\Fake.cs')))
        ,@('a change only to Tasks selects no test project', 'none', (& $select @('Tasks\BL-001-x.md')))
        ,@('a change to the root Directory.Build.props selects all', 'all', (& $select @('Directory.Build.props')))
        ,@('a .props nested in a project folder selects all', 'all', (& $select @('Lib.C.UnitLibrary\Build\Extra.props')))
        ,@('a .csproj change selects all', 'all', (& $select @('Lib.C.UnitLibrary\Lib.C.UnitLibrary.csproj')))
        ,@('a base git cannot read selects all', 'all', (& $select @('Lib.C.UnitLibrary\Code.cs') 'no-such-ref'))
        ,@('the trace line says how many and why', '1 of 4 test projects: Lib.C.UnitLibrary changed', $(& $select @('Lib.C.UnitLibrary\Code.cs') | Out-Null; (Get-AffectedTestProjects -Repo $repo -Base $base).Why)))
    $failed = 0
    foreach ($case in $cases) {
        if ($case[1] -ceq $case[2]) { Write-Host "PASS $($case[0]): $($case[2])" -ForegroundColor Green }
        else { Write-Host "FAIL $($case[0]): expected $($case[1]), got $($case[2])" -ForegroundColor Red; $failed++ }
    }
    Remove-Item -Recurse -Force -Path $repo -ErrorAction SilentlyContinue
    exit $(if ($failed) { 1 } else { 0 })
}

if ($TestFlakyTests) {
    $cases = @(
        ,@('one named failure', 'Curl.Quic.UnitTests: Loop_ServerGoesSilent_SendsKeepAlivesThenFailsWithTheIdleTimeout', (Get-FailedTestNames @(
            'Passed!  - Failed:     0, Passed:    44, Skipped:     0, Total:    44, Duration: 140 ms - Curl.Original.Protocol.Dict.UnitTests.dll (net10.0)',
            '  Failed Loop_ServerGoesSilent_SendsKeepAlivesThenFailsWithTheIdleTimeout [484 ms]',
            'Failed!  - Failed:     1, Passed:   398, Skipped:     0, Total:   399, Duration: 1 s - Curl.Quic.UnitTests.dll (net10.0)')))
        ,@('many named failures', 'Curl.Cli.UnitTests, Curl.Core.UnitTests: A, B, C, D and 1 more', (Get-FailedTestNames @(
            '  Failed A [1 ms]', '  Failed B [1 ms]', '  Failed C [1 ms]',
            'Failed!  - Failed:     3, Passed:     1, Skipped:     0, Total:     4, Duration: 1 s - Curl.Cli.UnitTests.dll (net10.0)',
            '  Failed D [1 ms]', '  Failed E [1 ms]',
            'Failed!  - Failed:     2, Passed:     1, Skipped:     0, Total:     3, Duration: 1 s - Curl.Core.UnitTests.dll (net10.0)')))
        ,@('aborted host', 'a test host aborted', (Get-FailedTestNames @('The active test run was aborted. Reason: Test host process crashed')))
        ,@('red without a name', 'no test named', (Get-FailedTestNames @('Passed!  - Failed:     0, Passed:     1 - Curl.Core.UnitTests.dll (net10.0)')))
        ,@('failed projects to rerun alone', 'Curl.Cli.UnitTests,Curl.Cookies.UnitTests', ((Get-FailedTestProjects @(
            '  Failed A [1 ms]',
            'Failed!  - Failed:     1, Passed:     1, Skipped:     0, Total:     2, Duration: 1 s - Curl.Cli.UnitTests.dll (net10.0)',
            'Passed!  - Failed:     0, Passed:     1 - Curl.Core.UnitTests.dll (net10.0)',
            'Failed!  - Failed:     1, Passed:     1, Skipped:     0, Total:     2, Duration: 1 s - Curl.Cookies.UnitTests.dll (net10.0)')) -join ','))
        ,@('red without a name says the error line', 'no test named: Testhost process for source(s) Curl.Ftp.UnitTests.dll exited with error: x', (Get-FailedTestNames @(
            'Passed!  - Failed:     0, Passed:     1 - Curl.Core.UnitTests.dll (net10.0)',
            'Testhost process for source(s) Curl.Ftp.UnitTests.dll exited with error: x')))
        ,@('crashed host project rerun alone', 'Curl.Cli.UnitTests,Curl.Ftp.UnitTests', ((Get-FailedTestProjects -TestProjects 'Curl.Cli.UnitTests', 'Curl.Core.UnitTests', 'Curl.Ftp.UnitTests' -Output @(
            'Failed!  - Failed:     1, Passed:     1 - Curl.Cli.UnitTests.dll (net10.0)',
            'Passed!  - Failed:     0, Passed:     1 - Curl.Core.UnitTests.dll (net10.0)',
            'The active test run was aborted. Reason: Test host process crashed')) -join ','))
        ,@('unreported project rerun alone when none is named', 'Curl.Ftp.UnitTests', ((Get-FailedTestProjects -TestProjects 'Curl.Core.UnitTests', 'Curl.Ftp.UnitTests' -Output @(
            'Passed!  - Failed:     0, Passed:     1 - Curl.Core.UnitTests.dll (net10.0)')) -join ','))
        ,@('no project to rerun when the whole run broke', '', ((Get-FailedTestProjects -Most 1 -TestProjects 'Curl.Core.UnitTests', 'Curl.Ftp.UnitTests' -Output @('error MSB1009')) -join ','))
        ,@('no project to rerun when none is named or known', '', ((Get-FailedTestProjects @('Passed!  - Failed:     0, Passed:     1 - Curl.Core.UnitTests.dll (net10.0)')) -join ',')))
    $failed = 0
    foreach ($case in $cases) {
        if ($case[1] -ceq $case[2]) { Write-Host "PASS $($case[0]): $($case[2])" -ForegroundColor Green }
        else { Write-Host "FAIL $($case[0]): expected $($case[1]), got $($case[2])" -ForegroundColor Red; $failed++ }
    }
    exit $(if ($failed) { 1 } else { 0 })
}

# This file ran to its end; RunDarkFactory.ps1 goes on to the next one.
$AreaFileFinished = $true
