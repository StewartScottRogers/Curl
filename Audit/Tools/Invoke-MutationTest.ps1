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

    Each mutant names its member: the method, constructor, property or field whose declaration
    is the nearest one above the site (a line with a C# modifier such as public or private
    followed by a name and "(", "{", "=>" or "="), or "-" when there is none. A surviving-mutant
    finding's key is built from it (Quality.md, ADR-0422).

    Targeted mode (-Site): instead of sampling, mutate exactly one site and report its outcome,
    so a finding's reproduction is the one mutant it names (ADR-0422). The site is
    <library-relative or repository-relative file>:<line>[:<operator>]. With no operator, the
    line's first site is used. When the line holds no site with that operator - lines move
    between commits - the nearest line with one inside the same member (-Member, else the member
    found at the given line) is used, and resolvedLine says which. When there is none, outcome
    is site-missing and no mutant runs. The JSON then also carries site, resolvedLine and
    outcome.

    -ExcludeBaselineFailures: when the unmutated tests fail, record the failing tests (from a
    trx log), rerun the baseline without them, and, if that passes, run every mutant without
    them too; their names are in excludedTests. One red test elsewhere in the twin then no
    longer stops the run. A baseline that does not build still stops it.

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

.PARAMETER Site
    Targeted mode: <file>:<line>[:<operator>], e.g.
    Curl.Networking.UnitLibrary/TcpPendingConnection.cs:77:true. -Library may be left out: it
    is the file's first folder.

.PARAMETER Member
    Targeted mode: the member the site is in, used to find the site again when its line moved.

.PARAMETER ExcludeBaselineFailures
    Leave out the tests that already fail unmutated, instead of stopping.

.PARAMETER SelfTest
    Check the site finder, the operators, the member finder, site resolution, the test filter
    and the baseline-failure path on samples, print PASS or FAIL per case, and exit.

.OUTPUTS
    JSON: { library, commit, seed, baselineMs, excludedTests, mutants: [ { file, line, member,
    operator, original, mutated, outcome, ms } ], killed, survived, timedOut, stillborn, score },
    and on a failed baseline also baselineError; with -Site also site, resolvedLine, outcome.

.EXAMPLE
    powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Library Curl.Protocol.Dict.UnitLibrary -MaxMutants 5 -Seed 1 -OutFile $env:TEMP\dict.json
.EXAMPLE
    powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/TcpPendingConnection.cs:77:true -ExcludeBaselineFailures -TimeoutSeconds 300
#>
param(
    [string]$Library,
    [string]$Commit = 'HEAD',
    [int]$MaxMutants = 50,
    [int]$Seed = 0,
    [int]$TimeoutSeconds = 600,
    [string]$OutFile,
    [string]$Site,
    [string]$Member,
    [switch]$ExcludeBaselineFailures,
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

# A member declaration: one or more C# modifiers, then a name followed by "(", "{", "=>" or "=".
# The same rule as Write-AuditFindings.ps1's Get-MemberMap.
$MemberPattern = '^\s*(?:(?:public|private|protected|internal|static|async|override|virtual|sealed|abstract|extern|unsafe|new|partial|readonly|required|file)\s+)+[^=;(){}]*?\b([A-Za-z_]\w*)\s*(?:<[^<>]*(?:<[^<>]*>[^<>]*)*>)?\s*(?:\(|\{|=>|=(?![=>]))'

function Get-MemberMap([string[]]$Lines) {
    # The member each line is in: the nearest declaration at or above it, or '-' above the first.
    $map = New-Object string[] $Lines.Length
    $current = '-'
    for ($i = 0; $i -lt $Lines.Length; $i++) {
        if ($Lines[$i] -match $MemberPattern) { $current = $Matches[1] }
        $map[$i] = $current
    }
    return , $map
}

function Resolve-Site([string[]]$Lines, [int]$Line, [string]$Operator, [string]$Member) {
    # The site -Site names: on its line when that line has one with the operator, otherwise the
    # nearest such line in the same member (lines move between commits), otherwise $null.
    $map = Get-MemberMap $Lines
    $index = $Line - 1
    $inFile = ($index -ge 0) -and ($index -lt $Lines.Length)
    $siteOn = { param([int]$i) @(Get-LineSites $Lines[$i] | Where-Object { (-not $Operator) -or ($_.Operator -ceq $Operator) })[0] }
    if ($inFile) {
        $here = & $siteOn $index
        if ($here) { return [pscustomobject]@{ Index = $index; Site = $here; Member = $map[$index] } }
    }
    $owner = if ($Member) { $Member } elseif ($inFile) { $map[$index] } else { '-' }
    if (-not $Operator -or $owner -eq '-') { return $null }
    $best = $null
    for ($i = 0; $i -lt $Lines.Length; $i++) {
        if ($map[$i] -cne $owner) { continue }
        $candidate = & $siteOn $i
        if ($candidate -and ((-not $best) -or ([math]::Abs($i - $index) -lt [math]::Abs($best.Index - $index)))) {
            $best = [pscustomobject]@{ Index = $i; Site = $candidate; Member = $owner }
        }
    }
    return $best
}

function ConvertTo-FilterValue([string]$Name) {
    # dotnet test --filter escapes these characters with a backslash.
    return [regex]::Replace($Name, '([\\()&|=!~,])', '\$1')
}

function Get-TestFilter([string[]]$Excluded) {
    $filter = 'TestCategory!=Integration'
    foreach ($name in @($Excluded | Where-Object { $_ })) { $filter += '&FullyQualifiedName!=' + (ConvertTo-FilterValue $name) }
    return $filter
}

function Get-FailedTests([string]$TrxPath) {
    # The fully qualified names of the failed tests in a trx log, once each.
    if (-not (Test-Path -LiteralPath $TrxPath)) { return @() }
    [xml]$trx = [IO.File]::ReadAllText($TrxPath)
    $names = @{}
    foreach ($test in @($trx.TestRun.TestDefinitions.UnitTest)) { if ($test) { $names[$test.id] = "$($test.TestMethod.className).$($test.TestMethod.name)" } }
    $failed = @($trx.TestRun.Results.UnitTestResult | Where-Object { $_ -and $_.outcome -eq 'Failed' } | ForEach-Object { $names[$_.testId] })
    return @($failed | Where-Object { $_ } | Sort-Object -Unique)
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

function Invoke-Tests([string]$TestProject, [string]$WorkDir, [int]$Seconds, [string[]]$Excluded, [string]$TrxPath) {
    # The twin's fast tests, without the excluded ones; a trx log when $TrxPath is given.
    $arguments = @('test', "`"$TestProject`"", '-c', 'Release', '--no-build', '--filter', "`"$(Get-TestFilter $Excluded)`"", '-nologo')
    if ($TrxPath) { $arguments += @('--logger', "`"trx;LogFileName=$TrxPath`"") }
    return Invoke-Timed 'dotnet' $arguments $Seconds $WorkDir
}

function Invoke-Baseline([string]$TestProject, [string]$WorkDir, [int]$Seconds, [switch]$Exclude) {
    # Builds and tests -TestProject unmutated. Ok, Ms, Excluded (the failing tests left out under
    # -Exclude) and, when it fails, Error.
    $build = Invoke-Timed 'dotnet' @('build', "`"$TestProject`"", '-c', 'Release', '-p:TreatWarningsAsErrors=false', '-nologo', '-v', 'q') $Seconds $WorkDir
    if ($build.TimedOut -or $build.ExitCode -ne 0) {
        return [pscustomobject]@{ Ok = $false; Ms = $build.Ms; Excluded = @(); Error = "the unmutated build failed: $((($build.Output -split "`n") | Where-Object { $_ -match 'error' } | Select-Object -First 3) -join ' | ')" }
    }
    $trx = if ($Exclude) { Join-Path ([IO.Path]::GetTempPath()) ('mutation-baseline-' + [guid]::NewGuid().ToString('N') + '.trx') } else { '' }
    $test = Invoke-Tests $TestProject $WorkDir $Seconds @() $trx
    $ms = $build.Ms + $test.Ms
    if (-not $test.TimedOut -and $test.ExitCode -eq 0) { return [pscustomobject]@{ Ok = $true; Ms = $ms; Excluded = @(); Error = '' } }
    $failing = if ($Exclude -and -not $test.TimedOut) { @(Get-FailedTests $trx) } else { @() }
    if ($trx) { Remove-Item -LiteralPath $trx -ErrorAction SilentlyContinue }
    if (-not $failing.Count) {
        return [pscustomobject]@{ Ok = $false; Ms = $ms; Excluded = @(); Error = "the unmutated tests failed or timed out (exit $($test.ExitCode))" }
    }
    $again = Invoke-Tests $TestProject $WorkDir $Seconds $failing ''
    $ms += $again.Ms
    if ($again.TimedOut -or $again.ExitCode -ne 0) {
        return [pscustomobject]@{ Ok = $false; Ms = $ms; Excluded = $failing; Error = "the unmutated tests still failed or timed out without the $($failing.Count) failing tests (exit $($again.ExitCode))" }
    }
    return [pscustomobject]@{ Ok = $true; Ms = $ms; Excluded = $failing; Error = '' }
}

function ConvertFrom-SiteArgument([string]$Text, [string]$DefaultLibrary) {
    # -Site <file>:<line>[:<operator>] -> Library, File (repository-relative, / separators), Line, Operator.
    if ($Text -notmatch '^(?<file>.+?\.cs):(?<line>\d+)(?::(?<op>.+))?$') { throw "-Site $Text is not <file>:<line>[:<operator>]." }
    $file = $Matches['file'] -replace '\\', '/'
    $first = ($file -split '/')[0]
    $library = if ($first -like '*.UnitLibrary') { $first } else { $DefaultLibrary }
    if (-not $library) { throw "-Site $Text names no Curl.<Area>.UnitLibrary folder; give -Library too." }
    if ($first -ne $library) { $file = "$library/$file" }
    return [ordered]@{ Library = $library; File = $file; Line = [int]$Matches['line']; Operator = "$($Matches['op'])" }
}

function New-SiteRecord([string]$Relative, [string]$Full, [int]$Index, $Site, [string]$MemberName) {
    return [pscustomobject]@{ File = $Relative; Full = $Full; Line = $Index + 1; Member = $MemberName; Operator = $Site.Operator; Column = $Site.Column; Original = $Site.Original; Mutated = $Site.Mutated }
}

function Get-TargetSite($Target, [string]$WorkTree, [string]$MemberName) {
    # The one site -Site names, or nothing when it is not in the file at this commit.
    $full = Join-Path $WorkTree ($Target.File -replace '/', '\')
    if (-not (Test-Path -LiteralPath $full)) { return @() }
    $resolved = Resolve-Site ([IO.File]::ReadAllLines($full)) $Target.Line $Target.Operator $MemberName
    if (-not $resolved) { return @() }
    return @(New-SiteRecord $Target.File $full $resolved.Index $resolved.Site $resolved.Member)
}

function Get-SampledSites([string]$LibraryDir, [string]$WorkTree) {
    # Every site in the library, in a stable order, shuffled with -Seed; the first -MaxMutants.
    $sites = @()
    foreach ($file in @(Get-ChildItem $LibraryDir -Recurse -File -Filter '*.cs' | Where-Object { $_.FullName -notmatch '[\\/](obj|bin)[\\/]' })) {
        $relative = $file.FullName.Substring($WorkTree.Length + 1).Replace('\', '/')
        $lines = [IO.File]::ReadAllLines($file.FullName)
        $map = Get-MemberMap $lines
        for ($n = 0; $n -lt $lines.Length; $n++) {
            foreach ($s in (Get-LineSites $lines[$n])) { $sites += New-SiteRecord $relative $file.FullName $n $s $map[$n] }
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
    return $chosen
}

function Invoke-Mutant($SiteRecord, [string]$TestProject, [string]$WorkTree, [int]$Seconds, [string[]]$Excluded) {
    # Applies one mutant, rebuilds and tests the twin, restores the file; returns the mutant's record.
    $lines = [IO.File]::ReadAllLines($SiteRecord.Full)
    $originalLine = $lines[$SiteRecord.Line - 1]
    $lines[$SiteRecord.Line - 1] = Get-MutatedLine $originalLine $SiteRecord
    [IO.File]::WriteAllLines($SiteRecord.Full, $lines)
    try {
        $build = Invoke-Timed 'dotnet' @('build', "`"$TestProject`"", '-c', 'Release', '-p:TreatWarningsAsErrors=false', '-nologo', '-v', 'q') $Seconds $WorkTree
        if ($build.TimedOut -or $build.ExitCode -ne 0) { $outcome = 'stillborn'; $ms = $build.Ms }
        else {
            $test = Invoke-Tests $TestProject $WorkTree $Seconds $Excluded ''
            $ms = $build.Ms + $test.Ms
            $outcome = if ($test.TimedOut) { 'timedOut' } elseif ($test.ExitCode -ne 0) { 'killed' } else { 'survived' }
        }
    }
    finally { Invoke-Git @('-C', $WorkTree, 'checkout', '--', $SiteRecord.File) | Out-Null }
    return [ordered]@{ file = $SiteRecord.File; line = $SiteRecord.Line; member = $SiteRecord.Member; operator = $SiteRecord.Operator; original = $originalLine.Trim(); mutated = (Get-MutatedLine $originalLine $SiteRecord).Trim(); outcome = $outcome; ms = $ms }
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
    $sample = @(
        'namespace X;',
        'internal sealed class TcpPendingConnection',
        '{',
        '    private readonly int limit = 3;',
        '    public async Task<Socket> AcceptAsync(CancellationToken token)',
        '    {',
        '        var accepted = await listener.AcceptAsync(token);',
        '        accepted.NoDelay = true;',
        '        return accepted;',
        '    }',
        '    public bool IsOpen => count < limit;',
        '    public static string Name(bool forIssuer) { return Lookup(forIssuer: false); }',
        '}')
    $map = Get-MemberMap $sample
    Check 'the member of each line' '-,-,-,limit,AcceptAsync,AcceptAsync,AcceptAsync,AcceptAsync,AcceptAsync,AcceptAsync,IsOpen,Name,Name' ($map -join ',')
    $at = { param($Line, $Operator, $Member) $r = Resolve-Site $sample $Line $Operator $Member; if ($r) { "$($r.Index + 1) $($r.Site.Operator) $($r.Member)" } else { 'none' } }
    Check 'a site on its own line' '8 true AcceptAsync' (& $at 8 'true' '')
    Check 'a site whose line moved is found in its member' '8 true AcceptAsync' (& $at 6 'true' 'AcceptAsync')
    Check 'a moved site with no -Member uses the member at the line' '8 true AcceptAsync' (& $at 9 'true' '')
    Check 'a site missing from its member is none' 'none' (& $at 8 '==' 'AcceptAsync')
    Check 'no operator takes the line''s first site' '12 false Name' (& $at 12 '' '')
    Check 'a line past the end with no member is none' 'none' (& $at 99 'true' '')
    Check 'the plain test filter' 'TestCategory!=Integration' (Get-TestFilter @())
    Check 'excluded tests join the filter, escaped' 'TestCategory!=Integration&FullyQualifiedName!=A.B.C&FullyQualifiedName!=A.B.D\(1\)' (Get-TestFilter @('A.B.C', 'A.B.D(1)'))
    $trxPath = Join-Path ([IO.Path]::GetTempPath()) ('mutation-selftest-' + [guid]::NewGuid().ToString('N') + '.trx')
    $trxText = '<?xml version="1.0"?><TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010"><Results>' +
        '<UnitTestResult testId="1" testName="A" outcome="Failed" /><UnitTestResult testId="2" testName="B" outcome="Passed" /><UnitTestResult testId="1" testName="A" outcome="Failed" /></Results>' +
        '<TestDefinitions><UnitTest id="1" name="A"><TestMethod className="Ns.EchTests" name="A" /></UnitTest><UnitTest id="2" name="B"><TestMethod className="Ns.EchTests" name="B" /></UnitTest></TestDefinitions></TestRun>'
    [IO.File]::WriteAllText($trxPath, $trxText)
    try { Check 'the failed tests of a trx log, once each' 'Ns.EchTests.A' ((Get-FailedTests $trxPath) -join ',') }
    finally { Remove-Item -LiteralPath $trxPath -ErrorAction SilentlyContinue }
    Check 'the site argument' 'Curl.Networking.UnitLibrary|Curl.Networking.UnitLibrary/TcpPendingConnection.cs|77|true' ((ConvertFrom-SiteArgument 'Curl.Networking.UnitLibrary\TcpPendingConnection.cs:77:true' '').Values -join '|')
    Check 'a site relative to its library' 'Curl.Cli.UnitLibrary|Curl.Cli.UnitLibrary/Parser.cs|5|' ((ConvertFrom-SiteArgument 'Parser.cs:5' 'Curl.Cli.UnitLibrary').Values -join '|')
    exit $(if ($failed) { 1 } else { 0 })
}

if ($Site) {
    $target = ConvertFrom-SiteArgument $Site $Library
    $Library = $target.Library
}
if (-not $Library) { throw 'Give the library to mutate with -Library, e.g. -Library Curl.Protocol.Dict.UnitLibrary, or a -Site.' }
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

$result = [ordered]@{ library = $Library; commit = $sha; seed = $Seed; baselineMs = $null; excludedTests = @(); mutants = @(); killed = 0; survived = 0; timedOut = 0; stillborn = 0; score = $null }
if ($Site) { $result.site = $Site; $result.resolvedLine = $null; $result.outcome = $null }
$exitCode = 0
try {
    if ((Invoke-Git @('-C', $repo, 'worktree', 'add', '--detach', $worktree, $sha)) -ne 0) { throw "git worktree add failed for $worktree" }
    $libraryDir = Join-Path $worktree $Library
    $testProject = Join-Path $worktree "$twin\$twin.csproj"
    if (-not (Test-Path $libraryDir)) { throw "$Library does not exist at $sha." }
    if (-not (Test-Path $testProject)) { throw "$twin\$twin.csproj does not exist at $sha." }

    Write-Host "baseline: building and testing $twin unmutated ..."
    $baseline = Invoke-Baseline $testProject $worktree $TimeoutSeconds -Exclude:$ExcludeBaselineFailures
    $result.baselineMs = $baseline.Ms
    $result.excludedTests = @($baseline.Excluded)
    if ($baseline.Excluded.Count) { Write-Host "left out $($baseline.Excluded.Count) tests that fail unmutated: $($baseline.Excluded -join ', ')" }
    if (-not $baseline.Ok) {
        $result.baselineError = $baseline.Error
        Write-Host "Stopped: $($baseline.Error). No mutants were run." -ForegroundColor Red
        $exitCode = 2
    }
    else {
        # @() around the if: Windows PowerShell 5.1 unrolls one site to a PSCustomObject with no Count.
        $chosen = @(if ($Site) { Get-TargetSite $target $worktree $Member } else { Get-SampledSites $libraryDir $worktree })
        if ($Site -and -not $chosen.Count) { $result.outcome = 'site-missing'; Write-Host "site-missing $Site" }
        foreach ($s in $chosen) {
            $mutant = Invoke-Mutant $s $testProject $worktree $TimeoutSeconds @($baseline.Excluded)
            $result.mutants += $mutant
            $result[$mutant.outcome]++
            Write-Host ("{0,-9} {1}:{2} {3}" -f $mutant.outcome, $s.File, $s.Line, $s.Operator)
        }
        if ($Site -and $chosen.Count) { $result.resolvedLine = $chosen[0].Line; $result.outcome = $result.mutants[0].outcome }
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

