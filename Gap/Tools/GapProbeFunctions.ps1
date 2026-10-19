<#
.SYNOPSIS
    The functions behind Invoke-GapProbe.ps1: Invoke-GapProbe, Get-GapReferenceCurl,
    Get-GapCandidateCurl, Invoke-GapRun and the self-test.

.DESCRIPTION
    This file has no param block on purpose. Dot-sourcing a script that has one binds
    its parameters in the caller's scope, so a measurer that dot-sourced
    Invoke-GapProbe.ps1 inside a function had its own -CandidatePath and -TargetVersion
    overwritten with $null, and the explicit Curl.Console it was given was ignored
    (BL-1747). The measurers dot-source this file; Invoke-GapProbe.ps1 is the
    command-line front end to it.
#>
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:GapToolsDirectory = $PSScriptRoot
$script:GapRepositoryRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$script:GapIsWindows = [System.IO.Path]::DirectorySeparatorChar -eq '\'

function Get-GapTargetVersion {
    <#
    .SYNOPSIS
        Returns the version Gap/Baselines/target.json names.
    #>
    $targetPath = Join-Path $script:GapRepositoryRoot 'Gap/Baselines/target.json'
    return [string] (Get-Content -LiteralPath $targetPath -Raw | ConvertFrom-Json).version
}

function ConvertTo-GapCommandLineArgument {
    <#
    .SYNOPSIS
        Quotes one argument so CommandLineToArgvW (and the C runtime) reads it back
        unchanged; the same rule as Record-CurlExchange.ps1's ConvertTo-CommandLineArgument.
    #>
    param([AllowEmptyString()] [string] $Argument)
    if ($Argument.Length -gt 0 -and $Argument -notmatch '[\s"]') { return $Argument }
    $builder = New-Object System.Text.StringBuilder
    [void] $builder.Append('"')
    $backslashes = 0
    foreach ($character in $Argument.ToCharArray()) {
        if ($character -eq '\') { $backslashes++; continue }
        if ($character -eq '"') {
            [void] $builder.Append('\', 2 * $backslashes + 1)
        } elseif ($backslashes -gt 0) {
            [void] $builder.Append('\', $backslashes)
        }
        $backslashes = 0
        [void] $builder.Append($character)
    }
    if ($backslashes -gt 0) { [void] $builder.Append('\', 2 * $backslashes) }
    [void] $builder.Append('"')
    return $builder.ToString()
}

function Get-GapLaunch {
    <#
    .SYNOPSIS
        Returns the file name and leading arguments that run a binary, or a .ps1 stand-in
        through the current PowerShell host.
    #>
    param([string] $Path)
    if ($Path -like '*.ps1') {
        $hostPath = [System.Diagnostics.Process]::GetCurrentProcess().MainModule.FileName
        $hostPrefix = @()
        # pwsh installed as a .NET tool runs as dotnet.exe hosting pwsh.dll.
        if ([System.IO.Path]::GetFileNameWithoutExtension($hostPath) -eq 'dotnet') { $hostPrefix = @(Join-Path $PSHOME 'pwsh.dll') }
        return @{ FileName = $hostPath; Prefix = $hostPrefix + @('-NoProfile', '-NonInteractive', '-ExecutionPolicy', 'Bypass', '-File', $Path) }
    }
    return @{ FileName = $Path; Prefix = @() }
}

function Get-GapReferenceCurlPath {
    <#
    .SYNOPSIS
        Finds the reference curl: CURL_GAP_REFERENCE_CURL when it is set
        (gap-platforms.yml points it at the curl it built from the release tarball,
        ADR-0505); else Git for Windows' ucrt64\bin\curl.exe (from Git 2.56.0) or
        mingw64\bin\curl.exe (before it) on Windows, the lookup Record-CurlExchange.ps1's
        Get-ReferenceCurlPath uses; curl on PATH elsewhere; $null when there is none.
    #>
    if ($env:CURL_GAP_REFERENCE_CURL) { return $env:CURL_GAP_REFERENCE_CURL }
    if (-not $script:GapIsWindows) {
        $curl = Get-Command curl -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($null -ne $curl) { return $curl.Source }
        return $null
    }
    # git.exe lives in <Git>\cmd or <Git>\bin.
    $roots = @()
    $git = Get-Command git.exe -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($null -ne $git) { $roots += Split-Path (Split-Path $git.Source -Parent) -Parent }
    if ($env:ProgramFiles) { $roots += Join-Path $env:ProgramFiles 'Git' }
    foreach ($root in $roots) {
        foreach ($folder in 'ucrt64\bin', 'mingw64\bin') {
            $candidate = Join-Path (Join-Path $root $folder) 'curl.exe'
            if (Test-Path -LiteralPath $candidate) { return $candidate }
        }
    }
    return $null
}

function Get-GapReferenceCurl {
    <#
    .SYNOPSIS
        Finds the matched reference curl and says whether it is the targeted version.

    .DESCRIPTION
        Returns { Path, VersionLine, Matches }, where VersionLine is the first line of
        "<Path> --version" and Matches is true when it starts "curl <TargetVersion> ".
        Returns $null when no reference is found.

    .PARAMETER TargetVersion
        The version to match. Default: the version in Gap/Baselines/target.json.

    .PARAMETER Path
        A reference to use instead of the lookup (or a .ps1 stand-in).
    #>
    param([string] $TargetVersion, [string] $Path)
    if (-not $TargetVersion) { $TargetVersion = Get-GapTargetVersion }
    if (-not $Path) { $Path = Get-GapReferenceCurlPath }
    if (-not $Path) { return $null }
    $run = Invoke-GapRun -Path $Path -Arguments @('--version') -Environment @{} -TimeoutSeconds 20
    $text = [System.Text.Encoding]::UTF8.GetString($run.Stdout)
    $versionLine = ($text -split "`r?`n")[0]
    $isMatch = $versionLine.StartsWith("curl $TargetVersion ", [System.StringComparison]::Ordinal)
    return [pscustomobject] @{ Path = $Path; VersionLine = $versionLine; Matches = $isMatch }
}

function Get-GapCandidateCurl {
    <#
    .SYNOPSIS
        Returns the Curl.Console binary to probe.

    .DESCRIPTION
        Returns -Path when given, else the newest curl.exe (curl off Windows) under
        Curl.Console/bin/Release. Throws, naming the build command, when there is none.

    .PARAMETER Path
        The binary to use.
    #>
    param([string] $Path)
    if ($Path) { return $Path }
    $name = if ($script:GapIsWindows) { 'curl.exe' } else { 'curl' }
    $releaseDirectory = Join-Path $script:GapRepositoryRoot 'Curl.Console/bin/Release'
    $newest = $null
    if (Test-Path -LiteralPath $releaseDirectory) {
        $newest = Get-ChildItem -LiteralPath $releaseDirectory -Recurse -File -Filter $name |
            Where-Object { $_.Name -ceq $name } | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
    }
    if ($null -eq $newest) { throw "No Curl.Console $name under $releaseDirectory; run: dotnet build Curl.Console -c Release" }
    return $newest.FullName
}

function Invoke-GapRun {
    <#
    .SYNOPSIS
        Runs one binary once in the controlled environment and returns { ExitCode,
        Stdout (byte[]), Stderr (string), TimedOut }.
    #>
    param(
        [string] $Path,
        [AllowEmptyString()] [string[]] $Arguments,
        [hashtable] $Environment,
        [string] $WorkingDirectory,
        [byte[]] $StandardInput,
        [int] $TimeoutSeconds
    )
    # A run without a working directory gets a fresh empty one, never the caller's: a
    # command line that writes a file (-o, -D, -O) must not write into the checkout (BL-1840).
    $scratchDirectory = $null
    if (-not $WorkingDirectory) {
        $scratchDirectory = Join-Path ([System.IO.Path]::GetTempPath()) ('gap-probe-cwd-' + [guid]::NewGuid().ToString('N'))
        [void] (New-Item -ItemType Directory -Path $scratchDirectory)
        $WorkingDirectory = $scratchDirectory
    }
    $emptyHome = Join-Path ([System.IO.Path]::GetTempPath()) ('gap-probe-' + [guid]::NewGuid().ToString('N'))
    [void] (New-Item -ItemType Directory -Path $emptyHome)
    try {
        $launch = Get-GapLaunch -Path $Path
        $startInfo = New-Object System.Diagnostics.ProcessStartInfo
        $startInfo.FileName = $launch.FileName
        $startInfo.Arguments = (@(@($launch.Prefix) + @($Arguments) | ForEach-Object { ConvertTo-GapCommandLineArgument -Argument $_ }) -join ' ')
        $startInfo.UseShellExecute = $false
        $startInfo.RedirectStandardInput = $true
        $startInfo.RedirectStandardOutput = $true
        $startInfo.RedirectStandardError = $true
        $startInfo.CreateNoWindow = $true
        $startInfo.WorkingDirectory = $WorkingDirectory

        $kept = @{}
        # DOTNET_ROOT* tell Curl.Console's app host where .NET is when it is not in a
        # default location (setup-dotnet on a macOS runner); curl never reads them (ADR-0505).
        foreach ($name in 'PATH', 'SystemRoot', 'TEMP', 'TMP', 'DOTNET_ROOT', 'DOTNET_ROOT_X64', 'DOTNET_ROOT_ARM64') {
            $value = [Environment]::GetEnvironmentVariable($name)
            if ($null -ne $value) { $kept[$name] = $value }
        }
        foreach ($name in 'HOME', 'USERPROFILE', 'APPDATA', 'CURL_HOME', 'XDG_CONFIG_HOME') { $kept[$name] = $emptyHome }
        foreach ($name in $Environment.Keys) { $kept[$name] = $Environment[$name] }
        $startInfo.EnvironmentVariables.Clear()
        foreach ($name in $kept.Keys) {
            if ($null -ne $kept[$name]) { $startInfo.EnvironmentVariables[$name] = [string] $kept[$name] }
        }

        # The standard input writer takes the console's input encoding, whose UTF-8 byte
        # order mark would reach the process ahead of StandardInput; Latin-1 has none.
        $consoleInputEncoding = [System.Console]::InputEncoding
        [System.Console]::InputEncoding = [System.Text.Encoding]::GetEncoding(28591)
        try {
            $process = [System.Diagnostics.Process]::Start($startInfo)
        } finally {
            [System.Console]::InputEncoding = $consoleInputEncoding
        }
        try {
            $stdout = New-Object System.IO.MemoryStream
            $stderr = New-Object System.IO.MemoryStream
            # Both pipes drain at once, so the process never blocks on a full one.
            $stdoutCopy = $process.StandardOutput.BaseStream.CopyToAsync($stdout)
            $stderrCopy = $process.StandardError.BaseStream.CopyToAsync($stderr)
            try {
                if ($null -ne $StandardInput -and $StandardInput.Length -gt 0) {
                    $process.StandardInput.BaseStream.Write($StandardInput, 0, $StandardInput.Length)
                }
                $process.StandardInput.Close()
            } catch [System.IO.IOException] {
                # The process exited without reading its input.
            }
            $timedOut = -not $process.WaitForExit($TimeoutSeconds * 1000)
            if ($timedOut) {
                try { $process.Kill() } catch [System.InvalidOperationException] { }
            }
            $process.WaitForExit()
            [void] [System.Threading.Tasks.Task]::WaitAll(@($stdoutCopy, $stderrCopy), 5000)
            $exitCode = if ($timedOut) { $null } else { $process.ExitCode }
            return [pscustomobject] @{
                ExitCode = $exitCode
                Stdout   = [byte[]] $stdout.ToArray()
                Stderr   = [System.Text.Encoding]::UTF8.GetString($stderr.ToArray())
                TimedOut = $timedOut
            }
        } finally {
            $process.Dispose()
        }
    } finally {
        Remove-Item -LiteralPath $emptyHome -Recurse -Force -ErrorAction SilentlyContinue
        if ($scratchDirectory) { Remove-Item -LiteralPath $scratchDirectory -Recurse -Force -ErrorAction SilentlyContinue }
    }
}

$script:GapDebugBuildWarning = 'WARNING: this libcurl is Debug-enabled, do not use in production'

function Remove-GapDebugBuildWarning {
    <#
    .SYNOPSIS
        Returns -Text without the line a debug-enabled libcurl prints on curl -V
        ("WARNING: this libcurl is Debug-enabled, do not use in production"), line ending
        and all; every other line is kept byte for byte.

    .DESCRIPTION
        Git for Windows 2.56.0's ucrt64 curl 8.22.0, the Windows reference, is a debug build
        (BL-2125). Curl matches the release build, which prints no such line, so the line is
        a property of the reference build and never a gap (BL-2128).
    #>
    param([AllowNull()] [AllowEmptyString()] [string] $Text)
    if ([string]::IsNullOrEmpty($Text)) { return $Text }
    $kept = @($Text -split '(?<=\n)' | Where-Object { $_.TrimEnd("`r", "`n") -cne $script:GapDebugBuildWarning })
    return ($kept -join '')
}

function Invoke-GapProbe {
    <#
    .SYNOPSIS
        Runs one command line through the matched reference curl and through Curl.Console.

    .DESCRIPTION
        Runs the reference (only when its version line names -TargetVersion) and then the
        candidate, each in the controlled environment described in the script's help.
        Returns { Reference, Candidate }, each { ExitCode, Stdout (byte[]), Stderr
        (string), TimedOut }; Reference is $null when no matching reference is found. The
        reference's Stderr has lost the debug-build warning line (Remove-GapDebugBuildWarning).

    .PARAMETER Arguments
        The command line's arguments, without the program name.

    .PARAMETER Environment
        Variables to add or override; a $null value removes the variable.

    .PARAMETER WorkingDirectory
        The folder both runs start in. Default: a fresh empty folder under the system temp
        directory, deleted after each run (BL-1840).

    .PARAMETER StandardInput
        Bytes written to each run's standard input.

    .PARAMETER TimeoutSeconds
        Each run is killed after this many seconds and reported with ExitCode $null and
        TimedOut $true. Default: 20.

    .PARAMETER CandidatePath
        The Curl.Console binary. Default: Get-GapCandidateCurl.

    .PARAMETER ReferencePath
        The reference curl. Default: Get-GapReferenceCurl's lookup.

    .PARAMETER TargetVersion
        The version the reference must name. Default: Gap/Baselines/target.json.

    .PARAMETER BeforeEachRun
        Called before each binary's run, so a file both command lines name starts the same
        for each: curl --ssl-sessions empties its file as it exits, which left Curl reading
        an empty one (GF-0090, BL-2717).
    #>
    param(
        [Parameter(Mandatory = $true)] [AllowEmptyString()] [AllowEmptyCollection()] [string[]] $Arguments,
        [hashtable] $Environment = @{},
        [string] $WorkingDirectory,
        [byte[]] $StandardInput,
        [int] $TimeoutSeconds = 20,
        [string] $CandidatePath,
        [string] $ReferencePath,
        [string] $TargetVersion,
        [scriptblock] $BeforeEachRun
    )
    $run = @{ Arguments = $Arguments; Environment = $Environment; WorkingDirectory = $WorkingDirectory; StandardInput = $StandardInput; TimeoutSeconds = $TimeoutSeconds }
    $referenceResult = $null
    $reference = Get-GapReferenceCurl -TargetVersion $TargetVersion -Path $ReferencePath
    if ($null -ne $reference -and $reference.Matches) {
        if ($BeforeEachRun) { & $BeforeEachRun }
        $referenceResult = Invoke-GapRun -Path $reference.Path @run
        $referenceResult.Stderr = Remove-GapDebugBuildWarning $referenceResult.Stderr
    }
    if ($BeforeEachRun) { & $BeforeEachRun }
    $candidateResult = Invoke-GapRun -Path (Get-GapCandidateCurl -Path $CandidatePath) @run
    return [pscustomobject] @{ Reference = $referenceResult; Candidate = $candidateResult }
}

function Invoke-GapProbeSelfTest {
    <#
    .SYNOPSIS
        Runs the self-test against Gap/Tools/Fixtures/probe/Write-ProbeEcho.ps1 and
        returns one PASS or FAIL line per check.
    #>
    $echo = Join-Path $script:GapToolsDirectory 'Fixtures/probe/Write-ProbeEcho.ps1'
    $report = {
        param([string] $Name, [bool] $Passed, [string] $Detail)
        if ($Passed) { "PASS $Name" } else { "FAIL $Name - $Detail" }
    }
    $tricky = 'a b "c" \d\'

    $probe = Invoke-GapProbe -Arguments @($tricky, '') -CandidatePath $echo -ReferencePath $echo -TargetVersion '0.0.1' -Environment @{ GAP_PROBE_ADDED = 'yes'; GAP_PROBE_REMOVED = $null }
    $echoed = [System.Text.Encoding]::UTF8.GetString($probe.Candidate.Stdout) | ConvertFrom-Json
    $received = @($echoed.Arguments)
    & $report 'argument with a space and a double quote arrives as one argument' ($received.Count -eq 2 -and $received[0] -ceq $tricky -and $received[1] -eq '') ("received: " + ($received -join ' | '))

    $homes = @('HOME', 'USERPROFILE', 'APPDATA', 'CURL_HOME', 'XDG_CONFIG_HOME' | ForEach-Object { $echoed.Environment.$_ })
    # The stand-in is a PowerShell host, which creates these in its profile folder as it
    # starts; anything else there leaked in.
    $hostCreated = @('AppData', 'Microsoft')
    $oneFolder =@($homes | Select-Object -Unique).Count -eq 1 -and $homes[0] -and $homes[0] -ne $env:USERPROFILE -and $homes[0] -ne $env:HOME
    & $report 'HOME, USERPROFILE, APPDATA, CURL_HOME and XDG_CONFIG_HOME point at one empty folder' ($oneFolder -and $null -ne $echoed.HomeItems -and @($echoed.HomeItems | Where-Object { $_ -notin $hostCreated }).Count -eq 0) ("folders: " + ($homes -join ' | ') + "; items: " + (@($echoed.HomeItems) -join ' | '))

    [Environment]::SetEnvironmentVariable('GAP_PROBE_REMOVED', 'leaked')
    try {
        $removal = Invoke-GapProbe -Arguments @('x') -CandidatePath $echo -ReferencePath $echo -TargetVersion '0.0.1' -Environment @{ GAP_PROBE_ADDED = 'yes'; HOME = 'override'; GAP_PROBE_REMOVED = $null }
    } finally {
        [Environment]::SetEnvironmentVariable('GAP_PROBE_REMOVED', $null)
    }
    $both = @($removal.Reference, $removal.Candidate | ForEach-Object { [System.Text.Encoding]::UTF8.GetString($_.Stdout) | ConvertFrom-Json })
    $environmentPassed = @($both | Where-Object { $_.Environment.GAP_PROBE_ADDED -eq 'yes' -and $_.Environment.HOME -eq 'override' -and $null -eq $_.Environment.GAP_PROBE_REMOVED }).Count -eq 2
    & $report 'an -Environment entry is passed and a $null entry removes the variable' $environmentPassed ("candidate stdout: " + [System.Text.Encoding]::UTF8.GetString($removal.Candidate.Stdout))

    $script:BeforeEachRunCalls = 0
    [void] (Invoke-GapProbe -Arguments @('x') -CandidatePath $echo -ReferencePath $echo -TargetVersion '0.0.1' -BeforeEachRun { $script:BeforeEachRunCalls++ })
    $withReference = $script:BeforeEachRunCalls
    $script:BeforeEachRunCalls = 0
    [void] (Invoke-GapProbe -Arguments @('x') -CandidatePath $echo -ReferencePath $echo -TargetVersion '9.9.9' -BeforeEachRun { $script:BeforeEachRunCalls++ })
    & $report '-BeforeEachRun runs before each binary: twice with a matching reference, once without (BL-2717)' ($withReference -eq 2 -and $script:BeforeEachRunCalls -eq 1) ("calls: $withReference, then $($script:BeforeEachRunCalls)")

    $slow = Invoke-GapProbe -Arguments @('--probe-sleep', '30') -CandidatePath $echo -ReferencePath $echo -TargetVersion '9.9.9' -TimeoutSeconds 2
    $callerDirectory = (Get-Location).ProviderPath
    $startedIn = $echoed.WorkingDirectory
    & $report 'a run without -WorkingDirectory starts in a fresh folder, not the caller''s, and the folder is deleted afterwards' ($startedIn -and $startedIn -ne $callerDirectory -and (Split-Path $startedIn -Leaf) -like 'gap-probe-cwd-*' -and -not (Test-Path -LiteralPath $startedIn)) ('started in: ' + $startedIn)

    & $report 'a run past -TimeoutSeconds reports TimedOut' ($slow.Candidate.TimedOut -and $null -eq $slow.Candidate.ExitCode) ("TimedOut=$($slow.Candidate.TimedOut) ExitCode=$($slow.Candidate.ExitCode)")

    $other = Get-GapReferenceCurl -Path $echo -TargetVersion '8.21.0'
    $matched = Get-GapReferenceCurl -Path $echo -TargetVersion '0.0.1'
    $versionPassed = (-not $other.Matches) -and $matched.Matches -and $null -eq $slow.Reference -and $null -ne $probe.Reference -and $probe.Candidate.ExitCode -eq 7
    & $report 'a reference whose version line names another version is reported as not matching' $versionPassed ("line: $($other.VersionLine)")

    $warned = "WARNING: this libcurl is Debug-enabled, do not use in production`r`ncurl: (7) x`nWARNING: this libcurl is Debug-enabled, do not use in production"
    $stripped = Remove-GapDebugBuildWarning $warned
    $kept = Remove-GapDebugBuildWarning "warning: this libcurl is Debug-enabled, do not use in production`nWARNING: this libcurl is Debug-enabled, do not use in production now`n"
    & $report 'the debug-build warning line is removed from stderr, line ending and all, and nothing else is (BL-2128)' ($stripped -ceq "curl: (7) x`n" -and $kept -ceq "warning: this libcurl is Debug-enabled, do not use in production`nWARNING: this libcurl is Debug-enabled, do not use in production now`n" -and (Remove-GapDebugBuildWarning '') -ceq '') ("stripped: [$stripped] kept: [$kept]")

    $savedReference = $env:CURL_GAP_REFERENCE_CURL
    try {
        $env:CURL_GAP_REFERENCE_CURL = $echo
        $overridden = Get-GapReferenceCurlPath
    } finally { $env:CURL_GAP_REFERENCE_CURL = $savedReference }
    & $report 'CURL_GAP_REFERENCE_CURL names the reference curl when it is set (ADR-0505)' ($overridden -eq $echo) ("found: $overridden")

}
