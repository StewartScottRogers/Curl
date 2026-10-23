<#
.SYNOPSIS
  Fails when resources.json and the files of a release disagree.

.DESCRIPTION
  Checks, independently of New-ResourcesJson.ps1, that resources.json lists every file in
  -DistDirectory except itself and nothing else, once each, with each file's real SHA-256
  and size; and, when -CurlPath is given, that ai-help.md is byte-identical to what
  "<CurlPath> --ai-help all" prints. The format is documented in New-ResourcesJson.ps1.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$DistDirectory,
    [string]$CurlPath
)
$ErrorActionPreference = 'Stop'
$problems = [System.Collections.Generic.List[string]]::new()

$listed = Get-Content -LiteralPath (Join-Path $DistDirectory 'resources.json') -Raw | ConvertFrom-Json
if ($listed.schema -ne 1) { $problems.Add("schema is '$($listed.schema)', not 1") }
$entries = @($listed.resources)
$actual = @(Get-ChildItem -LiteralPath $DistDirectory -File | Where-Object { $_.Name -ne 'resources.json' })

foreach ($entry in $entries) {
    $path = Join-Path $DistDirectory $entry.file
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { $problems.Add("$($entry.file) is listed but is not there"); continue }
    $hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($hash -ne $entry.sha256) { $problems.Add("$($entry.file): sha256 is $hash, resources.json says $($entry.sha256)") }
    if ((Get-Item -LiteralPath $path).Length -ne $entry.size) { $problems.Add("$($entry.file): size differs from resources.json") }
}
foreach ($duplicate in ($entries | Group-Object file | Where-Object Count -gt 1)) { $problems.Add("$($duplicate.Name) is listed twice") }
foreach ($file in $actual) {
    if ($file.Name -notin $entries.file) { $problems.Add("$($file.Name) is in the release but not in resources.json") }
}

if ($CurlPath) {
    $startInfo = [Diagnostics.ProcessStartInfo]::new($CurlPath)
    $startInfo.ArgumentList.Add('--ai-help')
    $startInfo.ArgumentList.Add('all')
    $startInfo.RedirectStandardOutput = $true
    $process = [Diagnostics.Process]::Start($startInfo)
    $memory = [IO.MemoryStream]::new()
    $process.StandardOutput.BaseStream.CopyTo($memory)
    $process.WaitForExit()
    $written = [IO.File]::ReadAllBytes((Join-Path $DistDirectory 'ai-help.md'))
    if (-not [Linq.Enumerable]::SequenceEqual([byte[]]$memory.ToArray(), [byte[]]$written)) {
        $problems.Add('ai-help.md is not byte-identical to curl --ai-help all')
    }
}

if ($problems.Count -gt 0) {
    $problems | ForEach-Object { Write-Error $_ -ErrorAction Continue }
    throw "resources.json and the release files disagree ($($problems.Count) problems)"
}
Write-Host "resources.json agrees with all $($actual.Count) release files"
