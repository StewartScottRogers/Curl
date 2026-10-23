<#
.SYNOPSIS
Generates the certificate directory the TLS differential cases name as {PKI}, with no interaction.

.DESCRIPTION
Writes these files into -Directory (created if missing; existing files are overwritten, so running
it twice is safe). Every certificate is signed by the generated ca.pem, which is the only
certificate authority; private keys are written only inside -Directory. Uses only
System.Security.Cryptography (no openssl), in Windows PowerShell 5.1 and PowerShell 7 on Windows.

  ca.pem                  The certificate authority (ECDSA P-256). The cases pass it as --cacert and
                          the MTLS endpoint accepts only client certificates that chain to it.
  server.pem, server.key  Server certificate (ECDSA) for 127.0.0.1, ::1 and localhost: served by
                          the T, T12, T13 and MTLS endpoints. Also used as a wrong --cacert.
  wrong.pem,  wrong.key   Server certificate for another host name (wrong.example): the WRONG endpoint.
  expired.pem, expired.key Server certificate for 127.0.0.1 and localhost that expired a year ago: the EXP endpoint.
  rsa.pem,    rsa.key     Server certificate with an RSA 2048 key, otherwise like server: the RSA endpoint.
  client.pem, client.key  Client certificate (ECDSA) for the MTLS endpoint, PEM certificate and PKCS#8 key.
  client.p12              The same client certificate and key as PKCS#12 with the password "pw".

.PARAMETER Directory
The directory to write into. Point CURL_DIFFERENTIAL_PKI at it.

.EXAMPLE
.\New-DifferentialPki.ps1 -Directory "$env:TEMP\curl-pki"
#>
[CmdletBinding()]
param([Parameter(Mandatory)][string]$Directory)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Security
$null = New-Item -ItemType Directory -Force -Path $Directory
$Directory = (Resolve-Path -LiteralPath $Directory).Path

function ConvertTo-Pem([string]$Label, [byte[]]$Der) {
    $b64 = [Convert]::ToBase64String($Der) -replace '.{64}', "`$&`n"
    "-----BEGIN $Label-----`n$($b64.TrimEnd())`n-----END $Label-----`n"
}

function Write-Text([string]$Name, [string]$Text) {
    [IO.File]::WriteAllText((Join-Path $Directory $Name), $Text, (New-Object Text.UTF8Encoding($false)))
}

# Exportable CNG key (named, deleted at the end, so no key outlives the run) so the PKCS#8 and PKCS#12 exports work on both PowerShell editions.
function New-KeyPair([string]$Kind) {
    $policy = New-Object Security.Cryptography.CngKeyCreationParameters
    $policy.ExportPolicy = [Security.Cryptography.CngExportPolicies]::AllowPlaintextExport
    if ($Kind -eq 'rsa') {
        $policy.Parameters.Add((New-Object Security.Cryptography.CngProperty('Length', [BitConverter]::GetBytes(2048), 'None')))
        $cng = [Security.Cryptography.CngKey]::Create([Security.Cryptography.CngAlgorithm]::Rsa, [guid]::NewGuid().ToString(), $policy)
        $key = New-Object Security.Cryptography.RSACng($cng)
    } else {
        $cng = [Security.Cryptography.CngKey]::Create([Security.Cryptography.CngAlgorithm]::ECDsaP256, [guid]::NewGuid().ToString(), $policy)
        $key = New-Object Security.Cryptography.ECDsaCng($cng)
    }
    $script:Created.Add($cng)
    @{ Cng = $cng; Key = $key }
}

function New-Request([string]$Subject, $Pair, [string[]]$DnsNames, [bool]$IsAuthority) {
    $hash = [Security.Cryptography.HashAlgorithmName]::SHA256
    if ($Pair.Key -is [Security.Cryptography.RSA]) {
        $req = New-Object Security.Cryptography.X509Certificates.CertificateRequest($Subject, $Pair.Key, $hash, [Security.Cryptography.RSASignaturePadding]::Pkcs1)
    } else {
        $req = New-Object Security.Cryptography.X509Certificates.CertificateRequest($Subject, $Pair.Key, $hash)
    }
    $req.CertificateExtensions.Add((New-Object Security.Cryptography.X509Certificates.X509BasicConstraintsExtension($IsAuthority, $false, 0, $true)))
    if (-not $IsAuthority) {
        $usage = [Security.Cryptography.X509Certificates.X509KeyUsageFlags]::DigitalSignature
        $req.CertificateExtensions.Add((New-Object Security.Cryptography.X509Certificates.X509KeyUsageExtension($usage, $true)))
        $oids = New-Object Security.Cryptography.OidCollection
        $null = $oids.Add((New-Object Security.Cryptography.Oid('1.3.6.1.5.5.7.3.1')))
        $null = $oids.Add((New-Object Security.Cryptography.Oid('1.3.6.1.5.5.7.3.2')))
        $req.CertificateExtensions.Add((New-Object Security.Cryptography.X509Certificates.X509EnhancedKeyUsageExtension($oids, $false)))
    }
    if ($DnsNames) {
        $san = New-Object Security.Cryptography.X509Certificates.SubjectAlternativeNameBuilder
        foreach ($n in $DnsNames) {
            if ($n -match '^[0-9.]+$|:') { $san.AddIpAddress([Net.IPAddress]::Parse($n)) } else { $san.AddDnsName($n) }
        }
        $req.CertificateExtensions.Add($san.Build())
    }
    $req
}

function New-Serial { $b = New-Object byte[] 8; [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($b); $b[0] = $b[0] -band 0x7f; $b }

$script:Created = New-Object Collections.Generic.List[Security.Cryptography.CngKey]
try {
$now = [DateTimeOffset]::UtcNow
$caPair = New-KeyPair 'ec'
$ca = (New-Request 'CN=Curl differential test CA' $caPair $null $true).CreateSelfSigned($now.AddYears(-5), $now.AddYears(10))
Write-Text 'ca.pem' (ConvertTo-Pem 'CERTIFICATE' $ca.RawData)
$script:CaSigner = [Security.Cryptography.X509Certificates.X509SignatureGenerator]::CreateForECDsa($caPair.Key)

function New-Issued([string]$Name, [string]$Kind, [string[]]$Names, $NotBefore, $NotAfter) {
    $pair = New-KeyPair $Kind
    $cert = (New-Request "CN=$($Names[0])" $pair $Names $false).Create($ca.SubjectName, $script:CaSigner, $NotBefore, $NotAfter, (New-Serial))
    Write-Text "$Name.pem" (ConvertTo-Pem 'CERTIFICATE' $cert.RawData)
    Write-Text "$Name.key" (ConvertTo-Pem 'PRIVATE KEY' $pair.Cng.Export([Security.Cryptography.CngKeyBlobFormat]::Pkcs8PrivateBlob))
    @{ Cert = $cert; Pair = $pair }
}

$hosts = @('localhost', '127.0.0.1', '::1')
$valid = @($now.AddDays(-1), $now.AddYears(5))
$null = New-Issued 'server' 'ec' $hosts $valid[0] $valid[1]
$null = New-Issued 'wrong' 'ec' @('wrong.example') $valid[0] $valid[1]
$null = New-Issued 'expired' 'ec' $hosts $now.AddYears(-2) $now.AddYears(-1)
$null = New-Issued 'rsa' 'rsa' $hosts $valid[0] $valid[1]
$client = New-Issued 'client' 'ec' @('curl-differential-client') $valid[0] $valid[1]

$withKey = [Security.Cryptography.X509Certificates.ECDsaCertificateExtensions]::CopyWithPrivateKey($client.Cert, $client.Pair.Key)
[IO.File]::WriteAllBytes((Join-Path $Directory 'client.p12'), $withKey.Export([Security.Cryptography.X509Certificates.X509ContentType]::Pfx, 'pw'))
Write-Host "Wrote the differential PKI to $Directory"
} finally {
    foreach ($k in $script:Created) { $k.Delete() }
}
