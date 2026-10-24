<#
.SYNOPSIS
    Replaces the vendored upstream curl test data with the files at a named curl release tag.

.DESCRIPTION
    Downloads the source archive of curl at -Tag from GitHub, then replaces every test* file
    in this folder with the test* files of tests/data at that tag as test*.curltest, every
    file in certs\ with the files of tests/certs at that tag except its build files
    (Makefile.*, CMakeLists.txt) and dot files, and COPYING with curl's
    COPYING at that tag, plus the extra folders it names below (docs, include, src, and lib/libcurl.def, lib/optiontable.pl,
    lib/easyoptions.c and projects/OS400/ccsidcurl.c for the source-analysis cases, and configure.ac, m4, CMakeLists.txt, the lib and src C sources and every Markdown file for cases 1165 and 1275). Files are written byte for byte; the .gitattributes beside them marks
    them -text so git keeps their line endings. ADR-0013, decision 3: the data is vendored and
    pinned, and tests never download anything - only this script does.

.PARAMETER Tag
    The curl release tag, as named in https://github.com/curl/curl/tags, e.g. curl-8_22_0.

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File Update-UpstreamTestData.ps1 -Tag curl-8_22_0
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
    & $tar -xzf $archive -C $workFolder "$sourceRoot/COPYING" "$sourceRoot/tests/data" "$sourceRoot/tests/certs" "$sourceRoot/docs/options-in-versions" "$sourceRoot/docs/VERSIONS.md" "$sourceRoot/docs/cmdline-opts" "$sourceRoot/src/tool_writeout.c" "$sourceRoot/src/tool_getparam.c" "$sourceRoot/src/tool_listhelp.c" "$sourceRoot/docs/libcurl" "$sourceRoot/include/curl" "$sourceRoot/lib/libcurl.def" "$sourceRoot/lib/optiontable.pl" "$sourceRoot/lib/easyoptions.c" "$sourceRoot/projects/OS400/ccsidcurl.c"
    if ($LASTEXITCODE -ne 0) { throw "tar could not extract $sourceRoot from $archive." }

    $upstreamData = Join-Path $workFolder "$sourceRoot/tests/data"
    $upstreamTests = @(Get-ChildItem -LiteralPath $upstreamData -File -Filter 'test*')
    if ($upstreamTests.Count -eq 0) { throw "No test* files found in tests/data at $Tag." }

    # tests/certs without its build files (Makefile.*, CMakeLists.txt) or git's dot files:
    # the same set the release tarball ships (BL-1922, BL-2153).
    $upstreamCerts = @(Get-ChildItem -LiteralPath (Join-Path $workFolder "$sourceRoot/tests/certs") -File |
        Where-Object { $_.Name -notlike 'Makefile*' -and $_.Name -ne 'CMakeLists.txt' -and -not $_.Name.StartsWith('.') })
    if ($upstreamCerts.Count -eq 0) { throw "No files found in tests/certs at $Tag." }

    Get-ChildItem -LiteralPath $destination -File -Filter 'test*.curltest' | Remove-Item -Force
    foreach ($test in $upstreamTests) {
        Copy-Item -LiteralPath $test.FullName -Destination (Join-Path $destination ($test.Name + '.curltest'))
    }
    Copy-Item -LiteralPath (Join-Path $workFolder "$sourceRoot/COPYING") -Destination (Join-Path $destination 'COPYING') -Force

    # The files test1165.pl and test1275.pl read (BL-3407): configure.ac, m4/*.m4, CMakeLists.txt, lib/curl_config-cmake.h.in,
    # the lib and src C sources (*.c, *.h) and every Markdown file of the tree, at the same relative paths.
    & $tar -xzf $archive -C $workFolder --include "$sourceRoot/*.md" --include "$sourceRoot/lib/*" --include "$sourceRoot/src/*" --include "$sourceRoot/m4/*" --include "$sourceRoot/configure.ac" --include "$sourceRoot/CMakeLists.txt"
    if ($LASTEXITCODE -ne 0) { throw "tar could not extract the configure, C source and Markdown files from $archive." }
    $sourceRootFull = (Join-Path $workFolder $sourceRoot).TrimEnd('\') + '\'
    $scanFiles = @(Get-ChildItem -LiteralPath (Join-Path $workFolder $sourceRoot) -File -Recurse | Where-Object {
        $relative = $_.FullName.Substring($sourceRootFull.Length).Replace('\', '/')
        $relative -eq 'configure.ac' -or $relative -eq 'CMakeLists.txt' -or $relative -eq 'lib/curl_config-cmake.h.in' -or
        $_.Extension -eq '.md' -or
        (($_.Extension -eq '.c' -or $_.Extension -eq '.h') -and ($relative.StartsWith('lib/') -or $relative.StartsWith('src/'))) -or
        ($_.Extension -eq '.m4' -and $relative.StartsWith('m4/')) })
    if ($scanFiles.Count -eq 0) { throw "No configure, C source or Markdown files found at $Tag." }
    foreach ($scanFile in $scanFiles) {
        $scanRelative = $scanFile.FullName.Substring($sourceRootFull.Length)
        # Upstream's root README.md would overwrite this folder's own README.md, so it is not vendored.
        if ($scanRelative -eq 'README.md') { continue }
        $scanDestination = Join-Path $destination $scanRelative
        New-Item -ItemType Directory -Path (Split-Path -Parent $scanDestination) -Force | Out-Null
        Copy-Item -LiteralPath $scanFile.FullName -Destination $scanDestination -Force
    }


    $certsDestination = Join-Path $destination 'certs'
    New-Item -ItemType Directory -Path $certsDestination -Force | Out-Null
    Get-ChildItem -LiteralPath $certsDestination -File | Remove-Item -Force
    foreach ($certFile in $upstreamCerts) {
        Copy-Item -LiteralPath $certFile.FullName -Destination (Join-Path $certsDestination $certFile.Name)
    }

    # The data files cases name as %SRCDIR/data/<file>: only those the harness can use, one by one (BL-2183);
    # data-xml1 is the document the compressed-reply cases 222, 230, 232, 314, 396, 1123 and 1277 include (BL-2829).
    $dataDestination = Join-Path $destination 'data'
    New-Item -ItemType Directory -Path $dataDestination -Force | Out-Null
    Get-ChildItem -LiteralPath $dataDestination -File | Remove-Item -Force
    # data1461.txt and data1463.txt are curl's --help and --help file text, which cases 1461, 1463 and 1464 include (BL-2830).
    # data1400.c to data1407.c, data1420.c, data1465.c and data1481.c are the C the --libcurl cases expect (BL-2965).
    $libcurlFiles = '1400', '1401', '1402', '1403', '1404', '1405', '1406', '1407', '1420', '1465', '1481' | ForEach-Object { "data$_.c" }
    # data1705-1.md to -4.md, data1706-1.md to -4.md, data1705-stdout.1 and data1706-stdout.txt are the managen cases' pages and expected output (BL-3002).
    $managenFiles = @('1705', '1706' | ForEach-Object { $case = $_; '1', '2', '3', '4' | ForEach-Object { "data$case-$_.md" } }) + 'data1705-stdout.1', 'data1706-stdout.txt'
    foreach ($dataFile in @('data-httpsig-ed25519.key', 'data-httpsig-hmac-sha256.key', 'data-xml1', 'data1461.txt', 'data1463.txt') + $libcurlFiles + $managenFiles) {
        Copy-Item -LiteralPath (Join-Path $upstreamData $dataFile) -Destination (Join-Path $dataDestination $dataFile)
    }

    # The files the documentation check scripts test971.pl, test1486.pl and test1707.pl open (BL-2864):
    # docs/options-in-versions, docs/VERSIONS.md, docs/cmdline-opts/*.md and src/tool_writeout.c, at the
    # same relative paths. curl.txt is generated by upstream's managen at build time and is not vendored.
    $docsDestination = Join-Path $destination 'docs'
    $cmdlineOptsDestination = Join-Path $docsDestination 'cmdline-opts'
    New-Item -ItemType Directory -Path $cmdlineOptsDestination -Force | Out-Null
    Get-ChildItem -LiteralPath $cmdlineOptsDestination -File | Where-Object { $_.Name -ne 'curl.txt' } | Remove-Item -Force
    Copy-Item -LiteralPath (Join-Path $workFolder "$sourceRoot/docs/options-in-versions") -Destination (Join-Path $docsDestination 'options-in-versions') -Force
    Copy-Item -LiteralPath (Join-Path $workFolder "$sourceRoot/docs/VERSIONS.md") -Destination (Join-Path $docsDestination 'VERSIONS.md') -Force
    $upstreamOptionDocs = @(Get-ChildItem -LiteralPath (Join-Path $workFolder "$sourceRoot/docs/cmdline-opts") -File -Filter '*.md')
    if ($upstreamOptionDocs.Count -eq 0) { throw "No docs/cmdline-opts/*.md files found at $Tag." }
    foreach ($optionDoc in $upstreamOptionDocs) {
        Copy-Item -LiteralPath $optionDoc.FullName -Destination (Join-Path $cmdlineOptsDestination $optionDoc.Name)
    }
    $sourceDestination = Join-Path $destination 'src'
    New-Item -ItemType Directory -Path $sourceDestination -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $workFolder "$sourceRoot/src/tool_writeout.c") -Destination (Join-Path $sourceDestination 'tool_writeout.c') -Force

    Copy-Item -LiteralPath (Join-Path $workFolder "$sourceRoot/src/tool_getparam.c") -Destination (Join-Path $sourceDestination 'tool_getparam.c') -Force
    Copy-Item -LiteralPath (Join-Path $workFolder "$sourceRoot/src/tool_listhelp.c") -Destination (Join-Path $sourceDestination 'tool_listhelp.c') -Force

    # The files test1135.pl (case 1279), test1276.pl and test1544.pl open (BL-3391): lib/libcurl.def,
    # lib/optiontable.pl, lib/easyoptions.c and projects/OS400/ccsidcurl.c, at the same relative paths.
    foreach ($sourceAnalysisFile in @('lib/libcurl.def', 'lib/optiontable.pl', 'lib/easyoptions.c', 'projects/OS400/ccsidcurl.c')) {
        $sourceAnalysisDestination = Join-Path $destination $sourceAnalysisFile
        New-Item -ItemType Directory -Path (Split-Path -Parent $sourceAnalysisDestination) -Force | Out-Null
        Copy-Item -LiteralPath (Join-Path $workFolder "$sourceRoot/$sourceAnalysisFile") -Destination $sourceAnalysisDestination -Force
    }

    # The files the libcurl table check scripts test1119.pl, test1139.pl, test1175.pl, test1177.pl, test1222.pl,
    # test1477.pl and test1488.pl open (BL-2990): docs/libcurl (symbols-in-versions and the *.md pages, with
    # opts\*.md) and include/curl/*.h, at the same relative paths. curl_version_info.3, which test1177.pl reads,
    # is generated from curl_version_info.md at upstream's build time and is not vendored; lib/version.c is
    # not vendored either, because Libcurl's tables stand for it.
    $libcurlDocsDestination = Join-Path $docsDestination 'libcurl'
    $libcurlOptsDestination = Join-Path $libcurlDocsDestination 'opts'
    New-Item -ItemType Directory -Path $libcurlOptsDestination -Force | Out-Null
    Get-ChildItem -LiteralPath $libcurlDocsDestination -File | Remove-Item -Force
    Get-ChildItem -LiteralPath $libcurlOptsDestination -File | Remove-Item -Force
    $upstreamLibcurlDocs = @(Get-ChildItem -LiteralPath (Join-Path $workFolder "$sourceRoot/docs/libcurl") -File |
        Where-Object { $_.Extension -eq '.md' -or $_.Name -eq 'symbols-in-versions' })
    $upstreamLibcurlOptionDocs = @(Get-ChildItem -LiteralPath (Join-Path $workFolder "$sourceRoot/docs/libcurl/opts") -File -Filter '*.md')
    if ($upstreamLibcurlDocs.Count -eq 0 -or $upstreamLibcurlOptionDocs.Count -eq 0) { throw "No docs/libcurl files found at $Tag." }
    foreach ($libcurlDoc in $upstreamLibcurlDocs) {
        Copy-Item -LiteralPath $libcurlDoc.FullName -Destination (Join-Path $libcurlDocsDestination $libcurlDoc.Name)
    }
    foreach ($libcurlOptionDoc in $upstreamLibcurlOptionDocs) {
        Copy-Item -LiteralPath $libcurlOptionDoc.FullName -Destination (Join-Path $libcurlOptsDestination $libcurlOptionDoc.Name)
    }
    $includeDestination = Join-Path $destination 'include\curl'
    New-Item -ItemType Directory -Path $includeDestination -Force | Out-Null
    Get-ChildItem -LiteralPath $includeDestination -File | Remove-Item -Force
    $upstreamHeaders = @(Get-ChildItem -LiteralPath (Join-Path $workFolder "$sourceRoot/include/curl") -File -Filter '*.h')
    if ($upstreamHeaders.Count -eq 0) { throw "No include/curl/*.h files found at $Tag." }
    foreach ($header in $upstreamHeaders) {
        Copy-Item -LiteralPath $header.FullName -Destination (Join-Path $includeDestination $header.Name)
    }

    Write-Output "Vendored $($upstreamTests.Count) test files, $($upstreamCerts.Count) certs files, $($upstreamOptionDocs.Count) docs/cmdline-opts files, $($upstreamLibcurlDocs.Count) docs/libcurl files, $($upstreamLibcurlOptionDocs.Count) docs/libcurl/opts files, $($upstreamHeaders.Count) include/curl headers, the lib and projects source-analysis files, the httpsig key files, data-xml1, data1461.txt, data1463.txt, the managen case files and COPYING from $Tag into $destination."
    Write-Output "If the count changed, update VendoredTestFileCount in UpstreamTestDataTests.cs and the tag in README.md."
}
finally {
    Remove-Item -LiteralPath $workFolder -Recurse -Force -ErrorAction SilentlyContinue
}
