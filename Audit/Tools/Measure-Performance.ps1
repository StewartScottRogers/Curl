<#
.SYNOPSIS
    Publishes Curl.Console as a native AOT binary and times a fixed set of transfers through it
    and through real curl, the same number of times each.

.DESCRIPTION
    The performance auditor's measurement (BL-1011, ADR-0267). It publishes Curl.Console with
    `dotnet publish Curl.Console -c Release -o <repo>.audit\publish-<stamp>` (native AOT by
    default; on Windows the Visual Studio Installer folder is put on PATH for vswhere when it
    exists). A failed publish is itself a result: the script says so and exits 1, with no
    table.

    Both binaries are measured by the same code against the same canned loopback server:
    Record-CurlExchange.ps1, called in this process, writes timing.json with the run's wall
    time ("elapsedMilliseconds") and sampled peak working set ("peakWorkingSetBytes"). Each
    scenario runs 2 discarded warm-up runs per binary, then -Iterations measured runs per
    binary, alternating the two and swapping which goes first on every iteration, so machine
    noise falls on both alike. Every run is logged to runs.log beside the JSON.

    Scenarios, in this fixed order (changing the list starts a new scorecard series):
        startup          --version, no server
        small-get        -s -o <file> of a 1 KiB Content-Length body
        large-get        the same with a 50 MiB body
        headers-verbose  -sv -o <file> of a response with 50 headers and no body
        chunked          -s -o <file> of a chunked response of 1000 one-byte chunks
        redirects        -sL -o <file> through 5 redirects (302, one connection per hop)

    Record-CurlExchange.ps1 decodes its response text in PowerShell at about 1 MiB a second,
    before curl starts, so large-get's 50 MiB body adds that to every run's wall clock but not
    to the time it measures.

    Two readings to interpret with care. A run shorter than the recorder's 10 ms memory sample
    (curl's --version) reports 0 bytes peak. And the reference curl.exe is small because
    libcurl is a separate DLL beside it, while Curl's native binary is self-contained, so the
    binary sizes compare an executable with a whole program.

.PARAMETER Iterations
    Measured runs per scenario per binary. Default 20.

.PARAMETER Curl
    The reference curl. Default: curl 8.21.0 from Git for Windows (mingw64), as
    Record-CurlExchange.ps1 finds it.

.PARAMETER Runtime
    A runtime identifier for dotnet publish (-r). Default: the SDK's for this machine.

.PARAMETER PublishArguments
    Extra arguments for dotnet publish, as one string split at spaces.

.PARAMETER OutDirectory
    Where performance.json, performance.md, runs.log and the runs go. Default: a new folder
    under the temp directory.

.OUTPUTS
    performance.json: { referenceVersion, candidateVersion, candidateCommit, processorCount,
    iterations, referenceBinaryBytes, candidateBinaryBytes, largeGetBytes, scenarios: [ {
    name, curl: { medianMs, p90Ms, medianPeakWorkingSetBytes }, candidate: { ... } } ] }.
    publish.log: the dotnet publish output, for its trim and AOT warnings.
    performance.md: the same as a Markdown table, rows in the scenario order above, which the
    scorecard (BL-1017) copies.

.EXAMPLE
    powershell -NoProfile -File Audit/Tools/Measure-Performance.ps1 -Iterations 3 -OutDirectory $env:TEMP\perf
#>
param(
    [ValidateRange(1, 1000)][int]$Iterations = 20,
    [string]$Curl,
    [string]$Runtime,
    [string]$PublishArguments = '',
    [string]$OutDirectory
)

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$record = Join-Path $repo 'Record-CurlExchange.ps1'
$WarmUps = 2
$LargeBytes = 50MB

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

function Get-Scenarios {
    # Each: Name, CurlArgs (URL marked PORT, output file OUTFILE), Response (Record-CurlExchange escapes), Connections, NoServer.
    $ok = 'HTTP/1.1 200 OK\r\n'
    $headers = (1..50 | ForEach-Object { "X-Header-${_}: value-$_\r\n" }) -join ''
    $chunks = ('1\r\na\r\n' * 1000) + '0\r\n\r\n'
    $hops = @(1..5 | ForEach-Object { "HTTP/1.1 302 Found\r\nLocation: /hop$_\r\nContent-Length: 0\r\n\r\n" })
    return @(
        [pscustomobject]@{ Name = 'startup'; CurlArgs = @('--version'); Response = @(); Connections = 1; NoServer = $true }
        [pscustomobject]@{ Name = 'small-get'; CurlArgs = @('-s', '-o', 'OUTFILE', 'http://127.0.0.1:PORT/'); Response = @("${ok}Content-Length: 1024\r\n\r\n" + ('a' * 1024)); Connections = 1; NoServer = $false }
        [pscustomobject]@{ Name = 'large-get'; CurlArgs = @('-s', '-o', 'OUTFILE', 'http://127.0.0.1:PORT/'); Response = @("${ok}Content-Length: $LargeBytes\r\n\r\n" + ('a' * $LargeBytes)); Connections = 1; NoServer = $false }
        [pscustomobject]@{ Name = 'headers-verbose'; CurlArgs = @('-sv', '-o', 'OUTFILE', 'http://127.0.0.1:PORT/'); Response = @("${ok}${headers}Content-Length: 0\r\n\r\n"); Connections = 1; NoServer = $false }
        [pscustomobject]@{ Name = 'chunked'; CurlArgs = @('-s', '-o', 'OUTFILE', 'http://127.0.0.1:PORT/'); Response = @("${ok}Transfer-Encoding: chunked\r\n\r\n$chunks"); Connections = 1; NoServer = $false }
        [pscustomobject]@{ Name = 'redirects'; CurlArgs = @('-sL', '-o', 'OUTFILE', 'http://127.0.0.1:PORT/'); Response = $hops + @("${ok}Content-Length: 2\r\n\r\nok"); Connections = 6; NoServer = $false }
    )
}

function Get-FreePort {
    $listener = New-Object System.Net.Sockets.TcpListener([System.Net.IPAddress]::Loopback, 0)
    $listener.Start(); $port = $listener.LocalEndpoint.Port; $listener.Stop()
    return $port
}

function Invoke-Run($Scenario, [string]$Exe, [string]$Folder) {
    # One recorded run, from its own folder; returns wall ms and peak working set bytes.
    New-Item -ItemType Directory -Force $Folder | Out-Null
    $port = Get-FreePort
    # -o gets a full path in the run's folder: curl resolves a relative path against the
    # process's working directory, which is not the run's folder (BL-1055).
    $outFile = Join-Path $Folder 'out.bin'
    $arguments = @($Scenario.CurlArgs | ForEach-Object { $_.Replace('PORT', "$port").Replace('OUTFILE', $outFile) })
    # 6>$null: the recorder's own "curl exited ..." line per run is noise here; runs.log has it.
    if ($Scenario.NoServer) {
        & $record -NoServer -Curl $Exe -CurlArgs $arguments -OutDirectory $Folder 6>$null | Out-Null
    } else {
        & $record -Port $port -Curl $Exe -CurlArgs $arguments -OutDirectory $Folder -Response $Scenario.Response -Connections $Scenario.Connections 6>$null | Out-Null
    }
    $timing = Get-Content -LiteralPath (Join-Path $Folder 'timing.json') -Raw | ConvertFrom-Json
    $exit = "$(Get-Content -LiteralPath (Join-Path $Folder 'exitcode.txt') -Raw)".Trim()
    # A large body is only written to out.bin; drop it once measured.
    Remove-Item -LiteralPath $outFile -ErrorAction SilentlyContinue
    return [pscustomobject]@{ Ms = [double]$timing.elapsedMilliseconds; Bytes = [long]$timing.peakWorkingSetBytes; Exit = $exit }
}

function Get-Percentile([double[]]$Values, [double]$Fraction) {
    # Nearest rank: the smallest value with at least Fraction of the values at or below it.
    $sorted = [double[]]@($Values | Sort-Object)
    $rank = [math]::Ceiling($Fraction * $sorted.Length)
    return $sorted[[math]::Max(0, $rank - 1)]
}

function Get-Median([double[]]$Values) {
    $sorted = [double[]]@($Values | Sort-Object)
    $mid = [int][math]::Floor($sorted.Length / 2)
    if ($sorted.Length % 2) { return $sorted[$mid] }
    return ($sorted[$mid - 1] + $sorted[$mid]) / 2
}

$reference = Get-ReferenceCurl
if (-not $OutDirectory) { $OutDirectory = Join-Path ([IO.Path]::GetTempPath()) ('performance-' + (Get-Date -Format 'yyyyMMdd-HHmmss')) }
New-Item -ItemType Directory -Force $OutDirectory | Out-Null
$OutDirectory = (Resolve-Path $OutDirectory).Path

# Publish the candidate as a native AOT binary.
$installer = 'C:\Program Files (x86)\Microsoft Visual Studio\Installer'
if ((Test-Path -LiteralPath $installer) -and ($env:PATH -notlike "*$installer*")) { $env:PATH = "$installer;$env:PATH" }
$publishDir = "$repo.audit\publish-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
$publish = @('publish', (Join-Path $repo 'Curl.Console'), '-c', 'Release', '-o', $publishDir, '-nologo')
if ($Runtime) { $publish += @('-r', $Runtime) }
if ($PublishArguments) { $publish += @($PublishArguments -split ' ' | Where-Object { $_ }) }
Write-Host "publishing Curl.Console: dotnet $($publish -join ' ')"
$ErrorActionPreference = 'Continue'
$publishOutput = & dotnet @publish 2>&1 | Out-String
$publishExit = $LASTEXITCODE
# Kept for the performance auditor, which reads it for trim and AOT warnings (IL2xxx, IL3xxx).
[IO.File]::WriteAllText((Join-Path $OutDirectory 'publish.log'), $publishOutput)
$ErrorActionPreference = 'Stop'
$candidate = Join-Path $publishDir 'curl.exe'
if ($publishExit -ne 0 -or -not (Test-Path -LiteralPath $candidate)) {
    $why = @($publishOutput -split "`n" | Where-Object { $_ -match 'error' } | Select-Object -First 3) -join ' | '
    Write-Host "Publishing Curl.Console failed (exit $publishExit): $why" -ForegroundColor Red
    Write-Host 'No measurements were taken.'
    exit 1
}

$referenceVersion = "$((& $reference --version | Select-Object -First 1))".Trim()
$candidateVersion = "$((& $candidate --version | Select-Object -First 1))".Trim()
$commit = "$(git -C $repo rev-parse HEAD 2>$null)".Trim()
$binaries = @(
    [pscustomobject]@{ Key = 'curl'; Exe = $reference }
    [pscustomobject]@{ Key = 'candidate'; Exe = $candidate }
)
$log = Join-Path $OutDirectory 'runs.log'
[IO.File]::WriteAllText($log, "scenario`titeration`tbinary`tms`tpeakWorkingSetBytes`texit`r`n")
Write-Host "reference: $referenceVersion"
Write-Host "candidate: $candidateVersion ($candidate, commit $commit)"

$results = @()
foreach ($scenario in Get-Scenarios) {
    $samples = @{ curl = @(); candidate = @() }
    for ($i = 1 - $WarmUps; $i -le $Iterations; $i++) {
        # Alternate the binaries, and swap which goes first on every iteration.
        $order = if ($i % 2) { $binaries } else { @($binaries[1], $binaries[0]) }
        foreach ($binary in $order) {
            # Warm-ups are numbered in the order they run: warmup1, warmup2, then 1, 2, ...
            $label = if ($i -le 0) { "warmup$($i + $WarmUps)" } else { "$i" }
            $folder = Join-Path $OutDirectory "runs\$($scenario.Name)\$($binary.Key)-$label"
            $run = Invoke-Run $scenario $binary.Exe $folder
            [IO.File]::AppendAllText($log, "$($scenario.Name)`t$label`t$($binary.Key)`t$($run.Ms)`t$($run.Bytes)`t$($run.Exit)`r`n")
            if ($i -ge 1) { $samples[$binary.Key] += $run }
        }
    }
    $entry = [ordered]@{ name = $scenario.Name }
    foreach ($key in 'curl', 'candidate') {
        $ms = [double[]]@($samples[$key] | ForEach-Object { $_.Ms })
        $bytes = [double[]]@($samples[$key] | ForEach-Object { $_.Bytes })
        $entry[$key] = [ordered]@{ medianMs = (Get-Median $ms); p90Ms = (Get-Percentile $ms 0.9); medianPeakWorkingSetBytes = [long](Get-Median $bytes) }
    }
    $results += [pscustomobject]$entry
    Write-Host ("{0,-16} curl {1,8:0.0} ms   candidate {2,8:0.0} ms" -f $scenario.Name, $entry.curl.medianMs, $entry.candidate.medianMs)
}

$performance = [ordered]@{
    referenceVersion = $referenceVersion; candidateVersion = $candidateVersion; candidateCommit = $commit
    processorCount = [Environment]::ProcessorCount; iterations = $Iterations; warmUps = $WarmUps
    referenceBinaryBytes = (Get-Item -LiteralPath $reference).Length; candidateBinaryBytes = (Get-Item -LiteralPath $candidate).Length
    largeGetBytes = $LargeBytes; scenarios = $results
}
[IO.File]::WriteAllText((Join-Path $OutDirectory 'performance.json'), ([pscustomobject]$performance | ConvertTo-Json -Depth 6))

$lines = @(
    '| Scenario | curl median ms | curl p90 ms | curl median peak MiB | Curl median ms | Curl p90 ms | Curl median peak MiB |'
    '| --- | ---: | ---: | ---: | ---: | ---: | ---: |'
)
foreach ($r in $results) {
    $lines += '| {0} | {1:0.0} | {2:0.0} | {3:0.0} | {4:0.0} | {5:0.0} | {6:0.0} |' -f $r.name, $r.curl.medianMs, $r.curl.p90Ms, ($r.curl.medianPeakWorkingSetBytes / 1MB), $r.candidate.medianMs, $r.candidate.p90Ms, ($r.candidate.medianPeakWorkingSetBytes / 1MB)
}
$lines += ''
$lines += "Binary size: curl $($performance.referenceBinaryBytes) bytes, Curl $($performance.candidateBinaryBytes) bytes. $Iterations runs per cell after $WarmUps warm-ups; $($performance.processorCount) processors."
[IO.File]::WriteAllText((Join-Path $OutDirectory 'performance.md'), ($lines -join "`r`n") + "`r`n")
Remove-Item -Recurse -Force -LiteralPath $publishDir -ErrorAction SilentlyContinue
Write-Host "wrote $OutDirectory\performance.json and performance.md"
