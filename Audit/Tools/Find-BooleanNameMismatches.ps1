<#
.SYNOPSIS
    Lists public bool members whose name and doc comment point opposite ways - one says "not"
    and the other does not: candidates for the truthfulness auditor's steps 1 and 2.

.DESCRIPTION
    A bool member's name and its doc state the same condition, so their polarity must agree:
    HasScheme returns true "when the URL names its own scheme"; LacksScheme with that same doc
    says the opposite of what it does. Sampling 30 methods at random rarely meets one such
    member (PD-401 went unseen, BL-1370), so this scans them all: every public or internal bool
    method or property in every *.UnitLibrary project and Curl.Console whose name starts Is, Has,
    Can, Should, Lacks, Not, No, Never or Missing.

      name polarity   negative when, after an optional Is/Has/Can/Should, the name starts
                      Not, No, Non, Never, Lacks, Missing, Without, Cannot or Un, or names an absence (Empty,
                      Absent, Blank, Null, None, Unset, Zero), whose doc rightly says no
      doc polarity    negative when the main clause of the <returns> text (else the <summary>) -
                      parentheses dropped, cut at the first comma, semicolon, colon, dash or so - says not, no, never,
                      none, without, lacks, missing, neither, nor or ...n't
      candidate       a documented member whose two polarities differ

    A candidate is not yet a finding: the auditor reads each and files those that are wrong.
    Prints JSON: membersScanned and one object per candidate with file (repository-relative),
    line, member, kind (polarity-mismatch), name and doc polarity, and the doc.

.PARAMETER Root
    The tree to scan. Default: the repository holding this script.

.PARAMETER OutFile
    Write the JSON here as well as to standard output.

.PARAMETER SelfTest
    Scan Audit/Tools/Fixtures/boolean-names and check exactly the planted members are found.
#>
param(
    [string]$Root,
    [string]$OutFile,
    [switch]$SelfTest
)

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$NegativeName = '^(?:Is|Has|Can|Should)?(?:Not|No|Non|Never|Lacks|Missing|Without|Cannot|Un|Empty|Absent|Blank|Null|None|Unset|Zero)(?=[A-Z]|$)'
$NegativeDoc = "(?i)\b(?:not|no|never|none|without|lacks?|missing|neither|nor)\b|n't\b"
$Prefixes = '^(?:Is|Has|Can|Should|Lacks|Not|No|Never|Missing)(?=[A-Z])'

function Get-DocText([string[]]$DocLines) {
    $xml = ($DocLines | ForEach-Object { $_ -replace '^\s*///\s?', '' }) -join ' '
    $part = if ($xml -match '(?s)<returns>(.*?)</returns>') { $Matches[1] } elseif ($xml -match '(?s)<summary>(.*?)</summary>') { $Matches[1] } else { '' }
    # Keep the words a reader sees: <see langword="true" /> becomes true, cref names their member.
    $part = [regex]::Replace($part, '<see\s+(?:langword|cref)="(?:[A-Z]:)?([^"]+)"\s*/>', '$1')
    $part = [regex]::Replace($part, '<paramref\s+name="([^"]+)"\s*/>', '$1')
    $part = [regex]::Replace($part, '<[^>]+>', '')
    return ($part -replace '\s+', ' ').Trim()
}

function Get-MainClause([string]$Text) {
    # The condition itself: parentheses dropped, and cut at the first comma, semicolon, colon,
    # dash or "so", where a doc goes on to explain (BL-1370: "(not a POST)" or ", so curl warns"
    # is not the condition's polarity).
    $main = [regex]::Replace($Text, '\([^)]*\)', '')
    return ($main -split ',|;|:| - | so ', 2)[0]
}

function Get-Members([string]$Path) {
    $lines = [IO.File]::ReadAllLines($Path)
    $members = @()
    for ($i = 0; $i -lt $lines.Length; $i++) {
        if ($lines[$i] -notmatch '^\s*(?:public|internal)\s+(?:static\s+)?(?:override\s+)?(?:virtual\s+)?bool\s+(\w+)\s*(\(|\{|=>)') { continue }
        $name = $Matches[1]
        if ($name -notmatch $Prefixes) { continue }
        $doc = @(); $j = $i - 1
        while ($j -ge 0 -and $lines[$j] -match '^\s*\[') { $j-- }
        while ($j -ge 0 -and $lines[$j] -match '^\s*///') { $doc = @($lines[$j]) + $doc; $j-- }
        $members += [pscustomobject]@{ Name = $name; Line = $i + 1; Doc = (Get-DocText $doc); HasDoc = [bool]$doc.Count; Public = $lines[$i] -match '^\s*public\b' }
    }
    return $members
}

function Invoke-Scan([string]$Tree) {
    $candidates = @(); $scanned = 0
    $projects = @(Get-ChildItem -LiteralPath $Tree -Directory | Where-Object { $_.Name -like '*.UnitLibrary' -or $_.Name -eq 'Curl.Console' })
    foreach ($project in $projects) {
        foreach ($file in @(Get-ChildItem -LiteralPath $project.FullName -Recurse -Filter '*.cs' -File | Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' })) {
            foreach ($m in @(Get-Members $file.FullName)) {
                $scanned++
                $nameNegative = $m.Name -cmatch $NegativeName
                $docNegative = (Get-MainClause $m.Doc) -match $NegativeDoc
                # A member without a doc is the compiler's to catch (CS1591 is an error here), not this scan's.
                $kind = if (-not $m.Doc) { $null } elseif ($nameNegative -ne $docNegative) { 'polarity-mismatch' } else { $null }
                if (-not $kind) { continue }
                $candidates += [ordered]@{
                    file = $file.FullName.Substring($Tree.Length).TrimStart('\', '/') -replace '\\', '/'; line = $m.Line; member = $m.Name; kind = $kind
                    name = $(if ($nameNegative) { 'negative' } else { 'positive' }); doc = $(if ($docNegative) { 'negative' } else { 'positive' }); text = $m.Doc
                }
            }
        }
    }
    return [ordered]@{ membersScanned = $scanned; candidates = $candidates }
}

if ($SelfTest) {
    $r = Invoke-Scan (Resolve-Path (Join-Path $PSScriptRoot 'Fixtures\boolean-names')).Path
    $got = @($r.candidates | ForEach-Object { "$($_.member):$($_.kind)" } | Sort-Object) -join '|'
    $want = @('LacksScheme:polarity-mismatch', 'IsComplete:polarity-mismatch') | Sort-Object
    $failed = 0
    function Check([string]$Name, [bool]$Ok, [string]$Detail) { if (-not $Ok) { $script:failed++ }; Write-Host "$(if ($Ok) { 'PASS' } else { 'FAIL' }) ${Name}: $Detail" }
    Check 'exactly the mismatched members are found, an undocumented one is not' ($got -eq ($want -join '|')) $got
    Check 'every prefixed bool member is scanned, and no other' ($r.membersScanned -eq 7) "members $($r.membersScanned)"
    $lacks = @($r.candidates | Where-Object { $_.member -eq 'LacksScheme' })[0]
    Check 'a candidate carries its file, line and the doc as read' ($lacks.file -eq 'Sample.UnitLibrary/UrlTool.cs' -and $lacks.line -gt 0 -and $lacks.text -eq 'true when the URL names its own scheme.') "$($lacks.file):$($lacks.line) '$($lacks.text)'"
    exit $(if ($failed) { 1 } else { 0 })
}

if (-not $Root) { $Root = $repo }
$result = Invoke-Scan (Resolve-Path $Root).Path
$json = [pscustomobject]$result | ConvertTo-Json -Depth 5
if ($OutFile) { [IO.File]::WriteAllText($OutFile, $json) }
Write-Output $json
