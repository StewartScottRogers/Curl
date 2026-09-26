<#
.SYNOPSIS
    Replaces the vendored upstream curl test data with the files at a named curl release tag.

.DESCRIPTION
    Downloads the source archive of curl at -Tag from GitHub, then replaces every test* file
    in this folder with the test* files of tests/data at that tag, and COPYING with curl's
    COPYING at that tag. Files are written byte for byte; the .gitattributes beside them marks
    them -text so git keeps their line endings. ADR-0013, decision 3: the data is vendored and
    pinned, and tests never download anything - only this script does.

.PARAMETER Tag
    The curl release tag, as named in https://github.com/curl/curl/tags, e.g. curl-8_21_0.

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File Update-UpstreamTestData.ps1 -Tag curl-8_21_0
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^curl-\d+_\d+_\d+$')]
    [string]$Tag
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$destination = $PSScriptRoot
$workFolder = Join-Path ([System.IO.Path]::GetTempPath()) ("curl-upstream-" + [System.Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $workFolder | Out-Null

try {
    $archive = Join-Path $workFolder "$Tag.tar.gz"
    Invoke-WebRequest -Uri "https://github.com/curl/curl/archive/refs/tags/$Tag.tar.gz" -OutFile $archive -UseBasicParsing

    $sourceRoot = "curl-$Tag"
    # Windows' own bsdtar: a Git Bash GNU tar earlier on PATH reads "C:\..." as a remote host.
    $tar = Join-Path $env:SystemRoot 'System32\tar.exe'
    if (-not (Test-Path -LiteralPath $tar)) { $tar = 'tar' }
    & $tar -xzf $archive -C $workFolder "$sourceRoot/COPYING" "$sourceRoot/tests/data"
    if ($LASTEXITCODE -ne 0) { throw "tar could not extract $sourceRoot from $archive." }

    $upstreamData = Join-Path $workFolder "$sourceRoot/tests/data"
    $upstreamTests = @(Get-ChildItem -LiteralPath $upstreamData -File -Filter 'test*')
    if ($upstreamTests.Count -eq 0) { throw "No test* files found in tests/data at $Tag." }

    Get-ChildItem -LiteralPath $destination -File -Filter 'test*' | Remove-Item -Force
    foreach ($test in $upstreamTests) {
        Copy-Item -LiteralPath $test.FullName -Destination (Join-Path $destination $test.Name)
    }
    Copy-Item -LiteralPath (Join-Path $workFolder "$sourceRoot/COPYING") -Destination (Join-Path $destination 'COPYING') -Force

    Write-Output "Vendored $($upstreamTests.Count) test files and COPYING from $Tag into $destination."
    Write-Output "If the count changed, update VendoredTestFileCount in UpstreamTestDataTests.cs and the tag in README.md."
}
finally {
    Remove-Item -LiteralPath $workFolder -Recurse -Force -ErrorAction SilentlyContinue
}
