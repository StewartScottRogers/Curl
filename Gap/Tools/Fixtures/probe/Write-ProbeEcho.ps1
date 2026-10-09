<#
.SYNOPSIS
    Stand-in for curl in Invoke-GapProbe.ps1's self-test: echoes its arguments and
    environment as JSON.

.DESCRIPTION
    --version prints "curl 0.0.1 (probe-echo)", a version no target names.
    --probe-sleep <seconds> sleeps that long first, so a timeout can be tested.
    Otherwise it prints one JSON object on standard output: Arguments (each argument as
    received), Environment (the variables the probe controls, $null when unset) and
    HomeItems (the names of the items in the folder HOME names; the PowerShell host
    itself creates an AppData or Microsoft folder there as it starts) and WorkingDirectory
    (the folder it started in).
#>
if ($args.Count -eq 1 -and $args[0] -eq '--version') {
    [Console]::Out.Write("curl 0.0.1 (probe-echo) libcurl/0.0.1`n")
    exit 0
}
if ($args.Count -ge 2 -and $args[0] -eq '--probe-sleep') {
    Start-Sleep -Seconds ([int] $args[1])
}
$names = 'HOME', 'USERPROFILE', 'APPDATA', 'CURL_HOME', 'XDG_CONFIG_HOME', 'GAP_PROBE_ADDED', 'GAP_PROBE_REMOVED'
$environment = [ordered] @{}
foreach ($name in $names) { $environment[$name] = [Environment]::GetEnvironmentVariable($name) }
$homeItems = $null
if ($env:HOME -and (Test-Path -LiteralPath $env:HOME)) { $homeItems = @(Get-ChildItem -LiteralPath $env:HOME -Force | ForEach-Object { $_.Name }) }
$report = [ordered] @{ Arguments = @($args); Environment = $environment; HomeItems = $homeItems; WorkingDirectory = [Environment]::CurrentDirectory }
[Console]::Out.Write((ConvertTo-Json -InputObject $report -Depth 4 -Compress))
exit 7
