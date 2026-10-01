<#
.SYNOPSIS
    Prints the planted-defect catalogue, decoded from Catalogue.md.b64, for the audit-seeder.

.DESCRIPTION
    The catalogue is stored base64-encoded so that its text - which defects exist and how they
    are planted - never shows up in a search of the repository (BL-1028, ADR-0267). Dark
    factory lanes search the repository root all the time; the PreToolUse hook stops them
    reading Audit/ directly, but a root-wide search would otherwise still return catalogue
    lines. Only the audit-seeder agent decodes it, and only in an interactive audit run.

    -Encode does the reverse, for whoever edits the catalogue: it reads a Markdown file and
    rewrites Catalogue.md.b64 from it. Edit a decoded copy outside the repository, then encode.

.PARAMETER Encode
    A decoded Markdown catalogue to encode into Catalogue.md.b64.

.EXAMPLE
    powershell -NoProfile -File Audit/PlantedDefects/Read-PlantedCatalogue.ps1 > $env:TEMP\catalogue.md
#>
param([string]$Encode)

$ErrorActionPreference = 'Stop'
$encoded = Join-Path $PSScriptRoot 'Catalogue.md.b64'

if ($Encode) {
    $bytes = [IO.File]::ReadAllBytes((Resolve-Path $Encode).Path)
    [IO.File]::WriteAllText($encoded, [Convert]::ToBase64String($bytes, 'InsertLineBreaks') + [Environment]::NewLine)
    Write-Output "Encoded $($bytes.Length) bytes into $encoded"
    exit 0
}

$text = ([IO.File]::ReadAllText($encoded)) -replace '\s', ''
[Console]::Out.Write([Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($text)))
