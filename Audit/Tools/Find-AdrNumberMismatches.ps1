<#
.SYNOPSIS
    Lists every ADR, of all of them in Documentation/Planning/Decisions, that states an exit code two ways:
    candidates for the truthfulness auditor's step 4.

.DESCRIPTION
    The truthfulness auditor read only the 10 most recent Accepted ADRs, so an exit code changed in
    the 11th went unseen although its own title still named the right one (audit
    20261102-172448). This scan reads every ADR, whatever the sample, and flags, as a candidate:

      exit-name-mismatch   an exit number written beside a curl code name whose value is another
                           number: "exits 9 (CURLE_WEIRD_SERVER_REPLY)", "CurlExitCode.CouldntConnect
                           (exit 6)". The value comes from CurlExitCode.cs in the tree; a CURLE_ name
                           is matched to its member without the prefix and the underscores
                           (CURLE_WEIRD_SERVER_REPLY is WeirdServerReply).
      title-exit-mismatch  a title that names its exit code ("ends with exit 8") while the Decision
                           section states an exit code the title does not name.
      exit-not-in-code     a Decision whose exit codes the code it names in backticks (`TypeName` or
                           `TypeName.Member`, read from TypeName.cs and its partial files in every
                           *.UnitLibrary and host; an interface I... is skipped) produces, all but
                           one: that one is a number the code may contradict, even with no code
                           name beside it ("fails with exit 9" where the named code only raises
                           CurlExitCode.WeirdServerReply). A file produces the exits of every
                           CurlExitCode.<Member> and (CurlExitCode)<N> in it. A Decision with two or
                           more exits the named code never produces takes them from code it does
                           not name, and is left alone.

    A candidate is not yet a finding: the auditor reads the ADR and the code, and files the
    sentence the code contradicts (stale-adr). Prints JSON: one object per candidate with file
    (repository-relative), line, kind and detail; and adrsScanned, exitCodesKnown.

.PARAMETER Root
    The tree to scan. Default: the repository holding this script.

.PARAMETER OutFile
    Write the JSON here as well as to standard output.

.PARAMETER SelfTest
    Scan Audit/Tools/Fixtures/adr-numbers and check that each kind is found, and nothing else: a
    code name beside the wrong number, a Decision that leaves its title's exit code, a Decision exit
    the code it names never produces, and sound ADRs that state their codes one way or take most of
    their exits from code they do not name.

.EXAMPLE
    powershell -NoProfile -File Audit/Tools/Find-AdrNumberMismatches.ps1 -Root . -OutFile $env:TEMP\adr-numbers.json
#>
param(
    [string]$Root,
    [string]$OutFile,
    [switch]$SelfTest
)

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path

function Get-ExitCodeValues([string]$Tree) {
    # Upper-case member name without underscores -> value, from CurlExitCode.cs in the tree.
    $values = @{}
    $file = Get-ChildItem -LiteralPath $Tree -Recurse -Depth 2 -Filter 'CurlExitCode.cs' -File -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' } | Select-Object -First 1
    if (-not $file) { return $values }
    foreach ($m in [regex]::Matches([IO.File]::ReadAllText($file.FullName), '(?m)^\s*([A-Z]\w*)\s*=\s*(\d+)\s*,?')) {
        $values[$m.Groups[1].Value.ToUpperInvariant()] = [int]$m.Groups[2].Value
    }
    return $values
}

function Get-CodeValue([string]$Name, [hashtable]$Values) {
    # CURLE_WEIRD_SERVER_REPLY or CurlExitCode.WeirdServerReply -> 8; $null for a name the enum lacks.
    $key = ($Name -replace '^CURLE_', '' -replace '^CurlExitCode\.', '' -replace '_', '').ToUpperInvariant()
    if ($Values.ContainsKey($key)) { return $Values[$key] }
    return $null
}

function Get-LineOf([string]$Text, [int]$Index) { return ($Text.Substring(0, $Index) -split "`n").Count }

$exitWord = '\bexit(?:s|ed|ing)?(?:\s+code)?(?:\s+with)?[\s-]+'
$codeName = '(CURLE_[A-Z0-9_]+|CurlExitCode\.[A-Z]\w*)'

function Get-StatedExits([string]$Text) {
    # Every exit number the text states: "exit 8", "exits 8", "exit code 8", "exit-8", "exit 8 or 9".
    $found = @()
    foreach ($m in [regex]::Matches($Text, "(?i)$exitWord(\d+)((?:\s*(?:,|or|and)\s*\d+)*)")) {
        $found += [int]$m.Groups[1].Value
        foreach ($n in [regex]::Matches($m.Groups[2].Value, '\d+')) { $found += [int]$n.Value }
    }
    return @($found | Select-Object -Unique)
}

function Get-Candidates([string]$Path, [string]$Relative, [hashtable]$Values) {
    $text = [IO.File]::ReadAllText($Path)
    $found = @()
    # A number and a code name side by side, either order.
    $pairs = @(
        @{ Pattern = "(?i)$exitWord(\d+)\s*\(\s*``?$codeName``?"; Number = 1; Name = 2 },
        @{ Pattern = "``?$codeName``?\s*\(\s*(?:(?i)exit(?:\s+code)?\s*)?(\d+)\s*\)"; Number = 2; Name = 1 }
    )
    foreach ($p in $pairs) {
        foreach ($m in [regex]::Matches($text, $p.Pattern)) {
            $number = [int]$m.Groups[$p.Number].Value
            $name = $m.Groups[$p.Name].Value
            $value = Get-CodeValue $name $Values
            if ($null -ne $value -and $value -ne $number) {
                $found += [ordered]@{ file = $Relative; line = (Get-LineOf $text $m.Index); kind = 'exit-name-mismatch'; detail = "'$($m.Value.Trim())' gives $name as $number; CurlExitCode says $value" }
            }
        }
    }
    # The title's exit codes against the Decision's.
    $title = [regex]::Match($text, '(?m)^#\s+(.+)$')
    $decision = [regex]::Match($text, '(?ms)^##\s+Decision\s*$(.*?)(?=^##\s|\z)')
    if ($title.Success -and $decision.Success) {
        $titled = @(Get-StatedExits $title.Groups[1].Value)
        if ($titled.Count) {
            $stated = @(Get-StatedExits $decision.Groups[1].Value)
            $other = @($stated | Where-Object { $_ -notin $titled })
            if ($other.Count) {
                $found += [ordered]@{ file = $Relative; line = (Get-LineOf $text $decision.Index); kind = 'title-exit-mismatch'; detail = "title names exit $($titled -join ', '); the Decision states exit $($other -join ', ') as well" }
            }
        }
    }
    return $found
}

function Get-CodeIndex([string]$Tree) {
    # Type name -> the production source files named for it (AzureBlobOperations.cs, and the parts
    # of a partial type such as CurlCommandRunner.Retry.cs), from every *.UnitLibrary and host.
    $index = @{}
    $projects = @(Get-ChildItem -LiteralPath $Tree -Directory | Where-Object { $_.Name -like '*.UnitLibrary' -or $_.Name -in 'Curl.Console', 'Curl.Api', 'Libcurl', 'Curl.Mcp', 'Curl.Config' })
    foreach ($project in $projects) {
        foreach ($file in @(Get-ChildItem -LiteralPath $project.FullName -Recurse -Filter '*.cs' -File | Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' })) {
            $type = $file.Name.Split('.')[0]
            if (-not $index.ContainsKey($type)) { $index[$type] = @() }
            $index[$type] += $file.FullName
        }
    }
    return $index
}

$script:ProducedExits = @{}
function Get-ProducedExits([string]$Path, [hashtable]$Values) {
    # The exit codes a source file produces: each CurlExitCode.<Member> it names, and each
    # (CurlExitCode)<N> cast, as numbers.
    if ($script:ProducedExits.ContainsKey($Path)) { return $script:ProducedExits[$Path] }
    $text = [IO.File]::ReadAllText($Path)
    $codes = @([regex]::Matches($text, '\bCurlExitCode\.([A-Z]\w*)') | ForEach-Object { Get-CodeValue $_.Groups[1].Value $Values } | Where-Object { $null -ne $_ })
    $codes += @([regex]::Matches($text, '\(\s*CurlExitCode\s*\)\s*(\d+)') | ForEach-Object { [int]$_.Groups[1].Value })
    $script:ProducedExits[$Path] = @($codes | Select-Object -Unique)
    return $script:ProducedExits[$Path]
}

function Get-CodeExitCandidates([string]$Text, [string]$Relative, [hashtable]$Values, [hashtable]$Index) {
    # The Decision's exit codes against the code the ADR names in backticks (`AzureBlobOperations`,
    # `AzureBlobOperations.BlockSizeFor`; an interface such as `ITransferContext` names no behaviour):
    # when the code produces every exit the Decision states but one, that one is a number the code
    # contradicts - whether or not a code name stands beside it. An ADR whose exits mostly come from
    # code it does not name (two or more the named code never produces) is left alone.
    $decision = [regex]::Match($Text, '(?ms)^##\s+Decision\s*$(.*?)(?=^##\s|\z)')
    if (-not $decision.Success) { return @() }
    $types = @([regex]::Matches($Text, '`([A-Z][A-Za-z0-9]+)(?:\.[A-Za-z]\w*)*(?:\(\))?`') | ForEach-Object { $_.Groups[1].Value } |
        Where-Object { $_ -cnotmatch '^I[A-Z]' -and $_ -ne 'CurlExitCode' -and $Index.ContainsKey($_) } | Select-Object -Unique)
    if (-not $types.Count) { return @() }
    $producing = @($types | Where-Object { @($Index[$_] | ForEach-Object { Get-ProducedExits $_ $Values }).Count })
    $produced = @($producing | ForEach-Object { $Index[$_] } | ForEach-Object { Get-ProducedExits $_ $Values } | Select-Object -Unique | Sort-Object)
    if (-not $produced.Count) { return @() }
    $stated = @{}    # exit number -> the line that first states it (a hashtable: an ordered one reads an int key as a position)
    foreach ($m in [regex]::Matches($decision.Groups[1].Value, "(?i)$exitWord(\d+)((?:\s*(?:,|or|and)\s*\d+)*)")) {
        $numbers = @([int]$m.Groups[1].Value) + @([regex]::Matches($m.Groups[2].Value, '\d+') | ForEach-Object { [int]$_.Value })
        foreach ($n in $numbers) { if ($n -ne 0 -and -not $stated.ContainsKey($n)) { $stated[$n] = Get-LineOf $Text ($decision.Groups[1].Index + $m.Index) } }
    }
    $unmatched = @($stated.Keys | Where-Object { $_ -notin $produced })
    $matched = @($stated.Keys | Where-Object { $_ -in $produced })
    if ($unmatched.Count -ne 1 -or -not $matched.Count) { return @() }
    $n = $unmatched[0]
    return @([ordered]@{ file = $Relative; line = $stated[$n]; kind = 'exit-not-in-code'
        detail = "the Decision states exit $n; the code it names ($($producing -join ', ')) produces exit $($produced -join ', ') and never $n" })
}

function Invoke-Scan([string]$Tree) {
    $values = Get-ExitCodeValues $Tree
    $index = Get-CodeIndex $Tree
    $decisions = Join-Path $Tree 'Documentation/Planning/Decisions'
    $candidates = @(); $adrs = 0
    if (Test-Path -LiteralPath $decisions) {
        foreach ($file in @(Get-ChildItem -LiteralPath $decisions -Filter 'ADR-*.md' -File | Sort-Object Name)) {
            $adrs++
            $relative = $file.FullName.Substring($Tree.Length).TrimStart('\', '/') -replace '\\', '/'
            $candidates += @(Get-Candidates $file.FullName $relative $values)
            $candidates += @(Get-CodeExitCandidates ([IO.File]::ReadAllText($file.FullName)) $relative $values $index)
        }
    }
    return [ordered]@{ adrsScanned = $adrs; exitCodesKnown = $values.Count; candidates = $candidates }
}

if ($SelfTest) {
    $r = Invoke-Scan (Resolve-Path (Join-Path $PSScriptRoot 'Fixtures/adr-numbers')).Path
    $got = @($r.candidates | ForEach-Object { "$(Split-Path $_.file -Leaf):$($_.kind)" } | Sort-Object)
    $want = @(
        'ADR-0002-wrong-code-name.md:exit-name-mismatch',
        'ADR-0002-wrong-code-name.md:title-exit-mismatch',
        'ADR-0003-member-name-beside-the-wrong-number.md:exit-name-mismatch',
        'ADR-0005-an-exit-the-named-code-never-produces.md:exit-not-in-code'
    ) | Sort-Object
    $failed = 0
    function Check([string]$Name, [bool]$Ok, [string]$Detail) { if (-not $Ok) { $script:failed++ }; Write-Host "$(if ($Ok) { 'PASS' } else { 'FAIL' }) ${Name}: $Detail" }
    Check 'each mismatch is found, and nothing else' (($got -join '|') -eq ($want -join '|')) ($got -join ', ')
    Check 'ADRs and exit codes are counted' ($r.adrsScanned -eq 6 -and $r.exitCodesKnown -eq 4) "adrs $($r.adrsScanned), exit codes $($r.exitCodesKnown)"
    $name = @($r.candidates | Where-Object { $_.file -like '*ADR-0002*' -and $_.kind -eq 'exit-name-mismatch' })[0]
    Check 'a code name beside the wrong number says both' ($name.detail -eq "'exits 9 (``CURLE_WEIRD_SERVER_REPLY``' gives CURLE_WEIRD_SERVER_REPLY as 9; CurlExitCode says 8" -and $name.line -gt 1) "$($name.line): $($name.detail)"
    $title = @($r.candidates | Where-Object { $_.kind -eq 'title-exit-mismatch' })[0]
    Check 'a Decision that leaves its title''s code says which' ($title.detail -eq 'title names exit 8; the Decision states exit 9 as well') $title.detail
    $code = @($r.candidates | Where-Object { $_.kind -eq 'exit-not-in-code' })[0]
    Check 'an exit the named code never produces says which code and what it produces' ($code.detail -eq 'the Decision states exit 6; the code it names (BlockPlanner) produces exit 7, 8 and never 6' -and $code.line -gt 1) "$($code.line): $($code.detail)"
    Check 'exit numbers are read in each written form' ((@(Get-StatedExits 'exits 8, exit code 9, exit-23 or 26 and ends with exit 7') -join ',') -eq '8,9,23,26,7') (@(Get-StatedExits 'exits 8, exit code 9, exit-23 or 26 and ends with exit 7') -join ',')
    exit $(if ($failed) { 1 } else { 0 })
}

if (-not $Root) { $Root = $repo }
$result = Invoke-Scan (Resolve-Path $Root).Path
$json = [pscustomobject]$result | ConvertTo-Json -Depth 5
if ($OutFile) { [IO.File]::WriteAllText($OutFile, $json) }
Write-Output $json
