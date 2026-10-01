<#
.SYNOPSIS
    Breaks one library's code on purpose, one small change at a time, and reports which
    changes its tests notice.

.DESCRIPTION
    Mutation testing for the quality auditor (BL-1009, ADR-0267), with no package: PowerShell
    over git and dotnet. For -Library Curl.<Area>.UnitLibrary it checks out -Commit in a
    throwaway worktree, <repo>.audit\mutation-<stamp> beside the repository, builds and tests
    the library's twin Curl.<Area>.UnitTests once unmutated (the baseline), then for each of
    up to -MaxMutants sampled sites applies one operator change, rebuilds the twin and runs
    its fast tests. The worktree is removed at the end, even on failure, so the checkout it
    is run from is never touched. If the baseline does not build and pass, it stops, writes
    no mutants, and exits 2.

    Sites: each line of each .cs file in the library (not obj/ or bin/), skipping comment
    lines, using and namespace lines, attributes and preprocessor lines, and never inside a
    string or char literal or a trailing // comment. Operators, one mutant per occurrence:

        ==  -> !=      !=  -> ==      ' < ' -> ' <= '   ' > ' -> ' >= '
        ' <= ' -> ' < '               ' >= ' -> ' > '   &&  -> ||      ||  -> &&
        '+ 1' -> '- 1'  '- 1' -> '+ 1' (not followed by a digit)
        true -> false  false -> true  (whole words, but never the one inside
                                       ConfigureAwait(...), an equivalent mutant)
                                                            !(  -> (

    The spaces around < and > keep generics such as List<int> out. The sites are sorted by
    file, line and column, then -MaxMutants are sampled with System.Random(-Seed), so the same
    commit and seed always give the same sites.

    Outcomes, per mutant:
        killed      the build succeeded and at least one test failed
        survived    the build succeeded and every test passed: the tests did not notice
        timedOut    the tests ran past -TimeoutSeconds (counted as killed)
        stillborn   the mutant did not build (not counted)

    score = (killed + timedOut) / (killed + timedOut + survived), or null with no counted
    mutant. The twin is built with -p:TreatWarningsAsErrors=false, so an analyzer warning a
    mutant causes does not make it stillborn and hide it.

.PARAMETER Library
    The library to mutate, e.g. Curl.Protocol.Dict.UnitLibrary. Its tests are the same name
    with .UnitTests.

.PARAMETER Commit
    The commit to check out. Default HEAD.

.PARAMETER MaxMutants
    How many sites to sample. Default 50.

.PARAMETER Seed
    The sampling seed. Default 0.

.PARAMETER TimeoutSeconds
    The limit for one test run. Default 600.

.PARAMETER OutFile
    Write the JSON result here as well as to standard output.

.PARAMETER SelfTest
    Check the site finder, the operators and the baseline-failure path on samples, print
    PASS or FAIL per case, and exit.

.OUTPUTS
    JSON: { library, commit, seed, baselineMs, mutants: [ { file, line, operator, original,
    mutated, outcome, ms } ], killed, survived, timedOut, stillborn, score }, and on a failed
    baseline also baselineError.

.EXAMPLE
    powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Library Curl.Protocol.Dict.UnitLibrary -MaxMutants 5 -Seed 1 -OutFile $env:TEMP\dict.json
#>
param(
    [string]$Library,
    [string]$Commit = 'HEAD',
    [int]$MaxMutants = 50,
    [int]$Seed = 0,
    [int]$TimeoutSeconds = 600,
    [string]$OutFile,
    [switch]$SelfTest
)

$ErrorActionPreference = 'Stop'

$Operators = @(
    @{ Name = '=='; Pattern = '=='; To = '!=' },
    @{ Name = '!='; Pattern = '!='; To = '==' },
    @{ Name = '<'; Pattern = ' < '; To = ' <= ' },
    @{ Name = '>'; Pattern = ' > '; To = ' >= ' },
    @{ Name = '<='; Pattern = ' <= '; To = ' < ' },
    @{ Name = '>='; Pattern = ' >= '; To = ' > ' },
    @{ Name = '&&'; Pattern = '&&'; To = '||' },
    @{ Name = '||'; Pattern = '\|\|'; To = '&&' },
    @{ Name = '+1'; Pattern = '\+ 1(?!\d)'; To = '- 1' },
    @{ Name = '-1'; Pattern = '- 1(?!\d)'; To = '+ 1' },
    @{ Name = 'true'; Pattern = '\btrue\b'; To = 'false' },
    @{ Name = 'false'; Pattern = '\bfalse\b'; To = 'true' },
    @{ Name = '!('; Pattern = '!\('; To = '(' }
)
# Literal patterns are regex-escaped once here; the rest are already regexes.
foreach ($op in $Operators) {
    if ($op.Name -in '==', '!=', '<', '>', '<=', '>=', '&&') { $op.Pattern = [regex]::Escape($op.Pattern) }
}

function Get-MaskedLine([string]$Line) {
    # The line with every string and char literal's text, and any trailing // comment,
    # replaced by spaces, so operators are only found in code. Lengths are kept, so a match
    # index in the masked line is the same column in the real one.
    $chars = $Line.ToCharArray()
    $i = 0
    while ($i -lt $chars.Length) {
        $c = $chars[$i]
        if ($c -eq '/' -and $i + 1 -lt $chars.Length -and $chars[$i + 1] -eq '/') {
            for ($j = $i; $j -lt $chars.Length; $j++) { $chars[$j] = ' ' }
            break
        }
        if ($c -eq '"') {
            $verbatim = ($i -gt 0 -and $chars[$i - 1] -eq '@') -or ($i -gt 1 -and $chars[$i - 1] -eq '$' -and $chars[$i - 2] -eq '@') -or ($i -gt 1 -and $chars[$i - 1] -eq '@' -and $chars[$i - 2] -eq '$')
            $j = $i + 1
            while ($j -lt $chars.Length) {
                if (-not $verbatim -and $chars[$j] -eq '\') { $chars[$j] = ' '; if ($j + 1 -lt $chars.Length) { $chars[$j + 1] = ' ' }; $j += 2; continue }
                if ($chars[$j] -eq '"') {
                    if ($verbatim -and $j + 1 -lt $chars.Length -and $chars[$j + 1] -eq '"') { $chars[$j] = ' '; $chars[$j + 1] = ' '; $j += 2; continue }
                    break
                }
                $chars[$j] = ' '
                $j++
            }
            $i = $j + 1
            continue
        }
        if ($c -eq "'") {
            $j = $i + 1
            while ($j -lt $chars.Length -and $chars[$j] -ne "'") {
                if ($chars[$j] -eq '\') { $chars[$j] = ' '; $j++ }
                if ($j -lt $chars.Length) { $chars[$j] = ' ' }
                $j++
            }
            $i = $j + 1
            continue
        }
        $i++
    }
    return -join $chars
}

function Test-SkippedLine([string]$Line) {
    $t = $Line.Trim()
    return ($t -eq '') -or $t.StartsWith('//') -or $t.StartsWith('/*') -or $t.StartsWith('*') -or
        $t.StartsWith('using ') -or $t.StartsWith('namespace ') -or $t.StartsWith('[') -or $t.StartsWith('#')
}

function Get-LineSites([string]$Line) {
    # One site per operator occurrence: its operator, column, the text it matched and the
    # replacement.
    if (Test-SkippedLine $Line) { return @() }
    $masked = Get-MaskedLine $Line
    # The true or false inside ConfigureAwait(...) is never a site: in a console app with no
    # synchronization context flipping it changes nothing, so no test can kill it (BL-1059).
    $equivalent = @([regex]::Matches($masked, 'ConfigureAwait\(\s*(true|false)\s*\)') | ForEach-Object { $_.Groups[1].Index })
    $sites = @()
    foreach ($op in $Operators) {
        foreach ($m in [regex]::Matches($masked, $op.Pattern)) {
            if ($op.Name -in 'true', 'false' -and $equivalent -contains $m.Index) { continue }
            $sites += [pscustomobject]@{ Operator = $op.Name; Column = $m.Index; Original = $m.Value; Mutated = $op.To }
        }
    }
    return @($sites | Sort-Object Column, Operator)
}

function Get-MutatedLine([string]$Line, $Site) {
    return $Line.Substring(0, $Site.Column) + $Site.Mutated + $Line.Substring($Site.Column + $Site.Original.Length)
}

function Invoke-Git([string[]]$Arguments) {
    # Runs git and returns its exit code. Continue, not Stop: git writes progress such as
    # "Preparing worktree" to stderr, which Windows PowerShell 5.1 would otherwise turn into
    # a terminating error.
    $ErrorActionPreference = 'Continue'
    & git @Arguments 2>&1 | Out-Null
    return $LASTEXITCODE
}

function Invoke-Timed([string]$Exe, [string[]]$Arguments, [int]$Seconds, [string]$WorkDir) {
    # Runs a command with its output in a temp file; returns ExitCode, TimedOut, Ms, Output.
    $out = [IO.Path]::GetTempFileName()
    $err = [IO.Path]::GetTempFileName()
    $watch = [Diagnostics.Stopwatch]::StartNew()
    $p = Start-Process -FilePath $Exe -ArgumentList $Arguments -WorkingDirectory $WorkDir -NoNewWindow -PassThru `
        -RedirectStandardOutput $out -RedirectStandardError $err
    $null = $p.Handle
    $done = $p.WaitForExit($Seconds * 1000)
    if (-not $done) {
        if ($env:OS -eq 'Windows_NT') { & taskkill /PID $p.Id /T /F 2>&1 | Out-Null } else { $p.Kill() }
        $p.WaitForExit()
    }
    $watch.Stop()
    $text = "$(Get-Content $out -Raw)`n$(Get-Content $err -Raw)"
    Remove-Item $out, $err -ErrorAction SilentlyContinue
    return [pscustomobject]@{ ExitCode = $(if ($done) { $p.ExitCode } else { -1 }); TimedOut = (-not $done); Ms = [int]$watch.ElapsedMilliseconds; Output = $text }
}

function Invoke-Baseline([string]$TestProject, [string]$WorkDir, [int]$Seconds) {
    # Builds and tests -TestProject unmutated. Ok, Ms and, when it fails, Error.
    $build = Invoke-Timed 'dotnet' @('build', "`"$TestProject`"", '-c', 'Release', '-p:TreatWarningsAsErrors=false', '-nologo', '-v', 'q') $Seconds $WorkDir
    if ($build.TimedOut -or $build.ExitCode -ne 0) {
        return [pscustomobject]@{ Ok = $false; Ms = $build.Ms; Error = "the unmutated build failed: $((($build.Output -split "`n") | Where-Object { $_ -match 'error' } | Select-Object -First 3) -join ' | ')" }
    }
    $test = Invoke-Timed 'dotnet' @('test', "`"$TestProject`"", '-c', 'Release', '--no-build', '--filter', 'TestCategory!=Integration', '-nologo') $Seconds $WorkDir
    if ($test.TimedOut -or $test.ExitCode -ne 0) {
        return [pscustomobject]@{ Ok = $false; Ms = $build.Ms + $test.Ms; Error = "the unmutated tests failed or timed out (exit $($test.ExitCode))" }
    }
    return [pscustomobject]@{ Ok = $true; Ms = $build.Ms + $test.Ms; Error = '' }
}

function Get-Score([int]$Killed, [int]$TimedOut, [int]$Survived) {
    $counted = $Killed + $TimedOut + $Survived
    if ($counted -eq 0) { return $null }
    return [math]::Round(($Killed + $TimedOut) / $counted, 4)
}

if ($SelfTest) {
    $failed = 0
    function Check([string]$Name, [string]$Expected, [string]$Got) {
        if ($Expected -ceq $Got) { Write-Host "PASS ${Name}: $Got" -ForegroundColor Green }
        else { Write-Host "FAIL ${Name}: expected '$Expected', got '$Got'" -ForegroundColor Red; $script:failed++ }
    }
    $show = { param([string]$Line) (@(Get-LineSites $Line) | ForEach-Object { "$($_.Operator)@$($_.Column)" }) -join ',' }
    Check 'string literal holding ==' '' (& $show 'var s = "a == b";')
    Check 'verbatim string holding && and ""' '' (& $show 'var s = @"a && ""b"" || c";')
    Check 'escaped quote inside a string' '==@23' (& $show 'var s = "a \" == b"; x == y;')
    Check 'char literal' '' (& $show "var c = '=';")
    Check 'List<int> generic' '' (& $show 'var list = new List<int>();')
    Check '// comment line' '' (& $show '// a == b')
    Check 'trailing // comment' '==@6' (& $show 'if (a == b) // c != d')
    Check 'two operators, one mutant each' '==@6,&&@11,!=@16' (& $show 'if (a == b && c != d)')
    Check 'comparisons with spaces' '<@5,>=@14' (& $show 'if (a < b || c >= d)' | ForEach-Object { $_ -replace '\|\|@\d+,', '' })
    Check '+ 1 but not + 10' '+1@6' (& $show 'x = y + 1 + 10;')
    Check 'true, false, !(' '!(@4,true@12,false@20' (& $show 'if (!(a) && true || false)' | ForEach-Object { ($_ -split ',' | Where-Object { $_ -notmatch '^(&&|\|\|)' }) -join ',' })
    Check 'attribute line' '' (& $show '[Theory(x == 1)]')
    Check 'using line' '' (& $show 'using static System.Math;')
    Check 'ConfigureAwait(false) is not a site' '' (& $show 'await x.ReadAsync(b).ConfigureAwait(false);')
    Check 'return false is still a site' 'false@7' (& $show 'return false;')
    Check 'mutation applied' 'if (a != b && c)' (Get-MutatedLine 'if (a == b && c)' (@(Get-LineSites 'if (a == b && c)')[0]))
    Check 'score' '0.75' "$(Get-Score 2 1 1)"
    Check 'score with nothing counted' '' "$(Get-Score 0 0 0)"
    # The baseline-failure path: a scratch project that does not compile.
    $scratch = Join-Path ([IO.Path]::GetTempPath()) ('mutation-selftest-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Force $scratch | Out-Null
    try {
        [IO.File]::WriteAllText((Join-Path $scratch 'Broken.csproj'), '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>')
        [IO.File]::WriteAllText((Join-Path $scratch 'Broken.cs'), "class Broken { int }`n")
        $baseline = Invoke-Baseline (Join-Path $scratch 'Broken.csproj') $scratch 300
        Check 'a baseline that does not build stops' 'False the unmutated build failed' "$($baseline.Ok) $($baseline.Error -replace ':.*$', '')"
    }
    finally { Remove-Item -Recurse -Force $scratch -ErrorAction SilentlyContinue }
    exit $(if ($failed) { 1 } else { 0 })
}

if (-not $Library) { throw 'Give the library to mutate with -Library, e.g. -Library Curl.Protocol.Dict.UnitLibrary.' }
if ($Library -notmatch '^Curl(\.[A-Za-z0-9]+)+\.UnitLibrary$') { throw "$Library is not a Curl.<Area>.UnitLibrary name." }
$twin = $Library -replace '\.UnitLibrary$', '.UnitTests'
$repo = "$(git -C $PSScriptRoot rev-parse --show-toplevel)".Trim()
if (-not $repo) { throw "cannot find the repository that holds $PSScriptRoot" }
$sha = "$(git -C $repo rev-parse --verify "$Commit^{commit}" 2>$null)".Trim()
if (-not $sha) { throw "$Commit is not a commit in $repo." }
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$workRoot = "$repo.audit"
$worktree = Join-Path $workRoot "mutation-$stamp"
New-Item -ItemType Directory -Force $workRoot | Out-Null

$result = [ordered]@{ library = $Library; commit = $sha; seed = $Seed; baselineMs = $null; mutants = @(); killed = 0; survived = 0; timedOut = 0; stillborn = 0; score = $null }
$exitCode = 0
try {
    if ((Invoke-Git @('-C', $repo, 'worktree', 'add', '--detach', $worktree, $sha)) -ne 0) { throw "git worktree add failed for $worktree" }
    $libraryDir = Join-Path $worktree $Library
    $testProject = Join-Path $worktree "$twin\$twin.csproj"
    if (-not (Test-Path $libraryDir)) { throw "$Library does not exist at $sha." }
    if (-not (Test-Path $testProject)) { throw "$twin\$twin.csproj does not exist at $sha." }

    Write-Host "baseline: building and testing $twin unmutated ..."
    $baseline = Invoke-Baseline $testProject $worktree $TimeoutSeconds
    $result.baselineMs = $baseline.Ms
    if (-not $baseline.Ok) {
        $result.baselineError = $baseline.Error
        Write-Host "Stopped: $($baseline.Error). No mutants were run." -ForegroundColor Red
        $exitCode = 2
    }
    else {
        $sites = @()
        $files = @(Get-ChildItem $libraryDir -Recurse -File -Filter '*.cs' | Where-Object { $_.FullName -notmatch '[\\/](obj|bin)[\\/]' })
        foreach ($file in $files) {
            $relative = $file.FullName.Substring($worktree.Length + 1).Replace('\', '/')
            $lines = [IO.File]::ReadAllLines($file.FullName)
            for ($n = 0; $n -lt $lines.Length; $n++) {
                foreach ($site in (Get-LineSites $lines[$n])) {
                    $sites += [pscustomobject]@{ File = $relative; Full = $file.FullName; Line = $n + 1; Operator = $site.Operator; Column = $site.Column; Original = $site.Original; Mutated = $site.Mutated }
                }
            }
        }
        $ordered = [object[]]@($sites | Sort-Object @{ Expression = { $_.File }; Descending = $false }, Line, Column, Operator)
        # A stable ordinal file order, whatever the culture Sort-Object uses.
        $ordered = [object[]]@($ordered | Sort-Object -Property @{ Expression = { [string]::Join('', ($_.File.ToCharArray() | ForEach-Object { '{0:X4}' -f [int]$_ })) } }, Line, Column, Operator)
        $random = New-Object System.Random $Seed
        for ($i = $ordered.Length - 1; $i -gt 0; $i--) {
            $j = $random.Next($i + 1)
            $tmp = $ordered[$i]; $ordered[$i] = $ordered[$j]; $ordered[$j] = $tmp
        }
        $chosen = @($ordered | Select-Object -First $MaxMutants)
        Write-Host "$($sites.Count) sites in $Library; running $($chosen.Count) mutants."
        foreach ($site in $chosen) {
            $lines = [IO.File]::ReadAllLines($site.Full)
            $originalLine = $lines[$site.Line - 1]
            $lines[$site.Line - 1] = Get-MutatedLine $originalLine $site
            [IO.File]::WriteAllLines($site.Full, $lines)
            try {
                $build = Invoke-Timed 'dotnet' @('build', "`"$testProject`"", '-c', 'Release', '-p:TreatWarningsAsErrors=false', '-nologo', '-v', 'q') $TimeoutSeconds $worktree
                if ($build.TimedOut -or $build.ExitCode -ne 0) { $outcome = 'stillborn'; $ms = $build.Ms }
                else {
                    $test = Invoke-Timed 'dotnet' @('test', "`"$testProject`"", '-c', 'Release', '--no-build', '--filter', 'TestCategory!=Integration', '-nologo') $TimeoutSeconds $worktree
                    $ms = $build.Ms + $test.Ms
                    $outcome = if ($test.TimedOut) { 'timedOut' } elseif ($test.ExitCode -ne 0) { 'killed' } else { 'survived' }
                }
            }
            finally { Invoke-Git @('-C', $worktree, 'checkout', '--', $site.File) | Out-Null }
            $result.mutants += [ordered]@{ file = $site.File; line = $site.Line; operator = $site.Operator; original = $originalLine.Trim(); mutated = (Get-MutatedLine $originalLine $site).Trim(); outcome = $outcome; ms = $ms }
            $result[$outcome]++
            Write-Host ("{0,-9} {1}:{2} {3}" -f $outcome, $site.File, $site.Line, $site.Operator)
        }
        $result.score = Get-Score $result.killed $result.timedOut $result.survived
    }
}
finally {
    Invoke-Git @('-C', $repo, 'worktree', 'remove', '--force', $worktree) | Out-Null
    if (Test-Path $worktree) { Remove-Item -Recurse -Force $worktree -ErrorAction SilentlyContinue; Invoke-Git @('-C', $repo, 'worktree', 'prune') | Out-Null }
}

$json = [pscustomobject]$result | ConvertTo-Json -Depth 5
if ($OutFile) { [IO.File]::WriteAllText($OutFile, $json) }
Write-Output $json
exit $exitCode
