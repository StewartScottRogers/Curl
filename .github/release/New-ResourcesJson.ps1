<#
.SYNOPSIS
  Writes resources.json, the machine-readable list of every file in a Curl release.

.DESCRIPTION
  Run by release.yml after every file of the release is in -DistDirectory, so the list is
  made from the files actually attached and can never name a file that does not exist.
  Test-ResourcesJson.ps1 checks the result in the same run.

  resources.json format (schema 1), UTF-8, no byte order mark:
    schema     1
    name       "Curl"
    version    the version stamped into the packages
    commit     the commit the build was made from
    release    URL of the release page
    resources  one entry for every file in the release except resources.json itself:
      name      what the file is, for example "curl (win-x64)"
      kind      cli             the curl command-line tool, one native archive per platform
                mcp-server      a Curl.Mcp NuGet package (the tool, or one platform's native binary)
                dotnet-library  a Curl.Api or Libcurl NuGet package, platform-neutral
                checksums       SHA256SUMS, covering the curl archives and the .nupkg files
                ai-help         ai-help.md, the output of "curl --ai-help all" from this build
      platform  a runtime identifier (win-x64, win-arm64, linux-x64, linux-arm64, osx-x64,
                osx-arm64) or "any"
      file      the file name
      url       where to download it: <repository>/releases/download/<tag>/<file>
      sha256    lowercase hex SHA-256 of the file
      size      size of the file in bytes
      use       one line on how to run or reference it
      docs      a URL to its documentation
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$DistDirectory,
    [Parameter(Mandatory)][string]$Version,
    [Parameter(Mandatory)][string]$Commit,
    [Parameter(Mandatory)][string]$Repository,
    [Parameter(Mandatory)][string]$Tag
)
$ErrorActionPreference = 'Stop'

$site = "https://github.com/$Repository"
$releasePage = "$site/releases/tag/$Tag"
$blob = "$site/blob/master"

function Get-ResourceDescription([string]$File) {
    switch -Regex ($File) {
        '^curl-(?<rid>[a-z0-9-]+)\.(zip|tar\.gz)$' {
            $rid = $Matches['rid']
            $exe = if ($rid -like 'win-*') { 'curl.exe' } else { 'curl' }
            return @{ name = "curl ($rid)"; kind = 'cli'; platform = $rid
                use = "Extract the archive and run $exe; it takes curl's options and gives curl's output."
                docs = "$blob/DOWNLOAD.md" }
        }
        '^Curl\.Mcp\.(?<rid>(win|linux|osx)-(x64|arm64))\.\d' {
            $rid = $Matches['rid']
            return @{ name = "Curl.Mcp native binary ($rid)"; kind = 'mcp-server'; platform = $rid
                use = 'Installed for you by the Curl.Mcp tool package; install that one, not this.'
                docs = "$blob/Curl.Mcp/README.md" }
        }
        '^Curl\.Mcp\.\d' {
            return @{ name = 'Curl.Mcp'; kind = 'mcp-server'; platform = 'any'
                use = 'Put every Curl.Mcp nupkg file in one folder and run the server over stdio with: dnx Curl.Mcp --add-source <folder>.'
                docs = "$blob/Curl.Mcp/README.md" }
        }
        '^Curl\.Api\.\d' {
            return @{ name = 'Curl.Api'; kind = 'dotnet-library'; platform = 'any'
                use = 'Use the folder holding the nupkg file as a package source, then: dotnet add package Curl.Api. For new .NET code.'
                docs = "$blob/Curl.Api/README.md" }
        }
        '^Libcurl\.\d' {
            return @{ name = 'Libcurl'; kind = 'dotnet-library'; platform = 'any'
                use = 'Use the folder holding the nupkg file as a package source, then: dotnet add package Libcurl. For porting C code that uses libcurl.'
                docs = "$blob/Libcurl/README.md" }
        }
        '^SHA256SUMS$' {
            return @{ name = 'SHA256SUMS'; kind = 'checksums'; platform = 'any'
                use = 'Check downloads with: sha256sum --check SHA256SUMS (each line is "<sha256>  <file>").'
                docs = "$blob/DOWNLOAD.md" }
        }
        '^ai-help\.md$' {
            return @{ name = 'ai-help.md'; kind = 'ai-help'; platform = 'any'
                use = 'Read it: Markdown help for AI agents, the output of "curl --ai-help all" from this build.'
                docs = "$blob/DOWNLOAD.md" }
        }
        default { throw "No description for release file '$File': add it to New-ResourcesJson.ps1" }
    }
}

$files = Get-ChildItem -LiteralPath $DistDirectory -File |
    Where-Object { $_.Name -ne 'resources.json' } | Sort-Object Name
$resources = foreach ($file in $files) {
    $d = Get-ResourceDescription $file.Name
    [ordered]@{
        name = $d.name; kind = $d.kind; platform = $d.platform; file = $file.Name
        url = "$site/releases/download/$Tag/$($file.Name)"
        sha256 = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        size = $file.Length; use = $d.use; docs = $d.docs
    }
}
$document = [ordered]@{
    schema = 1; name = 'Curl'; version = $Version; commit = $Commit
    release = $releasePage; resources = @($resources)
}
$json = $document | ConvertTo-Json -Depth 5
[IO.File]::WriteAllText((Join-Path (Resolve-Path $DistDirectory) 'resources.json'), $json + "`n", [Text.UTF8Encoding]::new($false))
Write-Host "resources.json lists $(@($resources).Count) files"
