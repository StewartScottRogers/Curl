<#
.SYNOPSIS
    Publishes Curl.Console as a native AOT binary and runs a fixed set of heavy transfers through
    it and through real curl, once each under a time limit, and reports where the outcomes differ.

.DESCRIPTION
    The conformance auditor's load check (BL-2132, ADR-0267; it replaces the retired performance
    auditor's Measure-Performance.ps1). Curl does not aim to match real curl's speed, memory or
    binary size - only its behaviour - so this tool times nothing and compares no speed or size.
    It asks one question per scenario: does Curl finish the transfer real curl finishes, with the
    same exit code and the same output bytes? A hang, a run past the time limit (quadratic
    blow-up on a large input shows as one) or a transfer that fails under load is a difference.

    It publishes Curl.Console with `dotnet publish Curl.Console -c Release -o
    <repo>.audit\publish-<stamp>` (native AOT by default; on Windows the Visual Studio Installer
    folder is put on PATH for vswhere when it exists), since the native binary is what ships. A
    failed publish is itself a result: the script says so and exits 1, with no table. -Candidate
    skips the publish and runs the given executable instead.

    Both binaries run the same scenarios against the same canned loopback server,
    Record-CurlExchange.ps1, each run in its own Windows PowerShell from its own folder under
    -OutDirectory. A run still going after -TimeoutSeconds is killed with its process tree and
    its exit code recorded as "timeout". The limit covers the whole recorded run, including the
    recorder decoding its response text (about 1 MiB a second, before the binary starts), so it
    is generous; a binary that finishes in linear time finishes well inside it.

    Scenarios, in this fixed order:
        large-get       -s -o <file> of a 50 MiB Content-Length body
        many-chunks     -s -o <file> of a chunked body of 1,000,000 one-byte chunks
        many-headers    -s -i -o <file> of a response with 2,000 headers and a 2-byte body
        many-redirects  -sL -o <file> through 40 redirects (302, one connection per hop)

    Compared per scenario: the exit code (or "timeout") and the output file's length and
    SHA-256. Each run's stderr.txt and run script stay in its folder for reading; the output
    file is deleted once hashed.

.PARAMETER Curl
    The reference curl. Default on Windows: Git for Windows' curl, found as
    Record-CurlExchange.ps1 finds it: ucrt64\bin\curl.exe first (Git 2.56.0 and later, curl
    8.22.0), then mingw64\bin\curl.exe, beside git.exe and then under Program Files\Git. Off
    Windows: CURL_DIFFERENTIAL_REFERENCE (set by sourcing Set-DifferentialEnvironment.sh, the
    OpenSSL build), else the first curl on PATH.

.PARAMETER Candidate
    The executable under test. Default: Curl.Console of this repository, published first as a
    native AOT binary.

.PARAMETER Scenario
    Run only these scenarios, by name. Default: all four.

.PARAMETER TimeoutSeconds
    The limit for one recorded run. Default 300.

.PARAMETER Runtime
    A runtime identifier for dotnet publish (-r). Default: the SDK's for this machine.

.PARAMETER PublishArguments
    Extra arguments for dotnet publish, as one string split at spaces.

.PARAMETER OutDirectory
    Where load.json, load.md, publish.log and the runs go. Default: a new folder under the temp
    directory.

.OUTPUTS
    load.json: { referenceVersion, candidateVersion, candidateCommit, timeoutSeconds, same,
    different, scenarios: [ { name, curl: { exit, outBytes, outSha256 }, candidate: { ... },
    differences: [ "exit" | "output" ] } ] }; a run that timed out has exit "timeout".
    publish.log: the dotnet publish output, for its trim and AOT warnings (absent with -Candidate).
    load.md: the same as a Markdown table, rows in the scenario order above.
    The script exits 0 when it ran, whatever the outcomes; 1 when the publish failed.

.EXAMPLE
    powershell -NoProfile -File Audit/Tools/Test-TransfersUnderLoad.ps1 -OutDirectory $env:TEMP\load
.EXAMPLE
    powershell -NoProfile -File Audit/Tools/Test-TransfersUnderLoad.ps1 -Candidate Curl.Console\bin\Release\net10.0\curl.exe -Scenario many-chunks
#>
param(
    [string]$Curl,
    [string]$Candidate,
    [string[]]$Scenario = @(),
    [ValidateRange(10, 3600)][int]$TimeoutSeconds = 300,
    [string]$Runtime,
    [string]$PublishArguments = '',
    [string]$OutDirectory
)

$ErrorActionPreference = 'Stop'
# Windows PowerShell 5.1 on Windows, PowerShell 7 elsewhere (BL-3927).
$OnWindows = $PSVersionTable.PSVersion.Major -lt 6 -or $IsWindows
$PowerShellExe = if ($OnWindows) { 'powershell' } else { 'pwsh' }
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$record = Join-Path $repo 'Record-CurlExchange.ps1'
$LargeBytes = 50MB

function Get-ReferenceCurl {
    if ($Curl) { return (Resolve-Path $Curl).Path }
    if (-not $OnWindows) {
        # Off Windows the reference is the OpenSSL build: the one Set-DifferentialEnvironment.sh
        # chose, else the first curl on PATH.
        if ($env:CURL_DIFFERENTIAL_REFERENCE) { return $env:CURL_DIFFERENTIAL_REFERENCE }
        $onPath = Get-Command curl -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($onPath) { return $onPath.Source }
        throw 'The reference curl was not found; source Set-DifferentialEnvironment.sh or pass -Curl.'
    }
    # Git for Windows 2.56.0 moved its curl from mingw64\bin to ucrt64\bin: try ucrt64 first,
    # then mingw64, beside git.exe and then under Program Files.
    $roots = @()
    $git = Get-Command git.exe -ErrorAction SilentlyContinue
    if ($git) { $roots += Split-Path (Split-Path $git.Source -Parent) -Parent }
    $roots += Join-Path $env:ProgramFiles 'Git'
    foreach ($root in $roots) {
        foreach ($folder in 'ucrt64', 'mingw64') {
            $found = Join-Path $root "$folder\bin\curl.exe"
            if (Test-Path -LiteralPath $found) { return $found }
        }
    }
    throw 'The reference curl (Git for Windows ucrt64\bin\curl.exe or mingw64\bin\curl.exe) was not found; pass -Curl.'
}

function Get-Scenarios {
    # Each: Name, CurlArgs (URL marked PORT, output file OUTFILE), Response (a PowerShell
    # expression the run script evaluates into Record-CurlExchange.ps1's escaped response
    # strings, so a large body is built in the run, not on a command line), Connections.
    $ok = "'HTTP/1.1 200 OK\r\n'"
    $hops = (1..40 | ForEach-Object { "'HTTP/1.1 302 Found\r\nLocation: /hop$_\r\nContent-Length: 0\r\nConnection: close\r\n\r\n'" }) -join ', '
    return @(
        [pscustomobject]@{ Name = 'large-get'; CurlArgs = @('-s', '-o', 'OUTFILE', 'http://127.0.0.1:PORT/'); Response = "@($ok + 'Content-Length: $LargeBytes\r\n\r\n' + ('a' * $LargeBytes))"; Connections = 1 }
        [pscustomobject]@{ Name = 'many-chunks'; CurlArgs = @('-s', '-o', 'OUTFILE', 'http://127.0.0.1:PORT/'); Response = "@($ok + 'Transfer-Encoding: chunked\r\n\r\n' + ('1\r\na\r\n' * 1000000) + '0\r\n\r\n')"; Connections = 1 }
        [pscustomobject]@{ Name = 'many-headers'; CurlArgs = @('-s', '-i', '-o', 'OUTFILE', 'http://127.0.0.1:PORT/'); Response = "@($ok + ((1..2000 | ForEach-Object { 'X-Header-' + `$_ + ': value-' + `$_ + '\r\n' }) -join '') + 'Content-Length: 2\r\n\r\nok')"; Connections = 1 }
        [pscustomobject]@{ Name = 'many-redirects'; CurlArgs = @('-sL', '-o', 'OUTFILE', 'http://127.0.0.1:PORT/'); Response = "@($hops, ($ok + 'Content-Length: 2\r\n\r\nok'))"; Connections = 41 }
    )
}

function Get-FreePort {
    $listener = New-Object System.Net.Sockets.TcpListener([System.Net.IPAddress]::Loopback, 0)
    $listener.Start(); $port = $listener.LocalEndpoint.Port; $listener.Stop()
    return $port
}

function ConvertTo-Quoted([string]$Text) { return "'" + $Text.Replace("'", "''") + "'" }

function Invoke-Run($Load, [string]$Exe, [string]$Folder) {
    # One recorded run in its own PowerShell process, killed after -TimeoutSeconds; returns its
    # exit code (or "timeout") and the output file's length and SHA-256.
    New-Item -ItemType Directory -Force $Folder | Out-Null
    $port = Get-FreePort
    # -o gets a full path in the run's folder: curl resolves a relative path against the
    # process's working directory, which is not the run's folder (BL-1055).
    $outFile = Join-Path $Folder 'out.bin'
    $arguments = @($Load.CurlArgs | ForEach-Object { ConvertTo-Quoted ($_.Replace('PORT', "$port").Replace('OUTFILE', $outFile)) }) -join ', '
    $script = Join-Path $Folder 'run.ps1'
    [IO.File]::WriteAllText($script, (@(
                "`$ErrorActionPreference = 'Stop'"
                "`$response = $($Load.Response)"
                "& $(ConvertTo-Quoted $record) -Port $port -Curl $(ConvertTo-Quoted $Exe) -CurlArgs @($arguments) -OutDirectory $(ConvertTo-Quoted $Folder) -Response `$response -Connections $($Load.Connections) 6>`$null | Out-Null"
            ) -join "`r`n") + "`r`n")
    $exitFile = Join-Path $Folder 'exitcode.txt'
    Remove-Item -LiteralPath $exitFile, $outFile -ErrorAction SilentlyContinue
    $p = Start-Process -FilePath $PowerShellExe -ArgumentList @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$script`"") `
        -WorkingDirectory $Folder -NoNewWindow -PassThru -RedirectStandardOutput (Join-Path $Folder 'record.out') -RedirectStandardError (Join-Path $Folder 'record.err')
    $null = $p.Handle
    $exit = if (-not $p.WaitForExit($TimeoutSeconds * 1000)) {
        if ($OnWindows) { & taskkill /PID $p.Id /T /F 2>&1 | Out-Null } else { $p.Kill($true) }
        'timeout'
    } elseif (Test-Path -LiteralPath $exitFile) {
        "$(Get-Content -LiteralPath $exitFile -Raw)".Trim()
    } else {
        'not recorded'
    }
    [IO.File]::WriteAllText($exitFile, $exit)
    $result = [ordered]@{ exit = $exit; outBytes = 0; outSha256 = '' }
    if (Test-Path -LiteralPath $outFile) {
        $result.outBytes = (Get-Item -LiteralPath $outFile).Length
        $sha = [Security.Cryptography.SHA256]::Create()
        $stream = [IO.File]::OpenRead($outFile)
        try { $result.outSha256 = -join ($sha.ComputeHash($stream) | ForEach-Object { $_.ToString('x2') }) } finally { $stream.Dispose(); $sha.Dispose() }
        Remove-Item -LiteralPath $outFile -ErrorAction SilentlyContinue
    }
    return [pscustomobject]$result
}

$reference = Get-ReferenceCurl
$scenarios = @(Get-Scenarios)
if ($Scenario.Count) {
    # powershell -File hands a comma-separated list over as one string; split it back.
    $Scenario = @($Scenario | ForEach-Object { "$_" -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ })
    $unknown = @($Scenario | Where-Object { $scenarios.Name -notcontains $_ })
    if ($unknown.Count) { throw "Unknown scenario $($unknown -join ', '); the scenarios are $($scenarios.Name -join ', ')." }
    $scenarios = @($scenarios | Where-Object { $Scenario -contains $_.Name })
}
if (-not $OutDirectory) { $OutDirectory = Join-Path ([IO.Path]::GetTempPath()) ('load-' + (Get-Date -Format 'yyyyMMdd-HHmmss')) }
New-Item -ItemType Directory -Force $OutDirectory | Out-Null
$OutDirectory = (Resolve-Path $OutDirectory).Path

$publishDir = $null
if ($Candidate) {
    $candidateExe = (Resolve-Path $Candidate).Path
} else {
    # Publish the candidate as a native AOT binary.
    $installer = 'C:\Program Files (x86)\Microsoft Visual Studio\Installer'
    if ((Test-Path -LiteralPath $installer) -and ($env:PATH -notlike "*$installer*")) { $env:PATH = "$installer;$env:PATH" }
    $publishDir = Join-Path "$repo.audit" "publish-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
    $publish = @('publish', (Join-Path $repo 'Curl.Console'), '-c', 'Release', '-o', $publishDir, '-nologo')
    if ($Runtime) { $publish += @('-r', $Runtime) }
    if ($PublishArguments) { $publish += @($PublishArguments -split ' ' | Where-Object { $_ }) }
    Write-Host "publishing Curl.Console: dotnet $($publish -join ' ')"
    $ErrorActionPreference = 'Continue'
    $publishOutput = & dotnet @publish 2>&1 | Out-String
    $publishExit = $LASTEXITCODE
    # Kept for the conformance auditor, which reads it for trim and AOT warnings (IL2xxx, IL3xxx).
    [IO.File]::WriteAllText((Join-Path $OutDirectory 'publish.log'), $publishOutput)
    $ErrorActionPreference = 'Stop'
    $candidateExe = Join-Path $publishDir $(if ($OnWindows) { 'curl.exe' } else { 'curl' })
    if ($publishExit -ne 0 -or -not (Test-Path -LiteralPath $candidateExe)) {
        $why = @($publishOutput -split "`n" | Where-Object { $_ -match 'error' } | Select-Object -First 3) -join ' | '
        Write-Host "Publishing Curl.Console failed (exit $publishExit): $why" -ForegroundColor Red
        Write-Host 'No transfers were run.'
        exit 1
    }
}

$referenceVersion = "$((& $reference --version | Select-Object -First 1))".Trim()
$candidateVersion = "$((& $candidateExe --version | Select-Object -First 1))".Trim()
$commit = "$(git -C $repo rev-parse HEAD 2>$null)".Trim()
Write-Host "reference: $referenceVersion"
Write-Host "candidate: $candidateVersion ($candidateExe, commit $commit)"

$results = @()
foreach ($load in $scenarios) {
    $curlRun = Invoke-Run $load $reference (Join-Path $OutDirectory "runs\$($load.Name)\curl")
    $candidateRun = Invoke-Run $load $candidateExe (Join-Path $OutDirectory "runs\$($load.Name)\candidate")
    $differences = @()
    if ($curlRun.exit -ne $candidateRun.exit) { $differences += 'exit' }
    if ($curlRun.outBytes -ne $candidateRun.outBytes -or $curlRun.outSha256 -ne $candidateRun.outSha256) { $differences += 'output' }
    $results += [pscustomobject][ordered]@{ name = $load.Name; curl = $curlRun; candidate = $candidateRun; differences = $differences }
    Write-Host ("{0,-15} curl exit {1,-8} candidate exit {2,-8} {3}" -f $load.Name, $curlRun.exit, $candidateRun.exit, $(if ($differences.Count) { 'differs: ' + ($differences -join ', ') } else { 'same' }))
}

$same = @($results | Where-Object { -not $_.differences.Count }).Count
$summary = [ordered]@{
    referenceVersion = $referenceVersion; candidateVersion = $candidateVersion; candidateCommit = $commit
    timeoutSeconds = $TimeoutSeconds; same = $same; different = $results.Count - $same; scenarios = $results
}
[IO.File]::WriteAllText((Join-Path $OutDirectory 'load.json'), ([pscustomobject]$summary | ConvertTo-Json -Depth 6))

$lines = @(
    '| Scenario | curl exit | curl output bytes | Curl exit | Curl output bytes | Differences |'
    '| --- | --- | ---: | --- | ---: | --- |'
)
foreach ($r in $results) {
    $lines += '| {0} | {1} | {2} | {3} | {4} | {5} |' -f $r.name, $r.curl.exit, $r.curl.outBytes, $r.candidate.exit, $r.candidate.outBytes, $(if ($r.differences.Count) { $r.differences -join ', ' } else { 'none' })
}
$lines += ''
$lines += "Reference: $referenceVersion. Time limit per run: $TimeoutSeconds s."
[IO.File]::WriteAllText((Join-Path $OutDirectory 'load.md'), ($lines -join "`r`n") + "`r`n")
if ($publishDir) { Remove-Item -Recurse -Force -LiteralPath $publishDir -ErrorAction SilentlyContinue }
Write-Host "wrote $OutDirectory\load.json and load.md"
