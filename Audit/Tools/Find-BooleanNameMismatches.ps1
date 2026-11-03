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
      candidate       a documented member whose two polarities differ (polarity-mismatch)

    Antonyms (antonym-mismatch). A name renamed to its opposite keeps a doc in the old sense:
    IsValid renamed IsInvalid still documents "true when the value is valid" (PD-401 went unseen
    in audit after audit, since the polarity words above do not include Invalid). So every
    documented public or internal method or property, of any type, is also checked: its key word
    (the first camel-case word past Try, Is, Has, Can, Should, Are, Was, Get and Set) against the
    main clause of its <summary> and <returns> text (cut as above), word forms such as reads,
    written, decoding counted:

      prefix          (a name starting Is, Has, Can, Should, Are or Was) a key word made of a negating prefix (Un, In, Im, Il, Ir, Dis, Non) and a
                      root of at least four letters (Invalid = In + valid) whose doc names the
                      root but not the word itself, in a main clause with no negation
                      ("valid", not "not valid"); and the reverse, a key word whose doc names
                      only its prefixed opposite (IsValid documented "is invalid")
      pairs           a key word from one side of an antonym pair whose doc names the other
                      side as a present-tense verb (reads, not encoded or encoding) and not its
                      own side in any form: Read/Write, Encode/Decode, Open/Close,
                      Enable/Disable, Allow/Deny, Allow/Refuse, Accept/Reject, Valid/Invalid,
                      Lock/Unlock (TryWrite documented "Tries to read")

    Body and callers. A doc need not use the name's word at all ("true for a sample+tls URL"), so a
    state name renamed to its opposite can pass both checks above; what the member returns and how
    its callers read it still show it. For every prefixed bool member:

      body-polarity-mismatch    a key word with a negating prefix (Untrusted = Un + trusted, as above)
                                whose body names the root (TrustedScheme) and never the word itself,
                                and does not turn the condition round (no !, is not, == false,
                                false or Not/No/Non/Never word); and the reverse, a key word of four
                                letters or more whose body names only its prefixed opposite
                                (IsEncrypted => mode == UnencryptedMode)
      caller-polarity-mismatch  such a negatively named member that a caller in the same project
                                reads as its opposite: Name(x) ? <the root's value> : <another value>
                                (IsUntrusted(url) ? TrustedDefaultPort : DefaultPort)

    A candidate is not yet a finding: the auditor reads each and files those that are wrong.
    Prints JSON: membersScanned (the prefixed bool members), namesScanned (every documented
    member the antonym check read) and one object per candidate with file (repository-relative),
    line, member, kind (polarity-mismatch, antonym-mismatch, body-polarity-mismatch or
    caller-polarity-mismatch), the name and doc polarity or the opposed words, and the doc, the
    body or the callers as read.

.PARAMETER Root
    The tree to scan. Default: the repository holding this script.

.PARAMETER OutFile
    Write the JSON here as well as to standard output.

.PARAMETER SelfTest
    Scan Audit/Tools/Fixtures/boolean-names and check exactly the planted members are found, the
    polarity, antonym, body and caller mismatches all.
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
# Antonym pairs, both ways. A name word on one side whose doc names only the other is a candidate.
$AntonymPairs = @(@('read', 'write'), @('encode', 'decode'), @('open', 'close'), @('enable', 'disable'), @('allow', 'deny'), @('allow', 'refuse'), @('accept', 'reject'), @('valid', 'invalid'), @('lock', 'unlock'))
$NegatingPrefix = '^(un|in|im|il|ir|dis|non)([a-z]{4,})$'

function Get-WordForms([string]$Word) {
    # The inflections a doc uses for a name word: read -> read, reads, reading, reader; write ->
    # writes, writing, written, wrote; deny -> denies, denied.
    $w = $Word.ToLowerInvariant()
    $forms = @($w, "${w}s", "${w}es", "${w}ed", "${w}d", "${w}ing", "${w}er", "${w}ers")
    if ($w.EndsWith('e')) { $stem = $w.Substring(0, $w.Length - 1); $forms += @("${stem}ing", "${stem}ten", "${stem}able") }
    if ($w -match '[^aeiou]y$') { $stem = $w.Substring(0, $w.Length - 1); $forms += @("${stem}ies", "${stem}ied") }
    if ($w -eq 'write') { $forms += 'wrote' }
    return @($forms | Select-Object -Unique)
}

function Test-DocNames([string]$Doc, [string]$Word, [switch]$VerbOnly) {
    # Whether the doc text uses the word in any of its forms, as a whole word; with -VerbOnly, only
    # as a verb in the present (read, reads, denies), never encoded or encoding.
    $w = $Word.ToLowerInvariant()
    $forms = if ($VerbOnly) { @($w, "${w}s", "${w}es") + @(if ($w -match '[^aeiou]y$') { $w.Substring(0, $w.Length - 1) + 'ies' }) } else { Get-WordForms $Word }
    $alternatives = ($forms | ForEach-Object { [regex]::Escape($_) }) -join '|'
    return $Doc -match "(?i)\b(?:$alternatives)\b"
}

function Get-NameWords([string]$Name) {
    return @([regex]::Matches($Name, '[A-Z]+(?![a-z])|[A-Z]?[a-z0-9]+') | ForEach-Object { $_.Value.ToLowerInvariant() })
}

function Get-KeyWord([string]$Name) {
    # The word that carries a name's sense: its first, past Try, Is, Has, Can, Should, Are, Was,
    # Get and Set (TryWrite -> write, IsInvalid -> invalid, Decode -> decode).
    $words = @(Get-NameWords $Name)
    $k = 0
    while ($k -lt $words.Count - 1 -and $words[$k] -in 'try', 'is', 'has', 'can', 'should', 'are', 'was', 'get', 'set') { $k++ }
    if (-not $words.Count) { return '' }
    return $words[$k]
}

function Get-AntonymMismatch([string]$Name, [string]$Doc) {
    # Whether the name's key word is contradicted by an antonym in the doc's main clause (before
    # its first comma, semicolon, colon, dash or "so"; the doc's own explanations are left out),
    # as "<name word> vs <doc word>"; '' when not.
    if (-not $Doc) { return '' }
    $main = Get-MainClause $Doc
    $mainNegative = $main -match $NegativeDoc
    # The prefix rules are for states (IsInvalid, CanUnlock): a verb such as Unwrap or Unescape is
    # documented by what it undoes, so only a name starting Is, Has, Can, Should, Are or Was is read.
    $state = $Name -cmatch '^(?:Is|Has|Can|Should|Are|Was)[A-Z]'
    foreach ($word in @(Get-KeyWord $Name | Where-Object { $_ })) {
        if (Test-DocNames $main $word) { continue }
        if ($state -and $word -match $NegatingPrefix) {
            $root = $Matches[2]
            if (-not $mainNegative -and (Test-DocNames $main $root)) { return "$word vs $root" }
        }
        if ($state) {
            foreach ($prefix in 'un', 'in', 'im', 'il', 'ir', 'dis', 'non') {
                if ($word.Length -ge 4 -and (Test-DocNames $main "$prefix$word")) { return "$word vs $prefix$word" }
            }
        }
        foreach ($pair in $AntonymPairs) {
            foreach ($side in 0, 1) {
                if ((Get-WordForms $pair[$side]) -notcontains $word) { continue }
                $other = $pair[1 - $side]
                # The other side as a verb (read, reads), not a participle or a noun (encoded, encoding).
                if (Test-DocNames $main $other -VerbOnly) { return "$word vs $other" }
            }
        }
    }
    return ''
}

function Get-DocumentedMembers([string]$Path) {
    # Every public or internal method or property with a doc comment, whatever its type.
    $lines = [IO.File]::ReadAllLines($Path)
    $members = @()
    for ($i = 0; $i -lt $lines.Length; $i++) {
        if ($lines[$i] -notmatch '^\s*(?:public|internal)\s+(?:(?:static|override|virtual|abstract|async|sealed|new|partial|readonly|required|unsafe|extern)\s+)*(?!class\b|record\b|struct\b|interface\b|enum\b|delegate\b|event\b|const\b)[\w<>\[\],.?()]+(?:\s*<[^>]*>)?\s+(\w+)\s*(?:<[^>]*>)?\s*(\(|\{|=>)') { continue }
        $name = $Matches[1]
        $doc = @(); $j = $i - 1
        while ($j -ge 0 -and $lines[$j] -match '^\s*\[') { $j-- }
        while ($j -ge 0 -and $lines[$j] -match '^\s*///') { $doc = @($lines[$j]) + $doc; $j-- }
        if (-not $doc.Count) { continue }
        $xml = ($doc | ForEach-Object { $_ -replace '^\s*///\s?', '' }) -join ' '
        $text = @(foreach ($tag in 'summary', 'returns') { if ($xml -match "(?s)<$tag>(.*?)</$tag>") { $Matches[1] } }) -join ' '
        $text = [regex]::Replace($text, '<see\s+(?:langword|cref)="(?:[A-Z]:)?([^"]+)"\s*/>', '$1')
        $text = [regex]::Replace($text, '<paramref\s+name="([^"]+)"\s*/>', '$1')
        $text = [regex]::Replace($text, '<[^>]+>', '')
        $members += [pscustomobject]@{ Name = $name; Line = $i + 1; Doc = ($text -replace '\s+', ' ').Trim() }
    }
    return $members
}

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
        $members += [pscustomobject]@{ Name = $name; Line = $i + 1; Doc = (Get-DocText $doc); HasDoc = [bool]$doc.Count; Public = $lines[$i] -match '^\s*public\b'; Body = (Get-MemberBody $lines $i) }
    }
    return $members
}

function Get-OppositeWords([string]$Name) {
    # A state name's key word and its opposite, when one is the other with a negating prefix:
    # IsUntrusted -> (untrusted, trusted), IsTrusted -> (trusted, untrusted); $null for any other name.
    if ($Name -cnotmatch '^(?:Is|Has|Can|Should|Are|Was)[A-Z]') { return $null }
    $word = Get-KeyWord $Name
    if ($word -match $NegatingPrefix) { return [pscustomobject]@{ Word = $word; Opposite = $Matches[2]; Negative = $true } }
    if ($word.Length -ge 4) { return [pscustomobject]@{ Word = $word; Opposite = $null; Negative = $false } }
    return $null
}

function Get-IdentifierWords([string]$Code) {
    # The camel-case words of every identifier and string in the code, lower case:
    # TrustedScheme -> trusted, scheme; "tls" -> tls.
    return @([regex]::Matches($Code, '[A-Za-z_]\w*') | ForEach-Object { Get-NameWords $_.Value })
}

function Test-Negates([string]$Code) {
    # Whether the code turns a condition round: !, is not, == false, or a Not/No/Non word.
    return ($Code -match '!|\bis\s+not\b|==\s*false\b|\bfalse\b') -or ($Code -cmatch '\b(?:Not|No|Non|Never)[A-Z]')
}

function Get-BodyPolarityMismatch([string]$Name, [string]$Body) {
    # A state name whose body tests the opposite word without turning it round: IsUntrusted =>
    # Scheme == TrustedScheme, IsValid => Invalid.Count > 0. The body says what the member returns,
    # so a name renamed to its opposite contradicts it even when the doc never uses the word.
    $o = Get-OppositeWords $Name
    if (-not $o -or -not $Body -or (Test-Negates $Body)) { return '' }
    $words = @(Get-IdentifierWords $Body)
    if ($words -contains $o.Word) { return '' }
    if ($o.Negative) {
        if ($words -contains $o.Opposite) { return "$($o.Word) vs $($o.Opposite)" }
        return ''
    }
    foreach ($prefix in 'un', 'in', 'im', 'il', 'ir', 'dis', 'non') {
        if ($words -contains "$prefix$($o.Word)") { return "$($o.Word) vs $prefix$($o.Word)" }
    }
    return ''
}

function Get-CallerPolarityMismatches([string]$Name, [string[]]$Texts) {
    # Callers that read a negatively named member as its opposite: Name(x) ? TrustedDefaultPort :
    # DefaultPort picks the root word's value when the name says the reverse.
    $o = Get-OppositeWords $Name
    if (-not $o -or -not $o.Negative) { return @() }
    $found = @()
    $pattern = "(?<!!)\b$([regex]::Escape($Name))\b(?:\s*\((?:[^()]|\([^()]*\))*\))?\s*\?\s*([^:;?]+?)\s*:\s*([^;,)]+)"
    foreach ($t in $Texts) {
        foreach ($m in [regex]::Matches($t, $pattern)) {
            $whenTrue = @(Get-IdentifierWords $m.Groups[1].Value)
            $whenFalse = @(Get-IdentifierWords $m.Groups[2].Value)
            if ($whenTrue -contains $o.Opposite -and $whenTrue -notcontains $o.Word -and $whenFalse -notcontains $o.Opposite) {
                $found += ($m.Value -replace '\s+', ' ').Trim()
            }
        }
    }
    return @($found | Select-Object -Unique)
}

function Get-MemberBody([string[]]$Lines, [int]$Index) {
    # A member's body from its declaration line: an expression body to its semicolon, or a block to
    # its closing brace; '' for an auto-property or an abstract member.
    $text = ($Lines[$Index..([Math]::Min($Lines.Length - 1, $Index + 60))]) -join "`n"
    $arrow = $text.IndexOf('=>'); $brace = $text.IndexOf('{')
    if ($arrow -ge 0 -and ($brace -lt 0 -or $arrow -lt $brace)) {
        $end = $text.IndexOf(';', $arrow)
        if ($end -lt 0) { return '' }
        return $text.Substring($arrow + 2, $end - $arrow - 2)
    }
    if ($brace -lt 0) { return '' }
    $depth = 0
    for ($i = $brace; $i -lt $text.Length; $i++) {
        if ($text[$i] -eq '{') { $depth++ }
        elseif ($text[$i] -eq '}') { $depth--; if ($depth -eq 0) { $body = $text.Substring($brace + 1, $i - $brace - 1); if ($body -match '^\s*get\s*;') { return '' }; return $body } }
    }
    return ''
}

function Invoke-Scan([string]$Tree) {
    $candidates = @(); $scanned = 0; $named = 0
    $projects = @(Get-ChildItem -LiteralPath $Tree -Directory | Where-Object { $_.Name -like '*.UnitLibrary' -or $_.Name -eq 'Curl.Console' })
    foreach ($project in $projects) {
        $files = @(Get-ChildItem -LiteralPath $project.FullName -Recurse -Filter '*.cs' -File | Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' })
        $negatives = @()
        foreach ($file in $files) {
            $relative =$file.FullName.Substring($Tree.Length).TrimStart('\', '/') -replace '\\', '/'
            foreach ($m in @(Get-DocumentedMembers $file.FullName)) {
                $named++
                $opposed = Get-AntonymMismatch $m.Name $m.Doc
                if ($opposed) { $candidates += [ordered]@{ file = $relative; line = $m.Line; member = $m.Name; kind = 'antonym-mismatch'; words = $opposed; text = $m.Doc } }
            }
            foreach ($m in @(Get-Members $file.FullName)) {
                $scanned++
                $opposedBody = Get-BodyPolarityMismatch $m.Name $m.Body
                if ($opposedBody) { $candidates += [ordered]@{ file = $relative; line = $m.Line; member = $m.Name; kind = 'body-polarity-mismatch'; words = $opposedBody; text = ($m.Body -replace '\s+', ' ').Trim() } }
                if ((Get-OppositeWords $m.Name).Negative) { $negatives += [pscustomobject]@{ File = $relative; Line = $m.Line; Name = $m.Name } }
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
        # Callers in the same project of each negatively named member (IsUntrusted, IsInvalid).
        if ($negatives.Count) {
            $texts = @($files | ForEach-Object { [IO.File]::ReadAllText($_.FullName) })
            foreach ($n in $negatives) {
                $calls = @(Get-CallerPolarityMismatches $n.Name $texts)
                if ($calls.Count) { $candidates += [ordered]@{ file = $n.File; line = $n.Line; member = $n.Name; kind = 'caller-polarity-mismatch'; words = (Get-OppositeWords $n.Name | ForEach-Object { "$($_.Word) vs $($_.Opposite)" }); text = ($calls -join ' | ') } }
            }
        }
    }
    return [ordered]@{ membersScanned = $scanned; namesScanned = $named; candidates = $candidates }
}

if ($SelfTest) {
    $r = Invoke-Scan (Resolve-Path (Join-Path $PSScriptRoot 'Fixtures\boolean-names')).Path
    $got = @($r.candidates | ForEach-Object { "$($_.member):$($_.kind)" } | Sort-Object) -join '|'
    $want = @('LacksScheme:polarity-mismatch', 'IsComplete:polarity-mismatch',
        'IsInvalid:antonym-mismatch', 'IsValid:antonym-mismatch', 'TryWrite:antonym-mismatch', 'Decode:antonym-mismatch',
        'Enable:antonym-mismatch', 'IsAllowed:antonym-mismatch', 'Open:antonym-mismatch',
        'IsUntrusted:body-polarity-mismatch', 'IsUntrusted:caller-polarity-mismatch', 'IsEncrypted:body-polarity-mismatch') | Sort-Object
    $failed = 0
    function Check([string]$Name, [bool]$Ok, [string]$Detail) { if (-not $Ok) { $script:failed++ }; Write-Host "$(if ($Ok) { 'PASS' } else { 'FAIL' }) ${Name}: $Detail" }
    Check 'exactly the mismatched members are found, an undocumented one is not' ($got -eq ($want -join '|')) $got
    Check 'every prefixed bool member is scanned, and no other' ($r.membersScanned -eq 15) "members $($r.membersScanned)"
    $invalid = @($r.candidates | Where-Object { $_.member -eq 'IsInvalid' })[0]
    Check 'an antonym candidate names the opposed words and the doc' ($invalid.words -eq 'invalid vs valid' -and $invalid.text -eq 'true when the value is valid.') "$($invalid.words) '$($invalid.text)'"
    Check 'word forms count: written, decoding, denies' ((Test-DocNames 'it was written' 'write') -and (Test-DocNames 'while decoding' 'decode') -and (Test-DocNames 'the server denies it' 'deny') -and -not (Test-DocNames 'the reader is ready' 'write')) 'forms'
    $body = @($r.candidates | Where-Object { $_.member -eq 'IsUntrusted' -and $_.kind -eq 'body-polarity-mismatch' })[0]
    Check 'a body that tests the opposite word names both and quotes the body' ($body.words -eq 'untrusted vs trusted' -and $body.text -eq 'string.Equals(scheme, TrustedScheme, System.StringComparison.Ordinal)') "$($body.words) '$($body.text)'"
    $caller = @($r.candidates | Where-Object { $_.member -eq 'IsUntrusted' -and $_.kind -eq 'caller-polarity-mismatch' })[0]
    Check 'a caller that picks the opposite word''s value when the member is true is quoted' ($caller.text -eq 'IsUntrusted(scheme) ? TrustedPort : PlainPort') $caller.text
    $lacks = @($r.candidates | Where-Object { $_.member -eq 'LacksScheme' })[0]
    Check 'a candidate carries its file, line and the doc as read' ($lacks.file -eq 'Sample.UnitLibrary/UrlTool.cs' -and $lacks.line -gt 0 -and $lacks.text -eq 'true when the URL names its own scheme.') "$($lacks.file):$($lacks.line) '$($lacks.text)'"
    exit $(if ($failed) { 1 } else { 0 })
}

if (-not $Root) { $Root = $repo }
$result = Invoke-Scan (Resolve-Path $Root).Path
$json = [pscustomobject]$result | ConvertTo-Json -Depth 5
if ($OutFile) { [IO.File]::WriteAllText($OutFile, $json) }
Write-Output $json
