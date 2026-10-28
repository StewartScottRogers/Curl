# The platform seam (BL-3919): which PowerShell executable the factory starts for a child
# script and where the per-user data folder is. Dot-sourced by RunDarkFactory.ps1 in its
# order, before every file that uses it; see the loader there.

function Test-FactoryOnWindows {
    # $IsWindows does not exist on Windows PowerShell 5.1, which only runs on Windows.
    return ($PSVersionTable.PSVersion.Major -lt 6) -or [bool]$IsWindows
}

function Get-FactoryPowerShellExe {
    if (Test-FactoryOnWindows) { return 'powershell' }
    return 'pwsh'
}

function Get-FactoryDataDir {
    if (Test-FactoryOnWindows) { return (Join-Path $env:LOCALAPPDATA 'Curl') }
    if ($IsMacOS) { return (Join-Path $HOME 'Library/Application Support/Curl') }
    $base = if ($env:XDG_DATA_HOME) { $env:XDG_DATA_HOME } else { Join-Path $HOME '.local/share' }
    return (Join-Path $base 'Curl')
}

if ($TestPlatform) {
    # Proves both helpers answer as the current OS requires.
    $script:platformFailed = 0
    $check = { param([string]$Name, [bool]$Ok, [string]$Detail) if (-not $Ok) { $script:platformFailed++ }; Write-Host "$(if ($Ok) { 'PASS' } else { 'FAIL' }) ${Name}: $Detail" -ForegroundColor $(if ($Ok) { 'Green' } else { 'Red' }) }
    $exe = Get-FactoryPowerShellExe
    $dir = Get-FactoryDataDir
    if (Test-FactoryOnWindows) {
        & $check 'PowerShell executable on Windows' ($exe -eq 'powershell') $exe
        & $check 'data folder on Windows' ($dir -eq (Join-Path $env:LOCALAPPDATA 'Curl')) $dir
    } else {
        & $check 'PowerShell executable off Windows' ($exe -eq 'pwsh') $exe
        $want = if ($IsMacOS) { Join-Path $HOME 'Library/Application Support/Curl' } elseif ($env:XDG_DATA_HOME) { Join-Path $env:XDG_DATA_HOME 'Curl' } else { Join-Path $HOME '.local/share/Curl' }
        & $check 'data folder off Windows' ($dir -eq $want) $dir
    }
    exit $(if ($script:platformFailed) { 1 } else { 0 })
}

# This file ran to its end; RunDarkFactory.ps1 goes on to the next one.
$AreaFileFinished = $true
