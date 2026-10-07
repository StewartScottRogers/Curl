<#
.SYNOPSIS
    Runs generated command lines through real curl and through Curl.Console against the same
    canned loopback exchange, and reports every case where they differ.

.DESCRIPTION
    Differential conformance for the conformance auditor (BL-1012, ADR-0267). It builds on
    Record-CurlExchange.ps1, which runs the loopback server and records request.bin,
    stdout.bin, stderr.txt and exitcode.txt for any executable given as -Curl.

    Generator. The option grammar comes from the reference curl's own `curl --help all`: each
    option's long name, short alias and argument placeholder. Each case picks 1 to 4 options
    with System.Random(-Seed), gives each that takes an argument a value from a small pool
    chosen by its placeholder (numbers including 0, -1 and a huge one; headers; the empty
    string; relative file names; @file and @- forms; write-out formats), and ends with the URL
    http://127.0.0.1:<port>/. The same -Seed, -Count and reference curl always give the same
    argument lists. Options that could reach a host other than 127.0.0.1, prompt, read the
    user's own files, or print wall-clock time are excluded; $Excluded below lists each with
    its reason. File values are relative names, and each run's working directory is its own
    case folder, so every file an option reads or writes stays in -OutDirectory.

    Each case runs Record-CurlExchange.ps1 twice, on the same port with the same response,
    once with the reference as -Curl and once with the candidate, into case-<n>/curl and
    case-<n>/candidate, through a generated run script with a -TimeoutSeconds limit.

    Comparison: byte for byte on request.bin, stdout.bin, stderr.txt and exitcode.txt, after
    these normalisations, which undo only what differs between two runs of the same binary:
      program name     "<executable name>:" at a line start in stderr becomes "curl:"
      port             the loopback port number becomes PORT
      Date headers     a "Date: ..." line's value becomes DATE
      boundary         a multipart boundary (20 or more dashes and 16 or more alphanumerics)
                       becomes BOUNDARY; curl randomises it per run
      source port      the first port of a --haproxy-protocol PROXY line, and the port after
                       "from <address> port" in -v's "* Established connection" line, curl's
                       own ephemeral port, become SOURCEPORT
      elapsed time     "after <n> ms" and "after <n> milliseconds" in error and verbose text
                       become "after N ms" and "after N milliseconds"
      progress meter   progress-meter header and row lines in stderr are dropped, and a meter
                       row run into the start of a message line is cut off it; curl prints
                       the meter when stdout is not a terminal, with that run's timings
    The report lists the normalisations. State the reference curl's version with every
    comparison: summary.json carries `curl --version`'s first line.

.PARAMETER Count
    How many cases to generate. Default 100.

.PARAMETER Seed
    The generator's seed. Default 0.

.PARAMETER Curl
    The reference curl. Default: Record-CurlExchange.ps1's (curl 8.21.0 from Git for Windows).

.PARAMETER Candidate
    The executable under test. Default: Curl.Console of this repository, built first with
    `dotnet build Curl.Console -c Release`.

.PARAMETER Options
    Only generate cases from these long option names (without the leading dashes).

.PARAMETER OutDirectory
    Where the cases and summary.json go. Default: a new folder under the temp directory.

.PARAMETER TimeoutSeconds
    The limit for one recorded run. Default 60. A run over it is killed and its exit code
    recorded as "timeout".

.PARAMETER ListOnly
    Print the generated argument lists, one case per line, and exit without running anything.

.OUTPUTS
    <OutDirectory>/summary.json: { referenceVersion, candidateCommit, candidate, seed, count,
    same, different, normalisations, cases: [ { n, args, differences: [ "exitcode" | "stdout"
    | "stderr" | "request" ] } ] }, and for each differing case, case-<n>/repro.ps1 holding the
    two exact Record-CurlExchange.ps1 commands.

.EXAMPLE
    powershell -NoProfile -File Audit/Tools/Invoke-DifferentialConformance.ps1 -Count 10 -Seed 1 -OutDirectory $env:TEMP\diff
#>
param(
    [int]$Count = 100,
    [int]$Seed = 0,
    [string]$Curl,
    [string]$Candidate,
    [string[]]$Options = @(),
    [string]$OutDirectory,
    [int]$TimeoutSeconds = 60,
    [switch]$ListOnly
)

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$record = Join-Path $repo 'Record-CurlExchange.ps1'

# Excluded long options, each with why. A case that could reach a host other than 127.0.0.1,
# wait for a person, read the user's own files, or print wall-clock time is never generated.
$Excluded = [ordered]@{
    'proxy'                 = 'sends the request through another host'
    'preproxy'              = 'sends the request through another host'
    'socks4'                = 'sends the request through a SOCKS host'
    'socks4a'               = 'sends the request through a SOCKS host'
    'socks5'                = 'sends the request through a SOCKS host'
    'socks5-hostname'       = 'sends the request through a SOCKS host'
    'resolve'               = 'maps host names to other addresses'
    'connect-to'            = 'connects to another host and port'
    'doh-url'               = 'resolves names through a DNS-over-HTTPS server'
    'dns-servers'           = 'resolves names through other DNS servers'
    'dns-interface'         = 'sends DNS queries out of a named interface'
    'dns-ipv4-addr'         = 'sends DNS queries from another address'
    'dns-ipv6-addr'         = 'sends DNS queries from another address'
    'ipfs-gateway'          = 'rewrites ipfs:// URLs to another host'
    'url'                   = 'adds a URL of the generator''s choosing'
    'unix-socket'           = 'connects to a Unix socket instead of 127.0.0.1'
    'abstract-unix-socket'  = 'connects to an abstract Unix socket instead of 127.0.0.1'
    'ech'                   = 'looks up ECH configs in DNS'
    'config'                = 'reads a config file, which can hold any option and URL'
    'netrc'                 = 'reads the user''s own .netrc'
    'netrc-optional'        = 'reads the user''s own .netrc'
    'netrc-file'            = 'reads a credentials file'
    'user'                  = 'prompts for a password when none is given'
    'help'                  = 'prints the help text, compared separately'
    'manual'                = 'prints the manual, compared separately'
    'version'               = 'prints the build''s feature list, compared separately'
    'trace-time'            = 'prints wall-clock time'
    'progress-bar'          = 'draws a bar whose updates depend on timing'
}

$Normalisations = @(
    'program name at a line start in stderr -> curl:',
    'loopback port -> PORT',
    'Date header value -> DATE',
    'multipart boundary -> BOUNDARY',
    'progress-meter header and row lines in stderr dropped; a meter row run into a message line cut off it',
    'the source port in a --haproxy-protocol PROXY line -> SOURCEPORT',
    'the source port in -v''s "* Established connection ... from <address> port N" -> SOURCEPORT',
    'elapsed "after N ms" / "after N milliseconds" -> after N ms / after N milliseconds'
)

function Get-ReferenceCurl {
    if ($Curl) { return (Resolve-Path $Curl).Path }
    $git = Get-Command git.exe -ErrorAction SilentlyContinue
    if ($git) {
        $candidate = Join-Path (Split-Path (Split-Path $git.Source -Parent) -Parent) 'mingw64\bin\curl.exe'
        if (Test-Path -LiteralPath $candidate) { return $candidate }
    }
    $candidate = Join-Path $env:ProgramFiles 'Git\mingw64\bin\curl.exe'
    if (Test-Path -LiteralPath $candidate) { return $candidate }
    throw 'The reference curl (Git for Windows mingw64\bin\curl.exe) was not found; pass -Curl.'
}

function Get-OptionGrammar([string]$Exe) {
    # One entry per option of `curl --help all`: Long, Short and Placeholder ('' for a flag).
    $grammar = @()
    foreach ($line in (& $Exe --help all)) {
        if ($line -match '^\s*(?:-(\S),\s+)?--([A-Za-z0-9][A-Za-z0-9-]*)(?:\s+(<[^>]+>|\[[^\]]+\]))?\s') {
            $grammar += [pscustomobject]@{ Long = $Matches[2]; Short = $Matches[1]; Placeholder = "$($Matches[3])" }
        }
    }
    return $grammar
}

function Test-ExcludedOption($Option) {
    if ($Excluded.Contains($Option.Long)) { return $true }
    # Every proxy-* option configures a proxy host or its credentials.
    if ($Option.Long -like 'proxy*') { return $true }
    return $false
}

function Get-ValuePool([string]$Placeholder) {
    $p = $Placeholder.ToLowerInvariant()
    if ($p -match 'header') { return @('X-Fuzz: 1', 'Accept:', '', '@hdr.txt') }
    if ($p -match 'format') { return @('%{http_code}\n', '%{url}', '%{size_download} %{content_type}', '', '@fmt.txt') }
    if ($p -match 'file|path|dir') { return @('f.txt', 'sub/f.txt', '', 'missing.txt') }
    if ($p -match 'name=content|name=') { return @('a=b', 'f=@f.txt', 'a=<data.txt', '') }
    if ($p -match 'data|query') { return @('a=1&b=2', '', '@data.txt', '@-') }
    if ($p -match 'num|seconds|ms|milli|integer|speed|size|bytes|count|retries|max|offset|time|port|level|value|limit') {
        return @('0', '-1', '1', '10', '99999999999999999999', '3.5')
    }
    if ($p -match 'range') { return @('0-99', '-5', '10-', 'x') }
    if ($p -match 'method|command') { return @('GET', 'POST', 'X', '') }
    return @('', 'x', '1', '-', '@f.txt')
}

function New-Cases([object[]]$Grammar, [int]$HowMany, [int]$RandomSeed, [string]$Url) {
    $pool = @($Grammar | Where-Object { -not (Test-ExcludedOption $_) })
    if ($Options.Count) { $pool = @($pool | Where-Object { $Options -contains $_.Long }) }
    if (-not $pool.Count) { throw 'No option is left to generate cases from.' }
    $random = New-Object System.Random $RandomSeed
    $cases = @()
    for ($n = 1; $n -le $HowMany; $n++) {
        $arguments = @()
        $picks = 1 + $random.Next(4)
        for ($k = 0; $k -lt $picks; $k++) {
            $option = $pool[$random.Next($pool.Count)]
            $useShort = $option.Short -and $random.Next(2) -eq 0
            $arguments += $(if ($useShort) { "-$($option.Short)" } else { "--$($option.Long)" })
            if ($option.Placeholder) {
                $values = @(Get-ValuePool $option.Placeholder)
                $arguments += $values[$random.Next($values.Count)]
            }
        }
        $arguments += $Url
        $cases += , @($arguments)
    }
    return $cases
}

function ConvertTo-Quoted([string]$Text) { return "'" + $Text.Replace("'", "''") + "'" }

function Get-RecordCommand([string[]]$Arguments, [int]$Port, [string]$Exe, [string]$Out) {
    $list = ($Arguments | ForEach-Object { ConvertTo-Quoted $_ }) -join ', '
    return "& $(ConvertTo-Quoted $record) -Port $Port -Curl $(ConvertTo-Quoted $Exe) -OutDirectory $(ConvertTo-Quoted $Out) -CurlArgs @($list)"
}

function New-RunFolder([string]$Folder) {
    # The files the value pools name, so @file and file values find something.
    New-Item -ItemType Directory -Force (Join-Path $Folder 'sub') | Out-Null
    foreach ($pair in @(@('hdr.txt', "X-From-File: 1`n"), @('data.txt', "from=file`n"), @('f.txt', "file body`n"), @('sub/f.txt', "sub body`n"), @('fmt.txt', "%{http_code}`n"))) {
        [IO.File]::WriteAllText((Join-Path $Folder $pair[0]), $pair[1])
    }
}

function Invoke-Recorded([string]$Command, [string]$Folder, [int]$Seconds) {
    # Runs one Record-CurlExchange.ps1 command from $Folder in its own Windows PowerShell.
    New-RunFolder $Folder
    $script = Join-Path $Folder 'run.ps1'
    [IO.File]::WriteAllText($script, "Set-Location -LiteralPath $(ConvertTo-Quoted $Folder)`r`n$Command`r`n")
    $p = Start-Process -FilePath 'powershell' -ArgumentList @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$script`"") `
        -WorkingDirectory $Folder -NoNewWindow -PassThru -RedirectStandardOutput (Join-Path $Folder 'record.out') -RedirectStandardError (Join-Path $Folder 'record.err')
    $null = $p.Handle
    if (-not $p.WaitForExit($Seconds * 1000)) {
        & taskkill /PID $p.Id /T /F 2>&1 | Out-Null
        [IO.File]::WriteAllText((Join-Path $Folder 'exitcode.txt'), 'timeout')
    }
}

function Get-Normalised([string]$Path, [string]$Kind, [int]$Port, [string]$ProgramName) {
    if (-not (Test-Path -LiteralPath $Path)) { return '<missing>' }
    $latin1 = [Text.Encoding]::GetEncoding(28591)
    $text = $latin1.GetString([IO.File]::ReadAllBytes($Path))
    # --haproxy-protocol's PROXY line names curl's own ephemeral source port first.
    $text = [regex]::Replace($text, '(?m)^(PROXY TCP[46] \S+ \S+ )\d+( \d+\r?)$', '${1}SOURCEPORT${2}')
    # -v's "* Established connection to ... from 127.0.0.1 port N" names the same ephemeral
    # source port. Masked before the server port, which could be a substring of it.
    $text = [regex]::Replace($text, '(?m)^(\* Established connection .* from \S+ port )\d+', '${1}SOURCEPORT')
    $text = $text.Replace("$Port", 'PORT')
    # Elapsed time in error and verbose text: "Failed to connect to ... after N ms",
    # "timed out after N milliseconds".
    $text = [regex]::Replace($text, '\bafter \d+ ms\b', 'after N ms')
    $text = [regex]::Replace($text, '\bafter \d+ milliseconds\b', 'after N milliseconds')
    $text = [regex]::Replace($text, '(?im)^(Date:\s*)[^\r\n]*', '${1}DATE')
    $text = [regex]::Replace($text, '-{20,}[0-9A-Za-z]{16,}', 'BOUNDARY')
    if ($Kind -eq 'stderr') {
        if ($ProgramName -and $ProgramName -ne 'curl') { $text = [regex]::Replace($text, "(?m)^$([regex]::Escape($ProgramName)):", 'curl:') }
        $kept = foreach ($line in ($text -split "\r\n|\r|\n")) {
            if ($line -match '^\s*%\s+Total\s' -or $line -match '^\s*Dload\s+Upload' -or $line -match '^\s*DL%\s+UL%') { continue }
            # A meter row with no line end before a message ("... --:--:--     0Warning: ...")
            # lands wherever that run's timing put it; keep only the message.
            $line = [regex]::Replace($line, '^[\s\d.:kMGTP-]*(?:--:--:--|\d+:\d\d:\d\d)[\s\d.:kMGTP-]*?(?=[A-Za-z*])', '')
            # A meter row is only numbers, units, dashes and colons; curl's own messages
            # ("curl: (N) ...", "* ...") always hold letters.
            if ($line -match '^[\s\d.:kMGTP-]+$' -and $line -match '\d') { continue }
            $line
        }
        $text = ($kept -join "`n")
    }
    return $text
}

$reference = Get-ReferenceCurl
$referenceVersion = "$((& $reference --version | Select-Object -First 1))".Trim()
$port = & {
    $listener = New-Object System.Net.Sockets.TcpListener([System.Net.IPAddress]::Loopback, 0)
    $listener.Start(); $p = $listener.LocalEndpoint.Port; $listener.Stop(); $p
}
# The same seed must give the same argument lists whatever port is free, so cases are
# generated with a placeholder port and the real one is filled in afterwards.
$grammar = @(Get-OptionGrammar $reference)
$cases = @(New-Cases $grammar $Count $Seed 'http://127.0.0.1:PORT/')

if ($ListOnly) {
    $n = 0
    foreach ($case in $cases) { $n++; Write-Output ("{0}: {1}" -f $n, ($case -join ' ')) }
    exit 0
}

if (-not $Candidate) {
    Write-Host 'building Curl.Console (Release) as the candidate ...'
    $ErrorActionPreference = 'Continue'
    & dotnet build (Join-Path $repo 'Curl.Console') -c Release -nologo -v q 2>&1 | Out-Null
    $ErrorActionPreference = 'Stop'
    if ($LASTEXITCODE -ne 0) { throw 'dotnet build Curl.Console -c Release failed.' }
    $Candidate = @(Get-ChildItem (Join-Path $repo 'Curl.Console\bin\Release') -Recurse -Filter 'curl.exe' | Sort-Object LastWriteTime -Descending)[0].FullName
}
$Candidate = (Resolve-Path $Candidate).Path
$candidateName = [IO.Path]::GetFileNameWithoutExtension($Candidate)
$candidateCommit = "$(git -C $repo rev-parse HEAD 2>$null)".Trim()
if (-not $OutDirectory) { $OutDirectory = Join-Path ([IO.Path]::GetTempPath()) ('differential-' + (Get-Date -Format 'yyyyMMdd-HHmmss')) }
New-Item -ItemType Directory -Force $OutDirectory | Out-Null
$OutDirectory = (Resolve-Path $OutDirectory).Path

Write-Host "reference: $referenceVersion"
Write-Host "candidate: $Candidate at $candidateCommit; $($cases.Count) cases on port $port"
$results = @()
$same = 0
$n = 0
foreach ($case in $cases) {
    $n++
    $arguments = @($case | ForEach-Object { $_.Replace('127.0.0.1:PORT', "127.0.0.1:$port") })
    $caseDir = Join-Path $OutDirectory "case-$n"
    $curlDir = Join-Path $caseDir 'curl'
    $candidateDir = Join-Path $caseDir 'candidate'
    $curlCommand = Get-RecordCommand $arguments $port $reference $curlDir
    $candidateCommand = Get-RecordCommand $arguments $port $Candidate $candidateDir
    Invoke-Recorded $curlCommand $curlDir $TimeoutSeconds
    Invoke-Recorded $candidateCommand $candidateDir $TimeoutSeconds

    $differences = @()
    foreach ($pair in @(@('exitcode', 'exitcode.txt'), @('stdout', 'stdout.bin'), @('stderr', 'stderr.txt'), @('request', 'request.bin'))) {
        $a = Get-Normalised (Join-Path $curlDir $pair[1]) $pair[0] $port 'curl'
        $b = Get-Normalised (Join-Path $candidateDir $pair[1]) $pair[0] $port $candidateName
        if ($a -cne $b) { $differences += $pair[0] }
    }
    if ($differences.Count) {
        [IO.File]::WriteAllText((Join-Path $caseDir 'repro.ps1'),
            "# Case $n, seed ${Seed}: $($arguments -join ' ')`r`n# Differs in: $($differences -join ', ')`r`n$curlCommand`r`n$candidateCommand`r`n")
    } else { $same++ }
    $results += [ordered]@{ n = $n; args = $arguments; differences = $differences }
    Write-Host ("case {0,3}: {1,-24} {2}" -f $n, $(if ($differences.Count) { $differences -join ',' } else { 'same' }), ($arguments -join ' '))
}

$summary = [ordered]@{
    referenceVersion = $referenceVersion; candidateCommit = $candidateCommit; candidate = $Candidate
    seed = $Seed; count = $cases.Count; same = $same; different = $cases.Count - $same
    normalisations = $Normalisations; cases = $results
}
$json = [pscustomobject]$summary | ConvertTo-Json -Depth 6
[IO.File]::WriteAllText((Join-Path $OutDirectory 'summary.json'), $json)
Write-Host "same $same, different $($cases.Count - $same); $OutDirectory\summary.json"
