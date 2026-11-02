<#
.SYNOPSIS
    Lists every verbose, trace, log or diagnostic write in Curl's production code that passes a
    password, passphrase, secret, key or token: candidates for the security auditor's step 3.

.DESCRIPTION
    The security auditor searched six libraries for secrets in log calls, so a password appended
    to the AWS_SIGV4 "Server auth using ... with user" line in Curl.Engine, or to the CONNECT
    tunnel's "Proxy auth using" line in Curl.Networking, went unseen (PD-102, audits of 2026-10
    and 2026-11). This scan reads every production project instead: every folder under -Root
    named *.UnitLibrary or holding a .csproj, whose name does not end in Tests (Curl.Console and
    the other hosts included).

    A writer is a call to a member named Report*, Info, Warn*, Trace*, Debug, Verbose* or Log*,
    or a Write, WriteLine, WriteAsync or WriteLineAsync on a receiver whose name holds error,
    stderr, verbose, trace or diagnostic (any case). Its statement, up to the ending semicolon,
    is read with the text of every string literal removed and the holes of an interpolated
    string kept, so "Password:" in a message is not a secret and {credential.Password} is.

    A secret is an identifier among the call's arguments ending in password, passwd, passphrase,
    secret, privatekey, apikey, accesskey, secretkey or token (any case; never a
    CancellationToken, and never a method called with it, as in IsSecret(name)). A method's
    declaration is not a call and is not read.

    A candidate is not yet a finding: the auditor reads each, and files the ones whose output
    real curl would not print (secret-in-output when it reaches -v or --trace by default,
    secret-in-log otherwise). Prints JSON: projectsScanned, writersScanned, and one object per
    candidate with file (relative to -Root), line, writer, secret and the statement's text.

.PARAMETER Root
    The tree to scan. Default: the repository holding this script.

.PARAMETER OutFile
    Write the JSON here as well as to standard output.

.PARAMETER SelfTest
    Scan Audit/Tools/Fixtures/secrets and check exactly the planted writes are found: a password
    in a Server and in a Proxy auth-using line, a passphrase to a trace writer and a bearer token,
    with a user name, a literal "Password", a cancellation token, a wire write, a commented-out
    write, a method declaration and a test project left alone.

.EXAMPLE
    powershell -NoProfile -File Audit/Tools/Find-SecretsInDiagnostics.ps1 -Root . -OutFile $env:TEMP\secrets.json
#>
param(
    [string]$Root,
    [string]$OutFile,
    [switch]$SelfTest
)

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$Writer = '(?<![\w.])(?:[A-Za-z_][\w?]*(?:\?)?\.)*(?:(?<name>Report\w*|Info|Warn\w*|Trace\w*|Debug|Verbose\w*|Log\w*)|(?i:(?<receiver>\w*(?:error|stderr|verbose|trace|diagnostic)\w*))\??\.(?<name>Write(?:Line)?(?:Async)?))\s*\('
# An identifier ending in a secret's word, not followed by "(" (IsSecret(name) is a check, not a secret).
$SecretPattern = '(?i)\b(?!cancellation)\w*(?:password|passwd|passphrase|secret|privatekey|apikey|accesskey|secretkey|token)\b(?!\s*\()'

function Remove-StringText([string]$Statement) {
    # Every string literal's text dropped; an interpolated string keeps its {holes}.
    $pattern = '\$@"(?:""|[^"])*"|@\$"(?:""|[^"])*"|\$"(?:\\.|[^"\\])*"|@"(?:""|[^"])*"|"(?:\\.|[^"\\])*"|''(?:\\.|[^''\\])*'''
    return [regex]::Replace($Statement, $pattern, {
            param($m)
            if ($m.Value.StartsWith('$') -or $m.Value.StartsWith('@$')) { ' ' + (@([regex]::Matches($m.Value, '\{[^{}]*\}') | ForEach-Object { $_.Value }) -join ' ') + ' ' } else { ' ' }
        })
}

function Get-WriterCandidates([string]$Text) {
    # Each writer call whose statement passes a secret: Line, Writer, Secret, Text.
    $found = @()
    $lineStarts = @(0) + @([regex]::Matches($Text, "`n") | ForEach-Object { $_.Index + 1 })
    $writers = 0
    foreach ($m in [regex]::Matches($Text, $Writer)) {
        # Not in a comment line.
        $lineIndex = [Array]::BinarySearch([int[]]$lineStarts, $m.Index); if ($lineIndex -lt 0) { $lineIndex = (-bnot $lineIndex) - 1 }
        $lineText = $Text.Substring($lineStarts[$lineIndex], $m.Index - $lineStarts[$lineIndex])
        if ($lineText -match '^\s*(?://|\*|/\*)') { continue }
        # A declaration (private static void LogSecret(...), string LoginKeyOf(...)), not a call.
        # A type before the name: a word that is no keyword, a generic's >, a nullable's ?, an array's ];
        # never => or a ternary's ? (a call there).
        if ($lineText -match '(?:\b(?!(?:return|await|throw|else|yield|new|in|is|as|case|when|and|or|not)\b)\w+|\w>|\w\?|\])\s+$') { continue }
        $writers++
        $end = $Text.IndexOf(';', $m.Index)
        if ($end -lt 0) { $end = $Text.Length - 1 }
        $statement = $Text.Substring($m.Index, [Math]::Min($end - $m.Index + 1, 600))
        # The arguments only: the writer's own name (LogSecret) is not a secret passed to it.
        $code = Remove-StringText $statement.Substring($m.Length - 1)
        $hit = [regex]::Match($code, $SecretPattern)
        if (-not $hit.Success) { continue }
        $found += [pscustomobject]@{ Line = $lineIndex + 1; Writer = $m.Groups['name'].Value; Secret = $hit.Value; Text = ($statement -replace '\s+', ' ').Trim() }
    }
    return [pscustomobject]@{ Writers = $writers; Candidates = $found }
}

function Invoke-Scan([string]$Tree) {
    $candidates = @(); $projects = 0; $writers = 0
    foreach ($project in @(Get-ChildItem -LiteralPath $Tree -Directory | Where-Object { $_.Name -notlike '*Tests' -and $_.Name -notin 'Audit', 'Gap', 'Documentation', 'Tasks' -and ($_.Name -like '*.UnitLibrary' -or @(Get-ChildItem -LiteralPath $_.FullName -Filter '*.csproj' -File).Count) } | Sort-Object Name)) {
        $projects++
        foreach ($file in @(Get-ChildItem -LiteralPath $project.FullName -Recurse -Filter '*.cs' -File | Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' })) {
            $r = Get-WriterCandidates ([IO.File]::ReadAllText($file.FullName))
            $writers += $r.Writers
            foreach ($c in $r.Candidates) {
                $candidates += [ordered]@{ file = $file.FullName.Substring($Tree.Length).TrimStart('\', '/') -replace '\\', '/'; line = $c.Line; writer = $c.Writer; secret = $c.Secret; text = $c.Text }
            }
        }
    }
    return [ordered]@{ projectsScanned = $projects; writersScanned = $writers; candidates = $candidates }
}

if ($SelfTest) {
    $r = Invoke-Scan (Resolve-Path (Join-Path $PSScriptRoot 'Fixtures\secrets')).Path
    $got = @($r.candidates | ForEach-Object { "$($_.line):$($_.writer):$($_.secret)" } | Sort-Object) -join '|'
    $want = @('10:ReportInfo:Password', '15:Write:KeyPassphrase', '20:ReportInfo:bearerToken', '22:ReportInfo:Password') | Sort-Object
    $failed = 0
    function Check([string]$Name, [bool]$Ok, [string]$Detail) { if (-not $Ok) { $script:failed++ }; Write-Host "$(if ($Ok) { 'PASS' } else { 'FAIL' }) ${Name}: $Detail" }
    Check 'exactly the writes passing a secret are found' ($got -eq ($want -join '|')) $got
    Check 'every production project is scanned, a test project is not' ($r.projectsScanned -eq 2) "projects $($r.projectsScanned)"
    Check 'every writer call is counted, comments and declarations left out' ($r.writersScanned -eq 8) "writers $($r.writersScanned)"
    $first = @($r.candidates | Where-Object { $_.secret -eq 'Password' })[0]
    Check 'a candidate names its file and the whole statement' ($first.file -eq 'Sample.Engine.UnitLibrary/AuthLines.cs' -and $first.text -like 'events.ReportInfo($"Server auth using AWS_SIGV4 with user*{credential.Password}*);') "$($first.file) $($first.text)"
    exit $(if ($failed) { 1 } else { 0 })
}

if (-not $Root) { $Root = $repo }
$result = Invoke-Scan (Resolve-Path $Root).Path
$json = [pscustomobject]$result | ConvertTo-Json -Depth 5
if ($OutFile) { [IO.File]::WriteAllText($OutFile, $json) }
Write-Output $json
