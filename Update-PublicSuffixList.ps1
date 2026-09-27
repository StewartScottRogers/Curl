<#
.SYNOPSIS
    Refreshes the Public Suffix List snapshot embedded in Curl.Cookies.UnitLibrary.

.DESCRIPTION
    Downloads https://publicsuffix.org/list/public_suffix_list.dat and writes it to
    Curl.Cookies.UnitLibrary\PublicSuffixList\public_suffix_list.dat as ADR-0049 says:
    one dated comment line first,

      // Curl snapshot: yyyy-MM-dd, from https://publicsuffix.org/list/public_suffix_list.dat

    ended by a line feed, then the upstream bytes unchanged. The upstream MPL-2.0 notice
    is part of those bytes and so is kept. The README beside the snapshot is left alone.

    The build never runs this script; a task runs it when a refresh is wanted and commits
    the result. The date is today's date (UTC) unless -SnapshotDate is given.

.PARAMETER SnapshotDate
    The date written into the first line, as yyyy-MM-dd. Defaults to today (UTC).

.EXAMPLE
    powershell -NoProfile -File Update-PublicSuffixList.ps1
#>
[CmdletBinding()]
param(
    [string] $SnapshotDate = [DateTime]::UtcNow.ToString('yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture)
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$sourceUri = 'https://publicsuffix.org/list/public_suffix_list.dat'
$snapshotPath = Join-Path $PSScriptRoot 'Curl.Cookies.UnitLibrary\PublicSuffixList\public_suffix_list.dat'

$parsedDate = [DateTime]::MinValue
if (-not [DateTime]::TryParseExact($SnapshotDate, 'yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture, [Globalization.DateTimeStyles]::None, [ref] $parsedDate)) {
    throw "SnapshotDate must be yyyy-MM-dd, not '$SnapshotDate'."
}

[Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12

$client = New-Object System.Net.WebClient
try {
    $upstreamBytes = $client.DownloadData($sourceUri)
}
finally {
    $client.Dispose()
}

$upstreamText = [Text.Encoding]::UTF8.GetString($upstreamBytes)
if ($upstreamText.IndexOf('// ===BEGIN ICANN DOMAINS===', [StringComparison]::Ordinal) -lt 0 -or
    $upstreamText.IndexOf('// ===BEGIN PRIVATE DOMAINS===', [StringComparison]::Ordinal) -lt 0) {
    throw "The download from $sourceUri does not look like the Public Suffix List; the snapshot was not changed."
}

$datedLine = "// Curl snapshot: $SnapshotDate, from $sourceUri`n"
$datedLineBytes = [Text.Encoding]::ASCII.GetBytes($datedLine)

$snapshotBytes = New-Object byte[] ($datedLineBytes.Length + $upstreamBytes.Length)
[Array]::Copy($datedLineBytes, 0, $snapshotBytes, 0, $datedLineBytes.Length)
[Array]::Copy($upstreamBytes, 0, $snapshotBytes, $datedLineBytes.Length, $upstreamBytes.Length)

$snapshotDirectory = Split-Path -Parent $snapshotPath
if (-not (Test-Path -LiteralPath $snapshotDirectory)) {
    New-Item -ItemType Directory -Path $snapshotDirectory | Out-Null
}
[IO.File]::WriteAllBytes($snapshotPath, $snapshotBytes)

Write-Output "Wrote $($snapshotBytes.Length) bytes to $snapshotPath (snapshot $SnapshotDate)."
