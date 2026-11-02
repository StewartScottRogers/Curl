<#
.SYNOPSIS
    Lists every default a PowerShell script's help claims for a parameter that its param block
    does not give: candidates for the truthfulness auditor's step 5.

.DESCRIPTION
    A script's help that says "-HeartbeatMinutes (default 5)" while its param block says
    $HeartbeatMinutes = 3 is false help an agent would act on. Reading ten sampled statements per
    script missed exactly that (PD-403), so this scan checks every default claim in every script
    with a param block - RunDarkFactory.ps1, Audit/RunAudit.ps1, Gap/RunGapAnalysis.ps1 and all
    the others - against the param block's real default, parsed with PowerShell's own parser.

    Claims read, in a script's comment-based help (its first block comment with a .SYNOPSIS,
    .DESCRIPTION or .PARAMETER keyword) and in the comments directly above a parameter in the
    param block:

      .PARAMETER X     the first "default N", "Default: N", "defaults to N" or "default is N" in
                       that parameter's section ($ before a number, as in $10, is dropped)
      -X ... (default  "-X" followed on the same line, within 60 characters, by "(default N"
                       anywhere in the help, as RunDarkFactory.ps1 writes them
      param comment    "default N" in the comment lines directly above the parameter

    N is a number, or, when the code's default is a non-empty string constant, the claimed word
    (quotes, backticks and trailing punctuation dropped) when it is quoted or stands alone
    ("Default origin/work/dark-factory."), never the first word of a description ("Default a
    four-line message"). A claim is checked against the
    parameter's default when that default is a literal constant, and against "none" when the
    parameter has no default; a default computed by an expression cannot be checked and is
    counted as unverifiable. A [switch] parameter is never checked.

    A candidate is not yet a finding: the auditor reads each one and files those that are real.
    Prints JSON: scriptsScanned, claimsChecked, unverifiable, and one object per candidate with
    file (relative to -Root), line, parameter, claimed, actual, source (help, parameter-help or
    param-comment) and the text of the claim.

.PARAMETER Root
    The tree to scan. Default: the repository holding this script.

.PARAMETER Path
    Scan only these scripts (relative to -Root or absolute) instead of every .ps1 under -Root.

.PARAMETER OutFile
    Write the JSON here as well as to standard output.

.PARAMETER SelfTest
    Scan Audit/Tools/Fixtures/help-defaults and check exactly the planted mismatches are found:
    a .PARAMETER section, an inline "-X (default N)", a param-block comment and a claimed default
    the code does not have, with the true claims, a computed default and a switch left alone.

.EXAMPLE
    powershell -NoProfile -File Audit/Tools/Find-HelpDefaultMismatches.ps1 -Root . -OutFile $env:TEMP\help-defaults.json
#>
param(
    [string]$Root,
    [string[]]$Path = @(),
    [string]$OutFile,
    [switch]$SelfTest
)

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$Number = '\$?(-?\d+(?:\.\d+)?)'
$DefaultWord = '(?i)\b(?:default(?:s to| is)?:?)\s+'

function Get-ParameterDefaults($ParamBlock) {
    # name -> @{ Kind = 'number'|'string'|'none'|'expression'|'switch'; Value; Line }
    $defaults = [ordered]@{}
    foreach ($p in $ParamBlock.Parameters) {
        $name = $p.Name.VariablePath.UserPath
        $isSwitch = @($p.Attributes | Where-Object { $_.TypeName -and $_.TypeName.Name -in 'switch', 'System.Management.Automation.SwitchParameter' }).Count -gt 0
        $entry = @{ Kind = 'none'; Value = $null; Line = $p.Extent.StartLineNumber }
        if ($isSwitch) { $entry.Kind = 'switch' }
        elseif ($p.DefaultValue) {
            $d = $p.DefaultValue
            if ($d -is [System.Management.Automation.Language.ConstantExpressionAst] -and $d -isnot [System.Management.Automation.Language.StringConstantExpressionAst]) { $entry.Kind = 'number'; $entry.Value = [double]$d.Value }
            elseif ($d -is [System.Management.Automation.Language.StringConstantExpressionAst]) { $entry.Kind = 'string'; $entry.Value = "$($d.Value)" }
            elseif ($d -is [System.Management.Automation.Language.UnaryExpressionAst] -and $d.Child -is [System.Management.Automation.Language.ConstantExpressionAst] -and "$($d.TokenKind)" -eq 'Minus') { $entry.Kind = 'number'; $entry.Value = - [double]$d.Child.Value }
            else { $entry.Kind = 'expression'; $entry.Value = $d.Extent.Text }
        }
        $defaults[$name] = $entry
    }
    return $defaults
}

function Get-HelpClaims([string]$Help, [int]$FirstLine, [string[]]$Names) {
    # Each claim in the comment-based help: Parameter, Claimed, Line, Source, Text.
    $claims = @()
    $lines = $Help -split "`r?`n"
    $section = $null
    for ($i = 0; $i -lt $lines.Length; $i++) {
        $line = $lines[$i]
        if ($line -match '^\s*\.PARAMETER\s+(\w+)') { $section = $Matches[1]; continue }
        if ($line -match '^\s*\.[A-Z]+\b') { $section = $null }
        if ($section -and $Names -contains $section -and -not @($claims | Where-Object { $_.Source -eq 'parameter-help' -and $_.Parameter -eq $section }).Count) {
            if ($line -match "$DefaultWord(\S+)(.*)$") {
                # A word is a claimed value only when quoted or alone ("Default origin/work/dark-factory.",
                # "Default: none,"), not the start of a description ("Default a four-line message").
                $word = $Matches[1]; $restOfLine = $Matches[2]
                $alone = $word -match '^[`''"]|[.,;:)]$' -or -not $restOfLine.Trim()
                $claims += [pscustomobject]@{ Parameter = $section; Claimed = (ConvertTo-ClaimValue $word); Alone = $alone; Line = $FirstLine + $i; Source = 'parameter-help'; Text = $line.Trim() }
            }
        }
        foreach ($m in [regex]::Matches($line, "(?<![\w-])-(\w+)\b[^\r\n(]{0,60}?\((?:the\s+|its\s+)?default:?\s+$Number")) {
            if ($Names -notcontains $m.Groups[1].Value) { continue }
            $claims += [pscustomobject]@{ Parameter = $m.Groups[1].Value; Claimed = $m.Groups[2].Value; Line = $FirstLine + $i; Source = 'help'; Text = $line.Trim() }
        }
    }
    return $claims
}

function Get-ParamCommentClaims($ParamBlock, [object[]]$Tokens, [hashtable]$Defaults) {
    # "default N" in the comment lines directly above each parameter (its attributes included).
    $claims = @()
    $comments = @($Tokens | Where-Object { $_.Kind -eq 'Comment' -and $_.Extent.StartOffset -ge $ParamBlock.Extent.StartOffset -and $_.Extent.EndOffset -le $ParamBlock.Extent.EndOffset })
    foreach ($p in $ParamBlock.Parameters) {
        $name = $p.Name.VariablePath.UserPath
        $line = $p.Extent.StartLineNumber - 1
        $above = @()
        while ($true) {
            $c = @($comments | Where-Object { $_.Extent.EndLineNumber -eq $line } | Select-Object -Last 1)
            if (-not $c.Count) { break }
            $above = @($c[0]) + $above
            $line = $c[0].Extent.StartLineNumber - 1
        }
        foreach ($c in $above) {
            if ($c.Text -match "$DefaultWord$Number\b") {
                $claims += [pscustomobject]@{ Parameter = $name; Claimed = $Matches[1]; Line = $c.Extent.StartLineNumber; Source = 'param-comment'; Text = $c.Text.Trim() }
                break
            }
        }
    }
    return $claims
}

function ConvertTo-ClaimValue([string]$Text) {
    # The claimed value as written, without quotes, backticks or trailing punctuation.
    return (($Text -replace '[.,;:)]+$', '') -replace '^[`''"]', '' -replace '[`''"]$', '')
}

function Test-Claim($Claim, $Default) {
    # 'match', 'mismatch' or 'unverifiable', with the actual default as text.
    $claimed = $Claim.Claimed
    $isNumber = $claimed -match '^\$?-?\d+(?:\.\d+)?$'
    $value = if ($isNumber) { [double]($claimed -replace '^\$', '') } else { $null }
    switch ($Default.Kind) {
        'switch' { return @('unverifiable', 'switch') }
        'expression' { return @('unverifiable', $Default.Value) }
        'number' { if (-not $isNumber) { return @('unverifiable', "$($Default.Value)") }; return @($(if ($value -eq $Default.Value) { 'match' } else { 'mismatch' }), [string]$Default.Value) }
        'string' {
            if ($isNumber) { return @($(if ($Default.Value -eq $claimed) { 'match' } else { 'mismatch' }), "'$($Default.Value)'") }
            if (-not $Default.Value -or -not $Claim.Alone) { return @('unverifiable', "'$($Default.Value)'") }
            return @($(if ($Default.Value -ieq $claimed) { 'match' } else { 'mismatch' }), "'$($Default.Value)'")
        }
        default { if (-not $isNumber) { return @('unverifiable', 'none') }; return @('mismatch', 'none') }
    }
}

function Invoke-ScriptScan([string]$File) {
    $tokens = $null; $errors = $null
    $ast = [System.Management.Automation.Language.Parser]::ParseFile($File, [ref]$tokens, [ref]$errors)
    if (-not $ast.ParamBlock) { return $null }
    $defaults = Get-ParameterDefaults $ast.ParamBlock
    $names = @($defaults.Keys)
    $claims = @()
    $help = @($tokens | Where-Object { $_.Kind -eq 'Comment' -and $_.Text.StartsWith('<#') -and $_.Text -match '(?m)^\s*\.(?:SYNOPSIS|DESCRIPTION|PARAMETER)\b' } | Select-Object -First 1)
    if ($help.Count) { $claims += @(Get-HelpClaims $help[0].Text $help[0].Extent.StartLineNumber $names) }
    $claims += @(Get-ParamCommentClaims $ast.ParamBlock $tokens $defaults)
    $results = @()
    foreach ($c in $claims) {
        $verdict = Test-Claim $c $defaults[$c.Parameter]
        $results += [pscustomobject]@{ Claim = $c; Verdict = $verdict[0]; Actual = $verdict[1] }
    }
    return $results
}

function Invoke-Scan([string]$Tree, [string[]]$Only) {
    $files = if ($Only.Count) { @($Only | ForEach-Object { if ([IO.Path]::IsPathRooted($_)) { $_ } else { Join-Path $Tree $_ } } | ForEach-Object { Get-Item -LiteralPath $_ }) }
    else { @(Get-ChildItem -LiteralPath $Tree -Recurse -Filter '*.ps1' -File | Where-Object { $_.FullName.Substring($Tree.Length) -notmatch '[\\/](bin|obj|\.git|node_modules|Fixtures)[\\/]' }) }
    $candidates = @(); $scanned = 0; $checked = 0; $unverifiable = 0
    foreach ($f in $files) {
        $results = Invoke-ScriptScan $f.FullName
        if ($null -eq $results) { continue }
        $scanned++
        foreach ($r in @($results)) {
            if ($r.Verdict -eq 'unverifiable') { $unverifiable++; continue }
            $checked++
            if ($r.Verdict -ne 'mismatch') { continue }
            $candidates += [ordered]@{
                file = $f.FullName.Substring($Tree.Length).TrimStart('\', '/') -replace '\\', '/'; line = $r.Claim.Line; parameter = $r.Claim.Parameter
                claimed = $r.Claim.Claimed; actual = $r.Actual; source = $r.Claim.Source; text = $r.Claim.Text
            }
        }
    }
    return [ordered]@{ scriptsScanned = $scanned; claimsChecked = $checked; unverifiable = $unverifiable; candidates = $candidates }
}

if ($SelfTest) {
    $fixture = (Resolve-Path (Join-Path $PSScriptRoot 'Fixtures\help-defaults')).Path
    $r = Invoke-Scan $fixture @()
    $got = @($r.candidates | ForEach-Object { "$($_.parameter):$($_.source):$($_.claimed)->$($_.actual)" } | Sort-Object) -join '|'
    $want = @('HeartbeatMinutes:help:5->3', 'Planted:parameter-help:10->8', 'Retries:param-comment:4->2', 'Name:parameter-help:none->''main''', 'Limit:help:7->none') | Sort-Object
    $failed = 0
    function Check([string]$Name, [bool]$Ok, [string]$Detail) { if (-not $Ok) { $script:failed++ }; Write-Host "$(if ($Ok) { 'PASS' } else { 'FAIL' }) ${Name}: $Detail" }
    Check 'exactly the false defaults are found, true ones, a computed one and a switch are not' ($got -eq ($want -join '|')) $got
    Check 'the scripts with a param block are scanned, and the one without is not' ($r.scriptsScanned -eq 1) "scripts $($r.scriptsScanned)"
    Check 'true claims are counted as checked, a computed default as unverifiable' ($r.claimsChecked -eq 9 -and $r.unverifiable -eq 2) "checked $($r.claimsChecked), unverifiable $($r.unverifiable)"
    $heartbeat = @($r.candidates | Where-Object { $_.parameter -eq 'HeartbeatMinutes' })[0]
    Check 'a candidate names its file, line and the claim''s text' ($heartbeat.file -eq 'Sample.ps1' -and $heartbeat.line -eq 9 -and $heartbeat.text -like 'Every -HeartbeatMinutes (default 5*') "$($heartbeat.file):$($heartbeat.line) $($heartbeat.text)"
    exit $(if ($failed) { 1 } else { 0 })
}

if (-not $Root) { $Root = $repo }
$result = Invoke-Scan (Resolve-Path $Root).Path $Path
$json = [pscustomobject]$result | ConvertTo-Json -Depth 5
if ($OutFile) { [IO.File]::WriteAllText($OutFile, $json) }
Write-Output $json
