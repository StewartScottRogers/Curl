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

function Get-FactoryTotalMemoryBytes {
    if (Test-FactoryOnWindows) { return [double](Get-CimInstance Win32_ComputerSystem).TotalPhysicalMemory }
    if ($IsMacOS) { return [double]((& sysctl -n hw.memsize) -join '').Trim() }
    foreach ($line in Get-Content -LiteralPath '/proc/meminfo') {
        if ($line -match '^MemTotal:\s+(\d+)\s*kB') { return [double]$Matches[1] * 1KB }
    }
    return 0.0
}

function Get-FactoryFreeMemoryBytes {
    if (Test-FactoryOnWindows) { return [double](Get-CimInstance Win32_OperatingSystem).FreePhysicalMemory * 1KB }
    if ($IsMacOS) {
        $vmStat = (& vm_stat) -join "`n"
        $pageSize = if ($vmStat -match 'page size of (\d+) bytes') { [double]$Matches[1] } else { 4096.0 }
        $pages = 0.0
        foreach ($kind in 'free', 'inactive', 'speculative') {
            if ($vmStat -match "Pages ${kind}:\s+(\d+)") { $pages += [double]$Matches[1] }
        }
        return $pages * $pageSize
    }
    foreach ($line in Get-Content -LiteralPath '/proc/meminfo') {
        if ($line -match '^MemAvailable:\s+(\d+)\s*kB') { return [double]$Matches[1] * 1KB }
    }
    return 0.0
}

function Get-FactoryProcessTable {
    # Objects with ProcessId, ParentProcessId, CommandLine and CreationDate (null off Windows).
    if (Test-FactoryOnWindows) {
        return @(Get-CimInstance Win32_Process -Property ProcessId, ParentProcessId, CommandLine, CreationDate)
    }
    $table = foreach ($line in (& ps -axo 'pid=,ppid=,args=')) {
        if ("$line" -match '^\s*(\d+)\s+(\d+)\s*(.*)$') {
            [pscustomobject]@{ ProcessId = [int]$Matches[1]; ParentProcessId = [int]$Matches[2]; CommandLine = $Matches[3]; CreationDate = $null }
        }
    }
    return @($table)
}

function Get-FactoryNullDevice {
    # The null device a shell command line redirects into: nul for cmd.exe, /dev/null for sh.
    if (Test-FactoryOnWindows) { return 'nul' }
    return '/dev/null'
}

function Set-FactoryShellCommand {
    # Points $StartInfo at the platform's shell running $CommandLine, so a command line with
    # redirections (claude -p ... 2>"err.txt") runs the same way everywhere (BL-3933):
    # cmd.exe /d /c on Windows, sh -c elsewhere.
    param([System.Diagnostics.ProcessStartInfo]$StartInfo, [string]$CommandLine)
    if (Test-FactoryOnWindows) {
        $StartInfo.FileName = $env:ComSpec
        $StartInfo.Arguments = "/d /c $CommandLine"
        return
    }
    $StartInfo.FileName = '/bin/sh'
    $StartInfo.ArgumentList.Add('-c')
    $StartInfo.ArgumentList.Add($CommandLine)
}

if ($TestPlatform) {
    # Proves the helpers answer as the current OS requires.
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
    $total = Get-FactoryTotalMemoryBytes
    $free = Get-FactoryFreeMemoryBytes
    & $check 'total memory above zero' ($total -gt 0) "$total"
    & $check 'free memory above zero and not above total' (($free -gt 0) -and ($free -le $total)) "$free"
    $me = @(Get-FactoryProcessTable | Where-Object ProcessId -eq $PID)
    & $check 'current process in the process table' ($me.Count -eq 1 -and "$($me[0].CommandLine)" -ne '') "$($me.Count) row(s)"
    $parent = (Get-Process -Id $PID).Parent
    if ($parent) {
        $parentRow = @(Get-FactoryProcessTable | Where-Object ProcessId -eq $parent.Id)
        & $check 'parent process in the process table' ($me.Count -eq 1 -and $me[0].ParentProcessId -eq $parent.Id -and $parentRow.Count -eq 1) "parent $($parent.Id)"
    }
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    Set-FactoryShellCommand $psi "echo shell-ok 2>$(Get-FactoryNullDevice)"
    $psi.UseShellExecute = $false
    $psi.RedirectStandardOutput = $true
    $shell = [System.Diagnostics.Process]::Start($psi)
    $said = $shell.StandardOutput.ReadToEnd().Trim()
    $shell.WaitForExit()
    & $check 'a shell command line runs, redirection and all' ($said -eq 'shell-ok' -and $shell.ExitCode -eq 0) "said '$said', exit $($shell.ExitCode)"
    exit $(if ($script:platformFailed) { 1 } else { 0 })
}

# This file ran to its end; RunDarkFactory.ps1 goes on to the next one.
$AreaFileFinished = $true
