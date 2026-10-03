<#
.SYNOPSIS
    Writes one audit's scorecard: each auditor's findings, re-audits and catch rate, the
    performance and process numbers, and the change since the previous scorecard.

.DESCRIPTION
    Format: Audit/Scorecards/README.md and SCORECARD-TEMPLATE.md (BL-1001). RunAudit.ps1
    (BL-1020) calls it twice: with -ReliabilityOnly before Write-AuditFindings.ps1 (BL-1016), to
    learn which auditors are unreliable, and in full after it.

    An auditor ran when -ReportDirectory holds <auditor>.md; otherwise its row and metrics read
    "not run". Rules:

      catch        A planted defect (from -Manifest) is caught by its own auditor when that
                   auditor's report has a finding in the defect's file whose title, key or
                   evidence contains the manifest's catch fragment - the same rule as
                   Write-AuditFindings.ps1. A catch by a different auditor counts for neither.
      catch rate   caught / planted for that auditor, a whole percent; "-" when none was planted.
      unreliable   an auditor that ran and missed a defect planted for it, returned no parseable
                   report block, or is named in -ChangedTree (it changed the audited tree).
      counts       New Critical..Low: finding files whose scorecard is this one, by severity.
                   Re-audited: the report's reaudits entries. Closed: findings closed-by this
                   scorecard. Still open: that auditor's open (proposed, accepted, deferred or blocked) findings.
      metrics      the performance and process tables come from those auditors' report
                   metrics; a number from an unreliable auditor is followed by " (unreliable)".
      change       deltas against the newest earlier scorecard in -ScorecardsDirectory, parsed
                   back from its tables; "first scorecard" and "-" deltas when there is none.

    Numbers use invariant culture: counts whole, milliseconds and minutes one decimal, dollars
    two, catch rates whole percent; deltas carry a sign. The same inputs always give the same
    bytes.

.PARAMETER ReportDirectory
    Holds <auditor>.md for each auditor that ran: its full reply.

.PARAMETER Manifest
    The seeder's manifest (BL-1015).

.PARAMETER FindingsDirectory
    Default: Audit/Findings in this repository.

.PARAMETER ScorecardsDirectory
    Default: Audit/Scorecards in this repository.

.PARAMETER Commit
    The audited commit. -Branch: the audited branch.

.PARAMETER Started
    The audit's start. Names the file yyyy-MM-dd_HHmm.md. -Finished: its end.

.PARAMETER CostUsd
    The audit's token cost.

.PARAMETER Models
    auditor=model pairs, e.g. quality=sonnet,security=opus.

.PARAMETER ChangedTree
    Auditors that changed the audited tree.

.PARAMETER Fingerprint
    The auditor fingerprint. Default: Get-AuditorFingerprint.ps1 on this repository.

.PARAMETER ReliabilityOnly
    Print the unreliable auditors, one per line, and write nothing.

.PARAMETER SelfTest
    Run against Audit/Tools/Fixtures/scorecard and check the rules.
#>
param(
    [string]$ReportDirectory,
    [string]$Manifest,
    [string]$FindingsDirectory,
    [string]$ScorecardsDirectory,
    [string]$Commit,
    [string]$Branch = 'master',
    [datetime]$Started = (Get-Date),
    [datetime]$Finished = (Get-Date),
    [double]$CostUsd = 0,
    [string[]]$Models = @(),
    [string[]]$ChangedTree = @(),
    [string]$Fingerprint,
    [switch]$ReliabilityOnly,
    [switch]$SelfTest
)

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
# RunAudit.ps1 passes -ChangedTree through powershell -File as one comma-joined argument,
# which arrives as a single string: split it, or an auditor that changed the tree is not
# found and stays reliable (BL-1244).
$ChangedTree = @($ChangedTree | ForEach-Object { "$_" -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ })
$Inv = [Globalization.CultureInfo]::InvariantCulture
$Auditors = @('quality', 'security', 'performance', 'conformance', 'truthfulness', 'process')
$Columns = @('NEW_CRITICAL', 'NEW_HIGH', 'NEW_MEDIUM', 'NEW_LOW', 'REAUDITED', 'CLOSED', 'STILL_OPEN', 'PLANTED_ASSIGNED', 'PLANTED_CAUGHT', 'CATCH_RATE')
$Scenarios = @('startup', 'small-get', 'large-get', 'headers-verbose', 'chunked', 'redirects')
$PerfFields = @('curl.medianMs', 'curl.p90Ms', 'curl.medianPeakWorkingSetBytes', 'candidate.medianMs', 'candidate.p90Ms', 'candidate.medianPeakWorkingSetBytes')
$ProcessMetrics = @('tasksDone', 'medianTaskMinutes', 'p90TaskMinutes', 'tasksClaimedMoreThanOnce', 'requeues', 'resumedRuns', 'ciRedMinutes', 'laneIdleMinutes', 'waitOverlapMinutes', 'waitNothingReadyMinutes', 'tokensInput', 'tokensOutput', 'costUsd', 'costUsdPerTaskDone')
$Utf8 = New-Object Text.UTF8Encoding $false

function ConvertTo-PlaceholderName([string]$Metric) {
    # small-get.curl.medianMs -> SMALL_GET_CURL_MEDIAN_MS; costUsdPerTaskDone -> COST_USD_PER_TASK_DONE
    $n = $Metric -replace '[.-]', '_'
    $n = [regex]::Replace($n, '(?<=[a-z0-9])([A-Z])', '_$1')
    return $n.ToUpperInvariant()
}

function Get-Kind([string]$Name) {
    # How a placeholder's number is written: count, decimal (ms, minutes), dollars or percent.
    if ($Name -match 'CATCH_RATE$') { return 'percent' }
    if ($Name -match 'COST_USD') { return 'dollars' }
    if ($Name -match '_MS$|MINUTES') { return 'decimal' }
    return 'count'
}

function Format-Number([double]$Value, [string]$Kind, [switch]$Signed) {
    $text = switch ($Kind) {
        'decimal' { $Value.ToString('0.0', $Inv) }
        'dollars' { $Value.ToString('0.00', $Inv) }
        'percent' { if ($Signed) { [math]::Round($Value).ToString('0', $Inv) } else { [math]::Round($Value).ToString('0', $Inv) + '%' } }
        default { [math]::Round($Value).ToString('0', $Inv) }
    }
    if ($Signed -and $Value -gt 0) { $text = '+' + $text }
    if ($Signed -and $text -match '^-0(\.0+)?$') { $text = $text.Substring(1) }
    return $text
}

function Get-ReportBlock([string]$Path) {
    $text = [IO.File]::ReadAllText($Path)
    $blocks = [regex]::Matches($text, '(?s)```json\r?\n(.*?)```')
    if ($blocks.Count -eq 0) { return $null }
    try { return $blocks[$blocks.Count - 1].Groups[1].Value | ConvertFrom-Json } catch { return $null }
}

function ConvertTo-Normalised([string]$Path) { return ("$Path" -replace '\\', '/' -replace ':\d+(-\d+)?$', '').Trim() }

function Test-SamePath([string]$Location, [string]$Planted) {
    # The finding's location names the planted file: the same path, or one ending with it (a
    # process defect's file is relative to the log copy, which auditors cite as logs/...).
    if (-not $Location -or -not $Planted) { return $false }
    return ($Location -ieq $Planted) -or $Location.EndsWith('/' + $Planted, [StringComparison]::OrdinalIgnoreCase)
}

function Test-AtPlantedLine([string]$Location, $Planted) {
    # Within 2 lines of the planted line; the same rule as Write-AuditFindings.ps1 (BL-1316).
    if (-not "$($Planted.line)" -or "$Location" -notmatch ':(\d+)(?:-(\d+))?$') { return $false }
    $first = [int]$Matches[1]
    $last = if ($Matches[2]) { [int]$Matches[2] } else { $first }
    $line = [int]$Planted.line
    return ($line -ge $first - 2) -and ($line -le $last + 2)
}

function Test-LogPlant($Planted) {
    # The same rule as Write-AuditFindings.ps1 (BL-1365).
    return ("$($Planted.auditor)" -eq 'process') -and ("$($Planted.file)" -notmatch '[\\/]')
}
function Test-Caught($Planted, $Report) {
    foreach ($f in @($Report.findings | Where-Object { $_ })) {
        $sameFile = Test-SamePath (ConvertTo-Normalised $f.location) (ConvertTo-Normalised $Planted.file)
        $text = "$($f.title) $($f.key) $($f.evidence)"
        $fragment = "$($Planted.catch)".Trim()
        $named = $fragment -and $text.IndexOf($fragment, [StringComparison]::OrdinalIgnoreCase) -ge 0
        if (Test-LogPlant $Planted) { if ($named) { return $true } else { continue } }
        if ($sameFile -and ($named -or (Test-AtPlantedLine "$($f.location)" $Planted))) { return $true }
    }
    return $false
}

function Read-Findings([string]$Directory) {
    $all = @()
    foreach ($file in @(Get-ChildItem -LiteralPath $Directory -Filter 'AF-*.md' -ErrorAction SilentlyContinue | Sort-Object Name)) {
        $text = [IO.File]::ReadAllText($file.FullName)
        $fields = @{}
        if ($text -match '(?s)^---\r?\n(.*?)\r?\n---') {
            foreach ($line in ($Matches[1] -split '\r?\n')) { if ($line -match '^([a-z-]+):\s?(.*)$') { $fields[$Matches[1]] = $Matches[2].Trim() } }
        }
        $all += [pscustomobject]@{ Name = $file.Name; Id = $fields['id']; Title = $fields['title']; Auditor = $fields['auditor']; Severity = $fields['severity']; Status = $fields['status']; Scorecard = $fields['scorecard']; ClosedBy = $fields['closed-by'] }
    }
    return $all
}

# The counts each auditor reports in metrics to show it ran every step of its method
# (Report-Format.md, "Method counts"). An auditor whose report lacks one, or gives it as
# null or 0, ran less than its method and is unreliable: two audits running, the
# truthfulness and process auditors did their re-audits only (BL-1364).
$MethodCounts = @{
    quality      = @('method.librariesMutated', 'method.testsRead')
    security     = @('method.fuzzTargets', 'method.timingSitesRead')
    performance  = @('method.scenariosRun')
    conformance  = @('method.casesRun')
    truthfulness = @('method.names', 'method.docComments', 'method.documentStatements', 'method.adrs', 'method.scriptStatements')
    process      = @('method.rulesChecked')
}

function Get-MissingMethodCounts([string]$Auditor, $Report) {
    $required = @($MethodCounts[$Auditor] | Where-Object { $_ })
    if (-not $Report) { return $required }
    $metrics = $Report.metrics
    return @($required | Where-Object {
        $property = if ($metrics) { $metrics.PSObject.Properties[$_] } else { $null }
        (-not $property) -or ($null -eq $property.Value) -or ([double]$property.Value -le 0)
    })
}

function Get-AuditState {
    # Which auditors ran, their reports, the planted defects and catches, and who is unreliable.
    $planted = @()
    if ($Manifest -and (Test-Path -LiteralPath $Manifest)) { $planted = @((Get-Content -LiteralPath $Manifest -Raw | ConvertFrom-Json).planted) }
    $state = [ordered]@{}
    foreach ($a in $Auditors) {
        $path = Join-Path $ReportDirectory "$a.md"
        $ran = Test-Path -LiteralPath $path
        $report = if ($ran) { Get-ReportBlock $path } else { $null }
        $mine = @($planted | Where-Object { $_.auditor -eq $a })
        $caught = @($mine | Where-Object { $report -and (Test-Caught $_ $report) }).Count
        $skipped = @(Get-MissingMethodCounts $a $report)
        $unreliable = $ran -and ((-not $report) -or ($caught -lt $mine.Count) -or ($ChangedTree -contains $a) -or $skipped.Count)
        $state[$a] = [pscustomobject]@{ Ran = $ran; Report = $report; Assigned = $mine.Count; Caught = $caught; Unreliable = $unreliable }
    }
    return [pscustomobject]@{ Auditors = $state; PlantedCount = $planted.Count }
}

function Get-Values($State, [string]$ScorecardName, [string]$FingerprintValue) {
    # Every placeholder's value: a number (double) or a marker string.
    $v = [ordered]@{}
    $findings = @(Read-Findings $FindingsDirectory)
    foreach ($a in $Auditors) {
        $s = $State.Auditors[$a]
        $p = $a.ToUpperInvariant()
        $modelPair = @($Models | ForEach-Object { $_ -split ',' } | Where-Object { $_ -like "$a=*" })[0]
        $v["${p}_MODEL"] = if (-not $s.Ran) { 'not run' } elseif ($modelPair) { ($modelPair -split '=', 2)[1] } else { '-' }
        if (-not $s.Ran) { foreach ($c in $Columns) { $v["${p}_$c"] = 'not run' }; $v["${p}_RELIABLE"] = 'not run'; continue }
        $new = @($findings | Where-Object { $_.Auditor -eq $a -and $_.Scorecard -eq $ScorecardName })
        foreach ($sev in 'Critical', 'High', 'Medium', 'Low') { $v["${p}_NEW_$($sev.ToUpperInvariant())"] = [double]@($new | Where-Object { $_.Severity -eq $sev }).Count }
        $v["${p}_REAUDITED"] = if ($s.Report) { [double]@($s.Report.reaudits | Where-Object { $_ }).Count } else { '-' }
        $v["${p}_CLOSED"] = [double]@($findings | Where-Object { $_.Auditor -eq $a -and $_.ClosedBy -eq $ScorecardName }).Count
        $v["${p}_STILL_OPEN"] = [double]@($findings | Where-Object { $_.Auditor -eq $a -and $_.Status -in 'proposed', 'accepted', 'deferred', 'blocked' }).Count
        $v["${p}_PLANTED_ASSIGNED"] = [double]$s.Assigned
        $v["${p}_PLANTED_CAUGHT"] = [double]$s.Caught
        $v["${p}_CATCH_RATE"] = if ($s.Assigned) { 100.0 * $s.Caught / $s.Assigned } else { '-' }
        $v["${p}_RELIABLE"] = if ($s.Unreliable) { 'no' } else { 'yes' }
    }
    foreach ($pair in @(@('performance', (@(foreach ($sc in $Scenarios) { foreach ($f in $PerfFields) { "$sc.$f" } }) + @('candidateBinaryBytes'))), @('process', $ProcessMetrics))) {
        $s = $State.Auditors[$pair[0]]
        foreach ($metric in $pair[1]) {
            $name = ConvertTo-PlaceholderName $metric
            if (-not $s.Ran) { $v[$name] = 'not run'; continue }
            $value = if ($s.Report -and $s.Report.metrics -and $s.Report.metrics.PSObject.Properties[$metric]) { $s.Report.metrics.$metric } else { $null }
            $v[$name] = if ($null -eq $value) { '-' } else { [double]$value }
        }
    }
    $v['FINGERPRINT'] = $FingerprintValue
    return $v
}

function Get-UnreliableNames($State) { return @($Auditors | Where-Object { $State.Auditors[$_].Unreliable }) }

function Test-FromUnreliable([string]$Name, $State) {
    # Which auditor's report a placeholder's number comes from, and whether it was unreliable.
    foreach ($a in $Auditors) { if ($Name -like "$($a.ToUpperInvariant())_*" -and $Name -notmatch '_(MODEL|PLANTED_ASSIGNED|PLANTED_CAUGHT|CATCH_RATE|RELIABLE)$') { return $State.Auditors[$a].Unreliable } }
    if ($Name -eq 'CANDIDATE_BINARY_BYTES' -or $Name -match '^(STARTUP|SMALL_GET|LARGE_GET|HEADERS_VERBOSE|CHUNKED|REDIRECTS)_') { return $State.Auditors['performance'].Unreliable }
    if ($ProcessMetrics | Where-Object { (ConvertTo-PlaceholderName $_) -eq $Name }) { return $State.Auditors['process'].Unreliable }
    return $false
}

function Format-Cell([string]$Name, $Value, $State, [switch]$Delta) {
    if ($Value -is [string]) { return $Value }
    $text = Format-Number $Value (Get-Kind $Name) -Signed:$Delta
    if (Test-FromUnreliable $Name $State) { $text += ' (unreliable)' }
    return $text
}

function Read-PreviousValues([string]$Path) {
    # Parses an earlier scorecard's tables back into placeholder names and numbers, by row label.
    $v = @{}
    $lines = [IO.File]::ReadAllLines($Path)
    foreach ($line in $lines) {
        if ($line -match '^## Change since') { break }
        if ($line -notmatch '^\| ') { continue }
        $cells = @(($line.Trim('|') -split '\|') | ForEach-Object { $_.Trim() -replace ' \(unreliable\)$', '' -replace '%$', '' })
        $label = $cells[0]
        if ($label -eq 'Auditor fingerprint') { $v['FINGERPRINT'] = $cells[1]; continue }
        if ($Auditors -contains $label -and $cells.Count -ge 13) {
            for ($i = 0; $i -lt $Columns.Count; $i++) { $v["$($label.ToUpperInvariant())_$($Columns[$i])"] = $cells[$i + 2] }
            continue
        }
        if ($Scenarios -contains $label) {
            for ($i = 0; $i -lt $PerfFields.Count; $i++) { $v[(ConvertTo-PlaceholderName "$label.$($PerfFields[$i])")] = $cells[$i + 1] }
            continue
        }
        if ($label -eq 'Curl native AOT') { $v['CANDIDATE_BINARY_BYTES'] = $cells[1]; continue }
        if ($label -match '^`(\w+)`$') { $v[(ConvertTo-PlaceholderName $Matches[1])] = $cells[1] }
    }
    return $v
}

function Write-Scorecard($State, [string]$FingerprintValue) {
    New-Item -ItemType Directory -Force $ScorecardsDirectory | Out-Null
    $name = $Started.ToString('yyyy-MM-dd_HHmm', $Inv) + '.md'
    $values = Get-Values $State $name $FingerprintValue
    $template = Join-Path $ScorecardsDirectory 'SCORECARD-TEMPLATE.md'
    if (-not (Test-Path -LiteralPath $template)) { $template = Join-Path $repo 'Audit\Scorecards\SCORECARD-TEMPLATE.md' }
    $text = [IO.File]::ReadAllText($template)

    $previous = @(Get-ChildItem -LiteralPath $ScorecardsDirectory -Filter '????-??-??_????.md' | Where-Object { $_.Name -lt $name } | Sort-Object Name)
    $previousFile = if ($previous.Count) { $previous[-1] } else { $null }
    $prior = if ($previousFile) { Read-PreviousValues $previousFile.FullName } else { $null }

    $replace = [ordered]@{
        'DATE' = $Started.ToString('yyyy-MM-dd', $Inv); 'TIME' = $Started.ToString('HH:mm', $Inv); 'BRANCH' = $Branch; 'COMMIT' = $Commit
        'FINGERPRINT' = $FingerprintValue; 'PLANTED_COUNT' = "$($State.PlantedCount)"
        'DURATION_MINUTES' = [math]::Round(($Finished - $Started).TotalMinutes).ToString('0', $Inv)
        'AUDIT_COST_USD' = $CostUsd.ToString('0.00', $Inv)
        'PREVIOUS_SCORECARD' = $(if ($previousFile) { "[$($previousFile.BaseName)]($($previousFile.Name))" } else { 'first scorecard' })
        'FINGERPRINT_CHANGE' = $(if (-not $prior) { '-' } elseif ($prior['FINGERPRINT'] -eq $FingerprintValue) { 'same' } else { 'changed' })
    }
    foreach ($k in $values.Keys) {
        if ($replace.Contains($k)) { continue }
        $replace[$k] = Format-Cell $k $values[$k] $State
        $delta = '-'
        if ($prior -and $values[$k] -isnot [string]) {
            $before = 0.0
            if ($prior.ContainsKey($k) -and [double]::TryParse("$($prior[$k])", [Globalization.NumberStyles]::Float, $Inv, [ref]$before)) {
                $delta = Format-Cell $k ([double]$values[$k] - $before) $State -Delta
            }
        }
        $replace["DELTA_$k"] = $delta
    }
    $newFindings = @(Read-Findings $FindingsDirectory | Where-Object { $_.Scorecard -eq $name } | Sort-Object Id)
    $replace['NEW_FINDING_LINKS'] = if ($newFindings.Count) { ($newFindings | ForEach-Object { "- [$($_.Id)](../Findings/$($_.Name)) - $($_.Severity) - $($_.Auditor) - $($_.Title)" }) -join "`n" } else { 'None.' }

    $text = [regex]::Replace($text, '\{\{([A-Z0-9_]+)\}\}', { param($m) $k = $m.Groups[1].Value; if ($replace.Contains($k)) { $replace[$k] } else { $m.Value } })
    $path = Join-Path $ScorecardsDirectory $name
    [IO.File]::WriteAllText($path, $text, $Utf8)
    return $path
}

if ($SelfTest) {
    $fixture = Join-Path $PSScriptRoot 'Fixtures\scorecard'
    $work = Join-Path ([IO.Path]::GetTempPath()) ('scorecard-selftest-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Force $work | Out-Null
    $failed = 0
    function Check([string]$Name, [bool]$Ok, [string]$Detail) { if (-not $Ok) { $script:failed++ }; Write-Host "$(if ($Ok) { 'PASS' } else { 'FAIL' }) ${Name}: $Detail" }
    try {
        $FindingsDirectory = Join-Path $fixture 'findings'
        $Manifest = Join-Path $fixture 'manifest.json'
        $Models = @('quality=sonnet,security=opus,performance=sonnet,conformance=opus,truthfulness=sonnet,process=sonnet')
        $Branch = 'master'; $Commit = 'abc1234'; $CostUsd = 12.4
        # The previous scorecard, from the previous reports, then this audit's.
        $ScorecardsDirectory = Join-Path $work 'a'
        $ReportDirectory = Join-Path $fixture 'previous'
        $Started = [datetime]'2026-10-07T09:00'; $Finished = [datetime]'2026-10-07T10:30'
        $null = Write-Scorecard (Get-AuditState) 'fp-old'
        Copy-Item -Recurse -LiteralPath $ScorecardsDirectory -Destination (Join-Path $work 'b')
        $ReportDirectory = Join-Path $fixture 'reports'
        $Started = [datetime]'2026-10-14T09:30'; $Finished = [datetime]'2026-10-14T11:00'
        $state = Get-AuditState
        $rates = ($Auditors | ForEach-Object { $s = $state.Auditors[$_]; "$_ $($s.Caught)/$($s.Assigned)" }) -join ', '
        Check 'catch rates per auditor' ($rates -eq 'quality 1/1, security 0/1, performance 1/1, conformance 0/1, truthfulness 1/1, process 1/1') $rates
        $unreliable = (Get-UnreliableNames $state) -join ','
        Check 'the auditor that missed and the one with no block are unreliable, nobody else' ($unreliable -eq 'security,conformance') $unreliable
        $reauditOnly = [pscustomobject]@{ metrics = [pscustomobject]@{} }
        $partial = [pscustomobject]@{ metrics = [pscustomobject]@{ 'method.rulesChecked' = 0 } }
        $full = [pscustomobject]@{ metrics = [pscustomobject]@{ 'method.rulesChecked' = 8 } }
        Check 'a report without its method counts is a skipped method' ((@(Get-MissingMethodCounts 'truthfulness' $reauditOnly).Count -eq 5) -and (@(Get-MissingMethodCounts 'process' $partial).Count -eq 1) -and (@(Get-MissingMethodCounts 'process' $full).Count -eq 0)) 'truthfulness 5 missing, process 0 counts as missing, 8 does not'
        Check 'a catch by the wrong auditor does not count' ($state.Auditors['security'].Caught -eq 0 -and $state.Auditors['truthfulness'].Caught -eq 1) "security caught $($state.Auditors['security'].Caught)"
        $only = @(& powershell -NoProfile -ExecutionPolicy Bypass -File $PSCommandPath -ReliabilityOnly -ReportDirectory $ReportDirectory -Manifest $Manifest -FindingsDirectory $FindingsDirectory) -join ','
        Check '-ReliabilityOnly prints exactly the two names' ($only -eq 'security,conformance') $only
        $first = Write-Scorecard $state 'fp-new'
        $ScorecardsDirectory = Join-Path $work 'b'
        $second = Write-Scorecard (Get-AuditState) 'fp-new'
        $text = [IO.File]::ReadAllText($first)
        $sections = @([regex]::Matches($text, '(?m)^## (.+)$') | ForEach-Object { $_.Groups[1].Value.Trim() }) -join '|'
        Check 'sections in the template''s order' ($sections -eq 'Auditors|Performance|Process|Change since the previous scorecard|New findings') $sections
        Check 'no placeholder left' ($text -notmatch '\{\{') 'none'
        Check 'the fingerprint is in the header' ($text -match '\| Auditor fingerprint \| fp-new \|') 'fp-new'
        Check 'previous scorecard and fingerprint change' ($text -match '\| Previous scorecard \| \[2026-10-07_0900\]\(2026-10-07_0900\.md\) \|' -and $text -match '\| Auditor fingerprint \| changed \|') 'linked, changed'
        Check 'a delta: small-get Curl median 70.0 -> 78.0 is +8.0' ($text -match '(?m)^\| small-get \|.*\| \+8\.0 \|.*\|$' -or ($text -split '## Change since')[1] -match '\| small-get \| [^|]+ \| [^|]+ \| [^|]+ \| \+8\.0 \|') 'small-get delta'
        Check 'a catch-rate delta in points: quality 0% -> 100% is +100' ((($text -split '## Change since')[1]) -match '(?m)^\| quality \|( [^|]+ \|){9} \+100 \|$') 'quality +100'
        Check 'unreliable numbers are marked' ($text -match '\| security \| opus \| 0 \(unreliable\) \|') 'security row'
        Check 'the model column comes from -Models' ($text -match '\| process \| sonnet \|' -and $text -match '\| security \| opus \|') 'process sonnet, security opus'
        Check 'new findings listed' ($text -match '- \[AF-0002\]\(\.\./Findings/AF-0002-x\.md\) - High - quality - New quality finding') 'AF-0002'
        Check 'a process defect cited under logs/ is the same file' ((Test-SamePath 'logs/ci-runs.json' 'ci-runs.json') -and -not (Test-SamePath 'logs/other-ci-runs.json' 'ci-runs.json') -and (Test-SamePath 'Curl.Tls.UnitLibrary/TlsMac.cs' 'Curl.Tls.UnitLibrary/TlsMac.cs')) 'logs/ci-runs.json'
        # The two real process reports of planted log defects (2026-10-02 and 2026-10-03, BL-1365).
        $plant1 = [pscustomobject]@{ id = 'PD-501'; auditor = 'process'; file = 'DarkFactory-20261001-120001-L9.log'; line = $null; catch = 'BL-1121' }
        $plant2 = [pscustomobject]@{ id = 'PD-501'; auditor = 'process'; file = 'DarkFactory-20261002-231500-L9.log'; line = 2; catch = 'BL-1289' }
        $report1 = [pscustomobject]@{ key = 'process:logs:BL-1121:redone-work'; title = 'BL-1121 claimed 5 times'; location = 'logs/BL-1121'; evidence = 'claimed 5 times' }
        $report2 = [pscustomobject]@{ key = 'process:logs:BL-1289:redone-work'; title = 'BL-1289 was claimed 6 times (134 minutes) before reaching Done'; location = 'logs/BL-1289-20261002-211047-L1.jsonl:1'; evidence = 'six claims' }
        $other = [pscustomobject]@{ key = 'process:logs:BL-1300:redone-work'; title = 'BL-1300 claimed 3 times'; location = 'logs/DarkFactory-20261001-120001-L9.log'; evidence = 'three claims' }
        Check 'a process report naming a planted log defect''s catch text is a catch, whatever file it cites' ((Test-Caught $plant1 ([pscustomobject]@{ findings = @($report1) })) -and (Test-Caught $plant2 ([pscustomobject]@{ findings = @($report2) })) -and -not (Test-Caught $plant1 ([pscustomobject]@{ findings = @($other) }))) 'both PD-501 reports; not another task'
        Check 'two runs give the same bytes' ([IO.File]::ReadAllText($second) -ceq $text) (Split-Path $second -Leaf)
    }
    finally { Remove-Item -Recurse -Force -LiteralPath $work -ErrorAction SilentlyContinue }
    exit $(if ($failed) { 1 } else { 0 })
}

if (-not $ReportDirectory) { throw 'Give -ReportDirectory.' }
if (-not $FindingsDirectory) { $FindingsDirectory = Join-Path $repo 'Audit\Findings' }
if (-not $ScorecardsDirectory) { $ScorecardsDirectory = Join-Path $repo 'Audit\Scorecards' }
$state = Get-AuditState
if ($ReliabilityOnly) { Get-UnreliableNames $state | ForEach-Object { Write-Output $_ }; exit 0 }
if (-not $Commit) { throw 'Give -Commit.' }
if (-not $Fingerprint) { $Fingerprint = "$(& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'Get-AuditorFingerprint.ps1') -Root $repo)".Trim() }
Write-Output (Write-Scorecard $state $Fingerprint)
