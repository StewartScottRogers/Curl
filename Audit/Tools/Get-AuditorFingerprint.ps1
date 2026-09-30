<#
.SYNOPSIS
    Prints one SHA-256 hash of the auditors: their agent definitions, their instructions
    and the audit tools.

.DESCRIPTION
    Every audit scorecard records this fingerprint (ADR-0267), so it says exactly which
    auditors produced it; BL-1017 writes it in. The hash changes whenever an auditor
    definition, an auditor instruction or an audit tool changes, and never otherwise.

    Inputs, under -Root: every .claude/agents/audit-*.md, and every file under
    Audit/Instructions/ and Audit/Tools/, recursively. Nothing else - not Audit/Findings,
    Audit/Scorecards or Audit/PlantedDefects: the fingerprint identifies the auditors, not
    their output or the defects they are tested with.

    The files are taken in ordinal order of their repository-relative paths, written with
    '/' separators. For each, the hash reads the path's UTF-8 bytes, a 0x0A byte, the
    file's bytes with every CRLF turned into LF, and a 0x00 byte, so the result is the same
    on Windows, Linux and macOS whatever core.autocrlf does. Runs under Windows PowerShell
    5.1 and PowerShell 7.

.PARAMETER Root
    The repository root. Default: the folder two levels above this script.

.PARAMETER List
    Also print each included path, in the order hashed, before the hash.

.EXAMPLE
    powershell -NoProfile -File Audit/Tools/Get-AuditorFingerprint.ps1
#>
param(
    [string]$Root = (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent),
    [switch]$List
)

$ErrorActionPreference = 'Stop'
$Root = [IO.Path]::GetFullPath($Root).TrimEnd('\', '/')

function Get-RelativePath([string]$Path) {
    return $Path.Substring($Root.Length + 1).Replace('\', '/')
}

$files = @()
$agents = Join-Path $Root '.claude/agents'
if (Test-Path -LiteralPath $agents) {
    $files += @(Get-ChildItem -LiteralPath $agents -File -Filter 'audit-*.md')
}
foreach ($folder in 'Audit/Instructions', 'Audit/Tools') {
    $full = Join-Path $Root $folder
    if (Test-Path -LiteralPath $full) { $files += @(Get-ChildItem -LiteralPath $full -File -Recurse) }
}

$entries = @($files | ForEach-Object { [pscustomobject]@{ Relative = (Get-RelativePath $_.FullName); Full = $_.FullName } })
$paths = [string[]]@($entries | ForEach-Object { $_.Relative })
[Array]::Sort($paths, [StringComparer]::Ordinal)
$byPath = @{}
foreach ($entry in $entries) { $byPath[$entry.Relative] = $entry.Full }

# Latin-1 maps each byte to one char and back unchanged, so CRLF -> LF can be one string
# replace over any file's bytes, text or not.
$latin1 = [Text.Encoding]::GetEncoding(28591)
$sha = [Security.Cryptography.SHA256]::Create()
try {
    $buffer = New-Object IO.MemoryStream
    foreach ($path in $paths) {
        $pathBytes = [Text.Encoding]::UTF8.GetBytes($path)
        $buffer.Write($pathBytes, 0, $pathBytes.Length)
        $buffer.WriteByte(0x0A)
        $text = $latin1.GetString([IO.File]::ReadAllBytes($byPath[$path])).Replace("`r`n", "`n")
        $bytes = $latin1.GetBytes($text)
        $buffer.Write($bytes, 0, $bytes.Length)
        $buffer.WriteByte(0x00)
        if ($List) { Write-Output $path }
    }
    $hash = $sha.ComputeHash($buffer.ToArray())
}
finally { $sha.Dispose() }

Write-Output (($hash | ForEach-Object { $_.ToString('x2') }) -join '')
