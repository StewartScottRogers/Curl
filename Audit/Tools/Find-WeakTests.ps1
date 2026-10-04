<#
.SYNOPSIS
    Lists test methods, across every *.UnitTests project of a tree, whose bodies do not check
    what their names promise: candidates for the quality auditor's steps 1 and 2.

.DESCRIPTION
    The quality auditor read only the twins of the libraries it mutated, so a weak test planted
    in any other test project went unseen in three audits running (BL-1368). This scan reads
    every test method of every *.UnitTests project and flags, as a candidate:

      ignored-test       [Ignore] on the method, or a body ending in Assert.Inconclusive
      no-assertion       a body with no assertion once comments are removed (an assertion is an
                         Assert.*, StringAssert.*, CollectionAssert.* or Throws* call, an
                         [ExpectedException], or a call to a helper whose name starts Assert,
                         Expect or Verify)
      weak-assertion     every assertion is Assert.IsNotNull or an IsTrue of a length, count
                         or Any() above zero
      name-lies          a name ending _ExitsWith<N> (or ...ExitsWith<N><Words>) whose body never
                         mentions N or the CurlExitCode member that equals N; or a name with
                         Throws<X> whose body never mentions X (X is the name's ...Exception
                         word, or the camel-case word after Throws)

    A candidate is not yet a finding: the auditor reads each one and files those that are real.
    Prints JSON: one object per candidate with file (repository-relative), line (the method's
    signature), test, kind and detail; and testsScanned, projectsScanned.

.PARAMETER Root
    The tree to scan. Default: the repository holding this script.

.PARAMETER OutFile
    Write the JSON here as well as to standard output.

.PARAMETER SelfTest
    Scan Audit/Tools/Fixtures/weak-tests and check every kind is found, and nothing else.
#>
param(
    [string]$Root,
    [string]$OutFile,
    [switch]$SelfTest
)

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path

function Get-ExitCodeValues([string]$Tree) {
    # CurlExitCode member name -> value, from the enum's source.
    $values = @{}
    $file = Get-ChildItem -LiteralPath $Tree -Recurse -Depth 2 -Filter 'CurlExitCode.cs' -File -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' } | Select-Object -First 1
    if (-not $file) { return $values }
    foreach ($m in [regex]::Matches([IO.File]::ReadAllText($file.FullName), '(?m)^\s*([A-Z]\w*)\s*=\s*(\d+)\s*,?')) {
        $values[$m.Groups[1].Value] = [int]$m.Groups[2].Value
    }
    return $values
}

function Remove-Comments([string]$Text) {
    # Drops // and /* */ comments, keeping string literals (so "//" in a string survives).
    $pattern = '"(?:\\.|[^"\\])*"|@"(?:""|[^"])*"|''(?:\\.|[^''\\])*''|//[^\r\n]*|/\*[\s\S]*?\*/'
    return [regex]::Replace($Text, $pattern, { param($m) if ($m.Value.StartsWith('//') -or $m.Value.StartsWith('/*')) { '' } else { $m.Value } })
}

function Get-TestMethods([string]$Path) {
    # Each [TestMethod]/[DataTestMethod]: its name, the line of its signature, its attributes and
    # its body with comments removed.
    $text = [IO.File]::ReadAllText($Path)
    $methods = @()
    $signature = '(?ms)((?:^[ \t]*\[[^\]\r\n]*\][ \t]*\r?\n)+)[ \t]*(?:public|internal)\s+(?:static\s+)?(?:async\s+)?(?:Task|void)\s+(\w+)\s*\('
    foreach ($m in [regex]::Matches($text, $signature)) {
        $attributes = $m.Groups[1].Value
        if ($attributes -notmatch '\[(?:Data)?TestMethod\b') { continue }
        # Past the parameter list: a body in braces, or an expression body (=> ...;).
        $rest = Remove-Comments $text.Substring($m.Index + $m.Length - 1)
        $depth = 0; $close = -1
        for ($i = 0; $i -lt $rest.Length; $i++) {
            if ($rest[$i] -eq '(') { $depth++ }
            elseif ($rest[$i] -eq ')') { $depth--; if ($depth -eq 0) { $close = $i; break } }
        }
        if ($close -lt 0) { continue }
        $after = $rest.Substring($close + 1)
        $start = $after.Length - $after.TrimStart().Length
        $body = $null
        if ($after.Substring($start).StartsWith('=>')) {
            $depth = 0
            for ($i = $start; $i -lt $after.Length; $i++) {
                if ($after[$i] -in '(', '{', '[') { $depth++ } elseif ($after[$i] -in ')', '}', ']') { $depth-- }
                elseif ($after[$i] -eq ';' -and $depth -eq 0) { $body = $after.Substring($start, $i - $start + 1); break }
            }
        }
        elseif ($after.Substring($start).StartsWith('{')) {
            $depth = 0
            for ($i = $start; $i -lt $after.Length; $i++) {
                if ($after[$i] -eq '{') { $depth++ }
                elseif ($after[$i] -eq '}') { $depth--; if ($depth -eq 0) { $body = $after.Substring($start, $i - $start + 1); break } }
            }
        }
        if ($null -eq $body) { continue }
        $line = ($text.Substring(0, $m.Groups[2].Index) -split "`n").Count
        $methods += [pscustomobject]@{ Name = $m.Groups[2].Value; Line = $line; Attributes = $attributes; Body = $body }
    }
    return $methods
}

function Get-Candidates($Method, [hashtable]$ExitCodes) {
    $found = @()
    $body = $Method.Body
    $name = $Method.Name
    $assertPattern = '\b(?:Assert|StringAssert|CollectionAssert)\.\w+|\bThrows(?:Exactly)?(?:Async)?\b|\b(?:Assert|Expect|Verify)\w*\s*\('
    $asserts = @([regex]::Matches($body, '\b(?:Assert|StringAssert|CollectionAssert)\.(\w+)\s*\(([^;]*)') | ForEach-Object { [pscustomobject]@{ Kind = $_.Groups[1].Value; Args = $_.Groups[2].Value } })
    $hasExpected = $Method.Attributes -match '\[ExpectedException\b'
    if ($Method.Attributes -match '\[Ignore\b') { $found += @{ kind = 'ignored-test'; detail = '[Ignore] on the method' } }
    elseif ($body -match 'Assert\.Inconclusive\s*\(') { $found += @{ kind = 'ignored-test'; detail = 'ends in Assert.Inconclusive' } }
    if (-not $hasExpected -and $body -notmatch $assertPattern) { $found += @{ kind = 'no-assertion'; detail = 'no assertion once comments are removed' } }
    elseif ($asserts.Count -and -not $hasExpected) {
        $weak = @($asserts | Where-Object { $_.Kind -eq 'IsNotNull' -or ($_.Kind -eq 'IsTrue' -and $_.Args -match '(?:Length|Count|Count\(\))\s*>\s*0|\.Any\(\s*\)') })
        $other = $body -match '\bThrows(?:Exactly)?(?:Async)?\b|\b(?:Expect|Verify)\w*\s*\(|\bAssert\w+\s*\('
        if ($weak.Count -eq $asserts.Count -and -not $other) { $found += @{ kind = 'weak-assertion'; detail = "only $(@($weak | ForEach-Object { 'Assert.' + $_.Kind } | Select-Object -Unique) -join ', ')" } }
    }
    if ($name -match 'ExitsWith(\d+)') {
        $n = [int]$Matches[1]
        $members = @($ExitCodes.Keys | Where-Object { $ExitCodes[$_] -eq $n })
        $named = ($body -match "\b$n\b") -or @($members | Where-Object { $body -match "CurlExitCode\.$_\b" }).Count
        if (-not $named) { $found += @{ kind = 'name-lies'; detail = "name says exit $n; the body never mentions $n$(if ($members) { ' or CurlExitCode.' + ($members -join '/') })" } }
    }
    # Throws as the outcome (_Throws...), naming an exception: its ...Exception word, or the
    # camel-case word after it unless that word joins a clause (WriteThrowsUnderSilent and
    # _ThrowsAndLeavesTheQueue promise no exception type).
    # _ThrowsExit<N> promises an exit code through the exception: N must appear, as for ExitsWith.
    $joiners = '(?:And|Or|Under|When|If|Unless|While|On|For|With|After|Before|Then|But|Without|Only|Again|Once|Naming|The|Its|It|A|An|Exit)(?=[A-Z_0-9]|$)'
    if ($name -match '_ThrowsExit(\d+)') {
        $n = [int]$Matches[1]
        $members = @($ExitCodes.Keys | Where-Object { $ExitCodes[$_] -eq $n })
        $named = ($body -match "\b$n\b") -or @($members | Where-Object { $body -match "CurlExitCode\.$_\b" }).Count
        if (-not $named) { $found += @{ kind = 'name-lies'; detail = "name says ThrowsExit$n; the body never mentions $n$(if ($members) { ' or CurlExitCode.' + ($members -join '/') })" } }
    }
    $x = if ($name -match '_Throws([A-Z]\w*?Exception)') { $Matches[1] }
         elseif ($name -match "_Throws(?!$joiners)([A-Z][a-z0-9]+(?:[A-Z][a-z0-9]+)?)") { $Matches[1] }
         else { $null }
    if ($x -and $body -notmatch [regex]::Escape($x)) { $found += @{ kind = 'name-lies'; detail = "name says Throws$x; the body never mentions $x" } }
    return $found
}

function Invoke-Scan([string]$Tree) {
    $exitCodes = Get-ExitCodeValues $Tree
    $candidates = @(); $tests = 0; $projects = 0
    foreach ($project in @(Get-ChildItem -LiteralPath $Tree -Directory -Filter '*.UnitTests')) {
        $projects++
        foreach ($file in @(Get-ChildItem -LiteralPath $project.FullName -Recurse -Filter '*.cs' -File | Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' })) {
            foreach ($method in @(Get-TestMethods $file.FullName)) {
                $tests++
                foreach ($c in @(Get-Candidates $method $exitCodes)) {
                    $candidates += [ordered]@{ file = $file.FullName.Substring($Tree.Length).TrimStart('\', '/') -replace '\\', '/'; line = $method.Line; test = $method.Name; kind = $c.kind; detail = $c.detail }
                }
            }
        }
    }
    return [ordered]@{ projectsScanned = $projects; testsScanned = $tests; candidates = $candidates }
}

if ($SelfTest) {
    $fixture = Join-Path $PSScriptRoot 'Fixtures\weak-tests'
    $r = Invoke-Scan (Resolve-Path $fixture).Path
    $got = @($r.candidates | ForEach-Object { "$($_.test):$($_.kind)" } | Sort-Object)
    $want = @(
        'Constructor_TwoHandlers_ThrowsInvalidOperationExceptionNamingDict:name-lies',
        'Parse_Value_ThrowsFormatException:name-lies',
        'RunAsync_SaveFails_ExitsWith23:name-lies',
        'Write_Info_WritesTheLine:weak-assertion',
        'Refusal_AfterExpiry_HasExpired:no-assertion',
        'Retry_WithoutMaxTime_IsNeverAbandoned:ignored-test'
    ) | Sort-Object
    $failed = 0
    function Check([string]$Name, [bool]$Ok, [string]$Detail) { if (-not $Ok) { $script:failed++ }; Write-Host "$(if ($Ok) { 'PASS' } else { 'FAIL' }) ${Name}: $Detail" }
    Check 'every planted kind is found, and nothing else' (($got -join '|') -eq ($want -join '|')) ($got -join ', ')
    Check 'tests and projects are counted' ($r.testsScanned -eq 11 -and $r.projectsScanned -eq 1) "tests $($r.testsScanned), projects $($r.projectsScanned)"
    $line = @($r.candidates | Where-Object { $_.test -eq 'RunAsync_SaveFails_ExitsWith23' })[0]
    Check 'a candidate names its file and signature line' ($line.file -eq 'Sample.UnitTests/SampleTests.cs' -and $line.line -gt 0) "$($line.file):$($line.line)"
    exit $(if ($failed) { 1 } else { 0 })
}

if (-not $Root) { $Root = $repo }
$result = Invoke-Scan (Resolve-Path $Root).Path
$json = [pscustomobject]$result | ConvertTo-Json -Depth 5
if ($OutFile) { [IO.File]::WriteAllText($OutFile, $json) }
Write-Output $json
