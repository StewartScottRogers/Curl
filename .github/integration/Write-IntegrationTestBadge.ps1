<#
.SYNOPSIS
    Writes the README's Integration tests badge from the per-platform result files.

.DESCRIPTION
    Reads windows.json, linux.json and macos.json (written by
    Write-IntegrationTestSummary.ps1) from -Directory and writes badge.svg beside them:
    "integration tests" then each platform's passed/run count, for example
    "Windows 32/32 · Linux 30/32 · macOS 32/32". Green when every platform reported and
    none failed, red when any test failed, grey while a platform has no result yet.
#>
param([Parameter(Mandatory)] [string] $Directory)
$ErrorActionPreference = 'Stop'

$parts = @()
$anyFailed = $false
$anyMissing = $false
foreach ($platform in 'Windows', 'Linux', 'macOS') {
    $file = Join-Path $Directory "$($platform.ToLowerInvariant()).json"
    if (-not (Test-Path $file)) { $parts += "$platform -"; $anyMissing = $true; continue }
    $result = Get-Content $file -Raw | ConvertFrom-Json
    $run = $result.passed + $result.failed
    if ($result.resultFiles -eq 0) { $parts += "$platform no results"; $anyFailed = $true; continue }
    if ($result.failed -gt 0) { $anyFailed = $true }
    $parts += "$platform $($result.passed)/$run"
}
$label = 'integration tests'
$message = $parts -join ' · '
$colour = if ($anyFailed) { '#e05d44' } elseif ($anyMissing) { '#9f9f9f' } else { '#4c1' }

# Verdana 11px averages about 6.5px a character; padding of 6px each side.
$labelWidth = [int][Math]::Ceiling($label.Length * 6.5) + 12
$messageWidth = [int][Math]::Ceiling($message.Length * 6.5) + 12
$width = $labelWidth + $messageWidth
$title = [System.Security.SecurityElement]::Escape("${label}: $message")
$labelText = [System.Security.SecurityElement]::Escape($label)
$messageText = [System.Security.SecurityElement]::Escape($message)
$svg = @"
<svg xmlns="http://www.w3.org/2000/svg" width="$width" height="20" role="img" aria-label="$title">
  <title>$title</title>
  <linearGradient id="s" x2="0" y2="100%"><stop offset="0" stop-color="#bbb" stop-opacity=".1"/><stop offset="1" stop-opacity=".1"/></linearGradient>
  <clipPath id="r"><rect width="$width" height="20" rx="3" fill="#fff"/></clipPath>
  <g clip-path="url(#r)">
    <rect width="$labelWidth" height="20" fill="#555"/>
    <rect x="$labelWidth" width="$messageWidth" height="20" fill="$colour"/>
    <rect width="$width" height="20" fill="url(#s)"/>
  </g>
  <g fill="#fff" text-anchor="middle" font-family="Verdana,Geneva,DejaVu Sans,sans-serif" font-size="11">
    <text x="$($labelWidth / 2)" y="14">$labelText</text>
    <text x="$($labelWidth + $messageWidth / 2)" y="14">$messageText</text>
  </g>
</svg>
"@
Set-Content -Path (Join-Path $Directory 'badge.svg') -Value $svg -Encoding utf8
"Badge: ${label}: $message"
