<#
.SYNOPSIS
    Builds the environment upstream inventory from a curl release and measures each
    environment variable and config-file rule through Curl.Console and the matched
    reference curl.

.DESCRIPTION
    Area environment of the gap analysis office (ADR-0433 decision 2). Formats are in
    Gap/Instructions/Gap-Format.md.

    Upstream sources, read as text from the release:
    - docs/cmdline-opts/_ENVIRONMENT.md: every "## `<NAME> ...`" heading is a variable,
      named by the first word inside the backticks.
    - docs/libcurl/libcurl-env.md: every "## `<NAME>`" heading under "# DESCRIPTION" is
      a variable. A name already given by _ENVIRONMENT.md is the same item.
    - docs/cmdline-opts/config.md: every "<n>) <text>" paragraph is a config-path item,
      in the document's order; each config-syntax rule below is an item when the sentence
      that states it is found in the document (whitespace folded to one space).

    Inventory, <InventoryDirectory>/<Version>/environment.json, keys:
    - environment:<NAME>                 kind variable, the name's case as upstream writes it.
    - environment:config-path:<n>        kind config-path, n the document's number.
    - environment:config-syntax:<slug>   kind config-syntax; q-disables is always present.
    Attributes are name and kind. introducedIn is the "(Added in <version>)" the
    document gives, else null.

    Recipes. A recipe sets up an environment and maybe a config file, runs both binaries
    and compares exit code, stdout, stderr and, for a recipe with a loopback server, the
    request bytes the server received and what the run wrote to a file the recipe names.
    Every run gets a fresh temporary home: HOME, USERPROFILE, APPDATA, CURL_HOME and
    XDG_CONFIG_HOME point at an empty folder unless the recipe points one at its own
    temporary folder or removes it, so no recipe reads or writes the user's real .curlrc,
    _curlrc, .netrc or environment.
    - Loopback server recipes: Record-CurlExchange.ps1 is the server, run once with
      -Curl <reference> and once with -Curl <candidate> on one port, with every variable
      these recipes set cleared from the process environment first. On Windows variable
      names are not case sensitive, so the case forms are the same variable.
      - http_proxy, HTTPS_PROXY, ALL_PROXY, NO_PROXY: the server is the proxy for
        "curl -s http://gap.invalid/". NO_PROXY=gap.invalid is set beside
        http_proxy=<the loopback proxy>.
      - [scheme]_proxy: ftp_proxy=<the loopback proxy>, "curl -s ftp://gap.invalid/".
      - [url-protocol]_PROXY: HTTPS_PROXY=<the loopback proxy>, "curl -s
        https://gap.invalid/", the proxy answering the CONNECT with 200.
      - NETRC: a netrc file holding "machine 127.0.0.1 login gapuser password gappass",
        NETRC naming it, "curl -s --netrc http://127.0.0.1:<port>/".
      - CURL_CA_BUNDLE, SSL_CERT_FILE, SSL_CERT_DIR: the server is a TLS server whose
        certificate a throwaway root issued; the variable names the root's PEM file (for
        SSL_CERT_DIR, a folder holding it as <OpenSSL subject hash>.0, as c_rehash names
        it), then "curl --no-progress-meter https://127.0.0.1:<port>/", with
        --ssl-revoke-best-effort on Windows, where Schannel's revocation check would
        otherwise fail on a root that names no CRL.
      - SSLKEYLOGFILE: the same TLS server, --cacert <the root> and SSLKEYLOGFILE naming
        a file; what was written there is compared as absent, empty, or each line label
        with its count, never the secrets.
    - Home variables: one config file per placement, each writing out
      "<variable>/<file>", so stdout names the file read. CURL_HOME: $CURL_HOME/.curlrc,
      $XDG_CONFIG_HOME/curlrc and $HOME/.curlrc. XDG_CONFIG_HOME: $XDG_CONFIG_HOME/curlrc
      and $HOME/.curlrc, CURL_HOME removed. HOME: $HOME/.curlrc and %USERPROFILE%\.curlrc,
      CURL_HOME and XDG_CONFIG_HOME removed. USERPROFILE (Windows only):
      %USERPROFILE%\Application Data\.curlrc, the other four removed. APPDATA (Windows
      only): %APPDATA%\.curlrc, the other four removed. Then "curl -s file:///<empty file>".
    - COLUMNS: COLUMNS=40, "curl -# -o gap-output.txt file:///<5-byte file>", comparing
      the progress bar on stderr.
    - config-path:<n>: a config file holding write-out = "gap-config-hit" in the location
      the item names (for item 8, the executable's folder, using copies of both binaries
      in a temporary folder), then "curl -s file:///<empty file>". Items 4 to 6 and 8 are
      Windows only. Item 7 (getpwuid) is non-Windows only: CURL_HOME, XDG_CONFIG_HOME and
      HOME are removed and the .curlrc goes in the user database's home folder, the real
      one, so it runs only on CI (GITHUB_ACTIONS is true), saving any .curlrc already there
      and putting it back; elsewhere it has no recipe.
    - config-syntax: whitespace-separator, colon-separator, equals-separator,
      dashed-long-option, dashed-no-separator, double-quotes, escape-sequences,
      backslash-other-letter, comment, stdin (-K -), url-option, q-disables (-q with a
      $CURL_HOME/.curlrc), windows-underscore-curlrc ($CURL_HOME/_curlrc, Windows only),
      one-option-per-line (a second option after the first on one line) and
      line-length-limit (a comment line one byte over 10 MB ahead of the option).

    Scoring:
    - An item for another platform only is excluded with reason platform:<os>:
      platform:windows, platform:unix, or platform:vms for SHELL, which only the VMS
      build reads (src/tool_vms.c).
    - An item that acts only with a curl -V feature the matched reference lacks is
      excluded with reason reference-lacks:<feature>: QLOGDIR needs HTTP3 and
      CURL_SSL_BACKEND needs MultiSSL. With no matching reference, or one with the
      feature, it has no recipe.
    - An item with no recipe is unmeasured with reason no-probe; off CI that includes
      config path 7.
    - With a matching reference: match when exit code, stdout, stderr, request bytes and
      what was written are equal, else gap. Stdout or stderr over 400 characters is shown
      in expected and actual as its first 200 and its length.
    - With no matching reference: a recipe whose result the document states (the config
      file is read, or -q skips it) is match when Curl exits 0 with that stdout, else gap;
      any other recipe is unmeasured with reason no-probe. reference is then null and
      referenceFallback "docs".

    Runs under Windows PowerShell 5.1 and PowerShell 7. -InventoryOnly uses no
    Windows-only API and runs under pwsh on Linux. The script is ASCII only.

.PARAMETER UpstreamRoot
    The extracted release folder holding docs/. Default: the folder
    Gap/Tools/Get-UpstreamRelease.ps1 -Version <Version> prints.

.PARAMETER Version
    The release version. Default: the version in Gap/Baselines/target.json.

.PARAMETER Candidate
    The Curl.Console binary. Default: Invoke-GapProbe.ps1's Get-GapCandidateCurl.

.PARAMETER InventoryDirectory
    The folder the inventory is written under, as <InventoryDirectory>/<Version>/. Default: the Gap/Upstream
    of the repository this script is in, never the measured -RepositoryRoot.
.PARAMETER RepositoryRoot
    The Curl tree written to. Default: the repository this script is in.

.PARAMETER OutFile
    Where to write the area measurement, normally <run>/measurements/environment.json.
    Required unless -InventoryOnly or -SelfTest is given.

.PARAMETER InventoryOnly
    Writes the inventory and stops; nothing is probed.

.PARAMETER ProbeResults
    A JSON file of canned recipe results used instead of running the recipes:
    { "<key>": { "reference": <run> or null, "candidate": <run> } }, each run
    { "exitCode", "stdout", "stderr", "request" } and optionally "written". A recipe key
    it lacks is measured as if no binary answered. "$reference" gives the reference's
    version line and "$referenceFeatures" its curl -V features.

.PARAMETER SelfTest
    Runs against the fake release in Gap/Tools/Fixtures/environment/upstream as version
    9.9.9 with the canned results in Gap/Tools/Fixtures/environment/probe-results.json,
    in a temporary folder, prints a PASS or FAIL line per check, and exits 1 on any FAIL.

.EXAMPLE
    Gap/Tools/Measure-EnvironmentGap.ps1 -OutFile ..\Curl.gap\2026-10-09_1430\measurements\environment.json

.EXAMPLE
    Gap/Tools/Measure-EnvironmentGap.ps1 -Version 8.21.0 -InventoryOnly
#>
[CmdletBinding()]
param(
    [string] $UpstreamRoot,
    [string] $Version,
    [string] $Candidate,
    [string] $RepositoryRoot,
    [string] $InventoryDirectory,
    [string] $OutFile,
    [switch] $InventoryOnly,
    [string] $ProbeResults,
    [switch] $SelfTest
)

$ErrorActionPreference = 'Stop'

$script:OnWindows = [System.IO.Path]::DirectorySeparatorChar -eq '\'
$script:Hit = 'gap-config-hit'
# The reference's curl -V features, or $null when no matching reference answered.
$script:ReferenceFeatures = $null

# Each config-syntax rule: its slug, the sentence (whitespace folded) that states it, and
# the config file text its recipe writes ($null: no recipe).
$script:SyntaxRules = @(
    @{ Slug = 'whitespace-separator'; Pattern = 'separated by whitespace'; Config = "write-out $Hit" }
    @{ Slug = 'colon-separator'; Pattern = 'separated by whitespace, colon'; Config = "write-out: $Hit" }
    @{ Slug = 'equals-separator'; Pattern = 'or the equals sign'; Config = "write-out = `"$Hit`"" }
    @{ Slug = 'dashed-long-option'; Pattern = 'specified with one or two dashes'; Config = "--write-out $Hit" }
    @{ Slug = 'dashed-no-separator'; Pattern = 'there can be no colon or equals character'; Config = "--write-out=$Hit" }
    @{ Slug = 'double-quotes'; Pattern = 'enclosed within double quotes'; Config = 'write-out = "gap config hit"' }
    @{ Slug = 'escape-sequences'; Pattern = 'escape sequences are available'; Config = 'write-out = "a\tb\\c\"d\ne"' }
    @{ Slug = 'backslash-other-letter'; Pattern = 'backslash preceding any other letter is ignored'; Config = 'write-out = "g\ap"' }
    @{ Slug = 'comment'; Pattern = 'line is treated as a comment'; Config = "   # write-out = bad`nwrite-out = $Hit" }
    @{ Slug = 'one-option-per-line'; Pattern = 'one option per physical line'; Config = "write-out = `"$Hit`" --show-error" }
    # Get-Recipe puts a comment line of 10 MB and one byte ahead of this line.
    @{ Slug = 'line-length-limit'; Pattern = 'no more than 10 megabytes'; Config = "write-out = $Hit" }
    @{ Slug = 'stdin'; Pattern = 'read the file from stdin'; Config = "write-out = $Hit" }
    @{ Slug = 'url-option'; Pattern = 'using the --url option'; Config = "write-out = $Hit" }
    @{ Slug = 'windows-underscore-curlrc'; Pattern = 'two filenames are checked per location'; Config = "write-out = $Hit" }
)

function Write-Utf8File([string] $Path, [string] $Text) {
    $folder = Split-Path $Path -Parent
    if ($folder -and -not (Test-Path -LiteralPath $folder)) { New-Item -ItemType Directory -Path $folder -Force | Out-Null }
    [System.IO.File]::WriteAllText($Path, $Text, (New-Object System.Text.UTF8Encoding($false)))
}

function New-InventoryItem([string] $Key, [string] $Name, [string] $Kind, $IntroducedIn) {
    return [ordered]@{ key = $Key; name = $Name; introducedIn = $IntroducedIn; attributes = [ordered]@{ name = $Name; kind = $Kind } }
}

function Read-VariableNames([string] $Path, [string] $Section) {
    $names = New-Object System.Collections.Generic.List[string]
    $inSection = [string]::IsNullOrEmpty($Section)
    foreach ($line in [System.IO.File]::ReadAllLines($Path)) {
        if ($line -match '^# (.+?)\s*$') { $inSection = [string]::IsNullOrEmpty($Section) -or $Matches[1] -eq $Section; continue }
        if ($inSection -and $line -match '^## `([^`\s]+)') { $names.Add($Matches[1]) }
    }
    return $names
}

function Read-ConfigPaths([string] $Text) {
    $paths = New-Object System.Collections.Generic.List[object]
    foreach ($m in [regex]::Matches($Text, '(?ms)^(\d+)\) (.+?)(?=\r?\n\s*\r?\n|\z)')) {
        $name = (($m.Groups[2].Value -replace '\*\*', '') -replace '\s+', ' ').Trim()
        $added = [regex]::Match($name, '\(Added in (\d+(?:\.\d+)+)\)')
        $paths.Add([pscustomobject]@{ Number = [int]$m.Groups[1].Value; Name = $name; Added = $(if ($added.Success) { $added.Groups[1].Value } else { $null }) })
    }
    return $paths
}

function Get-Inventory([string] $Root, [string] $ReleaseVersion) {
    $items = New-Object System.Collections.Generic.List[object]
    $seen = @{}
    $names = @(Read-VariableNames (Join-Path $Root 'docs/cmdline-opts/_ENVIRONMENT.md') '') + @(Read-VariableNames (Join-Path $Root 'docs/libcurl/libcurl-env.md') 'DESCRIPTION')
    foreach ($name in $names) {
        if ($seen.ContainsKey("v:$name")) { continue }
        $seen["v:$name"] = $true
        $items.Add((New-InventoryItem "environment:$name" $name 'variable' $null))
    }
    $config = [System.IO.File]::ReadAllText((Join-Path $Root 'docs/cmdline-opts/config.md'))
    foreach ($path in (Read-ConfigPaths $config)) {
        $items.Add((New-InventoryItem "environment:config-path:$($path.Number)" $path.Name 'config-path' $path.Added))
    }
    $folded = $config -replace '\s+', ' '
    foreach ($rule in $script:SyntaxRules) {
        if ($folded.Contains($rule.Pattern)) { $items.Add((New-InventoryItem "environment:config-syntax:$($rule.Slug)" $rule.Slug 'config-syntax' $null)) }
    }
    $items.Add((New-InventoryItem 'environment:config-syntax:q-disables' 'q-disables' 'config-syntax' $null))
    $byKey = @{}; foreach ($i in $items) { $byKey[$i.key] = $i }
    $sorted = [string[]]@($byKey.Keys)
    [Array]::Sort($sorted, [StringComparer]::Ordinal)
    return [ordered]@{
        area = 'environment'
        version = $ReleaseVersion
        sources = @('docs/cmdline-opts/_ENVIRONMENT.md', 'docs/libcurl/libcurl-env.md', 'docs/cmdline-opts/config.md')
        items = @($sorted | ForEach-Object { $byKey[$_] })
    }
}

function Get-ItemPlatform($Item) {
    $name = $Item.attributes.name
    if ($Item.attributes.kind -eq 'config-path') {
        if ($name -match '^Non-Windows') { return 'unix' }
        if ($name -match '^(On )?Windows') { return 'windows' }
    }
    if ($name -eq 'windows-underscore-curlrc') { return 'windows' }
    if ($Item.attributes.kind -eq 'variable') {
        if ($name -ceq 'APPDATA' -or $name -ceq 'USERPROFILE') { return 'windows' }
        # src/tool_vms.c reads SHELL, and only the VMS build compiles it.
        if ($name -ceq 'SHELL') { return 'vms' }
    }
    return $null
}

# Whether a run on Platform is on the item's platform: windows, unix (any non-Windows) or
# vms (never, since no run is on VMS).
function Test-OnItemPlatform([string] $ItemPlatform, [string] $Platform) {
    switch ($ItemPlatform) {
        'windows' { return $Platform -eq 'windows' }
        'unix' { return $Platform -ne 'windows' }
    }
    return $false
}

# The curl -V feature a variable acts only with: QLOGDIR needs HTTP3, CURL_SSL_BACKEND
# MultiSSL. $null for every other item.
function Get-ItemFeature($Item) {
    if ($Item.attributes.kind -ne 'variable') { return $null }
    switch -CaseSensitive ($Item.attributes.name) {
        'QLOGDIR' { return 'HTTP3' }
        'CURL_SSL_BACKEND' { return 'MultiSSL' }
    }
    return $null
}

function New-TempFolder([string] $Prefix) {
    $path = Join-Path ([System.IO.Path]::GetTempPath()) ($Prefix + [guid]::NewGuid().ToString('N'))
    [void] [System.IO.Directory]::CreateDirectory($path)
    return $path
}

function ConvertTo-FileUrl([string] $Path) { return 'file:///' + (($Path -replace '\\', '/') -replace ' ', '%20').TrimStart('/') }

function ConvertFrom-GapRun($Run) {
    if ($null -eq $Run) { return $null }
    return [pscustomobject]@{ exitCode = $Run.ExitCode; stdout = [System.Text.Encoding]::UTF8.GetString($Run.Stdout); stderr = $Run.Stderr; request = $null; written = $null }
}

# Runs both binaries with a config file at <temp>/<RelativePath> and every variable in
# PointAt set to <temp>; -q, -K - or a config-supplied URL come from Mode.
function Invoke-ConfigRecipe([string] $RelativePath, [string] $Content, [string[]] $PointAt, [string] $Mode = 'plain') {
    $folder = New-TempFolder 'gap-env-'
    try {
        $empty = Join-Path $folder 'gap-empty.txt'
        Write-Utf8File $empty ''
        $url = ConvertTo-FileUrl $empty
        if ($Mode -eq 'url-option') { $Content = "$Content`nurl = `"$url`"" }
        $environment = @{}
        foreach ($name in $PointAt) { $environment[$name] = $folder }
        $arguments = @('-s', $url)
        $stdin = $null
        if ($Mode -eq 'q') { $arguments = @('-q', '-s', $url) }
        if ($Mode -eq 'url-option') { $arguments = @('-s') }
        if ($Mode -eq 'stdin') { $arguments = @('-s', '-K', '-', $url); $stdin = [System.Text.Encoding]::ASCII.GetBytes($Content + "`n") }
        if ($Mode -ne 'stdin') { Write-Utf8File (Join-Path $folder $RelativePath) ($Content + "`n") }
        $probe = Invoke-GapProbe -Arguments $arguments -Environment $environment -WorkingDirectory $folder -StandardInput $stdin -CandidatePath $script:CandidatePath
        return [pscustomobject]@{ Reference = (ConvertFrom-GapRun $probe.Reference); Candidate = (ConvertFrom-GapRun $probe.Candidate) }
    } finally {
        Remove-Item -LiteralPath $folder -Recurse -Force -ErrorAction SilentlyContinue
    }
}

# Copies both binaries to a temporary folder each, puts a .curlrc beside them and runs
# them there, so nothing is written next to the installed binaries.
function Invoke-ExecutableFolderRecipe {
    $folder = New-TempFolder 'gap-env-exe-'
    try {
        $reference = Get-GapReferenceCurl
        $candidatePath = Get-GapCandidateCurl -Path $script:CandidatePath
        $candidateCopy = Join-Path $folder 'candidate'
        Copy-Item -LiteralPath (Split-Path $candidatePath -Parent) -Destination $candidateCopy -Recurse
        Write-Utf8File (Join-Path $candidateCopy '.curlrc') "write-out = $Hit`n"
        $referenceCopy = $null
        if ($null -ne $reference -and $reference.Matches) {
            $referenceFolder = Join-Path $folder 'reference'
            [void] [System.IO.Directory]::CreateDirectory($referenceFolder)
            Copy-Item -LiteralPath $reference.Path -Destination $referenceFolder
            Get-ChildItem -LiteralPath (Split-Path $reference.Path -Parent) -Filter '*.dll' | Copy-Item -Destination $referenceFolder
            Write-Utf8File (Join-Path $referenceFolder '.curlrc') "write-out = $Hit`n"
            $referenceCopy = Join-Path $referenceFolder (Split-Path $reference.Path -Leaf)
        }
        $empty = Join-Path $folder 'gap-empty.txt'
        Write-Utf8File $empty ''
        $arguments = @('-s', (ConvertTo-FileUrl $empty))
        $candidateRun = Invoke-GapRun -Path (Join-Path $candidateCopy (Split-Path $candidatePath -Leaf)) -Arguments $arguments -Environment @{} -WorkingDirectory $folder -TimeoutSeconds 20
        $referenceRun = $null
        if ($null -ne $referenceCopy) { $referenceRun = Invoke-GapRun -Path $referenceCopy -Arguments $arguments -Environment @{} -WorkingDirectory $folder -TimeoutSeconds 20 }
        return [pscustomobject]@{ Reference = (ConvertFrom-GapRun $referenceRun); Candidate = (ConvertFrom-GapRun $candidateRun) }
    } finally {
        Remove-Item -LiteralPath $folder -Recurse -Force -ErrorAction SilentlyContinue
    }
}

# Runs both binaries with CURL_HOME, XDG_CONFIG_HOME and HOME removed and a .curlrc in the
# home folder the user database names (getpwuid, read here through sh's ~user), so stdout
# shows whether that file was read. The folder is the real home, so any .curlrc already
# there is saved first and put back afterwards.
function Invoke-AccountHomeRecipe {
    $accountHome = (& sh -c 'eval echo ~$(id -un)' | Select-Object -First 1).Trim()
    $curlrc = Join-Path $accountHome '.curlrc'
    $saved = $null
    if (Test-Path -LiteralPath $curlrc) { $saved = [System.IO.File]::ReadAllBytes($curlrc) }
    $folder = New-TempFolder 'gap-env-account-'
    try {
        Write-Utf8File $curlrc "write-out = $Hit`n"
        $empty = Join-Path $folder 'gap-empty.txt'
        Write-Utf8File $empty ''
        $probe = Invoke-GapProbe -Arguments @('-s', (ConvertTo-FileUrl $empty)) -Environment @{ CURL_HOME = $null; XDG_CONFIG_HOME = $null; HOME = $null } -WorkingDirectory $folder -CandidatePath $script:CandidatePath
        return [pscustomobject]@{ Reference = (ConvertFrom-GapRun $probe.Reference); Candidate = (ConvertFrom-GapRun $probe.Candidate) }
    } finally {
        if ($null -eq $saved) { Remove-Item -LiteralPath $curlrc -Force -ErrorAction SilentlyContinue }
        else { [System.IO.File]::WriteAllBytes($curlrc, $saved) }
        Remove-Item -LiteralPath $folder -Recurse -Force -ErrorAction SilentlyContinue
    }
}

# Runs both binaries with one config file per placement, each placement's variable pointing
# at that variable's own temporary folder, every name in Unset removed and every other home
# variable pointing at an empty folder (Invoke-GapRun). Each file writes out
# "<variable>/<file>", so stdout names the file that was read.
function Invoke-HomeRecipe([object[]] $Placements, [string[]] $Unset) {
    $folder = New-TempFolder 'gap-env-home-'
    try {
        $empty = Join-Path $folder 'gap-empty.txt'
        Write-Utf8File $empty ''
        $environment = @{}
        foreach ($name in $Unset) { $environment[$name] = $null }
        foreach ($placement in $Placements) {
            $homeFolder = Join-Path $folder $placement.Variable
            $environment[$placement.Variable] = $homeFolder
            Write-Utf8File (Join-Path $homeFolder $placement.File) "write-out = `"$($placement.Variable)/$($placement.File)`"`n"
        }
        $probe = Invoke-GapProbe -Arguments @('-s', (ConvertTo-FileUrl $empty)) -Environment $environment -WorkingDirectory $folder -CandidatePath $script:CandidatePath
        return [pscustomobject]@{ Reference = (ConvertFrom-GapRun $probe.Reference); Candidate = (ConvertFrom-GapRun $probe.Candidate) }
    } finally {
        Remove-Item -LiteralPath $folder -Recurse -Force -ErrorAction SilentlyContinue
    }
}

# Runs both binaries with Variables set, in a temporary folder holding gap-input.txt
# ("hello"); {INPUT_URL} in Arguments becomes that file's file:// URL.
function Invoke-CommandRecipe([hashtable] $Variables, [string[]] $Arguments) {
    $folder = New-TempFolder 'gap-env-command-'
    try {
        $inputFile = Join-Path $folder 'gap-input.txt'
        Write-Utf8File $inputFile 'hello'
        $url = ConvertTo-FileUrl $inputFile
        $resolved = @($Arguments | ForEach-Object { $_.Replace('{INPUT_URL}', $url) })
        $probe = Invoke-GapProbe -Arguments $resolved -Environment $Variables -WorkingDirectory $folder -CandidatePath $script:CandidatePath
        return [pscustomobject]@{ Reference = (ConvertFrom-GapRun $probe.Reference); Candidate = (ConvertFrom-GapRun $probe.Candidate) }
    } finally {
        Remove-Item -LiteralPath $folder -Recurse -Force -ErrorAction SilentlyContinue
    }
}

# Every variable a recorded recipe may set; each recorded run clears them all first.
$script:RecordedVariableNames = @('http_proxy', 'HTTP_PROXY', 'https_proxy', 'HTTPS_PROXY', 'ftp_proxy', 'FTP_PROXY', 'all_proxy', 'ALL_PROXY', 'no_proxy', 'NO_PROXY', 'NETRC', 'CURL_CA_BUNDLE', 'SSL_CERT_FILE', 'SSL_CERT_DIR', 'SSLKEYLOGFILE', 'HOME', 'USERPROFILE', 'APPDATA', 'CURL_HOME', 'XDG_CONFIG_HOME')

# Set for every recorded run, so the recorder's own pwsh (and any dotnet it starts) sends
# nothing through a recipe's proxy. Under an empty HOME, pwsh 7 sends its startup telemetry
# to dc.services.visualstudio.com through HTTPS_PROXY - the recorder's own port - and that
# CONNECT was measured as curl's (GF-0097's [url-protocol]_PROXY item, BL-4382).
$script:RecorderQuietVariables = @{ POWERSHELL_TELEMETRY_OPTOUT = '1'; POWERSHELL_UPDATECHECK = 'Off'; DOTNET_CLI_TELEMETRY_OPTOUT = '1'; DOTNET_NOLOGO = '1' }

# The subject Record-CurlExchange.ps1 gives its throwaway root (New-IssuedByThrowawayRoot).
$script:ThrowawayRootCommonName = 'Record-CurlExchange throwaway root'

# OpenSSL's subject hash (X509_NAME_hash) of a name holding one common name: the SHA-1 of
# the name's canonical encoding (value lower-cased, whitespace folded, as a UTF8String),
# its first four bytes read little-endian, as eight hex digits. c_rehash names a CA file
# in a --capath folder <hash>.0.
function Get-OpenSslSubjectHash([string] $CommonName) {
    $value = [System.Text.Encoding]::UTF8.GetBytes((($CommonName.Trim() -replace '\s+', ' ').ToLowerInvariant()))
    $attribute = [byte[]](@(0x06, 0x03, 0x55, 0x04, 0x03, 0x0C, $value.Length) + $value)
    $sequence = [byte[]](@(0x30, $attribute.Length) + $attribute)
    $set = [byte[]](@(0x31, $sequence.Length) + $sequence)
    $sha1 = [System.Security.Cryptography.SHA1]::Create()
    try { $digest = $sha1.ComputeHash($set) } finally { $sha1.Dispose() }
    return '{0:x8}' -f [System.BitConverter]::ToUInt32($digest, 0)
}

# What a run wrote to the recipe's Written file: absent, empty, or each distinct first word
# of its lines (a key log's labels) with its count, so secrets never reach the measurement.
function Get-WrittenSummary([string] $Path) {
    if (-not (Test-Path -LiteralPath $Path)) { return 'absent' }
    $labels = @([System.IO.File]::ReadAllLines($Path) | Where-Object { $_ -and -not $_.StartsWith('#') } | ForEach-Object { ($_ -split ' ')[0] })
    if ($labels.Count -eq 0) { return 'empty' }
    $sorted = [string[]]@($labels | Select-Object -Unique)
    [Array]::Sort($sorted, [StringComparer]::Ordinal)
    return (@($sorted | ForEach-Object { $label = $_; "$label x$(@($labels | Where-Object { $_ -ceq $label }).Count)" }) -join ', ')
}

# Runs Record-CurlExchange.ps1 as the loopback server on Port for one binary. The process
# environment is cleared of every recorded-recipe variable, the homes point at an empty
# folder, the recipe's Directories and Files are made in a temporary folder, and its
# Variables are set. In Variables, Files, CurlArgs and RecorderArgs, {SERVER} becomes
# http://127.0.0.1:<port>, {PORT} the port and {FOLDER} the temporary folder. Written, when
# given, names a file in that folder summarised as the run's written field.
function Invoke-RecordedRun([string] $Curl, [hashtable] $Recipe, [int] $Port) {
    $folder = New-TempFolder 'gap-env-recorded-'
    $saved = @{}
    foreach ($name in @($script:RecordedVariableNames) + @($script:RecorderQuietVariables.Keys)) { $saved[$name] = [Environment]::GetEnvironmentVariable($name) }
    try {
        foreach ($name in $script:RecorderQuietVariables.Keys) { [Environment]::SetEnvironmentVariable($name, $script:RecorderQuietVariables[$name]) }
        $resolve ={ param([string] $Text) $Text.Replace('{SERVER}', "http://127.0.0.1:$port").Replace('{PORT}', [string]$port).Replace('{FOLDER}', $folder) }
        $emptyHome = Join-Path $folder 'gap-home'
        [void] [System.IO.Directory]::CreateDirectory($emptyHome)
        foreach ($directory in @($Recipe.Directories)) { if ($directory) { [void] [System.IO.Directory]::CreateDirectory((& $resolve $directory)) } }
        if ($null -ne $Recipe.Files) { foreach ($file in $Recipe.Files.Keys) { Write-Utf8File (Join-Path $folder $file) (& $resolve $Recipe.Files[$file]) } }
        foreach ($name in $script:RecordedVariableNames) { [Environment]::SetEnvironmentVariable($name, $null) }
        foreach ($name in 'HOME', 'USERPROFILE', 'APPDATA', 'CURL_HOME', 'XDG_CONFIG_HOME') { [Environment]::SetEnvironmentVariable($name, $emptyHome) }
        foreach ($name in $Recipe.Variables.Keys) { [Environment]::SetEnvironmentVariable($name, (& $resolve $Recipe.Variables[$name])) }
        $out = Join-Path $folder 'out'
        $launch = Get-GapLaunch -Path (Join-Path $script:RepositoryRootPath 'Record-CurlExchange.ps1')
        $prefix = @($launch.Prefix)
        $curlArgs = (@($Recipe.CurlArgs | ForEach-Object { & $resolve $_ }) -join ',')
        $recorderArgs = @(@($Recipe.RecorderArgs) | Where-Object { $_ } | ForEach-Object { & $resolve $_ })
        & $launch.FileName @prefix -Port $port -Curl $Curl -CurlArgs $curlArgs -OutDirectory $out @recorderArgs | Out-Null
        $requestPath = Join-Path $out 'request.bin'
        $request = if (Test-Path -LiteralPath $requestPath) { [System.Text.Encoding]::GetEncoding(28591).GetString([System.IO.File]::ReadAllBytes($requestPath)) } else { '' }
        $written = if ($Recipe.Written) { Get-WrittenSummary (Join-Path $folder $Recipe.Written) } else { $null }
        return [pscustomobject]@{
            exitCode = [int]([System.IO.File]::ReadAllText((Join-Path $out 'exitcode.txt')).Trim())
            stdout = [System.Text.Encoding]::UTF8.GetString([System.IO.File]::ReadAllBytes((Join-Path $out 'stdout.bin')))
            stderr = [System.Text.Encoding]::UTF8.GetString([System.IO.File]::ReadAllBytes((Join-Path $out 'stderr.txt')))
            request = $request
            written = $written
        }
    } finally {
        foreach ($name in $saved.Keys) { [Environment]::SetEnvironmentVariable($name, $saved[$name]) }
        Remove-Item -LiteralPath $folder -Recurse -Force -ErrorAction SilentlyContinue
    }
}

function Invoke-RecordedRecipe([hashtable] $Recipe) {
    # One port for both runs, so the Host header and any URL holding the port are the same.
    $listener = New-Object System.Net.Sockets.TcpListener([System.Net.IPAddress]::Loopback, 0)
    $listener.Start(); $port = $listener.LocalEndpoint.Port; $listener.Stop()
    $reference = Get-GapReferenceCurl
    $referenceRun = $null
    if ($null -ne $reference -and $reference.Matches) { $referenceRun = Invoke-RecordedRun $reference.Path $Recipe $port }
    return [pscustomobject]@{ Reference = $referenceRun; Candidate = (Invoke-RecordedRun (Get-GapCandidateCurl -Path $script:CandidatePath) $Recipe $port) }
}

# A recorded recipe: Variables to set, CurlArgs (no commas: Record-CurlExchange.ps1 under
# -File splits its list at commas), RecorderArgs, Files and Directories to make, Written.
function New-RecordedRecipe([hashtable] $Variables, [string[]] $CurlArgs, [string[]] $RecorderArgs = @(), [hashtable] $Files = @{}, [string[]] $Directories = @(), [string] $Written = $null) {
    return @{ Variables = $Variables; CurlArgs = $CurlArgs; RecorderArgs = $RecorderArgs; Files = $Files; Directories = $Directories; Written = $Written }
}

# A loopback TLS server serving a certificate issued by a throwaway root whose PEM is
# written to RootFile; Record-CurlExchange.ps1 makes the key with RSACng, so Windows only.
function Get-TlsRecorderArgs([string] $RootFile) { return @('-Tls', '-TlsRootCertificateFile', $RootFile) }

# A recipe's Run is called with its Arguments. It must not be a .GetNewClosure() block built
# from the values: a closure is bound to a new module that cannot see this script's functions
# (BL-1747), so the values travel as Arguments to a block that is defined here, at script scope.
$script:RunConfigRecipe = { param([object[]] $RecipeArguments) Invoke-ConfigRecipe $RecipeArguments[0] $RecipeArguments[1] $RecipeArguments[2] $RecipeArguments[3] }
$script:RunHomeRecipe = { param([object[]] $RecipeArguments) Invoke-HomeRecipe $RecipeArguments[0] $RecipeArguments[1] }
$script:RunCommandRecipe = { param([object[]] $RecipeArguments) Invoke-CommandRecipe $RecipeArguments[0] $RecipeArguments[1] }
$script:RunRecordedRecipe = { param([object[]] $RecipeArguments) Invoke-RecordedRecipe $RecipeArguments[0] }

# The recipe for a key: { Run (scriptblock called with Arguments), Arguments, Documented (the stdout the document states,
# or $null) }, or $null when the key has none.
function Get-Recipe($Item) {
    $key = $Item.key
    $plainGet = @('http://gap.invalid/', '-s')
    $root = '{FOLDER}/gap-root.pem'
    # Each recorded command line starts with its URL: pwsh -File takes a -CurlArgs value
    # starting with a dash for a parameter name.
    $tlsGet = @('https://127.0.0.1:{PORT}/', '--no-progress-meter')
    if ($script:OnWindows) { $tlsGet += '--ssl-revoke-best-effort' }
    $recorded = $null
    switch -CaseSensitive ($key) {
        'environment:http_proxy' { $recorded = New-RecordedRecipe @{ http_proxy = '{SERVER}' } $plainGet }
        'environment:HTTPS_PROXY' { $recorded = New-RecordedRecipe @{ HTTPS_PROXY = '{SERVER}' } $plainGet }
        'environment:ALL_PROXY' { $recorded = New-RecordedRecipe @{ ALL_PROXY = '{SERVER}' } $plainGet }
        'environment:NO_PROXY' { $recorded = New-RecordedRecipe @{ http_proxy = '{SERVER}'; NO_PROXY = 'gap.invalid' } $plainGet }
        'environment:[scheme]_proxy' { $recorded = New-RecordedRecipe @{ ftp_proxy = '{SERVER}' } @('ftp://gap.invalid/', '-s') }
        'environment:[url-protocol]_PROXY' { $recorded = New-RecordedRecipe @{ HTTPS_PROXY = '{SERVER}' } @('https://gap.invalid/', '-s') @('-Response', 'HTTP/1.1 200 Connection established\r\n\r\n') }
        'environment:NETRC' { $recorded = New-RecordedRecipe @{ NETRC = '{FOLDER}/gap-netrc' } @('http://127.0.0.1:{PORT}/', '-s', '--netrc') @() @{ 'gap-netrc' = "machine 127.0.0.1 login gapuser password gappass`n" } }
        'environment:config-syntax:q-disables' { return @{ Run = $script:RunConfigRecipe; Arguments = @('.curlrc', "write-out = $Hit", @('CURL_HOME'), 'q'); Documented = '' } }
        'environment:COLUMNS' { return @{ Run = $script:RunCommandRecipe; Arguments = @(@{ COLUMNS = '40' }, @('-#', '-o', 'gap-output.txt', '{INPUT_URL}')); Documented = $null } }
        'environment:CURL_CA_BUNDLE' { $recorded = New-RecordedRecipe @{ CURL_CA_BUNDLE = $root } $tlsGet (Get-TlsRecorderArgs $root) }
        'environment:SSL_CERT_FILE' { $recorded = New-RecordedRecipe @{ SSL_CERT_FILE = $root } $tlsGet (Get-TlsRecorderArgs $root) }
        'environment:SSL_CERT_DIR' {
            $hashed = "{FOLDER}/gap-certs/$(Get-OpenSslSubjectHash $script:ThrowawayRootCommonName).0"
            $recorded = New-RecordedRecipe @{ SSL_CERT_DIR = '{FOLDER}/gap-certs' } $tlsGet (Get-TlsRecorderArgs $hashed) @{} @('{FOLDER}/gap-certs')
        }
        'environment:SSLKEYLOGFILE' { $recorded = New-RecordedRecipe @{ SSLKEYLOGFILE = '{FOLDER}/gap-keylog.txt' } ($tlsGet + @('--cacert', $root)) (Get-TlsRecorderArgs $root) @{} @() 'gap-keylog.txt' }
    }
    if ($null -ne $recorded) { return @{ Run = $script:RunRecordedRecipe; Arguments = @(, $recorded); Documented = $null } }
    if ($Item.attributes.kind -eq 'variable') {
        # Each home variable's file, with the variables that would be read ahead of it removed.
        $homes = @{
            'CURL_HOME' = @(@(@{ Variable = 'CURL_HOME'; File = '.curlrc' }, @{ Variable = 'XDG_CONFIG_HOME'; File = 'curlrc' }, @{ Variable = 'HOME'; File = '.curlrc' }), @())
            'XDG_CONFIG_HOME' = @(@(@{ Variable = 'XDG_CONFIG_HOME'; File = 'curlrc' }, @{ Variable = 'HOME'; File = '.curlrc' }), @('CURL_HOME'))
            'HOME' = @(@(@{ Variable = 'HOME'; File = '.curlrc' }, @{ Variable = 'USERPROFILE'; File = '.curlrc' }), @('CURL_HOME', 'XDG_CONFIG_HOME'))
            'USERPROFILE' = @(@(, @{ Variable = 'USERPROFILE'; File = 'Application Data/.curlrc' }), @('CURL_HOME', 'XDG_CONFIG_HOME', 'HOME', 'APPDATA'))
            'APPDATA' = @(@(, @{ Variable = 'APPDATA'; File = '.curlrc' }), @('CURL_HOME', 'XDG_CONFIG_HOME', 'HOME', 'USERPROFILE'))
        }
        $name = $Item.attributes.name
        if ($homes.ContainsKey($name)) {
            $first = $homes[$name][0][0]
            return @{ Run = $script:RunHomeRecipe; Arguments = $homes[$name]; Documented = "$($first.Variable)/$($first.File)" }
        }
        return $null
    }
    if ($Item.attributes.kind -eq 'config-path') {
        $name = $Item.attributes.name
        $placements = @(
            @{ Pattern = '\$CURL_HOME/\.curlrc'; File = '.curlrc'; Variable = 'CURL_HOME' }
            @{ Pattern = '\$XDG_CONFIG_HOME/curlrc'; File = 'curlrc'; Variable = 'XDG_CONFIG_HOME' }
            @{ Pattern = '\$HOME/\.curlrc'; File = '.curlrc'; Variable = 'HOME' }
            @{ Pattern = '%USERPROFILE%\\Application Data\\\.curlrc'; File = 'Application Data/.curlrc'; Variable = 'USERPROFILE' }
            @{ Pattern = '%USERPROFILE%\\\.curlrc'; File = '.curlrc'; Variable = 'USERPROFILE' }
            @{ Pattern = '%APPDATA%\\\.curlrc'; File = '.curlrc'; Variable = 'APPDATA' }
        )
        foreach ($p in $placements) {
            if ($name -match $p.Pattern) {
                $file = $p.File; $variable = $p.Variable
                return @{ Run = $script:RunConfigRecipe; Arguments = @($file, "write-out = `"$Hit`"", @($variable), 'plain'); Documented = $Hit }
            }
        }
        if ($name -match 'same directory the curl executable') { return @{ Run = { Invoke-ExecutableFolderRecipe }; Arguments = $null; Documented = $Hit } }
        # The user database's home is the real one, so only a CI runner's is written to.
        if ($name -match 'getpwuid' -and -not $script:OnWindows -and $env:GITHUB_ACTIONS -eq 'true') { return @{ Run = { Invoke-AccountHomeRecipe }; Arguments = $null; Documented = $Hit } }
        return $null
    }
    if ($Item.attributes.kind -eq 'config-syntax') {
        $rule = $script:SyntaxRules | Where-Object { $_.Slug -eq $Item.attributes.name } | Select-Object -First 1
        if ($null -eq $rule -or $null -eq $rule.Config) { return $null }
        $config = $rule.Config; $slug = $rule.Slug
        # One byte over upstream's 10 MB limit (8.2.0), in a comment line so an accepted line prints nothing.
        if ($slug -eq 'line-length-limit') { $config = '#' + ('x' * (10 * 1024 * 1024)) + "`n" + $config }
        $file =if ($slug -eq 'windows-underscore-curlrc') { '_curlrc' } else { '.curlrc' }
        $mode = if ($slug -eq 'stdin' -or $slug -eq 'url-option') { $slug } else { 'plain' }
        # The document states no result for these three rules' recipes.
        $documented = if ($config.Contains($Hit) -and $slug -notin @('dashed-no-separator', 'one-option-per-line', 'line-length-limit')) { $Hit } else { $null }
        return @{ Run = $script:RunConfigRecipe; Arguments = @($file, $config, @('CURL_HOME'), $mode); Documented = $documented }
    }
    return $null
}

# Text over 400 characters is shown as its first 200 and its length, so a recipe whose
# binary echoes a 10 MB line keeps the measurement small; Test-SameRun compares it whole.
function Format-Text([string] $Text) {
    if ($Text.Length -le 400) { return $Text }
    return $Text.Substring(0, 200) + "... ($($Text.Length) characters)"
}

function Format-Run($Run) {
    if ($null -eq $Run) { return $null }
    $requestLine = if ([string]::IsNullOrEmpty($Run.request)) { 'none' } else { ($Run.request -split "`r?`n")[0] }
    $text = "exit $($Run.exitCode); stdout '$(Format-Text ([string]$Run.stdout))'; stderr '$(Format-Text (([string]$Run.stderr) -replace '\s+$', ''))'; request $requestLine"
    $written = $Run.PSObject.Properties['written']
    if ($null -ne $written -and $null -ne $written.Value) { $text += "; written $($written.Value)" }
    return $text
}

function Test-SameRun($A, $B) {
    foreach ($field in 'exitCode', 'stdout', 'stderr', 'request', 'written') {
        $left = $A.PSObject.Properties[$field]; $right = $B.PSObject.Properties[$field]
        $leftValue = if ($null -ne $left) { [string]$left.Value } else { '' }
        $rightValue = if ($null -ne $right) { [string]$right.Value } else { '' }
        if ($leftValue -cne $rightValue) { return $false }
    }
    return $true
}

function Measure-Item($Item, [string] $Platform, $Canned) {
    $result = [ordered]@{ key = $Item.key; state = $null; reason = $null; expected = $null; actual = $null; evidence = $null; introducedIn = $Item.introducedIn }
    $itemPlatform = Get-ItemPlatform $Item
    if ($null -ne $itemPlatform -and -not (Test-OnItemPlatform $itemPlatform $Platform)) {
        $result.state = 'excluded'; $result.reason = "platform:$itemPlatform"; return $result
    }
    $feature = Get-ItemFeature $Item
    if ($null -ne $feature -and $null -ne $script:ReferenceFeatures -and $script:ReferenceFeatures -notcontains $feature) {
        $result.state = 'excluded'; $result.reason = "reference-lacks:$feature"; return $result
    }
    $recipe = Get-Recipe $Item
    if ($null -eq $recipe) { $result.state = 'unmeasured'; $result.reason = 'no-probe'; return $result }
    if ($null -ne $Canned) {
        $entry = $Canned.PSObject.Properties[$Item.key]
        $runs = if ($null -ne $entry) { [pscustomobject]@{ Reference = $entry.Value.reference; Candidate = $entry.Value.candidate } } else { [pscustomobject]@{ Reference = $null; Candidate = $null } }
    } else {
        $runs = & $recipe.Run $recipe.Arguments
        if ($null -eq $runs) { throw "The recipe for $($Item.key) returned no runs." }
    }
    $result.actual = Format-Run $runs.Candidate
    if ($null -ne $runs.Reference) {
        $result.expected = Format-Run $runs.Reference
        $result.state = if ($null -ne $runs.Candidate -and (Test-SameRun $runs.Reference $runs.Candidate)) { 'match' } else { 'gap' }
        $result.evidence = 'recipe run through the reference and Curl.Console'
    } elseif ($null -ne $recipe.Documented) {
        $result.expected = "exit 0; stdout '$($recipe.Documented)'"
        $result.state = if ($null -ne $runs.Candidate -and $runs.Candidate.exitCode -eq 0 -and $runs.Candidate.stdout -ceq $recipe.Documented) { 'match' } else { 'gap' }
        $result.evidence = 'recipe run through Curl.Console, compared with docs/cmdline-opts/config.md'
    } else {
        $result.state = 'unmeasured'; $result.reason = 'no-probe'; $result.actual = $null
    }
    return $result
}

function Get-CandidateCommit([string] $Root) {
    try {
        $commit = (& git -C $Root rev-parse HEAD 2>$null)
        if ($LASTEXITCODE -eq 0 -and $commit -match '^[0-9a-f]{40}$') { return $commit }
    } catch { }
    return ('0' * 40)
}

function Get-Platform {
    if ($script:OnWindows) { return 'windows' }
    if ((& uname) -eq 'Darwin') { return 'macos' }
    return 'linux'
}

function Get-Measurement($Inventory, [string] $Root, $Canned) {
    $platform = Get-Platform
    $referenceLine = $null
    $script:ReferenceFeatures = $null
    if ($null -eq $Canned) {
        $reference = Get-GapReferenceCurl
        if ($null -ne $reference -and $reference.Matches) {
            $referenceLine = $reference.VersionLine
            $version = Invoke-GapRun -Path $reference.Path -Arguments @('--version') -Environment @{} -TimeoutSeconds 20
            $featuresLine = @(([System.Text.Encoding]::UTF8.GetString($version.Stdout) -split "`r?`n") | Where-Object { $_.StartsWith('Features:') }) | Select-Object -First 1
            if ($null -ne $featuresLine) { $script:ReferenceFeatures = @($featuresLine.Substring('Features:'.Length).Trim() -split '\s+') }
        }
    } else {
        if ($null -ne $Canned.PSObject.Properties['$reference']) { $referenceLine = $Canned.'$reference' }
        if ($null -ne $Canned.PSObject.Properties['$referenceFeatures']) { $script:ReferenceFeatures = @($Canned.'$referenceFeatures') }
    }
    $items = @($Inventory.items | ForEach-Object { Measure-Item $_ $platform $Canned })
    $counts = [ordered]@{}
    foreach ($state in 'match', 'gap', 'unmeasured', 'excluded') { $counts[$state] = @($items | Where-Object { $_.state -eq $state }).Count }
    $counts.x = $counts.match
    $counts.y = $counts.match + $counts.gap + $counts.unmeasured
    $measurement = [ordered]@{
        area = 'environment'
        targetVersion = $Inventory.version
        candidateCommit = Get-CandidateCommit $Root
        platform = $platform
        reference = $referenceLine
    }
    if ($null -eq $referenceLine) { $measurement.referenceFallback = 'docs' }
    $measurement.measuredAt = [DateTime]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ssZ')
    $measurement.items = $items
    $measurement.counts = $counts
    return $measurement
}

function Invoke-EnvironmentGap([string] $Upstream, [string] $ReleaseVersion, [string] $Root, [string] $Out, [bool] $OnlyInventory, [string] $CannedPath, [string] $InventoryDirectory) {
    if ([string]::IsNullOrEmpty($InventoryDirectory)) { $InventoryDirectory = Join-Path $Root 'Gap/Upstream' }
    $inventory = Get-Inventory $Upstream $ReleaseVersion
    Write-Utf8File (Join-Path $InventoryDirectory "$ReleaseVersion/environment.json") ($inventory | ConvertTo-Json -Depth 8)
    if ($OnlyInventory) { return $null }
    $canned = $null
    if (-not [string]::IsNullOrEmpty($CannedPath)) { $canned = [System.IO.File]::ReadAllText($CannedPath) | ConvertFrom-Json }
    else { . (Join-Path $PSScriptRoot 'GapProbeFunctions.ps1') -Arguments @() -SelfTest:$false 2>$null | Out-Null }
    $measurement = Get-Measurement $inventory $Root $canned
    Write-Utf8File $Out ($measurement | ConvertTo-Json -Depth 8)
    return $measurement
}

function Invoke-SelfTest {
    $script:failed = $false
    function Report([bool] $Passed, [string] $Check) {
        if ($Passed) { Write-Output "PASS $Check" } else { Write-Output "FAIL $Check"; $script:failed = $true }
    }
    $fixtures = Join-Path $PSScriptRoot 'Fixtures/environment'
    $temp = New-TempFolder 'environment-selftest-'
    try {
        $out = Join-Path $temp 'measurements/environment.json'
        Invoke-EnvironmentGap (Join-Path $fixtures 'upstream') '9.9.9' $temp $out $false (Join-Path $fixtures 'probe-results.json') $null | Out-Null
        $inventory = [System.IO.File]::ReadAllText((Join-Path $temp 'Gap/Upstream/9.9.9/environment.json')) | ConvertFrom-Json
        $measurement = [System.IO.File]::ReadAllText($out) | ConvertFrom-Json
        $item = @{}; foreach ($i in $inventory.items) { $item[$i.key] = $i }
        $state = @{}; foreach ($i in $measurement.items) { $state[$i.key] = $i }

        Report (@($inventory.items | Where-Object { $_.key -ceq 'environment:http_proxy' }).Count -eq 1 -and $item['environment:http_proxy'].attributes.kind -eq 'variable') 'a variable named in both documents is one item'
        Report ($item.ContainsKey('environment:ONLY_LIBCURL') -and -not $item.ContainsKey('environment:DEBUG_ONLY')) 'libcurl-env.md is read under DESCRIPTION only'
        Report ($item['environment:config-path:1'].name -like '"$CURL_HOME/.curlrc"*' -and $item['environment:config-path:2'].name -like 'Windows:*' -and $item['environment:config-path:3'].name -like 'Non-Windows:*') 'config locations keep the document''s order'
        Report ($item['environment:config-path:1'].introducedIn -eq '7.10.3') 'an (Added in) note gives introducedIn'
        Report ($item.ContainsKey('environment:config-syntax:comment') -and $item.ContainsKey('environment:config-syntax:equals-separator') -and -not $item.ContainsKey('environment:config-syntax:stdin')) 'config-syntax items follow the rules the document states'
        Report ($item.ContainsKey('environment:config-syntax:q-disables')) 'q-disables is always an item'
        Report ($state['environment:http_proxy'].state -eq 'match') 'equal recipe results give match'
        Report ($state['environment:ALL_PROXY'].state -eq 'gap') 'different recipe results give gap'
        Report ($state['environment:ONLY_LIBCURL'].state -eq 'unmeasured' -and $state['environment:ONLY_LIBCURL'].reason -eq 'no-probe') 'an item with no recipe is unmeasured with no-probe'
        Report ($state['environment:HOME'].state -eq 'gap' -and $state['environment:HOME'].expected -like "exit 0; stdout 'HOME/.curlrc'*") 'a home variable''s recipe is compared with the file the document says is read'
        Report ($state['environment:QLOGDIR'].state -eq 'excluded' -and $state['environment:QLOGDIR'].reason -eq 'reference-lacks:HTTP3') 'a variable needing a feature the reference lacks is excluded with reference-lacks'
        Report ($state['environment:SHELL'].state -eq 'excluded' -and $state['environment:SHELL'].reason -eq 'platform:vms') 'SHELL, read only on VMS, is excluded with platform:vms'
        Report ($state['environment:NETRC'].state -eq 'gap' -and $state['environment:NETRC'].actual -like '*; written CLIENT_RANDOM x1') 'runs differing only in what they wrote give gap'
        Report ((Get-OpenSslSubjectHash $script:ThrowawayRootCommonName) -ceq '1ca2228b') 'the SSL_CERT_DIR file is named by the OpenSSL subject hash (openssl x509 -hash gives 1ca2228b)'
        $keyLog = Join-Path $temp 'keylog.txt'
        Write-Utf8File $keyLog "# comment`nCLIENT_RANDOM a b`nSERVER_HANDSHAKE_TRAFFIC_SECRET c d`nCLIENT_RANDOM e f`n"
        Report ((Get-WrittenSummary $keyLog) -ceq 'CLIENT_RANDOM x2, SERVER_HANDSHAKE_TRAFFIC_SECRET x1' -and (Get-WrittenSummary (Join-Path $temp 'none.txt')) -ceq 'absent') 'a written file is summarised by its labels, never its secrets'
        $longItem = [pscustomobject]@{ key = 'environment:config-syntax:line-length-limit'; attributes = [pscustomobject]@{ name = 'line-length-limit'; kind = 'config-syntax' } }
        $longRecipe = Get-Recipe $longItem
        Report ($longRecipe.Arguments[1].IndexOf("`n") -eq (10 * 1024 * 1024 + 1) -and $null -eq $longRecipe.Documented) 'the line-length-limit recipe puts a line one byte over 10 MB ahead of its option'
        $other = if ($script:OnWindows) { @('environment:config-path:3', 'platform:unix') } else { @('environment:config-path:2', 'platform:windows') }
        Report ($state[$other[0]].state -eq 'excluded' -and $state[$other[0]].reason -eq $other[1]) 'another platform''s location is excluded with its platform'
        Report ($state['environment:config-syntax:comment'].state -eq 'gap' -and $state['environment:config-syntax:comment'].expected -like "exit 0; stdout 'gap-config-hit'*") 'with no reference a documented result is compared with Curl'
        Report ($state['environment:config-syntax:escape-sequences'].state -eq 'unmeasured') 'with no reference an undocumented result is unmeasured'
        Report ($measurement.reference -eq 'curl 9.9.9 (fake)' -and $null -eq $measurement.PSObject.Properties['referenceFallback']) 'a reference line drops the docs fallback'
        $c = $measurement.counts
        Report ($c.y -eq ($c.match + $c.gap + $c.unmeasured) -and $c.x -eq $c.match -and ($c.match + $c.gap + $c.unmeasured + $c.excluded) -eq @($measurement.items).Count) 'counts add up'
        $keys = [string[]]@($inventory.items | ForEach-Object { $_.key }); $sorted = [string[]]$keys.Clone(); [Array]::Sort($sorted, [StringComparer]::Ordinal)
        Report ((($keys -join '|') -ceq ($sorted -join '|'))) 'items are sorted by key'
        # Run every config recipe for real against a stand-in Curl.Console outside this script's repository (BL-1747).
        . (Join-Path $PSScriptRoot 'GapProbeFunctions.ps1')
        $script:CandidatePath = Join-Path $PSScriptRoot 'Fixtures/probe/Write-ProbeEcho.ps1'
        $script:RepositoryRootPath = $temp
        $ran = $true
        foreach ($recipeKey in 'environment:config-path:1', 'environment:config-syntax:comment', 'environment:config-syntax:q-disables', 'environment:HOME', 'environment:COLUMNS') {
            $runs = $null
            try { $recipe = Get-Recipe $item[$recipeKey]; $runs = & $recipe.Run $recipe.Arguments } catch { $ran = $false }
            if ($null -eq $runs -or $runs.Candidate.exitCode -ne 7) { $ran = $false }
        }
        Report $ran 'the config, home and command recipes run for real and reach the explicit Curl.Console'
        # A stand-in recorder that answers with the variables it was started with (BL-4382).
        Write-Utf8File (Join-Path $temp 'Record-CurlExchange.ps1') @'
param([int] $Port, [string] $Curl, [string] $CurlArgs, [string] $OutDirectory, [Parameter(ValueFromRemainingArguments)] $Rest)
[void] [System.IO.Directory]::CreateDirectory($OutDirectory)
[System.IO.File]::WriteAllText((Join-Path $OutDirectory 'exitcode.txt'), '0')
[System.IO.File]::WriteAllText((Join-Path $OutDirectory 'stdout.bin'), "$env:POWERSHELL_TELEMETRY_OPTOUT|$env:DOTNET_CLI_TELEMETRY_OPTOUT|$env:HTTPS_PROXY")
[System.IO.File]::WriteAllText((Join-Path $OutDirectory 'stderr.txt'), '')
'@
        $before = [Environment]::GetEnvironmentVariable('POWERSHELL_TELEMETRY_OPTOUT')
        $quiet = Invoke-RecordedRun 'stand-in-curl' (New-RecordedRecipe @{ HTTPS_PROXY = '{SERVER}' } @('https://gap.invalid/', '-s')) 5555
        Report ($quiet.stdout -ceq '1|1|http://127.0.0.1:5555' -and "$([Environment]::GetEnvironmentVariable('POWERSHELL_TELEMETRY_OPTOUT'))" -ceq "$before") 'a recorded run starts its recorder with pwsh and dotnet telemetry off, and puts the variables back after'
        $recipes = @($inventory.items | ForEach-Object { Get-Recipe $_ } | Where-Object { $null -ne $_ })
        Report ($recipes.Count -gt 0 -and @($recipes | Where-Object { -not ($_.ContainsKey('Run') -and $_.ContainsKey('Arguments') -and $_.ContainsKey('Documented')) }).Count -eq 0) 'every recipe has Run, Arguments and Documented'
    } finally {
        Remove-Item -LiteralPath $temp -Recurse -Force -ErrorAction SilentlyContinue
    }
    if ($script:failed) { exit 1 }
}

if ($SelfTest) { Invoke-SelfTest; return }

if ([string]::IsNullOrEmpty($RepositoryRoot)) { $RepositoryRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent }
$script:RepositoryRootPath = $RepositoryRoot
$script:CandidatePath = $Candidate
if ([string]::IsNullOrEmpty($Version)) {
    $Version = ([System.IO.File]::ReadAllText((Join-Path $RepositoryRoot 'Gap/Baselines/target.json')) | ConvertFrom-Json).version
}
if ([string]::IsNullOrEmpty($UpstreamRoot)) {
    $UpstreamRoot = @(& (Join-Path $PSScriptRoot 'Get-UpstreamRelease.ps1') -Version $Version)[-1]
}
if (-not $InventoryOnly -and [string]::IsNullOrEmpty($OutFile)) { throw 'Give -OutFile, or -InventoryOnly to write the inventory alone.' }

if ([string]::IsNullOrEmpty($InventoryDirectory)) { $InventoryDirectory = Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) 'Gap/Upstream' }
$result = Invoke-EnvironmentGap $UpstreamRoot $Version $RepositoryRoot $OutFile $InventoryOnly.IsPresent $ProbeResults $InventoryDirectory
if ($null -ne $result) {
    $c = $result.counts
    Write-Output "environment: match $($c.match), gap $($c.gap), unmeasured $($c.unmeasured), excluded $($c.excluded), X/Y $($c.x)/$($c.y)"
}
