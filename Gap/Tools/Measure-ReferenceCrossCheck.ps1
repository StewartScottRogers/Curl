<#
.SYNOPSIS
    Runs the upstream cases that need one plain-HTTP exchange through both real binaries,
    the matched reference curl and the built Curl.Console, and amends the behaviour
    measurement with what the two binaries say.

.DESCRIPTION
    Area behaviour of the gap analysis office, ADR-0433 decision 2 ("through both binaries
    where runnable"). The in-process harness (Gap/Tools/Measure-UpstreamCases.cs) cannot run
    the reference curl; this cross-check covers the cases where both binaries can see the
    same loopback server, Record-CurlExchange.ps1's.

    Selection. A case is cross-checked when all of these hold, tested in this order; the
    first that fails is the rule that leaves it out, and the measurement counts each rule:
      not-match-or-gap       its state in -Measurement is match or gap
      limit                  it is among the first -Limit such cases (when -Limit is given)
      not-parsed             the harness's expander and parser read it with no unknown variable
      server-not-http-alone  its <client><server> is http alone
      reply-not-one-data     its <reply> has one <data> part and no <dataN> or <servercmd>
    The last three are decided by Gap/Tools/Expand-UpstreamCase.cs, which expands each case
    with the harness's own UpstreamTestFileExpander and UpstreamTestCaseParser: %HOSTIP is
    127.0.0.1, %HTTPPORT is -Port (the same for both binaries), %LOGDIR is <work>/<N>.
    A case left out keeps its in-process verdict.

    Working directory. Each binary starts, process directory and all, in the case's own fresh
    %LOGDIR (as upstream's runner starts it), which is deleted afterwards, so a command such as
    test 1489's "-D %" writes its file there and never into the directory the run was started
    from (BL-1840).

    Per case, both binaries run through Record-CurlExchange.ps1 with the case's <data> as
    -Response, its <stdin> as -StandardInput, and its client files written fresh into its
    %LOGDIR before each run. The binaries' exit codes, request bytes, stdout and stderr are
    compared, in that order; the first that differs is the difference. Each read of a
    request waits up to 3 seconds for its next byte (Record-CurlExchange.ps1
    -RequestReadMilliseconds 3000), longer than curl's 1-second wait for 100 Continue, so
    the server answers only once the whole request is in, as upstream's sws does. After a
    response the server keeps the connection open, as sws does, unless the response holds
    sws's "swsclose" or its head a Connection: close header: it waits for curl to hang up or
    for 1.5 seconds without a byte, and answers up to 20 more requests on the connection
    with the same <data> (Record-CurlExchange.ps1 -HoldOpenMilliseconds 1500
    -AnswerHeldRequests 20); a server that closed at once would leave each binary to race
    the close its own way (test1075). Before comparing, stderr loses its progress meter
    lines (the "% Total" and "Dload" header lines and the lines of digits, colons and
    dashes), whose times differ from run to run, and the number in every "after <n> ms" and
    "after <n> milliseconds" becomes one fixed token, since how long a connect or a transfer
    took differs too (test1293); every
    multipart boundary= the request names is replaced by one fixed token in the request and
    stdout; and every number from 32768 to 65535 in the request, stdout and stderr by
    another, since each binary's source port is the system's pick. No <strip> rule of the
    harness is applied: the reference's own output is the yardstick.

    The two answers combine with the in-process verdict:
      agree,  match  -> unchanged
      agree,  gap    -> match, reason reference-diverges: the case's <verify> is not what
                        this platform's matched build produces; evidence keeps the
                        harness's first difference
      differ, match  -> gap, evidence "in-process and out-of-process differ"
      differ, gap    -> gap, expected is the reference's output
    Every changed item's evidence starts "was <previous state>". counts and reasons are
    recomputed as ConvertTo-BehaviourMeasurement.ps1 computes them, and the measurement
    gains "crossCheck": crossChecked, leftOut (count per rule), referenceDiverges,
    disagreements.

    Runs under Windows PowerShell 5.1 and PowerShell 7. The script is ASCII only.

.PARAMETER Measurement
    The behaviour measurement ConvertTo-BehaviourMeasurement.ps1 wrote.

.PARAMETER TestsData
    The release's tests/data folder the measurement was taken from.

.PARAMETER OutFile
    Where the amended copy of the measurement is written. -Measurement is not changed.

.PARAMETER Limit
    At most this many match or gap cases, in measurement order, go to the expander. 0, the
    default, means all of them.

.PARAMETER Port
    The loopback port both binaries' server listens on, %HTTPPORT. Default 18990.

.PARAMETER CaseTimeLimitSeconds
    Each binary's command line starts --max-time <this>, so a run that would wait forever
    ends; a case's own -m comes later and still wins. Default 20. The server answers up to
    20 connections, each with the same <data>, as upstream's server does for a followed
    redirect (and up to 20 more requests on each kept-alive one).

.PARAMETER WorkDirectory
    Where each case's %LOGDIR and recorded outputs go; it must hold no blank, as upstream's
    commands name %LOGDIR unquoted. Default: <system temp>\curl-crosscheck, or
    C:\curl-crosscheck when the temp path holds a blank.

.PARAMETER Reference
    The reference curl. Default: Get-GapReferenceCurlPath of GapProbeFunctions.ps1.

.PARAMETER Candidate
    The Curl.Console binary. Default: Get-GapCandidateCurl of GapProbeFunctions.ps1 (the
    newest Release build).

.PARAMETER ExpansionFile
    A JSON file Expand-UpstreamCase.cs already wrote, used instead of running it.

.PARAMETER RecordedResults
    A folder holding <N>\reference and <N>\curl, each with Record-CurlExchange.ps1's
    request.bin, stdout.bin, stderr.txt and exitcode.txt, read instead of running either
    binary, so no binary or socket is needed.

.PARAMETER SelfTest
    Runs the script on Gap/Tools/Fixtures/crosscheck (a measurement, an expansion and
    recorded results) and prints one PASS or FAIL line per check: the four combinations
    and the selection rule's counts, the stderr, boundary and port masking, and when the
    server keeps a connection alive.

.EXAMPLE
    pwsh -File Gap\Tools\Measure-ReferenceCrossCheck.ps1 -Measurement behaviour.json -TestsData $env:LOCALAPPDATA\Curl\gap\upstream\8.21.0\tests\data -OutFile behaviour-crosschecked.json -Limit 50
#>
param(
    [string] $Measurement,
    [string] $TestsData,
    [string] $OutFile,
    [ValidateRange(0, [int]::MaxValue)] [int] $Limit = 0,
    [ValidateRange(1, 65535)] [int] $Port = 18990,
    [ValidateRange(1, 3600)] [int] $CaseTimeLimitSeconds = 20,
    [string] $WorkDirectory,
    [string] $Reference,
    [string] $Candidate,
    [string] $ExpansionFile,
    [string] $RecordedResults,
    [switch] $SelfTest
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 3

function Get-DefaultWorkDirectory {
    $temp = Join-Path ([System.IO.Path]::GetTempPath()) 'curl-crosscheck'
    if ($temp -match '\s') { return 'C:\curl-crosscheck' }
    return $temp
}

# Bytes as Record-CurlExchange.ps1's backslash-escaped string: printable ASCII stays, all else \xHH.
function ConvertTo-EscapedBytes([byte[]] $Bytes) {
    $text = New-Object System.Text.StringBuilder
    foreach ($b in $Bytes) {
        if ($b -ge 0x20 -and $b -lt 0x7F -and $b -ne 0x5C) { [void]$text.Append([char]$b) }
        else { [void]$text.Append(('\x{0:X2}' -f $b)) }
    }
    return $text.ToString()
}

function Read-Bytes([string] $Path) {
    if (Test-Path -LiteralPath $Path) { return ,([byte[]][System.IO.File]::ReadAllBytes($Path)) }
    return ,([byte[]]@())
}

# stderr without the progress meter, whose times differ between runs.
function Remove-ProgressMeter([byte[]] $Bytes) {
    $text = [System.Text.Encoding]::GetEncoding(28591).GetString($Bytes)
    $kept = @($text -split '(?<=[\r\n])' | Where-Object { $_ -notmatch '^\s*%\s+Total|^\s*Dload\s|^[\s\d.:kMGTP-]+\r?\n?$' -or $_ -match '^\r?\n$' })
    return ($kept -join '')
}

# Bytes with every occurrence of each random multipart boundary the request names, read from
# every boundary= parameter in it (the outer Content-Type, a nested part's multipart/mixed, and
# one a -H Content-Type such as test277's text/info carries), replaced by a fixed token, as
# upstream's <strip> rules do; each run of curl picks its own boundaries (test1133, test1315).
function Set-FixedMultipartBoundary([byte[]] $Bytes, [byte[]] $Request) {
    $latin1 = [System.Text.Encoding]::GetEncoding(28591)
    $boundaries = @([regex]::Matches($latin1.GetString($Request), '(?im)^Content-Type:[^\r\n]*?;[ \t]*boundary=("?)([^";\r\n]+)\1') | ForEach-Object { $_.Groups[2].Value } | Select-Object -Unique)
    if ($boundaries.Count -eq 0) { return ,$Bytes }
    $text = $latin1.GetString($Bytes)
    foreach ($boundary in $boundaries) { $text = $text.Replace($boundary, '<multipart-boundary>') }
    return ,([byte[]]$latin1.GetBytes($text))
}

# Bytes with every number from 32768 to 65535 (the start of Linux's ephemeral port range, which
# holds Windows' 49152 and up) replaced by a fixed token: the source port each binary's connection
# gets is the system's pick, and curl names it in a PROXY line (test1455, test3201, test3220) and
# in %{local_port} (test435). A difference between two numbers both in that range is not seen.
function Set-FixedEphemeralPorts([byte[]] $Bytes) {
    $latin1 = [System.Text.Encoding]::GetEncoding(28591)
    $text = [regex]::Replace($latin1.GetString($Bytes), '(?<![0-9])[0-9]{5}(?![0-9])', {
            param($match)
            $number = [int] $match.Value
            if ($number -ge 32768 -and $number -le 65535) { '<ephemeral-port>' } else { $match.Value }
        })
    return ,([byte[]]$latin1.GetBytes($text))
}

# Text with the number in every "after <n> ms" and "after <n> milliseconds" replaced by a fixed
# token: curl names how long a connect or a transfer took ("Failed to connect to 0.0.0.0 port 80
# after 16 ms", "Operation timed out after 1001 milliseconds"), and that time differs from run to
# run and binary to binary (test1293). The unit is kept, so "ms" against "milliseconds" is seen.
function Set-FixedElapsedTimes([string] $Text) {
    return [regex]::Replace($Text, '(?<=\bafter )[0-9]+(?= (?:ms|milliseconds)\b)', '<elapsed>')
}

function Read-Recording([string] $Folder) {
    $exit = ''
    if (Test-Path -LiteralPath (Join-Path $Folder 'exitcode.txt')) { $exit = ([System.IO.File]::ReadAllText((Join-Path $Folder 'exitcode.txt'))).Trim() }
    $request = Read-Bytes (Join-Path $Folder 'request.bin')
    $latin1 = [System.Text.Encoding]::GetEncoding(28591)
    # The progress meter goes first: its lines hold numbers in the ephemeral range too. Elapsed
    # times go next, so an elapsed time from 32768 ms up is not read as a port.
    $stderr = Set-FixedElapsedTimes (Remove-ProgressMeter (Read-Bytes (Join-Path $Folder 'stderr.txt')))
    return [pscustomobject]@{
        ExitCode = $exit
        Request = Set-FixedEphemeralPorts (Set-FixedMultipartBoundary $request $request)
        Stdout = Set-FixedEphemeralPorts (Set-FixedMultipartBoundary (Read-Bytes (Join-Path $Folder 'stdout.bin')) $request)
        Stderr = $latin1.GetString((Set-FixedEphemeralPorts ([byte[]]$latin1.GetBytes($stderr))))
    }
}

# Whether the loopback server keeps a connection open after sending this response, as upstream's
# sws does: it closes only when the response holds "swsclose" anywhere (sws's own instruction,
# which upstream puts in the status line or body) or its head has a Connection: close header.
function Test-KeepsConnectionAlive([byte[]] $Response) {
    $text = [System.Text.Encoding]::GetEncoding(28591).GetString($Response)
    if ($text.Contains('swsclose')) { return $false }
    $headEnd = $text.IndexOf("`r`n`r`n")
    if ($headEnd -lt 0) { $headEnd = $text.IndexOf("`n`n") }
    $head = if ($headEnd -ge 0) { $text.Substring(0, $headEnd) } else { $text }
    return -not ($head -match '(?im)^Connection:[^\r\n]*\bclose\b')
}

function Get-FirstByteDifference([byte[]] $A, [byte[]] $B) {
    $n = [Math]::Min($A.Length, $B.Length)
    for ($i = 0; $i -lt $n; $i++) { if ($A[$i] -ne $B[$i]) { return $i } }
    if ($A.Length -ne $B.Length) { return $n }
    return -1
}

# The first way the candidate's recording differs from the reference's, or $null.
function Compare-Recording($Ref, $Cand) {
    if ($Ref.ExitCode -ne $Cand.ExitCode) { return "exit code $($Cand.ExitCode), reference $($Ref.ExitCode)" }
    $at = Get-FirstByteDifference $Ref.Request $Cand.Request
    if ($at -ge 0) { return "request differs at byte $at" }
    $at = Get-FirstByteDifference $Ref.Stdout $Cand.Stdout
    if ($at -ge 0) { return "stdout differs at byte $at" }
    if ($Ref.Stderr -cne $Cand.Stderr) { return 'stderr differs' }
    return $null
}

function Get-ReferenceSummary($Ref) {
    $stdout = [System.Text.Encoding]::GetEncoding(28591).GetString($Ref.Stdout)
    if ($stdout.Length -gt 200) { $stdout = $stdout.Substring(0, 200) }
    return "reference curl exits $($Ref.ExitCode); stdout $($Ref.Stdout.Length) bytes: $(ConvertTo-EscapedBytes ([System.Text.Encoding]::GetEncoding(28591).GetBytes($stdout)))"
}

# Runs the script block with the whole process, not only PowerShell's location, in the folder:
# a child started with no WorkingDirectory (Record-CurlExchange.ps1 sets none) inherits the
# process's directory, and Push-Location alone leaves that where the caller started (BL-1840).
function Invoke-InScratchDirectory([string] $Directory, [scriptblock] $Script) {
    $previous = [System.Environment]::CurrentDirectory
    [System.Environment]::CurrentDirectory = $Directory
    Push-Location -LiteralPath $Directory
    try { & $Script }
    finally {
        Pop-Location
        [System.Environment]::CurrentDirectory = $previous
    }
}

function Invoke-Recording($Case, [string] $Curl, [string] $LogRoot, [string] $Out) {
    $logDirectory = Join-Path $LogRoot ([string]$Case.number)
    if (Test-Path -LiteralPath $logDirectory) { Remove-Item -LiteralPath $logDirectory -Recurse -Force }
    New-Item -ItemType Directory -Force -Path $logDirectory | Out-Null
    foreach ($file in @($Case.clientFiles)) {
        $parent = Split-Path $file.path -Parent
        if ($parent) { New-Item -ItemType Directory -Force -Path $parent | Out-Null }
        [System.IO.File]::WriteAllBytes($file.path, [Convert]::FromBase64String($file.content))
    }
    $recorder = Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) 'Record-CurlExchange.ps1'
    # --max-time first, so a case's own -m still wins; a curl that would wait forever ends.
    $arguments = [string[]](@('--max-time', [string]$CaseTimeLimitSeconds) + @($Case.arguments))
    $responseBytes = [byte[]][Convert]::FromBase64String($Case.response)
    $response = ConvertTo-EscapedBytes $responseBytes
    $stdin = ConvertTo-EscapedBytes ([Convert]::FromBase64String($Case.standardInput))
    # A kept-alive connection stays open after each response, as sws keeps it, until curl hangs
    # up or sends nothing for 1.5 seconds, and up to 20 more requests on it are answered with the
    # same <data>: closing it at once makes each binary race the server's close its own way
    # (test1075's keep-alive 401 before the authenticated retry).
    $holdOpen = @{}
    if (Test-KeepsConnectionAlive $responseBytes) { $holdOpen = @{ HoldOpenMilliseconds = 1500; AnswerHeldRequests = 20 } }
    # The binary starts in the case's own fresh %LOGDIR, as upstream's runner starts it, so a
    # relative file an upstream command names (-D %, -o out) lands there; the folder is
    # deleted afterwards and nothing is written into the caller's directory (BL-1840).
    try {
        # Each read of a request waits up to 3 seconds, so curl's 1-second wait for 100 Continue
        # before it sends a body does not end the read early: upstream's sws reads the whole
        # request before it answers (test60, test1068).
        Invoke-InScratchDirectory $logDirectory { & $recorder -Port $Port -Response $response -Connections 20 -CurlArgs $arguments -OutDirectory $Out -Curl $Curl -StandardInput $stdin -RequestReadMilliseconds 3000 @holdOpen | Out-Null }
    }
    finally { Remove-Item -LiteralPath $logDirectory -Recurse -Force -ErrorAction SilentlyContinue }
}

function Update-Counts($M) {
    $items = @($M.items)
    $counts = [ordered]@{}
    foreach ($state in 'match', 'gap', 'unmeasured', 'excluded') { $counts[$state] = @($items | Where-Object { $_.state -eq $state }).Count }
    $counts.x = $counts.match
    $counts.y = $counts.match + $counts.gap + $counts.unmeasured
    $reasonNames = [string[]]@($items | Where-Object { $_.reason } | ForEach-Object { $_.reason } | Select-Object -Unique)
    [Array]::Sort($reasonNames, [StringComparer]::Ordinal)
    $reasons = [ordered]@{}
    foreach ($name in $reasonNames) { $reasons[$name] = @($items | Where-Object { $_.reason -eq $name }).Count }
    $M.counts = $counts
    $M.reasons = $reasons
}

function Invoke-CrossCheck {
    $m = Get-Content -LiteralPath $Measurement -Raw | ConvertFrom-Json
    $leftOut = [ordered]@{ 'not-match-or-gap' = 0; 'limit' = 0; 'not-parsed' = 0; 'server-not-http-alone' = 0; 'reply-not-one-data' = 0 }
    $candidates = New-Object System.Collections.Generic.List[object]
    foreach ($item in @($m.items)) {
        if ($item.state -ne 'match' -and $item.state -ne 'gap') { $leftOut['not-match-or-gap']++ }
        elseif ($Limit -gt 0 -and $candidates.Count -ge $Limit) { $leftOut['limit']++ }
        else { $candidates.Add($item) }
    }
    $work = $WorkDirectory
    if (-not $work) { $work = Get-DefaultWorkDirectory }
    $work = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($work)
    New-Item -ItemType Directory -Force -Path $work | Out-Null
    $logRoot = Join-Path $work 'logs'
    $byNumber = @{}
    if ($candidates.Count -gt 0) {
        $expansionPath = $ExpansionFile
        if (-not $expansionPath) {
            $expansionPath = Join-Path $work 'expansion.json'
            $numbers = ($candidates | ForEach-Object { $_.key -replace '^behaviour:test', '' }) -join ','
            & dotnet run --file (Join-Path $PSScriptRoot 'Expand-UpstreamCase.cs') -- $TestsData $Port $logRoot $expansionPath $numbers | Out-Null
            if ($LASTEXITCODE -ne 0) { throw "Expand-UpstreamCase.cs exited $LASTEXITCODE" }
        }
        foreach ($c in @((Get-Content -LiteralPath $expansionPath -Raw | ConvertFrom-Json).cases)) { $byNumber[[string]$c.number] = $c }
    }
    if (-not $RecordedResults) {
        . (Join-Path $PSScriptRoot 'Invoke-GapProbe.ps1')
        if (-not $script:Reference) { $script:Reference = Get-GapReferenceCurlPath }
        if (-not $script:Reference) { throw 'No reference curl found; pass -Reference.' }
        if (-not $script:Candidate) { $script:Candidate = Get-GapCandidateCurl }
    }
    $crossChecked = 0; $diverges = 0; $disagreements = 0
    foreach ($item in $candidates) {
        $number = $item.key -replace '^behaviour:test', ''
        $case = $byNumber[$number]
        if ($null -eq $case) { $leftOut['not-parsed']++; continue }
        if ($case.leftOutBy) { $leftOut[[string]$case.leftOutBy]++; continue }
        $crossChecked++
        $recordings = $RecordedResults
        if (-not $recordings) {
            $recordings = Join-Path $work 'recorded'
            Invoke-Recording $case $script:Reference $logRoot (Join-Path $recordings "$number\reference")
            Invoke-Recording $case $script:Candidate $logRoot (Join-Path $recordings "$number\curl")
        }
        $ref = Read-Recording (Join-Path $recordings "$number\reference")
        $cand = Read-Recording (Join-Path $recordings "$number\curl")
        $difference = Compare-Recording $ref $cand
        if ($null -eq $difference) {
            if ($item.state -eq 'gap') {
                $diverges++
                $item.evidence = "was gap; in-process: $($item.actual)"
                $item.state = 'match'; $item.reason = 'reference-diverges'
                $item.expected = "the reference curl's output"; $item.actual = 'same as the reference curl'
            }
        } else {
            $disagreements++
            if ($item.state -eq 'match') {
                $item.state = 'gap'; $item.reason = $null
                $item.evidence = 'was match; in-process and out-of-process differ'
                $item.expected = Get-ReferenceSummary $ref; $item.actual = $difference
            } else {
                $item.expected = Get-ReferenceSummary $ref
            }
        }
    }
    Update-Counts $m
    $m | Add-Member -NotePropertyName crossCheck -Force -NotePropertyValue ([ordered]@{
        crossChecked = $crossChecked; leftOut = $leftOut; referenceDiverges = $diverges; disagreements = $disagreements })
    $json = $m | ConvertTo-Json -Depth 8
    [System.IO.File]::WriteAllText([System.IO.Path]::GetFullPath($OutFile), $json, (New-Object System.Text.UTF8Encoding $false))
    return $m
}

if ($SelfTest) {
    $fixtures = Join-Path $PSScriptRoot 'Fixtures/crosscheck'
    $script:Measurement = Join-Path $fixtures 'measurement.json'
    $script:ExpansionFile = Join-Path $fixtures 'expansion.json'
    $script:RecordedResults = Join-Path $fixtures 'recorded'
    $script:OutFile = Join-Path ([System.IO.Path]::GetTempPath()) ("crosscheck-selftest-{0}.json" -f [guid]::NewGuid())
    $script:Limit = 0
    $failed = 0
    function Report([bool] $Ok, [string] $Text) {
        if ($Ok) { Write-Output "PASS $Text" } else { Write-Output "FAIL $Text"; $script:failed++ }
    }
    try {
        $r = Invoke-CrossCheck
        $s = @{}; foreach ($i in $r.items) { $s[$i.key] = $i }
        Report ($s['behaviour:test1'].state -eq 'match' -and $null -eq $s['behaviour:test1'].reason -and $s['behaviour:test1'].evidence -eq 'tests/data/test1') 'agree, match: unchanged'
        Report ($s['behaviour:test2'].state -eq 'match' -and $s['behaviour:test2'].reason -eq 'reference-diverges' -and $s['behaviour:test2'].evidence -eq 'was gap; in-process: stdout differs at byte 4') 'agree, gap: match, reference-diverges, the first difference kept in evidence'
        Report ($s['behaviour:test3'].state -eq 'gap' -and $s['behaviour:test3'].evidence -eq 'was match; in-process and out-of-process differ' -and $s['behaviour:test3'].actual -eq 'stdout differs at byte 0') 'differ, match: gap with the in-process and out-of-process evidence'
        Report ($s['behaviour:test4'].state -eq 'gap' -and $s['behaviour:test4'].expected -like 'reference curl exits 7;*' -and $s['behaviour:test4'].actual -eq 'stderr differs at byte 2') 'differ, gap: stays gap with the reference output as expected'
        Report ($s['behaviour:test5'].state -eq 'unmeasured' -and $s['behaviour:test6'].state -eq 'match' -and $s['behaviour:test7'].state -eq 'gap' -and $s['behaviour:test7'].actual -eq 'request differs') 'left-out cases keep their in-process verdict'
        Report ($s['behaviour:test8'].state -eq 'match') 'progress meter lines in stderr are not a difference'
        Report ($s['behaviour:test9'].state -eq 'match' -and $null -eq $s['behaviour:test9'].reason) 'recordings that differ only in their multipart boundary agree'
        $latin1 = [System.Text.Encoding]::GetEncoding(28591)
        $nested = $latin1.GetBytes("Content-Type: text/info; boundary=---outer`r`n`r`n-----outer`r`nContent-Type: multipart/mixed; boundary=---inner`r`n`r`n-----inner--`r`n")
        Report ($latin1.GetString((Set-FixedMultipartBoundary $nested $nested)) -eq "Content-Type: text/info; boundary=<multipart-boundary>`r`n`r`n--<multipart-boundary>`r`nContent-Type: multipart/mixed; boundary=<multipart-boundary>`r`n`r`n--<multipart-boundary>--`r`n") 'every boundary= the request names is fixed, a nested one and a non-multipart Content-Type''s too (test277, test1133)'
        Report ($latin1.GetString((Set-FixedEphemeralPorts $latin1.GetBytes("PROXY TCP4 127.0.0.1 127.0.0.1 55052 18990`r`nlocal port == 32768 65535 32767 65536 123456`r`n"))) -eq "PROXY TCP4 127.0.0.1 127.0.0.1 <ephemeral-port> 18990`r`nlocal port == <ephemeral-port> <ephemeral-port> 32767 65536 123456`r`n") 'numbers 32768 to 65535 are fixed, as a source port; others are kept (test1455, test435)'
        Report ($s['behaviour:test10'].state -eq 'match' -and $null -eq $s['behaviour:test10'].reason) 'recordings that differ only in an elapsed time in stderr agree (test1293)'
        Report ((Set-FixedElapsedTimes "after 16 ms: x`nafter 1001 milliseconds with`nafter 7 seconds`nafter 5 msx`n") -ceq "after <elapsed> ms: x`nafter <elapsed> milliseconds with`nafter 7 seconds`nafter 5 msx`n") 'after <n> ms and after <n> milliseconds lose their number; other numbers are kept'
        $keepAlive = $latin1.GetBytes("HTTP/1.1 401 Authorization Required`r`nContent-Length: 0`r`n`r`n")
        $connectionClose = $latin1.GetBytes("HTTP/1.1 200 OK`r`nContent-Length: 6`r`nconnection:  Close`r`n`r`n-foo-`n")
        $swsclose = $latin1.GetBytes("HTTP/1.0 200 OK swsclose`r`nContent-Length: 6`r`n`r`n-foo-`n")
        $closeInBody = $latin1.GetBytes("HTTP/1.1 200 OK`r`nContent-Length: 18`r`n`r`nConnection: close`n")
        Report ((Test-KeepsConnectionAlive $keepAlive) -and -not (Test-KeepsConnectionAlive $connectionClose) -and -not (Test-KeepsConnectionAlive $swsclose) -and (Test-KeepsConnectionAlive $closeInBody)) 'a connection is kept alive unless the response head says Connection: close or the response holds swsclose (test1075)'
        $x = $r.crossCheck
        $callerDirectory = Join-Path ([System.IO.Path]::GetTempPath()) ("crosscheck-selftest-caller-{0}" -f [guid]::NewGuid())
        $caseDirectory = Join-Path ([System.IO.Path]::GetTempPath()) ("crosscheck-selftest-case-{0}" -f [guid]::NewGuid())
        New-Item -ItemType Directory -Path $callerDirectory, $caseDirectory | Out-Null
        $processDirectory = [System.Environment]::CurrentDirectory
        Push-Location -LiteralPath $callerDirectory
        [System.Environment]::CurrentDirectory = $callerDirectory
        try {
            # Upstream test 1489 runs "-D %": a child that writes the file "%" with no working directory set.
            Invoke-InScratchDirectory $caseDirectory {
                $writer = New-Object System.Diagnostics.ProcessStartInfo
                $writer.FileName = [System.Diagnostics.Process]::GetCurrentProcess().MainModule.FileName
                $hostPrefix = ''
                # pwsh installed as a .NET tool runs as dotnet.exe hosting pwsh.dll.
                if ([System.IO.Path]::GetFileNameWithoutExtension($writer.FileName) -eq 'dotnet') { $hostPrefix = '"' + (Join-Path $PSHOME 'pwsh.dll') + '" ' }
                $writer.Arguments = $hostPrefix + '-NoProfile -NonInteractive -Command "[System.IO.File]::WriteAllText([string][char]37, 1)"'
                $writer.UseShellExecute = $false
                $child = [System.Diagnostics.Process]::Start($writer)
                $child.WaitForExit()
            }
            $restored = ([System.Environment]::CurrentDirectory -eq $callerDirectory) -and ((Get-Location).ProviderPath -eq $callerDirectory)
            Report ((Test-Path -LiteralPath (Join-Path $caseDirectory '%')) -and -not (Test-Path -LiteralPath (Join-Path $callerDirectory '%')) -and @(Get-ChildItem -LiteralPath $callerDirectory -Force).Count -eq 0 -and $restored) 'a case writing the file "%" lands in its scratch folder, never the caller''s directory, and the directories are restored'
        } finally {
            [System.Environment]::CurrentDirectory = $processDirectory
            Pop-Location
            Remove-Item -LiteralPath $callerDirectory, $caseDirectory -Recurse -Force -ErrorAction SilentlyContinue
        }
        Report ($x.crossChecked -eq 7 -and $x.referenceDiverges -eq 1 -and $x.disagreements -eq 2) 'crossChecked 7, referenceDiverges 1, disagreements 2'
        Report ($x.leftOut['not-match-or-gap'] -eq 1 -and $x.leftOut['server-not-http-alone'] -eq 1 -and $x.leftOut['reply-not-one-data'] -eq 1 -and $x.leftOut['not-parsed'] -eq 0 -and $x.leftOut['limit'] -eq 0) 'selection counts each rule'
        Report ($r.counts.match -eq 6 -and $r.counts.gap -eq 3 -and $r.counts.unmeasured -eq 1 -and $r.reasons.'reference-diverges' -eq 1) 'counts and reasons are recomputed'
        $script:Limit = 2
        $l = Invoke-CrossCheck
        Report ($l.crossCheck.crossChecked -eq 2 -and $l.crossCheck.leftOut['limit'] -eq 7) '-Limit caps the cases and counts the rest under limit'
    } finally {
        if (Test-Path -LiteralPath $script:OutFile) { Remove-Item -LiteralPath $script:OutFile -Force }
    }
    if ($failed -gt 0) { exit 1 }
    exit 0
}

if (-not $Measurement -or -not $OutFile -or (-not $TestsData -and -not $ExpansionFile)) { throw 'Give -Measurement, -TestsData and -OutFile, or -SelfTest.' }
$result = Invoke-CrossCheck
$x = $result.crossCheck
Write-Output "cross-checked $($x.crossChecked), reference-diverges $($x.referenceDiverges), disagreements $($x.disagreements)"
foreach ($name in $x.leftOut.Keys) { Write-Output "  left out by $name $($x.leftOut[$name])" }
