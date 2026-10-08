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

    Inventory, <RepositoryRoot>/Gap/Upstream/<Version>/environment.json, keys:
    - environment:<NAME>                 kind variable, the name's case as upstream writes it.
    - environment:config-path:<n>        kind config-path, n the document's number.
    - environment:config-syntax:<slug>   kind config-syntax; q-disables is always present.
    Attributes are name and kind. introducedIn is the "(Added in <version>)" the
    document gives, else null.

    Recipes. A recipe sets up an environment and maybe a config file, runs both binaries
    and compares exit code, stdout, stderr and, for the proxy variables, the request bytes
    the loopback server received. Every run gets a fresh temporary home: HOME, USERPROFILE,
    APPDATA, CURL_HOME and XDG_CONFIG_HOME point at an empty folder unless the recipe points
    one at its own temporary folder, so no recipe reads or writes the user's real .curlrc,
    _curlrc or environment.
    - http_proxy, HTTPS_PROXY, ALL_PROXY, NO_PROXY: Record-CurlExchange.ps1 is the
      loopback proxy, run once with -Curl <reference> and once with -Curl <candidate>, for
      "curl -s http://gap.invalid/". NO_PROXY=gap.invalid is set beside
      http_proxy=<the loopback proxy>. On Windows the variable names are not case
      sensitive, so the case forms are the same variable.
    - config-path:<n>: a config file holding write-out = "gap-config-hit" in the location
      the item names (for item 8, the executable's folder, using copies of both binaries
      in a temporary folder), then "curl -s file:///<empty file>". Items 4 to 6 and 8 are
      Windows only; item 7 (getpwuid) is non-Windows only and has no recipe.
    - config-syntax: whitespace-separator, colon-separator, equals-separator,
      dashed-long-option, dashed-no-separator, double-quotes, escape-sequences,
      backslash-other-letter, comment, stdin (-K -), url-option, q-disables (-q with a
      $CURL_HOME/.curlrc) and windows-underscore-curlrc ($CURL_HOME/_curlrc, Windows only).
      one-option-per-line and line-length-limit have no recipe.

    Scoring:
    - An item for another platform only is excluded with reason platform:windows or
      platform:unix.
    - An item with no recipe is unmeasured with reason no-probe.
    - With a matching reference: match when exit code, stdout, stderr and request bytes
      are equal, else gap.
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
    { "exitCode", "stdout", "stderr", "request" }. A recipe key it lacks is measured as
    if no binary answered.

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
    [string] $OutFile,
    [switch] $InventoryOnly,
    [string] $ProbeResults,
    [switch] $SelfTest
)

$ErrorActionPreference = 'Stop'

$script:OnWindows = [System.IO.Path]::DirectorySeparatorChar -eq '\'
$script:Hit = 'gap-config-hit'

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
    @{ Slug = 'one-option-per-line'; Pattern = 'one option per physical line'; Config = $null }
    @{ Slug = 'line-length-limit'; Pattern = 'no more than 10 megabytes'; Config = $null }
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
    return [pscustomobject]@{ exitCode = $Run.ExitCode; stdout = [System.Text.Encoding]::UTF8.GetString($Run.Stdout); stderr = $Run.Stderr; request = $null }
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

# Runs Record-CurlExchange.ps1 as the loopback proxy for one binary, with the process
# environment cleared of proxy variables, homes pointed at an empty folder, and Variables
# set (PROXY in a value is replaced by the proxy's URL).
function Invoke-ProxyRun([string] $Curl, [hashtable] $Variables) {
    $folder = New-TempFolder 'gap-env-proxy-'
    $saved = @{}
    $names = @('http_proxy', 'HTTP_PROXY', 'https_proxy', 'HTTPS_PROXY', 'all_proxy', 'ALL_PROXY', 'no_proxy', 'NO_PROXY', 'HOME', 'USERPROFILE', 'APPDATA', 'CURL_HOME', 'XDG_CONFIG_HOME')
    foreach ($name in $names) { $saved[$name] = [Environment]::GetEnvironmentVariable($name) }
    try {
        $listener = New-Object System.Net.Sockets.TcpListener([System.Net.IPAddress]::Loopback, 0)
        $listener.Start(); $port = $listener.LocalEndpoint.Port; $listener.Stop()
        foreach ($name in $names) { [Environment]::SetEnvironmentVariable($name, $null) }
        foreach ($name in 'HOME', 'USERPROFILE', 'APPDATA', 'CURL_HOME', 'XDG_CONFIG_HOME') { [Environment]::SetEnvironmentVariable($name, $folder) }
        foreach ($name in $Variables.Keys) { [Environment]::SetEnvironmentVariable($name, ($Variables[$name] -replace 'PROXY', "http://127.0.0.1:$port")) }
        $out = Join-Path $folder 'out'
        $launch = Get-GapLaunch -Path (Join-Path $script:RepositoryRootPath 'Record-CurlExchange.ps1')
        $prefix = @($launch.Prefix)
        & $launch.FileName @prefix -Port $port -Curl $Curl -CurlArgs 'http://gap.invalid/,-s' -OutDirectory $out | Out-Null
        $requestPath = Join-Path $out 'request.bin'
        $request = if (Test-Path -LiteralPath $requestPath) { [System.Text.Encoding]::GetEncoding(28591).GetString([System.IO.File]::ReadAllBytes($requestPath)) } else { '' }
        return [pscustomobject]@{
            exitCode = [int]([System.IO.File]::ReadAllText((Join-Path $out 'exitcode.txt')).Trim())
            stdout = [System.Text.Encoding]::UTF8.GetString([System.IO.File]::ReadAllBytes((Join-Path $out 'stdout.bin')))
            stderr = [System.Text.Encoding]::UTF8.GetString([System.IO.File]::ReadAllBytes((Join-Path $out 'stderr.txt')))
            request = $request
        }
    } finally {
        foreach ($name in $names) { [Environment]::SetEnvironmentVariable($name, $saved[$name]) }
        Remove-Item -LiteralPath $folder -Recurse -Force -ErrorAction SilentlyContinue
    }
}

function Invoke-ProxyRecipe([hashtable] $Variables) {
    $reference = Get-GapReferenceCurl
    $referenceRun = $null
    if ($null -ne $reference -and $reference.Matches) { $referenceRun = Invoke-ProxyRun $reference.Path $Variables }
    return [pscustomobject]@{ Reference = $referenceRun; Candidate = (Invoke-ProxyRun (Get-GapCandidateCurl -Path $script:CandidatePath) $Variables) }
}

# A recipe's Run is called with its Arguments. It must not be a .GetNewClosure() block built
# from the values: a closure is bound to a new module that cannot see this script's functions
# (BL-1747), so the values travel as Arguments to a block that is defined here, at script scope.
$script:RunConfigRecipe = { param([object[]] $RecipeArguments) Invoke-ConfigRecipe $RecipeArguments[0] $RecipeArguments[1] $RecipeArguments[2] $RecipeArguments[3] }

# The recipe for a key: { Run (scriptblock called with Arguments), Arguments, Documented (the stdout the document states,
# or $null) }, or $null when the key has none.
function Get-Recipe($Item) {
    $key = $Item.key
    switch -CaseSensitive ($key) {
        'environment:http_proxy' { return @{ Run = { Invoke-ProxyRecipe @{ http_proxy = 'PROXY' } }; Arguments = $null; Documented = $null } }
        'environment:HTTPS_PROXY' { return @{ Run = { Invoke-ProxyRecipe @{ HTTPS_PROXY = 'PROXY' } }; Arguments = $null; Documented = $null } }
        'environment:ALL_PROXY' { return @{ Run = { Invoke-ProxyRecipe @{ ALL_PROXY = 'PROXY' } }; Arguments = $null; Documented = $null } }
        'environment:NO_PROXY' { return @{ Run = { Invoke-ProxyRecipe @{ http_proxy = 'PROXY'; NO_PROXY = 'gap.invalid' } }; Arguments = $null; Documented = $null } }
        'environment:config-syntax:q-disables' { return @{ Run = $script:RunConfigRecipe; Arguments = @('.curlrc', "write-out = $Hit", @('CURL_HOME'), 'q'); Documented = '' } }
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
        return $null
    }
    if ($Item.attributes.kind -eq 'config-syntax') {
        $rule = $script:SyntaxRules | Where-Object { $_.Slug -eq $Item.attributes.name } | Select-Object -First 1
        if ($null -eq $rule -or $null -eq $rule.Config) { return $null }
        $config = $rule.Config; $slug = $rule.Slug
        $file = if ($slug -eq 'windows-underscore-curlrc') { '_curlrc' } else { '.curlrc' }
        $mode = if ($slug -eq 'stdin' -or $slug -eq 'url-option') { $slug } else { 'plain' }
        $documented = if ($config.Contains($Hit) -and $slug -ne 'dashed-no-separator') { $Hit } else { $null }
        return @{ Run = $script:RunConfigRecipe; Arguments = @($file, $config, @('CURL_HOME'), $mode); Documented = $documented }
    }
    return $null
}

function Format-Run($Run) {
    if ($null -eq $Run) { return $null }
    $requestLine = if ([string]::IsNullOrEmpty($Run.request)) { 'none' } else { ($Run.request -split "`r?`n")[0] }
    return "exit $($Run.exitCode); stdout '$($Run.stdout)'; stderr '$(($Run.stderr -replace '\s+$', ''))'; request $requestLine"
}

function Test-SameRun($A, $B) {
    foreach ($field in 'exitCode', 'stdout', 'stderr', 'request') {
        if ([string]$A.$field -cne [string]$B.$field) { return $false }
    }
    return $true
}

function Measure-Item($Item, [string] $Platform, $Canned) {
    $result = [ordered]@{ key = $Item.key; state = $null; reason = $null; expected = $null; actual = $null; evidence = $null; introducedIn = $Item.introducedIn }
    $itemPlatform = Get-ItemPlatform $Item
    if ($null -ne $itemPlatform -and ($itemPlatform -eq 'windows') -ne ($Platform -eq 'windows')) {
        $result.state = 'excluded'; $result.reason = "platform:$itemPlatform"; return $result
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
    if ($null -eq $Canned) {
        $reference = Get-GapReferenceCurl
        if ($null -ne $reference -and $reference.Matches) { $referenceLine = $reference.VersionLine }
    } elseif ($null -ne $Canned.PSObject.Properties['$reference']) {
        $referenceLine = $Canned.'$reference'
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

function Invoke-EnvironmentGap([string] $Upstream, [string] $ReleaseVersion, [string] $Root, [string] $Out, [bool] $OnlyInventory, [string] $CannedPath) {
    $inventory = Get-Inventory $Upstream $ReleaseVersion
    Write-Utf8File (Join-Path $Root "Gap/Upstream/$ReleaseVersion/environment.json") ($inventory | ConvertTo-Json -Depth 8)
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
        Invoke-EnvironmentGap (Join-Path $fixtures 'upstream') '9.9.9' $temp $out $false (Join-Path $fixtures 'probe-results.json') | Out-Null
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
        Report ($state['environment:HOME'].state -eq 'unmeasured' -and $state['environment:HOME'].reason -eq 'no-probe') 'an item with no recipe is unmeasured with no-probe'
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
        foreach ($recipeKey in 'environment:config-path:1', 'environment:config-syntax:comment', 'environment:config-syntax:q-disables') {
            $runs = $null
            try { $recipe = Get-Recipe $item[$recipeKey]; $runs = & $recipe.Run $recipe.Arguments } catch { $ran = $false }
            if ($null -eq $runs -or $runs.Candidate.exitCode -ne 7) { $ran = $false }
        }
        Report $ran 'the config recipes run for real and reach the explicit Curl.Console'
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

$result = Invoke-EnvironmentGap $UpstreamRoot $Version $RepositoryRoot $OutFile $InventoryOnly.IsPresent $ProbeResults
if ($null -ne $result) {
    $c = $result.counts
    Write-Output "environment: match $($c.match), gap $($c.gap), unmeasured $($c.unmeasured), excluded $($c.excluded), X/Y $($c.x)/$($c.y)"
}
