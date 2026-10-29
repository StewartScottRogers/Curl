<#
.SYNOPSIS
    Proves every hook command in .claude/settings.json starts on this platform, and that the
    audit guard hook refuses a lane's read of an audit path and fails closed without a shell.

.DESCRIPTION
    Claude Code runs a hook's command with sh: Git Bash on Windows, the system shell on
    Linux and macOS. settings.json writes each command as one sh line that runs its script
    with powershell where there is one and pwsh otherwise (ADR-1015, BL-3926), so the same
    file works on all three. This script runs each command the way Claude Code does - through
    sh, from the repository root, with the hook's JSON on standard input - and checks:

      guard, lane, Read Audit/README.md               exit 2 (refused)
      guard, lane, Bash cat Audit/README.md           exit 2
      guard, lane, Read README.md                     exit 0
      guard, no lane, Read Audit/README.md            exit 0 (interactive sessions pass)
      guard, lane, neither powershell nor pwsh        exit 2 (fails closed)
      every other hook command, a neutral payload     exit 0 (it starts)

    and that settings.json has no command hook this script did not run. Prints PASS or FAIL
    per case and exits 0 only when every case passes. CI runs it on Windows, Linux and macOS.

.PARAMETER Settings
    The settings file to test. Default: .claude/settings.json beside this script's
    repository.
#>
param([string]$Settings)

$ErrorActionPreference = 'Stop'
$RepoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if (-not $Settings) { $Settings = Join-Path $RepoRoot '.claude/settings.json' }

function Get-Sh {
    # The sh Claude Code would use: Git's on Windows (System32's bash.exe is WSL's), else
    # the one on PATH.
    if ([Environment]::OSVersion.Platform -eq 'Win32NT') {
        foreach ($root in $env:ProgramFiles, ${env:ProgramFiles(x86)}, $env:LOCALAPPDATA) {
            if (-not $root) { continue }
            foreach ($tail in 'Git/bin/sh.exe', 'Programs/Git/bin/sh.exe') {
                $candidate = Join-Path $root $tail
                if (Test-Path -LiteralPath $candidate) { return $candidate }
            }
        }
        throw 'Git Bash (Git/bin/sh.exe) was not found; Claude Code runs hooks with it on Windows.'
    }
    return (Get-Command sh -CommandType Application | Select-Object -First 1).Source
}

function Invoke-HookCommand([string]$Command, [string]$Payload, [hashtable]$Environment) {
    # Runs one hook command as Claude Code does and returns its exit code and standard error.
    $start = [Diagnostics.ProcessStartInfo]::new($script:Sh)
    $start.ArgumentList.Add('-c')
    $start.ArgumentList.Add($Command)
    $start.WorkingDirectory = $RepoRoot
    $start.UseShellExecute = $false
    $start.RedirectStandardInput = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $start.Environment.Remove('CURL_DARK_FACTORY_LANE') | Out-Null
    $start.Environment['CI'] = '1'
    foreach ($name in $Environment.Keys) { $start.Environment[$name] = $Environment[$name] }
    $process = [Diagnostics.Process]::Start($start)
    $process.StandardInput.Write($Payload)
    $process.StandardInput.Close()
    $stdout = $process.StandardOutput.ReadToEndAsync()
    $stderr = $process.StandardError.ReadToEndAsync()
    if (-not $process.WaitForExit(120000)) { $process.Kill($true); throw "Hook command timed out: $Command" }
    $process.WaitForExit()
    return [pscustomobject]@{ ExitCode = $process.ExitCode; StdErr = $stderr.Result.Trim(); StdOut = $stdout.Result.Trim() }
}

function Assert-Hook([string]$Name, [string]$Command, [string]$Payload, [hashtable]$Environment, [int]$Expected) {
    $result = Invoke-HookCommand $Command $Payload $Environment
    $ok = $result.ExitCode -eq $Expected
    if (-not $ok) { $script:failures++ }
    $detail = if ($ok) { '' } else { " stderr: $($result.StdErr)" }
    Write-Host "$(if ($ok) { 'PASS' } else { 'FAIL' }) ($Name) expected exit $Expected, got $($result.ExitCode)$detail"
}

$script:Sh = Get-Sh
$script:failures = 0
$commands = @((Get-Content -Raw -LiteralPath $Settings | ConvertFrom-Json).hooks.PSObject.Properties.Value |
    ForEach-Object { $_.hooks } | Where-Object { $_.type -eq 'command' } | ForEach-Object { $_.command })
$guard = @($commands | Where-Object { $_ -match 'guard-audit-paths\.ps1' })
if ($guard.Count -ne 1) {
    $script:failures++
    Write-Host "FAIL (settings.json registers the audit guard hook once) found $($guard.Count)"
}
else {
    $guard = $guard[0]
    $lane = @{ CURL_DARK_FACTORY_LANE = '1' }
    $readAudit = '{"tool_name":"Read","tool_input":{"file_path":"Audit/README.md"}}'
    Assert-Hook 'guard, lane, Read Audit/README.md' $guard $readAudit $lane 2
    Assert-Hook 'guard, lane, Bash cat Audit/README.md' $guard '{"tool_name":"Bash","tool_input":{"command":"cat Audit/README.md"}}' $lane 2
    Assert-Hook 'guard, lane, Read README.md' $guard '{"tool_name":"Read","tool_input":{"file_path":"README.md"}}' $lane 0
    Assert-Hook 'guard, no lane, Read Audit/README.md' $guard $readAudit @{} 0
    $empty = Join-Path ([IO.Path]::GetTempPath()) ('no-shell-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Force $empty | Out-Null
    try { Assert-Hook 'guard, lane, neither powershell nor pwsh on PATH' $guard $readAudit @{ CURL_DARK_FACTORY_LANE = '1'; PATH = $empty } 2 }
    finally { Remove-Item -Recurse -Force $empty -ErrorAction SilentlyContinue }
}
$neutral = '{"tool_name":"Bash","tool_input":{"command":"echo hello","file_path":"README.md"},"tool_response":{"stdout":"hello"}}'
foreach ($command in @($commands | Where-Object { $_ -notmatch 'guard-audit-paths\.ps1' })) {
    $script = if ($command -match '\.claude/hooks/([\w.-]+\.ps1)') { $Matches[1] } else { $command }
    Assert-Hook "$script starts" $command $neutral @{} 0
}
exit $(if ($script:failures) { 1 } else { 0 })
