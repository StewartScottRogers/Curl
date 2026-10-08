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

    Per case, both binaries run through Record-CurlExchange.ps1 with the case's <data> as
    -Response, its <stdin> as -StandardInput, and its client files written fresh into its
    %LOGDIR before each run. The binaries' exit codes, request bytes, stdout and stderr are
    compared, in that order; the first that differs is the difference. Before comparing,
    stderr loses its progress meter lines (the "% Total" and "Dload" header lines and the
    lines of digits, colons and dashes), whose times differ from run to run. No <strip>
    rule of the harness is applied: the reference's own output is the yardstick.

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
    redirect.

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
    and the selection rule's counts.

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

# Bytes with every occurrence of the request's random multipart boundary, read from its
# Content-Type: multipart/...; boundary= header, replaced by a fixed token, as upstream's
# <strip> rules do; each run of curl picks its own boundary.
function Set-FixedMultipartBoundary([byte[]] $Bytes, [byte[]] $Request) {
    $latin1 = [System.Text.Encoding]::GetEncoding(28591)
    $header = [regex]::Match($latin1.GetString($Request), '(?im)^Content-Type:[ \t]*multipart/[^\r\n]*?;[ \t]*boundary=("?)([^";\r\n]+)\1')
    if (-not $header.Success) { return ,$Bytes }
    $text = $latin1.GetString($Bytes).Replace($header.Groups[2].Value, '<multipart-boundary>')
    return ,([byte[]]$latin1.GetBytes($text))
}

function Read-Recording([string] $Folder) {
    $exit = ''
    if (Test-Path -LiteralPath (Join-Path $Folder 'exitcode.txt')) { $exit = ([System.IO.File]::ReadAllText((Join-Path $Folder 'exitcode.txt'))).Trim() }
    $request = Read-Bytes (Join-Path $Folder 'request.bin')
    return [pscustomobject]@{
        ExitCode = $exit
        Request = Set-FixedMultipartBoundary $request $request
        Stdout = Set-FixedMultipartBoundary (Read-Bytes (Join-Path $Folder 'stdout.bin')) $request
        Stderr = Remove-ProgressMeter (Read-Bytes (Join-Path $Folder 'stderr.txt'))
    }
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
    $response = ConvertTo-EscapedBytes ([Convert]::FromBase64String($Case.response))
    $stdin = ConvertTo-EscapedBytes ([Convert]::FromBase64String($Case.standardInput))
    Push-Location $logDirectory
    try { & $recorder -Port $Port -Response $response -Connections 20 -CurlArgs $arguments -OutDirectory $Out -Curl $Curl -StandardInput $stdin | Out-Null }
    finally { Pop-Location }
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
        $x = $r.crossCheck
        Report ($x.crossChecked -eq 6 -and $x.referenceDiverges -eq 1 -and $x.disagreements -eq 2) 'crossChecked 6, referenceDiverges 1, disagreements 2'
        Report ($x.leftOut['not-match-or-gap'] -eq 1 -and $x.leftOut['server-not-http-alone'] -eq 1 -and $x.leftOut['reply-not-one-data'] -eq 1 -and $x.leftOut['not-parsed'] -eq 0 -and $x.leftOut['limit'] -eq 0) 'selection counts each rule'
        Report ($r.counts.match -eq 5 -and $r.counts.gap -eq 3 -and $r.counts.unmeasured -eq 1 -and $r.reasons.'reference-diverges' -eq 1) 'counts and reasons are recomputed'
        $script:Limit = 2
        $l = Invoke-CrossCheck
        Report ($l.crossCheck.crossChecked -eq 2 -and $l.crossCheck.leftOut['limit'] -eq 6) '-Limit caps the cases and counts the rest under limit'
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
