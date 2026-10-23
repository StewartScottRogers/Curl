<#
.SYNOPSIS
  Renders the download page (plain HTML, no script) from resources.json.
.DESCRIPTION
  Fills download/index.html.template with one table row per entry of resources.json, so the
  page shows exactly the same table. Rows keep resources.json's order, which lists Curl.Api
  before Libcurl.
#>
param(
    [Parameter(Mandatory)][string]$ResourcesJson,
    [Parameter(Mandatory)][string]$OutFile,
    [string]$Template = (Join-Path $PSScriptRoot 'download/index.html.template')
)
$ErrorActionPreference = 'Stop'
function ConvertTo-HtmlText([object]$Value) { [System.Net.WebUtility]::HtmlEncode([string]$Value) }
$doc = Get-Content -Raw -LiteralPath $ResourcesJson | ConvertFrom-Json
$rows = foreach ($r in $doc.resources) {
    '<tr><td><a href="{0}">{1}</a></td><td>{2}</td><td>{3}</td><td><a href="{0}">{4}</a></td><td>{5}</td><td><code>{6}</code></td><td>{7} <a href="{8}">docs</a></td></tr>' -f `
        (ConvertTo-HtmlText $r.url), (ConvertTo-HtmlText $r.name), (ConvertTo-HtmlText $r.kind), (ConvertTo-HtmlText $r.platform), (ConvertTo-HtmlText $r.file), $r.size, (ConvertTo-HtmlText $r.sha256), (ConvertTo-HtmlText $r.use), (ConvertTo-HtmlText $r.docs)
}
(Get-Content -Raw -LiteralPath $Template).Replace('{{VERSION}}', (ConvertTo-HtmlText $doc.version)).Replace('{{COMMIT}}', (ConvertTo-HtmlText $doc.commit)).Replace('{{ROWS}}', ($rows -join "`n")) |
    Set-Content -LiteralPath $OutFile -Encoding utf8NoBOM
