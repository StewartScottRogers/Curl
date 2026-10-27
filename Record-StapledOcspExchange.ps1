<#
.SYNOPSIS
Records real curl and Curl against a TLS server that staples a good or a revoked OCSP response (CGR-014).

.DESCRIPTION
For each side (REF, real curl; PORT, Curl) the script builds a throwaway revocation PKI with
Git's openssl.exe in its own temporary directory, so the Windows URL cache cannot carry a
result from one side to the other:

  rca         the root certificate authority (the only --cacert)
  good leaf   CN=localhost, SAN localhost, signed by rca, index.txt entry V
  revoked     CN=localhost, SAN localhost, signed by rca, index.txt entry R
  responder   an OCSP signing certificate signed by rca
  *.ocsp      one DER OCSP response per leaf, signed by the responder

The leaves carry no CRL distribution point and an Authority Information Access OCSP URL
(http://127.0.0.1:9/) that nothing answers, so only the staple can decide revocation.
Each leaf is served with `openssl s_server -WWW -status_file <staple>` on -Port, and every
case runs `curl -sS --cacert <rca.crt> [flags] https://localhost:<Port>/index.txt`.

Cases, per staple (good, revoked): default checking, --ssl-revoke-best-effort, --cert-status,
and both flags together. One line per case and side is printed:

  <staple>|<flags>|<side>|exit=<code>|stdout=<text>|stderr=<text>

with CR, LF written as \r and \n. Everything lives under a temporary directory that is deleted
afterwards. Works on Windows only (real curl with Schannel is what CGR-014 compares).

.PARAMETER Port
The loopback TCP port the s_server listens on. Default 14433.

.PARAMETER Reference
The real curl to run. Default curl.exe from the PATH.

.PARAMETER PortCurl
The Curl executable to run. Default: the newest Curl.Console\bin\*\*\curl.exe beside this script.

.PARAMETER OpenSsl
The openssl.exe used to build the PKI and to serve. Default Git's ucrt64 openssl.

.EXAMPLE
.\Record-StapledOcspExchange.ps1
#>
[CmdletBinding()]
param(
    [int]$Port = 14433,
    [string]$Reference = 'curl.exe',
    [string]$PortCurl,
    [string]$OpenSsl = 'C:\Program Files\Git\ucrt64\bin\openssl.exe'
)

$ErrorActionPreference = 'Stop'
if (-not $PortCurl) {
    $PortCurl = Get-ChildItem -Path (Join-Path $PSScriptRoot 'Curl.Console\bin') -Recurse -Filter 'curl.exe' -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1 -ExpandProperty FullName
}
if (-not $PortCurl) { throw 'No Curl executable found: build Curl.Console or pass -PortCurl.' }

function Invoke-OpenSsl([string]$WorkDirectory, [string[]]$Arguments) {
    Push-Location -LiteralPath $WorkDirectory
    try {
        $output = & $OpenSsl @Arguments 2>&1
        if ($LASTEXITCODE -ne 0) { throw "openssl $($Arguments -join ' ') failed: $output" }
    } finally { Pop-Location }
}

function New-RevocationPki([string]$Directory) {
    $null = New-Item -ItemType Directory -Force -Path $Directory
    $leafExtensions = "basicConstraints=CA:FALSE`nkeyUsage=digitalSignature,keyEncipherment`nextendedKeyUsage=serverAuth`nsubjectAltName=DNS:localhost`nauthorityInfoAccess=OCSP;URI:http://127.0.0.1:9/`n"
    Set-Content -LiteralPath (Join-Path $Directory 'leaf.ext') -Value $leafExtensions -NoNewline
    Set-Content -LiteralPath (Join-Path $Directory 'resp.ext') -Value "basicConstraints=CA:FALSE`nextendedKeyUsage=OCSPSigning`n" -NoNewline
    Invoke-OpenSsl $Directory @('req', '-x509', '-newkey', 'rsa:2048', '-nodes', '-keyout', 'rca.key', '-out', 'rca.crt', '-days', '30', '-subj', '/CN=rca', '-addext', 'basicConstraints=critical,CA:TRUE', '-addext', 'keyUsage=critical,keyCertSign,cRLSign')
    $serial = 16
    $indexLines = @()
    foreach ($name in 'good', 'revoked', 'resp') {
        $serial++
        $extension = if ($name -eq 'resp') { 'resp.ext' } else { 'leaf.ext' }
        Invoke-OpenSsl $Directory @('req', '-newkey', 'rsa:2048', '-nodes', '-keyout', "$name.key", '-out', "$name.csr", '-subj', "/CN=$(if ($name -eq 'resp') { 'ocsp-responder' } else { 'localhost' })")
        Invoke-OpenSsl $Directory @('x509', '-req', '-in', "$name.csr", '-CA', 'rca.crt', '-CAkey', 'rca.key', '-set_serial', "$serial", '-out', "$name.crt", '-days', '30', '-extfile', $extension)
        $hexSerial = '{0:X2}' -f $serial
        $expiry = [DateTime]::UtcNow.AddDays(30).ToString('yyMMddHHmmss') + 'Z'
        $revoked = [DateTime]::UtcNow.AddDays(-1).ToString('yyMMddHHmmss') + 'Z'
        if ($name -eq 'good') { $indexLines += "V`t$expiry`t`t$hexSerial`tunknown`t/CN=localhost" }
        if ($name -eq 'revoked') { $indexLines += "R`t$expiry`t$revoked`t$hexSerial`tunknown`t/CN=localhost" }
    }
    [IO.File]::WriteAllText((Join-Path $Directory 'ocsp-index.txt'), (($indexLines -join "`n") + "`n"))
    foreach ($name in 'good', 'revoked') {
        Invoke-OpenSsl $Directory @('ocsp', '-index', 'ocsp-index.txt', '-CA', 'rca.crt', '-rsigner', 'resp.crt', '-rkey', 'resp.key', '-issuer', 'rca.crt', '-cert', "$name.crt", '-ndays', '7', '-respout', "$name.ocsp")
    }
    Set-Content -LiteralPath (Join-Path $Directory 'index.txt') -Value 'stapled ocsp ok' -NoNewline
}

function ConvertTo-OneLine([string]$Text) { ($Text -replace "`r", '\r') -replace "`n", '\n' }

function Invoke-Side([string]$Side, [string]$Executable, [string]$Directory) {
    foreach ($staple in 'good', 'revoked') {
        $server = Start-Process -FilePath $OpenSsl -WorkingDirectory $Directory -PassThru -WindowStyle Hidden -ArgumentList @(
            's_server', '-accept', "$Port", '-cert', "$staple.crt", '-key', "$staple.key", '-status_file', "$staple.ocsp", '-WWW')
        try {
            Start-Sleep -Milliseconds 1500
            foreach ($flags in @(@(), @('--ssl-revoke-best-effort'), @('--cert-status'), @('--ssl-revoke-best-effort', '--cert-status'))) {
                $startInfo = [Diagnostics.ProcessStartInfo]::new($Executable)
                $startInfo.RedirectStandardOutput = $true
                $startInfo.RedirectStandardError = $true
                $startInfo.UseShellExecute = $false
                foreach ($argument in (@('-sS', '--cacert', (Join-Path $Directory 'rca.crt')) + $flags + @("https://localhost:$Port/index.txt"))) { $startInfo.ArgumentList.Add($argument) }
                $process = [Diagnostics.Process]::Start($startInfo)
                $stdoutTask = $process.StandardOutput.ReadToEndAsync()
                $stderrTask = $process.StandardError.ReadToEndAsync()
                $process.WaitForExit()
                "$staple|$($flags -join ' ')|$Side|exit=$($process.ExitCode)|stdout=$(ConvertTo-OneLine $stdoutTask.Result)|stderr=$(ConvertTo-OneLine $stderrTask.Result)"
            }
        } finally {
            Stop-Process -Id $server.Id -Force -ErrorAction SilentlyContinue
        }
    }
}

$root = Join-Path ([IO.Path]::GetTempPath()) ("stapled-ocsp-" + [Guid]::NewGuid().ToString('N'))
try {
    foreach ($side in @(@('REF', $Reference), @('PORT', $PortCurl))) {
        $directory = Join-Path $root $side[0]
        New-RevocationPki $directory
        Invoke-Side $side[0] $side[1] $directory
    }
} finally {
    Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue
}
