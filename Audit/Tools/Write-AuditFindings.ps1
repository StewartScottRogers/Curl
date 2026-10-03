<#
.SYNOPSIS
    Turns the auditors' reports from one audit into finding files: new ones filed, repeats noted,
    re-audits recorded, and findings closed only by a confirming re-audit.

.DESCRIPTION
    Called by RunAudit.ps1 (BL-1020) after the auditors reply. Formats and rules:
    Audit/Findings/README.md, Audit/Findings/FINDING-TEMPLATE.md and
    Audit/Instructions/Report-Format.md (BL-1001).

    For each auditor's report, in the fixed order quality, security, performance, conformance,
    truthfulness, process (so new IDs are deterministic), it reads the last fenced json block of
    <auditor>.md in -ReportDirectory and applies these rules:

      catch       A reported finding from the same auditor as a planted defect in -Manifest,
                  whose location names the planted file and whose title, key or evidence holds
                  the manifest's catch text, is a catch, not a finding. It is never filed; it is
                  written to catches.json in -ReportDirectory for the scorecard (BL-1017).
      repeat      A reported finding whose key equals an open (proposed, accepted, deferred or
                  blocked) finding's key is the same finding: that file gains a Re-audits line
                  "still reported", its status is kept, and no new file is filed.
      rejected    A key equal to a rejected finding's key gains the same "still reported" line,
                  and its status stays rejected (Decided by Claude, 2026-09-30: otherwise every
                  audit re-files what Stewart turned down).
      reappeared  A key equal only to a closed finding's key is filed new, and its Summary says
                  "reappeared; previously AF-####". The closed file is not touched.
      new         Anything else is filed as the next AF-#### with status proposed. A finding from
                  an auditor in -Unreliable says so in its Summary.
      re-audit    Each reaudits entry appends a Re-audits line to the finding it names. When
                  reproduces is false, the auditor is the finding's own and not in -Unreliable,
                  and the finding is open, it is closed: status closed, reason, closed,
                  closed-by, and a Log line. Nothing else closes a finding - not its task's
                  state, and not an audit that did not report it. Closed findings are never
                  changed. Re-audits lines go at the end of Re-audits, before Log (BL-1183).

    Prints one summary line: new, still open, closed, catches.

.PARAMETER ReportDirectory
    Holds <auditor>.md for each auditor that ran: its full reply.

.PARAMETER Manifest
    The seeder's manifest (BL-1015). Optional: without it there are no catches.

.PARAMETER Unreliable
    Auditor names flagged unreliable on this audit's scorecard (BL-1017 computes them).

.PARAMETER Scorecard
    This audit's scorecard file name, e.g. 2026-10-14_0930.md.

.PARAMETER Commit
    The audited commit.

.PARAMETER Date
    The audit's date, yyyy-MM-dd. Default today.

.PARAMETER FindingsDirectory
    Default: Audit/Findings in this repository.

.PARAMETER SelfTest
    Run against Audit/Tools/Fixtures/findings, copied to a temporary folder, and check every rule.
#>
param(
    [string]$ReportDirectory,
    [string]$Manifest,
    [string[]]$Unreliable = @(),
    [string]$Scorecard,
    [string]$Commit,
    [string]$Date = (Get-Date -Format 'yyyy-MM-dd'),
    [string]$FindingsDirectory,
    [switch]$SelfTest
)

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path

function ConvertTo-NameList([string[]]$Names) {
    # RunAudit.ps1 passes the list through powershell -File as one comma-joined argument,
    # which arrives as a single string: split it, or 'quality,process' matches neither
    # auditor and an unreliable one closes findings (BL-1244).
    return @($Names | ForEach-Object { "$_" -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ })
}
$Unreliable = ConvertTo-NameList $Unreliable
$AuditorOrder = @('quality', 'security', 'performance', 'conformance', 'truthfulness', 'process')
$Utf8 = New-Object Text.UTF8Encoding $false

function Get-ReportBlock([string]$Path) {
    $text = [IO.File]::ReadAllText($Path)
    $blocks = [regex]::Matches($text, '(?s)```json\r?\n(.*?)```')
    if ($blocks.Count -eq 0) { return $null }
    return $blocks[$blocks.Count - 1].Groups[1].Value | ConvertFrom-Json
}

function Read-Finding([string]$Path) {
    $text = [IO.File]::ReadAllText($Path)
    $fields = @{}
    if ($text -match '(?s)^---\r?\n(.*?)\r?\n---') {
        foreach ($line in ($Matches[1] -split '\r?\n')) {
            if ($line -match '^([a-z-]+):\s?(.*)$') { $fields[$Matches[1]] = $Matches[2].Trim() }
        }
    }
    return [pscustomobject]@{ Path = $Path; Id = $fields['id']; Key = $fields['key']; Status = $fields['status']; Auditor = $fields['auditor'] }
}

function Set-FrontMatter([string]$Text, [string]$Name, [string]$Value) {
    return [regex]::new("(?m)^$([regex]::Escape($Name)):[^\r\n]*").Replace($Text, "${Name}: $Value", 1)
}

function Add-ReauditLine([string]$Path, [string]$Line) {
    # Re-audits comes before Log (BL-1183), so the line goes at the end of Re-audits, not
    # the end of the file. A finding written before Log existed has no Log to step over.
    $text = [IO.File]::ReadAllText($Path).TrimEnd()
    $newline = if ($text.Contains("`r`n")) { "`r`n" } else { "`n" }
    $log = ''
    $logHeading = [regex]::Match($text, '(?m)^## Log[ \t]*\r?$')
    if ($logHeading.Success) { $log = $text.Substring($logHeading.Index); $text = $text.Substring(0, $logHeading.Index).TrimEnd() }
    if ($text -match '## Re-audits$') { $text += $newline + $newline + $Line } else { $text += $newline + $Line }
    if ($log) { $text += $newline + $newline + $log }
    [IO.File]::WriteAllText($Path, $text + $newline, $Utf8)
}

function Add-LogLine([string]$Path, [string]$Line) {
    # Log is the last section, append-only, one dated line per status move (BL-1183). A
    # finding written before Log existed gains the section with this line.
    $text = [IO.File]::ReadAllText($Path).TrimEnd()
    $newline = if ($text.Contains("`r`n")) { "`r`n" } else { "`n" }
    if ($text -notmatch '(?m)^## Log[ \t]*\r?$') { $text += $newline + $newline + '## Log' }
    if ($text -match '## Log$') { $text += $newline + $newline + $Line } else { $text += $newline + $Line }
    [IO.File]::WriteAllText($Path, $text + $newline, $Utf8)
}

function Get-Slug([string]$Title) {
    $slug = ($Title.ToLowerInvariant() -replace '[^a-z0-9]+', '-').Trim('-')
    if ($slug.Length -gt 50) { $slug = $slug.Substring(0, 50).TrimEnd('-') }
    return $slug
}

function ConvertTo-Normalised([string]$Path) { return ("$Path" -replace '\\', '/' -replace ':\d+(-\d+)?$', '').Trim() }

function Test-SamePath([string]$Location, [string]$Planted) {
    # The finding's location names the planted file: the same path, or one ending with it (a
    # process defect's file is relative to the log copy, which auditors cite as logs/...).
    if (-not $Location -or -not $Planted) { return $false }
    return ($Location -ieq $Planted) -or $Location.EndsWith('/' + $Planted, [StringComparison]::OrdinalIgnoreCase)
}

function Test-Catch($Finding, [string]$Auditor, [object[]]$Planted) {
    # A catch: the planted defect's own auditor, in the defect's file, with the manifest's catch
    # fragment in the finding's title, key or evidence. The same rule as Write-AuditScorecard.ps1.
    foreach ($p in $Planted) {
        if ($p.auditor -ne $Auditor) { continue }
        $sameFile = Test-SamePath (ConvertTo-Normalised $Finding.location) (ConvertTo-Normalised $p.file)
        $haystack = "$($Finding.title) $($Finding.key) $($Finding.evidence)"
        $catchText = "$($p.catch)".Trim()
        $named = $catchText -and $haystack.IndexOf($catchText, [StringComparison]::OrdinalIgnoreCase) -ge 0
        if ($sameFile -and $named) { return $p }
    }
    return $null
}

function New-FindingFile($Finding, [string]$Auditor, [string]$Id, [string]$Note) {
    $template = Join-Path $FindingsDirectory 'FINDING-TEMPLATE.md'
    if (-not (Test-Path -LiteralPath $template)) { $template = Join-Path $repo 'Audit\Findings\FINDING-TEMPLATE.md' }
    $summary = "$($Finding.severity) finding from the $Auditor auditor at ``$($Finding.location)``: $($Finding.title)."
    if ($Note) { $summary += " $Note" }
    $values = [ordered]@{
        '{{ID}}' = $Id; '{{TITLE}}' = "$($Finding.title)"; '{{AUDITOR}}' = $Auditor; '{{SEVERITY}}' = "$($Finding.severity)"
        '{{KEY}}' = "$($Finding.key)"; '{{FOUND}}' = $Date; '{{FOUND_AT}}' = $Commit; '{{SCORECARD}}' = $Scorecard
        '{{SUMMARY}}' = $summary; '{{LOCATION}}' = "$($Finding.location)"; '{{EVIDENCE}}' = "$($Finding.evidence)"
        '{{REPRODUCTION_COMMAND}}' = "$($Finding.reproduction.command)"; '{{REPRODUCTION_EXPECTED}}' = "$($Finding.reproduction.expected)"
        '{{REPRODUCTION_ACTUAL}}' = "$($Finding.reproduction.actual)"
    }
    $text = [IO.File]::ReadAllText($template)
    foreach ($k in $values.Keys) { $text = $text.Replace($k, $values[$k]) }
    $path = Join-Path $FindingsDirectory "$Id-$(Get-Slug $Finding.title).md"
    [IO.File]::WriteAllText($path, $text, $Utf8)
    return $path
}

function Invoke-WriteFindings {
    if (-not (Test-Path -LiteralPath $FindingsDirectory)) { New-Item -ItemType Directory -Force $FindingsDirectory | Out-Null }
    $existing = @(Get-ChildItem -LiteralPath $FindingsDirectory -Filter 'AF-*.md' | ForEach-Object { Read-Finding $_.FullName })
    $next = 1 + [int](@($existing | ForEach-Object { [int]($_.Id -replace '\D', '') } | Measure-Object -Maximum).Maximum)
    $planted = @()
    if ($Manifest -and (Test-Path -LiteralPath $Manifest)) { $planted = @((Get-Content -LiteralPath $Manifest -Raw | ConvertFrom-Json).planted) }
    $counts = @{ new = 0; open = 0; closed = 0; catches = 0 }
    $catches = @()

    foreach ($auditor in $AuditorOrder) {
        $reportPath = Join-Path $ReportDirectory "$auditor.md"
        if (-not (Test-Path -LiteralPath $reportPath)) { continue }
        $report = Get-ReportBlock $reportPath
        if (-not $report) { continue }
        $flagged = $Unreliable -contains $auditor

        foreach ($f in @($report.findings | Where-Object { $_ })) {
            $planted1 = Test-Catch $f $auditor $planted
            if ($planted1) {
                $catches += [ordered]@{ auditor = $auditor; planted = $planted1.id; key = "$($f.key)"; title = "$($f.title)" }
                $counts.catches++
                continue
            }
            $same = @($existing | Where-Object { $_.Key -ceq "$($f.key)" })
            $live = @($same | Where-Object { $_.Status -in 'proposed', 'accepted', 'deferred', 'blocked', 'rejected' })[0]
            if ($live) {
                Add-ReauditLine $live.Path "- $Date | $Scorecard | reproduces: yes | still reported"
                if ($live.Status -ne 'rejected') { $counts.open++ }
                continue
            }
            $closedBefore = @($same | Where-Object { $_.Status -eq 'closed' })[0]
            $notes = @()
            if ($closedBefore) { $notes += "Reappeared; previously $($closedBefore.Id)." }
            if ($flagged) { $notes += "Reported by an auditor flagged unreliable in $Scorecard." }
            $id = 'AF-{0:D4}' -f $next
            $next++
            $path = New-FindingFile $f $auditor $id ($notes -join ' ')
            $existing += Read-Finding $path
            $counts.new++
        }

        foreach ($r in @($report.reaudits | Where-Object { $_ })) {
            $target = @($existing | Where-Object { $_.Id -eq "$($r.finding)" })[0]
            if (-not $target -or $target.Status -eq 'closed') { continue }
            $reproduces = [bool]$r.reproduces
            Add-ReauditLine $target.Path "- $Date | $Scorecard | reproduces: $(if ($reproduces) { 'yes' } else { 'no' }) | $($r.evidence)"
            $canClose = (-not $reproduces) -and ($target.Auditor -eq $auditor) -and (-not $flagged) -and ($target.Status -in 'proposed', 'accepted', 'deferred', 'blocked')
            if ($canClose) {
                $reason = "Re-audit $Scorecard`: the reproduction no longer reproduces."
                $text = [IO.File]::ReadAllText($target.Path)
                $text = Set-FrontMatter $text 'status' 'closed'
                $text = Set-FrontMatter $text 'reason' $reason
                $text = Set-FrontMatter $text 'closed' $Date
                $text = Set-FrontMatter $text 'closed-by' $Scorecard
                [IO.File]::WriteAllText($target.Path, $text, $Utf8)
                Add-LogLine $target.Path "- ${Date}: $($target.Status) -> closed. $reason"
                $target.Status = 'closed'
                $counts.closed++
            }
        }
    }

    [IO.File]::WriteAllText((Join-Path $ReportDirectory 'catches.json'), (ConvertTo-Json @($catches) -Depth 4), $Utf8)
    Write-Output ("findings: new {0}, still open {1}, closed {2}, catches {3}" -f $counts.new, $counts.open, $counts.closed, $counts.catches)
}

if ($SelfTest) {
    $fixture = Join-Path $PSScriptRoot 'Fixtures\findings'
    $work = Join-Path ([IO.Path]::GetTempPath()) ('findings-selftest-' + [guid]::NewGuid().ToString('N'))
    Copy-Item -Recurse -LiteralPath $fixture -Destination $work
    try {
        $FindingsDirectory = Join-Path $work 'findings'
        Copy-Item -LiteralPath (Join-Path $repo 'Audit\Findings\FINDING-TEMPLATE.md') -Destination $FindingsDirectory
        $ReportDirectory = Join-Path $work 'reports'
        $Manifest = Join-Path $work 'manifest.json'
        # One comma-joined string, as RunAudit.ps1 passes it through powershell -File (BL-1244).
        $Unreliable = ConvertTo-NameList @('performance,truthfulness-not-run'); $Scorecard = '2026-10-14_0930.md'; $Commit = 'abc1234'; $Date = '2026-10-14'
        $before = @{}
        foreach ($f in Get-ChildItem -LiteralPath $FindingsDirectory -Filter 'AF-*.md') { $before[$f.Name] = [IO.File]::ReadAllText($f.FullName) }
        $line = Invoke-WriteFindings
        $failed = 0
        function Check([string]$Name, [bool]$Ok, [string]$Detail) {
            if (-not $Ok) { $script:failed++ }
            Write-Host "$(if ($Ok) { 'PASS' } else { 'FAIL' }) ${Name}: $Detail"
        }
        function Text([string]$Prefix) { $f = Get-ChildItem -LiteralPath $FindingsDirectory -Filter "$Prefix-*.md" | Select-Object -First 1; if ($f) { [IO.File]::ReadAllText($f.FullName) } else { '' } }
        $af7 = Text 'AF-0007'
        Check 'new finding filed as proposed with the next ID' ($af7 -match '(?m)^status: proposed$' -and $af7 -match '(?m)^key: quality:Curl.Cli.UnitTests/ParserTests.cs:Parse_Empty_Throws:weak-assertion$') 'AF-0007'
        $af1 = Text 'AF-0001'
        Check 'a repeated key on a deferred finding keeps it deferred, its line inside Re-audits, before Log' ($af1 -match '(?m)^status: deferred\r?$' -and $af1 -match '(?s)## Re-audits\r?\n\r?\n- 2026-10-14 \| 2026-10-14_0930.md \| reproduces: yes \| still reported\r?\n\r?\n## Log\r?\n\r?\n- 2026-10-01: filed proposed\.\r?\n- 2026-10-02: proposed -> deferred\.[^\r\n]*\r?\n?$') 'AF-0001'
        Check 'a repeated key adds a re-audit line and files nothing' ((Text 'AF-0001') -match 'reproduces: yes \| still reported' -and -not ((Get-ChildItem -LiteralPath $FindingsDirectory -Filter 'AF-*.md' | ForEach-Object { [IO.File]::ReadAllText($_.FullName) }) -match 'key: quality:Curl.Core.UnitTests/UrlTests.cs:Parse_Port_Rejects:name-lies' | Measure-Object).Count -ne 1) 'AF-0001'
        $catchesJson = @(Get-Content -LiteralPath (Join-Path $ReportDirectory 'catches.json') -Raw | ConvertFrom-Json)
        Check 'a planted-defect match is skipped and in catches.json' ($catchesJson.Count -eq 1 -and $catchesJson[0].planted -eq 'PD-101' -and -not ((Get-ChildItem -LiteralPath $FindingsDirectory -Filter 'AF-*.md' | ForEach-Object { [IO.File]::ReadAllText($_.FullName) }) -match 'FixedTimeEquals replaced')) "catches $($catchesJson.Count)"
        $af6 = Text 'AF-0006'
        Check 'reproduces false from a reliable auditor closes it, blocked or not' ($af6 -match '(?m)^status: closed$' -and $af6 -match '(?m)^closed: 2026-10-14$' -and $af6 -match '(?m)^closed-by: 2026-10-14_0930.md$') 'AF-0006'
        Check 'closing writes the reason and a last Log line' ($af6 -match '(?m)^reason: Re-audit 2026-10-14_0930.md: the reproduction no longer reproduces\.$' -and $af6 -match '(?s)reproduces: no \|[^\n]*\n\n## Log\n\n- 2026-10-01: filed proposed\.\n- 2026-10-02: proposed -> blocked\.[^\n]*\n- 2026-10-14: blocked -> closed\. Re-audit 2026-10-14_0930.md: the reproduction no longer reproduces\.\n$') 'AF-0006'
        $af5 = Text 'AF-0005'
        Check 'reproduces false from an unreliable auditor does not close it' ($af5 -match '(?m)^status: proposed$' -and $af5 -match 'reproduces: no') 'AF-0005'
        $af2 = Text 'AF-0002'
        Check 'a finding whose task is Done, with no re-audit, stays open' ($af2 -ceq $before[(Split-Path (Get-ChildItem -LiteralPath $FindingsDirectory -Filter 'AF-0002-*.md').FullName -Leaf)] -and $af2 -match '(?m)^status: accepted$') 'AF-0002 unchanged'
        $af3 = Text 'AF-0003'
        Check 'a rejected finding stays rejected and gains only a still-reported line' ($af3 -match '(?m)^status: rejected$' -and $af3 -match 'still reported') 'AF-0003'
        $af4 = Text 'AF-0004'
        $af9 = Text 'AF-0009'
        Check 'a key matching a closed finding files a new one naming it' ($af4 -ceq $before[(Split-Path (Get-ChildItem -LiteralPath $FindingsDirectory -Filter 'AF-0004-*.md').FullName -Leaf)] -and $af9 -match 'Reappeared; previously AF-0004') 'AF-0009'
        Check 'an unreliable auditor''s new finding says so' ((Text 'AF-0008') -match 'flagged unreliable in 2026-10-14_0930.md') 'AF-0008'
        Check 'a comma-joined -Unreliable is split into names' (((ConvertTo-NameList @('quality, process', 'security')) -join '|') -eq 'quality|process|security') 'quality|process|security'
        $templateFields = @([regex]::Matches([IO.File]::ReadAllText((Join-Path $repo 'Audit\Findings\FINDING-TEMPLATE.md')), '(?m)^([a-z-]+):') | ForEach-Object { $_.Groups[1].Value }) -join ','
        $newFields = @([regex]::Matches(($af7 -split '\r?\n---')[0], '(?m)^([a-z-]+):') | ForEach-Object { $_.Groups[1].Value }) -join ','
        $sections = @([regex]::Matches($af7, '(?m)^## (.+)$') | ForEach-Object { $_.Groups[1].Value.Trim() }) -join ','
        Check 'front matter and sections match the template' ($newFields -eq $templateFields -and $sections -eq 'Summary,Evidence,Reproduction,Re-audits,Log' -and $af7 -match '(?m)^- 2026-10-14: filed proposed\.$' -and $af7 -notmatch '\{\{') "$newFields | $sections"
        Check 'a process defect cited under logs/ is the same file' ((Test-SamePath 'logs/ci-runs.json' 'ci-runs.json') -and -not (Test-SamePath 'logs/other-ci-runs.json' 'ci-runs.json') -and (Test-SamePath 'Curl.Tls.UnitLibrary/TlsMac.cs' 'Curl.Tls.UnitLibrary/TlsMac.cs')) 'logs/ci-runs.json'
        Check 'summary line' ($line -eq 'findings: new 3, still open 1, closed 1, catches 1') $line
        exit $(if ($failed) { 1 } else { 0 })
    }
    finally { Remove-Item -Recurse -Force -LiteralPath $work -ErrorAction SilentlyContinue }
}

if (-not $ReportDirectory -or -not $Scorecard -or -not $Commit) { throw 'Give -ReportDirectory, -Scorecard and -Commit (and -Manifest, -Unreliable as the audit has them).' }
if (-not $FindingsDirectory) { $FindingsDirectory = Join-Path $repo 'Audit\Findings' }
Invoke-WriteFindings
