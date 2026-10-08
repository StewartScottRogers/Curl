<#
.SYNOPSIS
    Runs one command line through the matched reference curl and through Curl.Console,
    each in a controlled environment, and returns both exit codes, stdout bytes and
    stderr text.

.DESCRIPTION
    The gap analysis office's area measurement tools call this script to compare Curl
    with the matched reference build (ADR-0433 decision 1): on Windows the Schannel
    mingw curl from Git for Windows (ADR-0018), on Linux and macOS the curl on PATH. The
    reference is used only when its --version first line names the version in
    Gap/Baselines/target.json; otherwise Reference is $null and the caller falls back to
    the release's documents.

    Dot-source the script to get Get-GapReferenceCurl, Get-GapCandidateCurl and
    Invoke-GapProbe; run it directly to probe one command line, or with -SelfTest.

    Each run starts from an environment holding only PATH, SystemRoot, TEMP, TMP,
    USERPROFILE, HOME and APPDATA, with HOME, USERPROFILE, APPDATA, CURL_HOME and
    XDG_CONFIG_HOME pointed at a fresh empty temporary folder, so no real .curlrc or
    _curlrc leaks in. The folder is deleted after the run.

    A path ending in .ps1 is run through the current PowerShell host with -File; the
    self-test uses that to stand Gap/Tools/Fixtures/probe/Write-ProbeEcho.ps1 in for
    both binaries. ASCII only; runs under Windows PowerShell 5.1 and PowerShell 7.

.PARAMETER Arguments
    The command line's arguments, without the program name.

.PARAMETER Environment
    Variables to add to or override in the controlled environment; a $null value removes
    the variable.

.PARAMETER WorkingDirectory
    The folder both runs start in. Default: the current location.

.PARAMETER StandardInput
    Bytes written to each run's standard input, which is then closed. Default: none.

.PARAMETER TimeoutSeconds
    Each run is killed after this many seconds and reported with ExitCode $null and
    TimedOut $true. Default: 20.

.PARAMETER CandidatePath
    The Curl.Console binary (or a .ps1 stand-in). Default: Get-GapCandidateCurl.

.PARAMETER ReferencePath
    The reference curl (or a .ps1 stand-in). Default: Get-GapReferenceCurl's lookup.

.PARAMETER TargetVersion
    The version the reference must name. Default: the version in
    Gap/Baselines/target.json.

.PARAMETER SelfTest
    Checks the quoting, the environment control, -Environment, the timeout and the
    version match against the stand-in fixture, prints a PASS or FAIL line per check,
    and exits 1 on any FAIL. Needs neither curl.

.EXAMPLE
    . Gap/Tools/Invoke-GapProbe.ps1; Get-GapReferenceCurl

.EXAMPLE
    Gap/Tools/Invoke-GapProbe.ps1 -Arguments '--version'
#>
[CmdletBinding()]
param(
    [AllowEmptyString()] [string[]] $Arguments = @(),
    [hashtable] $Environment = @{},
    [string] $WorkingDirectory,
    [byte[]] $StandardInput,
    [int] $TimeoutSeconds = 20,
    [string] $CandidatePath,
    [string] $ReferencePath,
    [string] $TargetVersion,
    [switch] $SelfTest
)


. (Join-Path $PSScriptRoot 'GapProbeFunctions.ps1')


if ($MyInvocation.InvocationName -ne '.') {
    if ($SelfTest) {
        $lines = @(Invoke-GapProbeSelfTest)
        $lines | ForEach-Object { Write-Output $_ }
        if (@($lines | Where-Object { $_ -like 'FAIL *' }).Count -gt 0) { exit 1 }
        exit 0
    }
    $probeParameters = @{ Arguments = $Arguments; Environment = $Environment; WorkingDirectory = $WorkingDirectory; StandardInput = $StandardInput; TimeoutSeconds = $TimeoutSeconds; CandidatePath = $CandidatePath; ReferencePath = $ReferencePath; TargetVersion = $TargetVersion }
    Invoke-GapProbe @probeParameters
}
