<#
.SYNOPSIS
    Lists test methods, across every *.UnitTests and *.IntegrationTests project of a tree, whose bodies do not check
    what their names promise: candidates for the quality auditor's steps 1 and 2.

.DESCRIPTION
    The quality auditor read only the twins of the libraries it mutated, so a weak test planted
    in any other test project went unseen in three audits running (BL-1368). This scan reads
    every test method of every *.UnitTests and *.IntegrationTests project (BL-1607) and flags, as a candidate:

      ignored-test       [Ignore] on the method, or a body ending in Assert.Inconclusive
      no-assertion       a body with no assertion once comments are removed (an assertion is an
                         Assert.*, StringAssert.*, CollectionAssert.* or Throws* call, an
                         [ExpectedException], or a call to a helper whose name starts Assert,
                         Expect or Verify)
      weak-assertion     every assertion is Assert.IsNotNull or an IsTrue of a length, count
                         or Any() above zero; or, in a test with stronger assertions too, one
                         such weak assertion is the only check of its value (no other assertion
                         names the local variable it checks: an exact AreEqual weakened to IsNotNull
                         beside an exit-code check, as PD-001 plants); or Assert.IsNotNull of a
                         value that cannot be null (a ToString, GetString, Substring, Trim,
                         Replace, string.Join/Format/Concat or similar call, an interpolated
                         string, a new expression or a .Length/.Count value)
      unasserted-fake-value
                         the sharper form of a weak check that is the only check of its value:
                         the test's fakes return an exact string (a lambda's result, a switch
                         arm's, or a return "...") that no assertion mentions, so the test
                         arranges the outcome its name promises and never looks at it (audit
                         20261102-172448 dismissed one among 120 weak-assertion candidates).
                         Listed instead of weak-assertion for that value; the quality method files
                         it unless an assertion that checks the value exactly is quoted
      commented-assertion
                         an Assert.*, CollectionAssert.* or StringAssert.* call commented out
                         with // or /* */ inside the test, whether or not other assertions
                         remain: the outcome it checked is no longer checked (PD-003)
      name-lies         a name that states a number as its outcome - straight after ExitsWith,
                         ExitsWithCode, Exits, ExitCode, Exit, ThrowsExit, FailsWith(Exit), Returns,
                         ReturnsStatus, Status, Code, Gives, Yields, Answers or RespondsWith, and not
                         after Not or Than (ExitsWithCode9, Returns404, FailsWithExit28) - whose body,
                         DataRows and called helpers never mention N, the CurlExitCode member that
                         equals N or the HttpStatusCode member that does; or a name with Throws<X>
                         whose body and called helpers never mention X (X is the name's ...Exception
                         word, or the camel-case word after Throws). Helpers are the methods of the
                         same test project the test calls, two calls deep, so a check left to
                         AssertRefused(...) counts. A number glued to another word (Http2, Sha256)
                         is part of a name, not an outcome
      commented-test    a whole test commented out: a // [TestMethod] (or one inside /* */) with a
                         commented-out signature after it, so the test no longer runs at all

    A candidate is not yet a finding: the auditor reads each one and files those that are real.
    Prints JSON: one object per candidate with file (repository-relative), line (the method's
    signature), test, kind and detail; and testsScanned, projectsScanned.

.PARAMETER Root
    The tree to scan. Default: the repository holding this script.

.PARAMETER OutFile
    Write the JSON here as well as to standard output.

.PARAMETER SelfTest
    Scan Audit/Tools/Fixtures/weak-tests (a UnitTests and an IntegrationTests project) and check every kind is found, and nothing else:
    an all-weak test, a weakened assertion beside a stronger one, an IsNotNull of a value that cannot
    be null, a commented-out assertion, a number stated after ExitsWithCode and Returns, a check left
    to a helper and a commented-out test among others included.
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
    # Blanks // and /* */ comments with spaces (line ends kept), keeping string literals (so "//"
    # in a string survives). The length is kept, so an index into the result is the same
    # character in $Text: the raw body, comments and all, is cut from the same range.
    $pattern = '"(?:\\.|[^"\\])*"|@"(?:""|[^"])*"|''(?:\\.|[^''\\])*''|//[^\r\n]*|/\*[\s\S]*?\*/'
    return [regex]::Replace($Text, $pattern, { param($m) if ($m.Value.StartsWith('//') -or $m.Value.StartsWith('/*')) { [regex]::Replace($m.Value, '[^\r\n]', ' ') } else { $m.Value } })
}

function Get-MethodBody([string]$Clean, [int]$OpenParen) {
    # From a method's opening parenthesis in comment-blanked text: the range of its body, a block in
    # braces or an expression body (=> ...;), as @(start, length); $null when it has none (abstract).
    $depth = 0; $close = -1
    for ($i = $OpenParen; $i -lt $Clean.Length; $i++) {
        if ($Clean[$i] -eq '(') { $depth++ }
        elseif ($Clean[$i] -eq ')') { $depth--; if ($depth -eq 0) { $close = $i; break } }
    }
    if ($close -lt 0) { return $null }
    $start = $close + 1
    while ($start -lt $Clean.Length -and [char]::IsWhiteSpace($Clean[$start])) { $start++ }
    # A where clause or a base/this call may stand between the parameters and the body.
    while ($start -lt $Clean.Length -and $Clean[$start] -notin '{', '=', ';') { $start++ }
    $end = -1
    if ($start + 1 -lt $Clean.Length -and $Clean[$start] -eq '=' -and $Clean[$start + 1] -eq '>') {
        $depth = 0
        for ($i = $start; $i -lt $Clean.Length; $i++) {
            if ($Clean[$i] -in '(', '{', '[') { $depth++ } elseif ($Clean[$i] -in ')', '}', ']') { $depth-- }
            elseif ($Clean[$i] -eq ';' -and $depth -eq 0) { $end = $i; break }
        }
    }
    elseif ($start -lt $Clean.Length -and $Clean[$start] -eq '{') {
        $depth = 0
        for ($i = $start; $i -lt $Clean.Length; $i++) {
            if ($Clean[$i] -eq '{') { $depth++ }
            elseif ($Clean[$i] -eq '}') { $depth--; if ($depth -eq 0) { $end = $i; break } }
        }
    }
    if ($end -lt 0) { return $null }
    return @($start, ($end - $start + 1))
}

function Get-TestMethods([string]$Path, [hashtable]$Helpers) {
    # Each [TestMethod]/[DataTestMethod]: its name, the line of its signature, its attributes, its
    # body with comments blanked, and its raw body with the comments. With -Helpers, every other
    # method of the file with an access modifier or static (a test's helper) is added to it,
    # name -> comment-blanked bodies, so a check a test leaves to a helper can be followed.
    $text = [IO.File]::ReadAllText($Path)
    $clean = Remove-Comments $text
    $methods = @()
    $signature = '(?ms)((?:^[ \t]*\[[^\]\r\n]*\][ \t]*\r?\n)+)[ \t]*(?:public|internal)\s+(?:static\s+)?(?:async\s+)?(?:Task|void)\s+(\w+)\s*\('
    foreach ($m in [regex]::Matches($text, $signature)) {
        $attributes = $m.Groups[1].Value
        if ($attributes -notmatch '\[(?:Data)?TestMethod\b') { continue }
        $range = Get-MethodBody $clean ($m.Index + $m.Length - 1)
        if (-not $range) { continue }
        $body = $clean.Substring($range[0], $range[1])
        $rawBody = $text.Substring($range[0], $range[1])
        $line = ($text.Substring(0, $m.Groups[2].Index) -split "`n").Count
        $methods += [pscustomobject]@{ Name = $m.Groups[2].Value; Line = $line; Attributes = $attributes; Body = $body; RawBody = $rawBody }
    }
    if ($null -ne $Helpers) {
        $helper = '(?m)^[ \t]*(?:(?:public|internal|private|protected|static|async|override|virtual|sealed|new|unsafe)\s+)+[\w<>\[\],?.]+(?:\s*\([^()]*\))?\s+(\w+)\s*(?:<[^>()]*>)?\s*\('
        foreach ($m in [regex]::Matches($clean, $helper)) {
            $range = Get-MethodBody $clean ($m.Index + $m.Length - 1)
            if (-not $range) { continue }
            $name = $m.Groups[1].Value
            $Helpers[$name] = "$($Helpers[$name])`n$($clean.Substring($m.Index, $range[0] + $range[1] - $m.Index))"
        }
    }
    return $methods
}

function Get-CommentedOutTests([string]$Path) {
    # A whole test commented out: a // [TestMethod] (or [DataTestMethod]) line, or one inside a
    # /* */ block, named by the first commented-out method signature after it.
    $text = [IO.File]::ReadAllText($Path)
    $found = @()
    $blocks = @([regex]::Matches($text, '/\*[\s\S]*?\*/') | ForEach-Object { $_.Value })
    $lineComments = '(?m)^[ \t]*//[ \t]*\[(?:Data)?TestMethod\b[^\r\n]*\r?\n(?:[ \t]*//[^\r\n]*\r?\n)*?[ \t]*//[^\r\n]*?\b(?:void|Task)\s+(\w+)\s*\('
    foreach ($m in [regex]::Matches($text, $lineComments)) {
        $found += [pscustomobject]@{ Name = $m.Groups[1].Value; Line = ($text.Substring(0, $m.Index) -split "`n").Count }
    }
    foreach ($b in $blocks) {
        foreach ($m in [regex]::Matches($b, '\[(?:Data)?TestMethod\b[\s\S]*?\b(?:void|Task)\s+(\w+)\s*\(')) {
            $found += [pscustomobject]@{ Name = $m.Groups[1].Value; Line = ($text.Substring(0, $text.IndexOf($b)) -split "`n").Count }
        }
    }
    return $found
}

function Get-TextWithHelpers([string]$Text, [hashtable]$Helpers, [int]$Depth = 2) {
    # The text with the bodies of the helpers it calls, and theirs, $Depth calls deep: a test that
    # leaves its exit-code check to AssertRefused(...) is read with AssertRefused's body.
    if ($null -eq $Helpers -or -not $Helpers.Count) { return $Text }
    $seen = @{}; $all = $Text; $frontier = $Text
    for ($d = 0; $d -lt $Depth; $d++) {
        $next = ''
        foreach ($call in [regex]::Matches($frontier, '\b([A-Za-z_]\w*)\s*(?:<[^<>()]*>)?\s*\(')) {
            $n = $call.Groups[1].Value
            if ($seen.ContainsKey($n) -or -not $Helpers.ContainsKey($n)) { continue }
            $seen[$n] = $true
            $next += "`n$($Helpers[$n])"
        }
        if (-not $next) { break }
        $all += $next; $frontier = $next
    }
    return $all
}

function Get-FirstArgument([string]$Arguments) {
    # The assertion's first argument: up to the first comma at depth 0, or its closing parenthesis.
    $depth = 0
    for ($i = 0; $i -lt $Arguments.Length; $i++) {
        $c = $Arguments[$i]
        if ($c -in '(', '[', '{') { $depth++ }
        elseif ($c -in ')', ']', '}') { if ($depth -eq 0) { return $Arguments.Substring(0, $i).Trim() }; $depth-- }
        elseif ($c -eq ',' -and $depth -eq 0) { return $Arguments.Substring(0, $i).Trim() }
        elseif ($c -eq '"') { $j = $Arguments.IndexOf('"', $i + 1); if ($j -lt 0) { break }; $i = $j }
    }
    return $Arguments.Trim()
}

function Get-RootIdentifier([string]$Arguments) {
    # The variable a check is about: the first identifier of its first argument (result in
    # result.Body). A local starts lower case; a type (TransferHooks.ActivatedBy(...)) or a method
    # called at once (OfferedPsksOf(x)) is no variable, and gives none.
    $first = Get-FirstArgument $Arguments
    if ($first -cmatch '^(?:await\s+)?([a-z_]\w*)(?!\w|\s*\()' -and $Matches[1] -notin 'new', 'typeof', 'default', 'this', 'base', 'nameof') { return $Matches[1] }
    return $null
}

function Test-WeakAssertion($Assertion) {
    return $Assertion.Kind -eq 'IsNotNull' -or ($Assertion.Kind -eq 'IsTrue' -and $Assertion.Args -match '(?:Length|Count|Count\(\))\s*>\s*0|\.Any\(\s*\)')
}

function Test-NeverNull([string]$Expression) {
    # An expression C# never makes null: a string-building call, an interpolated or literal string,
    # a new expression, a length or count, a number.
    $e = $Expression.Trim()
    if ($e -match '^\$?@?\$?"' -or $e -match '^-?\d') { return $true }
    # A new expression itself, not a member read from one (new X().Message can be null).
    if ($e -match '^new\b' -and $e -notmatch '[)\]}]\s*\??\.\s*\w+(?:\s*\(.*\))?$') { return $true }
    if ($e -match '^(?:string|String)\.(?:Join|Format|Concat)\s*\(') { return $true }
    if ($e -match '\.(?:ToString|GetString|Substring|Trim|TrimStart|TrimEnd|ToLower|ToLowerInvariant|ToUpper|ToUpperInvariant|Replace|PadLeft|PadRight|ToArray|ToList|ToCharArray|Split)\s*\((?:[^()]|\([^()]*\))*\)$'){ return $true }
    if ($e -match '\.(?:Length|Count|LongLength)$' -or $e -match '\.Count\(\s*\)$') { return $true }
    return $false
}

function Get-UnassertedFakeValues([string]$Body, $Asserts) {
    # The string literals the test's fakes return - a lambda's result (path => "x", or
    # path => path == "p" ? "x" : null, or a switch arm "k" => "x") or a return "x" - that no
    # assertion mentions, not even inside a longer expected value. A fake that hands the code an
    # exact value the test never checks is the test arranging an outcome and not looking at it.
    $returned = @([regex]::Matches($Body, '=>\s*(?:(?:(?!=>)[^;{}])*?\?\s*)?@?"((?:\\.|[^"\\])*)"|\breturn\s+@?"((?:\\.|[^"\\])*)"') |
        ForEach-Object { if ($_.Groups[1].Success) { $_.Groups[1].Value } else { $_.Groups[2].Value } } |
        Where-Object { $_.Length -ge 4 } | Select-Object -Unique)
    if (-not $returned.Count) { return @() }
    $assertText = (@($Asserts | ForEach-Object { $_.Args }) + @([regex]::Matches($Body, '\b(?:Assert\w+|Expect\w*|Verify\w*)\s*\([^;]*') | ForEach-Object { $_.Value })) -join "`n"
    return @($returned | Where-Object { $assertText.IndexOf($_, [StringComparison]::Ordinal) -lt 0 })
}

$script:HttpStatusNames = @([Enum]::GetNames([Net.HttpStatusCode]))

function Get-StatedNumbers([string]$Name) {
    # The numbers a test name states as its outcome: a number straight after an outcome word -
    # ExitsWith, ExitsWithCode, ExitsWithExit, Exits, ExitCode, Exit, ThrowsExit, FailsWith,
    # Returns, ReturnsStatus, Status, StatusCode, Code, Gives, Yields, Answers, RespondsWith - so a
    # renamed ExitsWithCode9 is read as well as ExitsWith9. A number glued to any other word
    # (Http2, Sha256, Base64, Rfc7230, Port8080) is part of a name, not an outcome, and NotExit15 or\n    # ThanExit15 states what the test does not end with.
    $outcome = '(?<!Not|Than)(ExitsWith(?:Code|Exit)?|ExitWith(?:Code)?|ExitCode|Exits|Exit|ThrowsExit|FailsWith(?:Exit|Code)?|Returns(?:Status(?:Code)?|Code)?|StatusCode|Status|Code|Gives|Yields|Answers|RespondsWith)(\d+)(?!\d)'
    return @([regex]::Matches($Name, $outcome) | ForEach-Object { [pscustomobject]@{ Words = $_.Groups[1].Value; Number = [int]$_.Groups[2].Value } } |
        Group-Object Number | ForEach-Object { $_.Group[0] })
}

function Get-Candidates($Method, [hashtable]$ExitCodes, [hashtable]$Helpers) {
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
        $weakDetails = @()
        $arrangedDetails = @()
        $weak = @($asserts | Where-Object { Test-WeakAssertion $_ })
        $other = $body -match '\bThrows(?:Exactly)?(?:Async)?\b|\b(?:Expect|Verify)\w*\s*\(|\bAssert\w+\s*\('
        if ($weak.Count -eq $asserts.Count -and -not $other) { $weakDetails += "only $(@($weak | ForEach-Object { 'Assert.' + $_.Kind } | Select-Object -Unique) -join ', ')" }
        else {
            # Per assertion: a weak check that is the only check of its value, beside stronger
            # assertions of other values (PD-001 keeps the exit-code check and weakens the bytes').
            $strongText = (@($asserts | Where-Object { -not (Test-WeakAssertion $_) } | ForEach-Object { $_.Args }) + @([regex]::Matches($body, '\b(?:Assert\w+|Expect\w*|Verify\w*)\s*\([^;]*') | ForEach-Object { $_.Value })) -join "`n"
            $unasserted = @(Get-UnassertedFakeValues $body $asserts)
            foreach ($w in $weak) {
                $root = Get-RootIdentifier $w.Args
                if ($root -and $strongText -notmatch "\b$([regex]::Escape($root))\b") {
                    $only = "Assert.$($w.Kind)($(Get-FirstArgument $w.Args)) is the only check of $root"
                    # The sharper case (audit 20261102-172448 dismissed one among 120 weak candidates):
                    # the test arranges a fake that returns an exact value, and nothing asserts that value.
                    if ($unasserted.Count) { $arrangedDetails += "$only, and no assertion checks the value$(if ($unasserted.Count -gt 1) { 's' }) the test's fakes return: $(@($unasserted | ForEach-Object { '"' + $_ + '"' }) -join ', ')" }
                    else { $weakDetails += "$only; the other assertions check other values" }
                }
            }
        }
        if ($arrangedDetails.Count) { $found += @{ kind = 'unasserted-fake-value'; detail = ($arrangedDetails | Select-Object -Unique) -join '; ' } }
        foreach ($n in @($asserts | Where-Object { $_.Kind -eq 'IsNotNull' -and (Test-NeverNull (Get-FirstArgument $_.Args)) })) {
            $weakDetails += "Assert.IsNotNull($(Get-FirstArgument $n.Args)) checks a value that cannot be null"
        }
        if ($weakDetails.Count) { $found += @{ kind = 'weak-assertion'; detail = ($weakDetails | Select-Object -Unique) -join '; ' } }
    }
    $commented = @([regex]::Matches($Method.RawBody, '(?://|/\*)\s*((?:Assert|CollectionAssert|StringAssert)\.\w+\s*\([^\r\n]*)') | ForEach-Object { $_.Groups[1].Value.Trim() })
    if ($commented.Count) { $found += @{ kind = 'commented-assertion'; detail = "commented out: $($commented -join ' | ')" } }
    # A number the name states as the outcome - ExitsWith23, ExitsWithCode9, ThrowsExit6, ExitCode3,
    # Returns404, Status200, FailsWith28 - must appear in the body or its DataRows, as itself, as the
    # CurlExitCode member that equals it, or as the HttpStatusCode member that equals it.
    foreach ($stated in @(Get-StatedNumbers $name)) {
        $n = $stated.Number
        $text = "$($Method.Attributes)`n$(Get-TextWithHelpers $body $Helpers)"
        $members = @($ExitCodes.Keys | Where-Object { $ExitCodes[$_] -eq $n })
        $statuses = @($script:HttpStatusNames | Where-Object { [int][Net.HttpStatusCode]$_ -eq $n })
        $named = ($text -match "\b$n\b") -or @($members | Where-Object { $text -match "CurlExitCode\.$_\b" }).Count -or @($statuses | Where-Object { $text -match "HttpStatusCode\.$_\b" }).Count
        if (-not $named) {
            $alternatives = @(@($members | ForEach-Object { "CurlExitCode.$_" }) + @($statuses | ForEach-Object { "HttpStatusCode.$_" }))
            $found += @{ kind = 'name-lies'; detail = "name says $($stated.Words)$n; the body never mentions $n$(if ($alternatives.Count) { ' or ' + ($alternatives -join '/') })" }
        }
    }
    # Throws as the outcome (_Throws...), naming an exception: its ...Exception word, or the
    # camel-case word after it unless that word joins a clause (WriteThrowsUnderSilent and
    # _ThrowsAndLeavesTheQueue promise no exception type). _ThrowsExit<N> is read above.
    $joiners = '(?:And|Or|Under|When|If|Unless|While|On|For|With|After|Before|Then|But|Without|Only|Again|Once|Naming|The|Its|It|A|An|Exit)(?=[A-Z_0-9]|$)'
    $x = if ($name -match '_Throws([A-Z]\w*?Exception)') { $Matches[1] }
         elseif ($name -match "_Throws(?!$joiners)([A-Z][a-z0-9]+(?:[A-Z][a-z0-9]+)?)") { $Matches[1] }
         else { $null }
    if ($x -and (Get-TextWithHelpers $body $Helpers) -notmatch [regex]::Escape($x)) { $found += @{ kind = 'name-lies'; detail = "name says Throws$x; the body never mentions $x" } }
    return $found
}

function Invoke-Scan([string]$Tree) {
    $exitCodes = Get-ExitCodeValues $Tree
    $candidates = @(); $tests = 0; $projects = 0
    foreach ($project in @(Get-ChildItem -LiteralPath $Tree -Directory | Where-Object { $_.Name -like '*.UnitTests' -or $_.Name -like '*.IntegrationTests' } | Sort-Object Name)) {
        $projects++
        # Every file of the project first, so a helper in another file of a partial class is known.
        $helpers = @{}; $found = @()
        foreach ($file in @(Get-ChildItem -LiteralPath $project.FullName -Recurse -Filter '*.cs' -File | Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' })) {
            foreach ($method in @(Get-TestMethods $file.FullName $helpers)) { $found += [pscustomobject]@{ File = $file; Method = $method } }
            foreach ($c in @(Get-CommentedOutTests $file.FullName)) {
                $candidates += [ordered]@{ file = $file.FullName.Substring($Tree.Length).TrimStart('\', '/') -replace '\\', '/'; line = $c.Line; test = $c.Name; kind = 'commented-test'; detail = 'a [TestMethod] commented out, so the test no longer runs' }
            }
        }
        foreach ($f in $found) {
            $tests++
            foreach ($c in @(Get-Candidates $f.Method $exitCodes $helpers)) {
                $candidates += [ordered]@{ file = $f.File.FullName.Substring($Tree.Length).TrimStart('\', '/') -replace '\\', '/'; line = $f.Method.Line; test = $f.Method.Name; kind = $c.kind; detail = $c.detail }
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
        'Fetch_Loopback_ReturnsTheBody:weak-assertion',
        'Parse_Value_ThrowsFormatException:name-lies',
        'RunAsync_SaveFails_ExitsWith23:name-lies',
        'Write_Info_WritesTheLine:weak-assertion',
        'Refusal_AfterExpiry_HasExpired:no-assertion',
        'Refusal_AfterExpiry_HasExpired:commented-assertion',
        'Retry_WithoutMaxTime_IsNeverAbandoned:ignored-test',
        'Encode_Header_WritesTheBytes:weak-assertion',
        'Format_Count_WritesTheDigits:weak-assertion',
        'Transfer_Timeout_SetsTheCode:commented-assertion',
        'Rewrite_EmptyPath_ReadsTheConfiguredFile:unasserted-fake-value',
        'Parse_DeepNesting_ExitsWithCode9:name-lies',
        'Get_MissingPage_Returns404:name-lies',
        'Close_Twice_IsHarmless:commented-test'
    ) | Sort-Object
    $failed = 0
    function Check([string]$Name, [bool]$Ok, [string]$Detail) { if (-not $Ok) { $script:failed++ }; Write-Host "$(if ($Ok) { 'PASS' } else { 'FAIL' }) ${Name}: $Detail" }
    Check 'every planted kind is found, and nothing else' (($got -join '|') -eq ($want -join '|')) ($got -join ', ')
    Check 'tests and projects are counted' ($r.testsScanned -eq 23 -and $r.projectsScanned -eq 2) "tests $($r.testsScanned), projects $($r.projectsScanned)"
    $detail = { param($Test) "$(@($r.candidates | Where-Object { $_.test -eq $Test -and $_.kind -ne 'no-assertion' })[0].detail)" }
    Check 'a fake value no assertion checks is named' ((& $detail 'Rewrite_EmptyPath_ReadsTheConfiguredFile') -eq 'Assert.IsNotNull(url) is the only check of url, and no assertion checks the value the test''s fakes return: "http://127.0.0.1:9"') (& $detail 'Rewrite_EmptyPath_ReadsTheConfiguredFile')
    Check 'a weakened assertion beside a stronger one names the value only it checks' ((& $detail 'Encode_Header_WritesTheBytes') -eq 'Assert.IsNotNull(bytes) is the only check of bytes; the other assertions check other values') (& $detail 'Encode_Header_WritesTheBytes')
    Check 'an IsNotNull of a value that cannot be null says so' ((& $detail 'Format_Count_WritesTheDigits') -eq 'Assert.IsNotNull(count.ToString()) checks a value that cannot be null') (& $detail 'Format_Count_WritesTheDigits')
    Check 'a commented-out assertion is quoted' ((& $detail 'Transfer_Timeout_SetsTheCode') -eq 'commented out: Assert.AreEqual(28, code);') (& $detail 'Transfer_Timeout_SetsTheCode')
    Check 'values that can and cannot be null' ((Test-NeverNull 'Encoding.UTF8.GetString(stream.ToArray())') -and (Test-NeverNull '$"a{b}"') -and (Test-NeverNull 'string.Join(",", x)') -and (Test-NeverNull 'list.Count') -and (Test-NeverNull 'new byte[2]') -and -not (Test-NeverNull 'result') -and -not (Test-NeverNull 'map.GetValueOrDefault(k)') -and -not (Test-NeverNull 'new X().Message')) 'GetString, interpolation, Join, Count, new; not a variable, a lookup or a member of a new object'
    Check 'a number the name states after any outcome word is checked' ((& $detail 'Parse_DeepNesting_ExitsWithCode9') -eq 'name says ExitsWithCode9; the body never mentions 9') (& $detail 'Parse_DeepNesting_ExitsWithCode9')
    Check 'outcome numbers are read after their words, not glued to names or negated' ((@(Get-StatedNumbers 'X_Http2Sha256_ExitsWithCode9AndReturns404NotExit15' | ForEach-Object { $_.Number }) -join ',') -eq '9,404') (@(Get-StatedNumbers 'X_Http2Sha256_ExitsWithCode9AndReturns404NotExit15' | ForEach-Object { $_.Number }) -join ',')
    $line = @($r.candidates | Where-Object { $_.test -eq 'RunAsync_SaveFails_ExitsWith23' })[0]
    Check 'a candidate names its file and signature line' ($line.file -eq 'Sample.UnitTests/SampleTests.cs' -and $line.line -gt 0) "$($line.file):$($line.line)"
    exit $(if ($failed) { 1 } else { 0 })
}

if (-not $Root) { $Root = $repo }
$result = Invoke-Scan (Resolve-Path $Root).Path
$json = [pscustomobject]$result | ConvertTo-Json -Depth 5
if ($OutFile) { [IO.File]::WriteAllText($OutFile, $json) }
Write-Output $json
