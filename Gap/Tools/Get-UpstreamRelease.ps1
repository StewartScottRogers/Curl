<#
.SYNOPSIS
    Downloads one upstream curl release tarball, checks it against a pinned SHA-256 and
    caches the parts the gap analysis office reads.

.DESCRIPTION
    The gap analysis office measures Curl against upstream curl's release tarball
    (ADR-0433 decision 1). This script fetches that tarball and extracts only
    curl-<version>/docs, curl-<version>/tests/data, curl-<version>/lib/strerror.c and
    curl-<version>/COPYING into <CacheRoot>/<version>/, a cache outside the repository.
    It prints that folder's full path as the only line on standard output.

    Download: first
    https://github.com/curl/curl/releases/download/curl-<8_21_0>/curl-<8.21.0>.tar.gz,
    then https://curl.se/download/curl-<8.21.0>.tar.gz.

    Hash rule: the tarball's SHA-256 is computed with Get-FileHash. When
    <BaselinesDirectory>/curl-<version>.json exists and its sha256 differs, the script
    throws, names both hashes and leaves the cache untouched. When that manifest does not
    exist, the script writes it (trust on first use). Its format is in
    Gap/Instructions/Gap-Format.md.

    Cache: <CacheRoot>/<version>/.complete is written last and holds the hash, so an
    interrupted extraction is redone from scratch. When .complete already holds the
    pinned hash and no -ArchivePath is given, the script returns at once without
    downloading.

    Extraction uses Windows' own System32\tar.exe (a Git Bash GNU tar earlier on PATH
    reads "C:\..." as a remote host), and plain tar off Windows. Runs under Windows
    PowerShell 5.1, PowerShell 7 and pwsh on Linux.

.PARAMETER Version
    The release version, for example 8.21.0. Default: the version in
    <BaselinesDirectory>/target.json.

.PARAMETER CacheRoot
    The folder holding one subfolder per cached version. Default on Windows:
    $env:LOCALAPPDATA\Curl\gap\upstream. Elsewhere: $env:XDG_CACHE_HOME/curl-gap/upstream,
    or ~/.cache/curl-gap/upstream when XDG_CACHE_HOME is not set.

.PARAMETER ArchivePath
    A local curl-<version>.tar.gz to use instead of downloading; for self-tests. It is
    always hashed and checked, even when the cache is already complete.

.PARAMETER BaselinesDirectory
    The folder holding target.json and the curl-<version>.json manifests. Default:
    Gap/Baselines.

.PARAMETER SelfTest
    Builds a tiny curl-9.9.9.tar.gz from Gap/Tools/Fixtures/release in a temporary
    folder, runs the script against it with temporary -CacheRoot and
    -BaselinesDirectory, prints a PASS or FAIL line per check, and exits 1 on any FAIL.

.EXAMPLE
    Gap/Tools/Get-UpstreamRelease.ps1 -Version 8.21.0
#>
[CmdletBinding()]
param(
    [string] $Version,
    [string] $CacheRoot,
    [string] $ArchivePath,
    [string] $BaselinesDirectory,
    [switch] $SelfTest
)

$ErrorActionPreference = 'Stop'
# Windows PowerShell 5.1 has no $PSScriptRoot while it binds parameter defaults.
if ([string]::IsNullOrEmpty($BaselinesDirectory)) {
    $BaselinesDirectory = Join-Path (Split-Path $PSScriptRoot -Parent) 'Baselines'
}
$ProgressPreference = 'SilentlyContinue'

function Test-OnWindows {
    return $env:OS -eq 'Windows_NT'
}

function Get-TarPath {
    if (Test-OnWindows) {
        $systemTar = Join-Path $env:SystemRoot 'System32\tar.exe'
        if (Test-Path -LiteralPath $systemTar) { return $systemTar }
    }
    return 'tar'
}

function Get-DefaultCacheRoot {
    if (Test-OnWindows) {
        return Join-Path $env:LOCALAPPDATA (Join-Path 'Curl' (Join-Path 'gap' 'upstream'))
    }
    $cacheHome = $env:XDG_CACHE_HOME
    if ([string]::IsNullOrEmpty($cacheHome)) { $cacheHome = Join-Path $HOME '.cache' }
    return Join-Path $cacheHome (Join-Path 'curl-gap' 'upstream')
}

function Write-Utf8File([string] $Path, [string] $Text) {
    [System.IO.File]::WriteAllText($Path, $Text, (New-Object System.Text.UTF8Encoding $false))
}

function Invoke-SelfTest {
    function Report([bool] $Passed, [string] $Check) {
        if ($Passed) { Write-Output "PASS $Check" } else { Write-Output "FAIL $Check"; $script:selfTestFailed = $true }
    }
    $script:selfTestFailed = $false
    $work = Join-Path ([System.IO.Path]::GetTempPath()) ("gap-release-selftest-" + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $work | Out-Null
    try {
        $tar = Get-TarPath
        $fixtures = Join-Path $PSScriptRoot (Join-Path 'Fixtures' 'release')
        $archive = Join-Path $work 'curl-9.9.9.tar.gz'
        & $tar -czf $archive -C $fixtures 'curl-9.9.9' | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "tar could not build $archive." }
        $cache = Join-Path $work 'cache'
        $baselines = Join-Path $work 'baselines'
        New-Item -ItemType Directory -Path $baselines | Out-Null
        $manifestPath = Join-Path $baselines 'curl-9.9.9.json'
        $versionFolder = Join-Path $cache '9.9.9'
        $hash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()

        $printed = @(& $PSCommandPath -Version 9.9.9 -ArchivePath $archive -CacheRoot $cache -BaselinesDirectory $baselines)
        $manifest = if (Test-Path -LiteralPath $manifestPath) { Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json } else { $null }
        Report ($null -ne $manifest -and $manifest.sha256 -eq $hash -and $manifest.version -eq '9.9.9' -and $manifest.tag -eq 'curl-9_9_9') 'first run writes the manifest with the archive''s hash'

        $wanted = @('docs/x.md', 'tests/data/test1', 'lib/strerror.c', 'COPYING')
        $allWanted = @($wanted | Where-Object { -not (Test-Path -LiteralPath (Join-Path $versionFolder $_)) }).Count -eq 0
        $otherAbsent = -not (Test-Path -LiteralPath (Join-Path $versionFolder (Join-Path 'lib' 'other.c')))
        Report ($allWanted -and $otherAbsent) 'only docs/, tests/data/, lib/strerror.c and COPYING are extracted'

        Report ($printed.Count -eq 1 -and $printed[0] -eq (Resolve-Path -LiteralPath $versionFolder).Path) 'the printed path is the version''s cache folder'

        $sentinel = Join-Path $versionFolder 'sentinel'
        Write-Utf8File $sentinel 'left by the self-test'
        $printedAgain = @(& $PSCommandPath -Version 9.9.9 -ArchivePath $archive -CacheRoot $cache -BaselinesDirectory $baselines)
        Report ((Test-Path -LiteralPath $sentinel) -and $printedAgain.Count -eq 1 -and $printedAgain[0] -eq $printed[0]) 'a second run with the same archive returns without re-extracting'

        $otherArchive = Join-Path $work (Join-Path 'other' 'curl-9.9.9.tar.gz')
        New-Item -ItemType Directory -Path (Split-Path $otherArchive -Parent) | Out-Null
        & $tar -czf $otherArchive -C $fixtures 'curl-9.9.9/COPYING' | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "tar could not build $otherArchive." }
        $manifestBefore = Get-Content -LiteralPath $manifestPath -Raw
        $completeBefore = Get-Content -LiteralPath (Join-Path $versionFolder '.complete') -Raw
        $threw = $false
        try { & $PSCommandPath -Version 9.9.9 -ArchivePath $otherArchive -CacheRoot $cache -BaselinesDirectory $baselines | Out-Null } catch { $threw = $_.Exception.Message -match $hash }
        $unchanged = ((Get-Content -LiteralPath $manifestPath -Raw) -eq $manifestBefore) -and
            ((Get-Content -LiteralPath (Join-Path $versionFolder '.complete') -Raw) -eq $completeBefore) -and
            (Test-Path -LiteralPath $sentinel) -and (Test-Path -LiteralPath (Join-Path $versionFolder 'docs/x.md'))
        Report ($threw -and $unchanged) 'an archive whose hash differs from the manifest throws and changes nothing'
    }
    finally {
        Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue
    }
    if ($script:selfTestFailed) { exit 1 }
    exit 0
}

if ($SelfTest) { Invoke-SelfTest }

if ([string]::IsNullOrEmpty($Version)) {
    $targetPath = Join-Path $BaselinesDirectory 'target.json'
    if (-not (Test-Path -LiteralPath $targetPath)) { throw "No -Version given and $targetPath does not exist." }
    $Version = (Get-Content -LiteralPath $targetPath -Raw | ConvertFrom-Json).version
}
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw "Version '$Version' is not of the form 8.21.0." }
if ([string]::IsNullOrEmpty($CacheRoot)) { $CacheRoot = Get-DefaultCacheRoot }

$tag = 'curl-' + $Version.Replace('.', '_')
$sourceRoot = "curl-$Version"
$manifestPath = Join-Path $BaselinesDirectory "$sourceRoot.json"
$versionFolder = Join-Path $CacheRoot $Version
$completePath = Join-Path $versionFolder '.complete'

$pinned = $null
if (Test-Path -LiteralPath $manifestPath) {
    $pinned = (Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json).sha256
}

if ($pinned -and -not $ArchivePath -and (Test-Path -LiteralPath $completePath) -and
    (Get-Content -LiteralPath $completePath -Raw).Trim() -eq $pinned) {
    Write-Output (Resolve-Path -LiteralPath $versionFolder).Path
    return
}

$work = Join-Path ([System.IO.Path]::GetTempPath()) ("gap-release-" + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $work | Out-Null
try {
    if ($ArchivePath) {
        $archive = (Resolve-Path -LiteralPath $ArchivePath).Path
        $url = $archive
    }
    else {
        [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.ServicePointManager]::SecurityProtocol -bor [System.Net.SecurityProtocolType]::Tls12
        $archive = Join-Path $work "$sourceRoot.tar.gz"
        $candidates = @(
            "https://github.com/curl/curl/releases/download/$tag/$sourceRoot.tar.gz",
            "https://curl.se/download/$sourceRoot.tar.gz"
        )
        $url = $null
        foreach ($candidate in $candidates) {
            try {
                Invoke-WebRequest -Uri $candidate -OutFile $archive -UseBasicParsing
                $url = $candidate
                break
            }
            catch {
                Write-Warning "Could not download ${candidate}: $($_.Exception.Message)"
            }
        }
        if (-not $url) { throw "Could not download $sourceRoot.tar.gz from any of: $($candidates -join ', ')." }
    }

    $hash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($pinned -and $pinned -ne $hash) {
        throw "SHA-256 mismatch for ${sourceRoot}: $manifestPath pins $pinned but $url hashes to $hash. The cache is untouched."
    }

    if ($pinned -and (Test-Path -LiteralPath $completePath) -and
        (Get-Content -LiteralPath $completePath -Raw).Trim() -eq $pinned) {
        Write-Output (Resolve-Path -LiteralPath $versionFolder).Path
        return
    }

    if (-not $pinned) {
        if (-not (Test-Path -LiteralPath $BaselinesDirectory)) { New-Item -ItemType Directory -Path $BaselinesDirectory | Out-Null }
        $manifest = "{`n" +
            "  `"version`": `"$Version`",`n" +
            "  `"tag`": `"$tag`",`n" +
            "  `"url`": `"$($url.Replace('\', '\\'))`",`n" +
            "  `"sha256`": `"$hash`",`n" +
            "  `"fetched`": `"$((Get-Date).ToString('yyyy-MM-dd'))`"`n" +
            "}`n"
        Write-Utf8File $manifestPath $manifest
    }

    if (Test-Path -LiteralPath $versionFolder) { Remove-Item -LiteralPath $versionFolder -Recurse -Force }
    New-Item -ItemType Directory -Path $versionFolder -Force | Out-Null
    $tar = Get-TarPath
    & $tar -xzf $archive -C $versionFolder --strip-components 1 "$sourceRoot/docs" "$sourceRoot/tests/data" "$sourceRoot/lib/strerror.c" "$sourceRoot/COPYING" | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "tar could not extract $sourceRoot from $archive." }
    Write-Utf8File $completePath $hash
    Write-Output (Resolve-Path -LiteralPath $versionFolder).Path
}
finally {
    Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue
}
