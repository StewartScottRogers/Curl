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
      mutant key  A finding with reproduction.mutation (<file>:<line>:<operator>, from
                  Invoke-MutationTest.ps1) is matched and filed under the mechanical key
                  quality:<file>:<member>-<operator word>:surviving-mutant, the member read from
                  -Tree at that line, with "reproduction: mutation <file>:<line>:<operator>" and
                  the targeted -Site command as its reproduction (ADR-0422).
      duplicate   Before anything else, open findings that share a key are one finding: all but
                  the lowest ID are closed with closed-how duplicate and duplicate-of, and their
                  tasks join the original's tasks list.
      set aside   A re-audit or repeat whose finding sits in a project that depends on a planted
                  defect's project (its .csproj references in -Tree, transitively), or whose
                  evidence cites a planted file by name, is recorded "not re-audited | overlaps
                  planted defect PD-###" and counts as neither yes nor no: auditors run their
                  reproductions in the planted tree.
      re-audit    Each other reaudits entry appends a Re-audits line to the finding it names (one
                  from another auditor marked "(re-audited by <auditor>)"). An open finding whose
                  own auditor says it no longer reproduces is closed (status, reason, closed,
                  closed-how, closed-by and a Log line) by the first of:
                    mechanical        it has a mutation reproduction and the runner's targeted
                                      rerun on the clean audited -Commit kills the mutant
                                      (-RerunReproductions), whatever -Unreliable says;
                    reliable-reaudit  no mechanical answer (none, or the rerun could not tell)
                                      and the auditor is not in -Unreliable;
                    consecutive       no mechanical answer and the finding's previous Re-audits
                                      line is its own auditor's "no" from a different audit;
                                      closed-by names both scorecards.
                  A rerun in which the mutant survives closes nothing. Nothing else closes a
                  finding - not its task's state, and not an audit that did not report it. Closed
                  findings are never changed. Re-audits lines go at the end of Re-audits, before
                  Log (BL-1183).

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

.PARAMETER Tree
    The audited (planted) tree: where a mutant's member and the projects' references are read.

.PARAMETER RerunReproductions
    Rerun each mechanical reproduction the closure rule needs, with Invoke-MutationTest.ps1 -Site
    on -Commit (the clean audited commit, not the planted tree). Without it the rerun reads as
    unverified and the other closure paths apply.

.PARAMETER SelfTest
    Run against Audit/Tools/Fixtures/findings and Fixtures/closure, copied to a temporary folder,
    and check every rule.
#>
param(
    [string]$ReportDirectory,
    [string]$Manifest,
    [string[]]$Unreliable = @(),
    [string]$Scorecard,
    [string]$Commit,
    [string]$Date = (Get-Date -Format 'yyyy-MM-dd'),
    [string]$FindingsDirectory,
    [string]$Tree,
    [switch]$RerunReproductions,
    [switch]$SelfTest
)
# Finding ID -> a mechanical outcome to use instead of rerunning; only the self-test sets it.
$MechanicalOutcomes = @{}

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
    $location = if ($text -match '(?m)^Location: `([^`]*)`') { $Matches[1] } else { '' }
    return [pscustomobject]@{ Path = $Path; Id = $fields['id']; Key = $fields['key']; Status = $fields['status']; Auditor = $fields['auditor']; Reproduction = "$($fields['reproduction'])"; Tasks = "$($fields['tasks'])"; Location = $location }
}

# --- planted defects overlapping a finding (ADR-0422) -----------------------------------------

function Get-ProjectClosure([string]$Project) {
    # The project, its .UnitLibrary/.UnitTests twin, and every project they reference, transitively,
    # as the audited tree (-Tree) has them: a planted defect in any of them can change what the
    # project's tests and reproductions show.
    $seen = @{}
    $queue = New-Object System.Collections.Queue
    foreach ($p in @($Project, ($Project -replace '\.UnitTests$', '.UnitLibrary'), ($Project -replace '\.UnitLibrary$', '.UnitTests'))) { $queue.Enqueue($p) }
    while ($queue.Count) {
        $p = $queue.Dequeue()
        if ($seen.ContainsKey($p)) { continue }
        $seen[$p] = $true
        $csproj = if ($Tree) { Join-Path $Tree "$p\$p.csproj" } else { '' }
        if (-not $csproj -or -not (Test-Path -LiteralPath $csproj)) { continue }
        foreach ($m in [regex]::Matches([IO.File]::ReadAllText($csproj), 'ProjectReference\s+Include="[^"]*?([^"\\/]+)\.csproj"')) { $queue.Enqueue($m.Groups[1].Value) }
    }
    return @($seen.Keys)
}

function Get-OverlappingPlant($Target, [string]$Evidence, [object[]]$Planted) {
    # The planted defect that may have produced this audit's verdict on the finding, or $null: one
    # in a project the finding's location depends on (Get-ProjectClosure), or one whose file name
    # the auditor's evidence cites. Auditors re-audit in the planted tree (BL-1597's audit: AF-0009
    # and AF-0026 "reproduced" because of PD-303 and PD-103).
    foreach ($p in $Planted) {
        $file = "$($p.file)" -replace '\\', '/'
        $name = $file.Substring($file.LastIndexOf('/') + 1)
        if ($name -and $Evidence.IndexOf($name, [StringComparison]::OrdinalIgnoreCase) -ge 0) { return $p }
        if ($file -notmatch '/') { continue }
        $plantProject = ($file -split '/')[0]
        foreach ($part in ("$($Target.Location)" -split ',\s*')) {
            $project = (($part -replace '\\', '/').Trim() -split '/')[0]
            if ($project -and (@(Get-ProjectClosure $project) -contains $plantProject)) { return $p }
        }
    }
    return $null
}

function Add-SetAsideLine($Target, $Plant, [string]$Verdict, [string]$Evidence) {
    Add-ReauditLine $Target.Path "- $Date | $Scorecard | not re-audited | overlaps planted defect $($Plant.id) in $($Plant.file), so the auditor's verdict (reproduces $Verdict) is set aside: $Evidence"
}

# The front matter keys in template order (Findings/README.md). Findings filed before ADR-0422 lack
# reproduction, tasks, duplicate-of and closed-how; Set-FrontMatter adds one in its place when set.
$FrontMatterOrder = @('id', 'title', 'auditor', 'severity', 'status', 'reason', 'key', 'reproduction', 'task', 'tasks', 'found', 'found-at', 'scorecard', 'duplicate-of', 'closed', 'closed-how', 'closed-by')

function Set-FrontMatter([string]$Text, [string]$Name, [string]$Value) {
    $line = "${Name}: $Value".TrimEnd()
    $existing = [regex]::new("(?m)^$([regex]::Escape($Name)):[^\r\n]*")
    if ($existing.IsMatch($Text)) { return $existing.Replace($Text, $line.Replace('$', '$$'), 1) }
    $newline = if ($Text.Contains("`r`n")) { "`r`n" } else { "`n" }
    $before = @($FrontMatterOrder[0..([array]::IndexOf($FrontMatterOrder, $Name) - 1)])
    [array]::Reverse($before)
    foreach ($previous in $before) {
        $at = [regex]::Match($Text, "(?m)^$([regex]::Escape($previous)):[^\r\n]*")
        if ($at.Success) { return $Text.Insert($at.Index + $at.Length, $newline + $line) }
    }
    return $Text
}

# --- surviving mutants: a mechanical key and reproduction (ADR-0422) ---------------------------

# Operator names from Invoke-MutationTest.ps1, as words a key may hold.
$OperatorWords = @{ '==' = 'eq'; '!=' = 'ne'; '<' = 'lt'; '>' = 'gt'; '<=' = 'le'; '>=' = 'ge'; '&&' = 'and'; '||' = 'or'; '+1' = 'plus1'; '-1' = 'minus1'; 'true' = 'true'; 'false' = 'false'; '!(' = 'not' }

# A member declaration; the same rule as Invoke-MutationTest.ps1's $MemberPattern.
$MemberPattern = '^\s*(?:(?:public|private|protected|internal|static|async|override|virtual|sealed|abstract|extern|unsafe|new|partial|readonly|required|file)\s+)+[^=;(){}]*?\b([A-Za-z_]\w*)\s*(?:<[^<>]*(?:<[^<>]*>[^<>]*)*>)?\s*(?:\(|\{|=>|=(?![=>]))'

function Get-MutationSite([string]$Text) {
    # "<file>:<line>:<operator>" (a report's reproduction.mutation) or "mutation <file>:<line>:<operator>"
    # (a finding's reproduction field) -> File, Line, Operator; $null for anything else.
    if ("$Text".Trim() -notmatch '^(?:mutation\s+)?(?<file>[^\s:]+\.cs):(?<line>\d+):(?<op>\S+)$') { return $null }
    if (-not $OperatorWords.ContainsKey($Matches['op'])) { return $null }
    return [pscustomobject]@{ File = ($Matches['file'] -replace '\\', '/'); Line = [int]$Matches['line']; Operator = $Matches['op'] }
}

function Get-SiteMember($Site, [string]$ReportedKey) {
    # The member the site is in: from the audited tree (-Tree) when it has the file, otherwise
    # the <what> of the auditor's key without an operator suffix, letters and digits only.
    $path = if ($Tree) { Join-Path $Tree ($Site.File -replace '/', '\') } else { '' }
    if ($path -and (Test-Path -LiteralPath $path)) {
        $lines = [IO.File]::ReadAllLines($path)
        for ($i = [math]::Min($Site.Line, $lines.Length) - 1; $i -ge 0; $i--) { if ($lines[$i] -match $MemberPattern) { return $Matches[1] } }
        return '-'
    }
    $what = @("$ReportedKey" -split ':')
    $name = if ($what.Count -ge 4) { $what[$what.Count - 2] } else { '-' }
    return (($name -split '-')[0] -replace '[^A-Za-z0-9_]', '')
}

function Get-MutantKey([string]$Auditor, $Site, [string]$Member) {
    return "${Auditor}:$($Site.File):$Member-$($OperatorWords[$Site.Operator]):surviving-mutant"
}

function Get-MutantCommand($Site, [string]$Member) {
    return "powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site $($Site.File):$($Site.Line):$($Site.Operator) -Member $Member -ExcludeBaselineFailures -TimeoutSeconds 600"
}

function ConvertTo-FiledFinding($Finding, [string]$Auditor) {
    # A reported finding as it is matched and filed. One with reproduction.mutation gets the
    # mechanical key, the targeted reproduction command and a reproduction field; so the same
    # mutant reported in other words is the same finding (ADR-0422).
    $site = Get-MutationSite "$($Finding.reproduction.mutation)"
    $filed = [pscustomobject]@{ Key = "$($Finding.key)"; Reproduction = 'none'; Command = "$($Finding.reproduction.command)" }
    if (-not $site) { return $filed }
    $member = Get-SiteMember $site "$($Finding.key)"
    $filed.Key = Get-MutantKey $Auditor $site $member
    $filed.Reproduction = "mutation $($site.File):$($site.Line):$($site.Operator)"
    $filed.Command = Get-MutantCommand $site $member
    return $filed
}

# --- closing (ADR-0422) -----------------------------------------------------------------------

function Get-ReauditLines([string]$Text) {
    # The Re-audits lines, oldest first: Date, Scorecard, Reproduces, Evidence, Own (written for the
    # finding's own auditor: every line not marked "(re-audited by <auditor>)").
    $section = if ($Text -match '(?s)## Re-audits[ \t]*\r?\n(.*?)(\r?\n## |\z)') { $Matches[1] } else { '' }
    $lines = @()
    foreach ($line in ($section -split '\r?\n')) {
        if ($line -notmatch '^- (\S+) \| (\S+) \| reproduces: (yes|no) \| ?(.*)$') { continue }
        $lines += [pscustomobject]@{ Date = $Matches[1]; Scorecard = $Matches[2]; Reproduces = ($Matches[3] -eq 'yes'); Evidence = $Matches[4]; Own = ($Matches[4] -notlike '(re-audited by *') }
    }
    return $lines
}

function Get-ConsecutiveNo([string]$Text, [string]$ScorecardName) {
    # The scorecard of the previous Re-audits line when it, too, is the finding's own auditor saying
    # "reproduces: no" on a different audit than this one; otherwise ''. Called after this audit's
    # line was added, so that line is the last.
    $lines = @(Get-ReauditLines $Text)
    if ($lines.Count -lt 2) { return '' }
    $previous = $lines[$lines.Count - 2]
    if ($previous.Own -and -not $previous.Reproduces -and $previous.Scorecard -ne $ScorecardName) { return $previous.Scorecard }
    return ''
}

function Invoke-MechanicalReproduction($Target) {
    # Reruns a finding's mechanical reproduction on the audited commit: 'killed' (the targeted
    # mutant is killed or timed out: fixed), 'survived' (still reproduces), 'unverified' (not run,
    # or it could not tell), or 'none' (the finding has no mechanical reproduction).
    $site = Get-MutationSite $Target.Reproduction
    if (-not $site) { return 'none' }
    if ($MechanicalOutcomes.ContainsKey($Target.Id)) { return $MechanicalOutcomes[$Target.Id] }
    if (-not $RerunReproductions) { return 'unverified' }
    $member = if ($Target.Key -match ':([A-Za-z0-9_]+)-[a-z0-9]+:surviving-mutant$') { $Matches[1] } else { '' }
    $out = Join-Path ([IO.Path]::GetTempPath()) ("reproduction-$($Target.Id)-" + [guid]::NewGuid().ToString('N') + '.json')
    $arguments = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $PSScriptRoot 'Invoke-MutationTest.ps1'), '-Site', "$($site.File):$($site.Line):$($site.Operator)", '-Commit', $Commit, '-ExcludeBaselineFailures', '-TimeoutSeconds', '600', '-OutFile', $out)
    if ($member) { $arguments += @('-Member', $member) }
    $ErrorActionPreference = 'Continue'
    & powershell @arguments *> $null
    $outcome = 'unverified'
    if (Test-Path -LiteralPath $out) {
        $result = Get-Content -LiteralPath $out -Raw | ConvertFrom-Json
        $outcome = switch ("$($result.outcome)") { 'killed' { 'killed' } 'timedOut' { 'killed' } 'survived' { 'survived' } default { 'unverified' } }
        Remove-Item -LiteralPath $out -ErrorAction SilentlyContinue
    }
    return $outcome
}

function Get-ClosePath($Target, [string]$Auditor, [bool]$Flagged, [string]$Mechanical) {
    # How a "reproduces: no" re-audit closes an open finding, or $null when it does not: its own
    # auditor's re-audit, then, in this order, the runner's mechanical rerun agreeing (whatever the
    # auditor's reliability), a reliable auditor (findings with no mechanical answer), or two
    # consecutive "no" re-audits on two different audits. A mechanical "survived" closes nothing.
    if ($Target.Auditor -ne $Auditor -or $Target.Status -notin 'proposed', 'accepted', 'deferred', 'blocked') { return $null }
    if ($Mechanical -eq 'killed') { return [pscustomobject]@{ How = 'mechanical'; By = $Scorecard } }
    if ($Mechanical -eq 'survived') { return $null }
    if (-not $Flagged) { return [pscustomobject]@{ How = 'reliable-reaudit'; By = $Scorecard } }
    $previous = Get-ConsecutiveNo ([IO.File]::ReadAllText($Target.Path)) $Scorecard
    if ($previous) { return [pscustomobject]@{ How = 'consecutive'; By = "$previous, $Scorecard" } }
    return $null
}

function Close-Finding($Target, $Path, [string]$Reason) {
    $text = [IO.File]::ReadAllText($Target.Path)
    $text = Set-FrontMatter $text 'status' 'closed'
    $text = Set-FrontMatter $text 'reason' $Reason
    $text = Set-FrontMatter $text 'closed' $Date
    $text = Set-FrontMatter $text 'closed-how' $Path.How
    $text = Set-FrontMatter $text 'closed-by' $Path.By
    [IO.File]::WriteAllText($Target.Path, $text, $Utf8)
    Add-LogLine $Target.Path "- ${Date}: $($Target.Status) -> closed. $Reason"
    $Target.Status = 'closed'
}

function Close-Duplicates([object[]]$Findings) {
    # Open findings with the same key are one finding: every one but the lowest ID is closed as a
    # duplicate of it (closed-how: duplicate, duplicate-of), and its tasks join the original's
    # tasks list. Returns how many were closed.
    $closed = 0
    $open = @($Findings | Where-Object { $_.Status -in 'proposed', 'accepted', 'deferred', 'blocked' } | Sort-Object Id)
    foreach ($group in @($open | Group-Object Key | Where-Object { $_.Count -gt 1 })) {
        $original = $group.Group[0]
        foreach ($dup in @($group.Group | Select-Object -Skip 1)) {
            $text = Set-FrontMatter ([IO.File]::ReadAllText($dup.Path)) 'duplicate-of' $original.Id
            [IO.File]::WriteAllText($dup.Path, $text, $Utf8)
            Add-FindingTasks $original (Get-FindingTasks $dup)
            Close-Finding $dup ([pscustomobject]@{ How = 'duplicate'; By = $Scorecard }) "Duplicate of $($original.Id): the same key."
            $closed++
        }
    }
    return $closed
}

function Get-FindingTasks($Finding) {
    # Every task filed for the finding: its tasks list, and its task when that is not in it.
    $text = [IO.File]::ReadAllText($Finding.Path)
    $all = @()
    if ($text -match '(?m)^tasks:[ \t]*([^\r\n]*)') { $all += @($Matches[1] -split ',\s*' | Where-Object { $_ -match '^BL-\d+$' }) }
    if ($text -match '(?m)^task:[ \t]*(BL-\d+)') { $all += $Matches[1] }
    return @($all | Select-Object -Unique)
}

function Add-FindingTasks($Finding, [string[]]$Ids) {
    $all = @(@(Get-FindingTasks $Finding) + @($Ids) | Where-Object { $_ } | Sort-Object { [int]($_ -replace '\D', '') } -Unique)
    if (-not $all.Count) { return }
    $text = Set-FrontMatter ([IO.File]::ReadAllText($Finding.Path)) 'tasks' ($all -join ', ')
    [IO.File]::WriteAllText($Finding.Path, $text, $Utf8)
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
    # Or the planted file's base name alone: an ADR or script cited by its file name (BL-1370).
    return ($Location -ieq $Planted) -or $Location.EndsWith('/' + $Planted, [StringComparison]::OrdinalIgnoreCase) -or
        ($Planted.Contains('/') -and $Location -ieq $Planted.Substring($Planted.LastIndexOf('/') + 1))
}

function Test-AtPlantedLine([string]$Location, $Planted) {
    # The finding's location (file:line or file:first-last) reaches within 2 lines of the
    # planted line. A defect reported twice for two symptoms - slow and memory-hungry, say -
    # names its catch text in one report only; the other still points at the planted line
    # (BL-1316). 2 lines, not more, so a real finding beside a planted one stays a finding.
    if (-not "$($Planted.line)" -or "$Location" -notmatch ':(\d+)(?:-(\d+))?$') { return $false }
    $first = [int]$Matches[1]
    $last = if ($Matches[2]) { [int]$Matches[2] } else { $first }
    $line = [int]$Planted.line
    return ($line -ge $first - 2) -and ($line -le $last + 2)
}

function Test-LogPlant($Planted) {
    # A process plant edits one file of the log copy (a lane trace, a run log, ci-runs.json),
    # named bare, with no project folder. A finding about it names the task or run it shows,
    # rarely the one file the seeder edited, so its catch text alone identifies it (BL-1365).
    return ("$($Planted.auditor)" -eq 'process') -and ("$($Planted.file)" -notmatch '[\\/]')
}
function Test-Catch($Finding, [string]$Auditor, [object[]]$Planted) {
    # A catch: the planted defect's own auditor, in the defect's file, with the manifest's catch
    # fragment in the finding's title, key or evidence, or with a location at the planted line.
    # The same rule as Write-AuditScorecard.ps1.
    foreach ($p in $Planted) {
        if ($p.auditor -ne $Auditor) { continue }
        $sameFile = Test-SamePath (ConvertTo-Normalised $Finding.location) (ConvertTo-Normalised $p.file)
        $haystack = "$($Finding.title) $($Finding.key) $($Finding.evidence)"
        $catchText = "$($p.catch)".Trim()
        $named = $catchText -and $haystack.IndexOf($catchText, [StringComparison]::OrdinalIgnoreCase) -ge 0
        if (Test-LogPlant $p) { if ($named) { return $p } else { continue } }
        if ($sameFile -and ($named -or (Test-AtPlantedLine "$($Finding.location)" $p))) { return $p }
    }
    return $null
}

function New-FindingFile($Finding, $Filed, [string]$Auditor, [string]$Id, [string]$Note) {
    $template = Join-Path $FindingsDirectory 'FINDING-TEMPLATE.md'
    if (-not (Test-Path -LiteralPath $template)) { $template = Join-Path $repo 'Audit\Findings\FINDING-TEMPLATE.md' }
    $summary = "$($Finding.severity) finding from the $Auditor auditor at ``$($Finding.location)``: $($Finding.title)."
    if ($Note) { $summary += " $Note" }
    $values = [ordered]@{
        '{{ID}}' = $Id; '{{TITLE}}' = "$($Finding.title)"; '{{AUDITOR}}' = $Auditor; '{{SEVERITY}}' = "$($Finding.severity)"
        '{{KEY}}' = $Filed.Key; '{{REPRODUCTION}}' = $Filed.Reproduction; '{{FOUND}}' = $Date; '{{FOUND_AT}}' = $Commit; '{{SCORECARD}}' = $Scorecard
        '{{SUMMARY}}' = $summary; '{{LOCATION}}' = "$($Finding.location)"; '{{EVIDENCE}}' = "$($Finding.evidence)"
        '{{REPRODUCTION_COMMAND}}' = $Filed.Command; '{{REPRODUCTION_EXPECTED}}' = "$($Finding.reproduction.expected)"
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
    $counts.closed += Close-Duplicates $existing

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
            $filed = ConvertTo-FiledFinding $f $auditor
            $same = @($existing | Where-Object { $_.Key -ceq $filed.Key })
            $live = @($same | Where-Object { $_.Status -in 'proposed', 'accepted', 'deferred', 'blocked', 'rejected' })[0]
            if ($live) {
                $plant = Get-OverlappingPlant $live "$($f.evidence)" $planted
                if ($plant) { Add-SetAsideLine $live $plant 'yes' 'still reported' }
                else { Add-ReauditLine $live.Path "- $Date | $Scorecard | reproduces: yes | still reported" }
                if ($live.Status -ne 'rejected') { $counts.open++ }
                continue
            }
            $closedBefore = @($same | Where-Object { $_.Status -eq 'closed' })[0]
            $notes = @()
            if ($closedBefore) { $notes += "Reappeared; previously $($closedBefore.Id)." }
            if ($flagged) { $notes += "Reported by an auditor flagged unreliable in $Scorecard." }
            $id = 'AF-{0:D4}' -f $next
            $next++
            $path = New-FindingFile $f $filed $auditor $id ($notes -join ' ')
            $existing += Read-Finding $path
            $counts.new++
        }

        foreach ($r in @($report.reaudits | Where-Object { $_ })) {
            $target = @($existing | Where-Object { $_.Id -eq "$($r.finding)" })[0]
            if (-not $target -or $target.Status -eq 'closed') { continue }
            if ($null -eq $r.reproduces) {
                # The auditor could not run the reproduction (a site its sample missed, a red baseline):
                # not a "no" (Quality.md, ADR-0422).
                Add-ReauditLine $target.Path "- $Date | $Scorecard | not re-audited | $($r.evidence)"
                continue
            }
            $reproduces = [bool]$r.reproduces
            $plant =Get-OverlappingPlant $target "$($r.evidence)" $planted
            if ($plant) { Add-SetAsideLine $target $plant $(if ($reproduces) { 'yes' } else { 'no' }) "$($r.evidence)"; continue }
            $own = $target.Auditor -eq $auditor
            $mechanical = if ((-not $reproduces) -and $own) { Invoke-MechanicalReproduction $target } else { 'none' }
            $evidence = "$($r.evidence)"
            if (-not $own) { $evidence = "(re-audited by $auditor) $evidence" }
            if ($mechanical -ne 'none') { $evidence += " Runner's targeted mutation rerun: $mechanical." }
            Add-ReauditLine $target.Path "- $Date | $Scorecard | reproduces: $(if ($reproduces) { 'yes' } else { 'no' }) | $evidence"
            $path = if ($reproduces) { $null } else { Get-ClosePath $target $auditor $flagged $mechanical }
            if ($path) {
                $because = switch ($path.How) {
                    'mechanical' { "the reproduction no longer reproduces, and the runner's targeted mutant was killed" }
                    'consecutive' { "a second consecutive re-audit by its own auditor found the reproduction no longer reproduces ($($path.By))" }
                    default { 'the reproduction no longer reproduces' }
                }
                Close-Finding $target $path "Re-audit $Scorecard`: $because."
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
        Check 'new finding filed as proposed with the next ID' ($af7 -match '(?m)^status: proposed\r?$' -and $af7 -match '(?m)^key: quality:Curl.Cli.UnitTests/ParserTests.cs:Parse_Empty_Throws:weak-assertion\r?$') 'AF-0007'
        $af1 = Text 'AF-0001'
        Check 'a repeated key on a deferred finding keeps it deferred, its line inside Re-audits, before Log' ($af1 -match '(?m)^status: deferred\r?$' -and $af1 -match '(?s)## Re-audits\r?\n\r?\n- 2026-10-14 \| 2026-10-14_0930.md \| reproduces: yes \| still reported\r?\n\r?\n## Log\r?\n\r?\n- 2026-10-01: filed proposed\.\r?\n- 2026-10-02: proposed -> deferred\.[^\r\n]*\r?\n?$') 'AF-0001'
        Check 'a repeated key adds a re-audit line and files nothing' ((Text 'AF-0001') -match 'reproduces: yes \| still reported' -and -not ((Get-ChildItem -LiteralPath $FindingsDirectory -Filter 'AF-*.md' | ForEach-Object { [IO.File]::ReadAllText($_.FullName) }) -match 'key: quality:Curl.Core.UnitTests/UrlTests.cs:Parse_Port_Rejects:name-lies' | Measure-Object).Count -ne 1) 'AF-0001'
        # Windows PowerShell 5.1's ConvertFrom-Json emits a JSON array as one object; unroll it.
        $catchesJson = @((Get-Content -LiteralPath (Join-Path $ReportDirectory 'catches.json') -Raw | ConvertFrom-Json) | ForEach-Object { $_ })
        Check 'a planted-defect match is skipped and in catches.json' ($catchesJson.Count -eq 2 -and @($catchesJson | Where-Object { $_.planted -eq 'PD-101' }).Count -eq 2 -and -not ((Get-ChildItem -LiteralPath $FindingsDirectory -Filter 'AF-*.md' | ForEach-Object { [IO.File]::ReadAllText($_.FullName) }) -match 'FixedTimeEquals replaced')) "catches $($catchesJson.Count)"
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
        Check 'front matter and sections match the template' ($newFields -eq $templateFields -and $sections -eq 'Summary,Evidence,Reproduction,Re-audits,Log' -and $af7 -match '(?m)^- 2026-10-14: filed proposed\.\r?$' -and $af7 -notmatch '\{\{') "$newFields | $sections"
        Check 'a process defect cited under logs/ is the same file' ((Test-SamePath 'logs/ci-runs.json' 'ci-runs.json') -and -not (Test-SamePath 'logs/other-ci-runs.json' 'ci-runs.json') -and (Test-SamePath 'Curl.Tls.UnitLibrary/TlsMac.cs' 'Curl.Tls.UnitLibrary/TlsMac.cs'))
        Check 'a file cited by its base name is the planted file' ((Test-SamePath 'ADR-0401-x.md' 'Documentation/Planning/Decisions/ADR-0401-x.md') -and -not (Test-SamePath 'ADR-0402-x.md' 'Documentation/Planning/Decisions/ADR-0401-x.md') -and -not (Test-SamePath 'TlsMac.cs' 'TlsMac.cs.bak')) 'ADR-0401-x.md' 'logs/ci-runs.json'
        Check 'a differently worded report at the planted line is a catch, not a finding' (-not ((Get-ChildItem -LiteralPath $FindingsDirectory -Filter 'AF-*.md' | ForEach-Object { [IO.File]::ReadAllText($_.FullName) }) -match 'returns at the first differing byte')) 'TlsMac.cs:41'
        $plantedAt40 = [pscustomobject]@{ line = 40 }
        Check 'the planted-line window is 2 lines' ((Test-AtPlantedLine 'x.cs:42' $plantedAt40) -and -not (Test-AtPlantedLine 'x.cs:43' $plantedAt40) -and (Test-AtPlantedLine 'x.cs:30-38' $plantedAt40) -and -not (Test-AtPlantedLine 'x.cs:30-37' $plantedAt40) -and -not (Test-AtPlantedLine 'x.cs' $plantedAt40) -and -not (Test-AtPlantedLine 'x.cs:40' ([pscustomobject]@{ line = $null }))) '42 yes, 43 no, 30-38 yes, 30-37 no, no line no'
        # The two real process reports of planted log defects (2026-10-02 and 2026-10-03, BL-1365).
        $plant1 = [pscustomobject]@{ id = 'PD-501'; auditor = 'process'; file = 'DarkFactory-20261001-120001-L9.log'; line = $null; catch = 'BL-1121' }
        $plant2 = [pscustomobject]@{ id = 'PD-501'; auditor = 'process'; file = 'DarkFactory-20261002-231500-L9.log'; line = 2; catch = 'BL-1289' }
        $report1 = [pscustomobject]@{ key = 'process:logs:BL-1121:redone-work'; title = 'BL-1121 claimed 5 times'; location = 'logs/BL-1121'; evidence = 'claimed 5 times' }
        $report2 = [pscustomobject]@{ key = 'process:logs:BL-1289:redone-work'; title = 'BL-1289 was claimed 6 times (134 minutes) before reaching Done'; location = 'logs/BL-1289-20261002-211047-L1.jsonl:1'; evidence = 'six claims' }
        $other = [pscustomobject]@{ key = 'process:logs:BL-1300:redone-work'; title = 'BL-1300 claimed 3 times'; location = 'logs/DarkFactory-20261001-120001-L9.log'; evidence = 'three claims' }
        Check 'a process report naming a planted log defect''s catch text is a catch, whatever file it cites' ((Test-Catch $report1 'process' @($plant1)) -and (Test-Catch $report2 'process' @($plant2)) -and -not (Test-Catch $other 'process' @($plant1)) -and -not (Test-Catch $report1 'security' @($plant1))) 'both PD-501 reports; not another task, not another auditor'
        Check 'summary line' ($line -eq 'findings: new 3, still open 1, closed 1, catches 2') $line

        # ADR-0422: closing on mechanical evidence, two consecutive "no"s, duplicates, mechanical keys.
        $closure = Join-Path $work 'closure'
        Copy-Item -Recurse -LiteralPath (Join-Path $PSScriptRoot 'Fixtures\closure') -Destination $closure
        $FindingsDirectory = Join-Path $closure 'findings'
        Copy-Item -LiteralPath (Join-Path $repo 'Audit\Findings\FINDING-TEMPLATE.md') -Destination $FindingsDirectory
        $ReportDirectory = Join-Path $closure 'reports'; $Manifest = Join-Path $closure 'manifest.json'; $Tree = Join-Path $closure 'tree'
        $Unreliable = @('quality')
        $script:MechanicalOutcomes = @{ 'AF-0010' = 'killed'; 'AF-0011' = 'survived' }
        $line2 = Invoke-WriteFindings
        $c10 = Text 'AF-0010'; $c11 = Text 'AF-0011'; $c12 = Text 'AF-0012'; $c13 = Text 'AF-0013'; $c14 = Text 'AF-0014'; $c15 = Text 'AF-0015'; $c16 = Text 'AF-0019'; $c17 = Text 'AF-0017'; $c18 = Text 'AF-0018'
        Check 'an unreliable auditor''s "no" closes when the runner''s targeted mutant is killed' ($c10 -match '(?m)^status: closed$' -and $c10 -match '(?m)^closed-how: mechanical$' -and $c10 -match '(?m)^closed-by: 2026-10-14_0930.md$' -and $c10 -match "rerun: killed\.") 'AF-0010'
        Check 'a "no" the runner''s rerun contradicts (survived) stays open' ($c11 -match '(?m)^status: accepted$' -and $c11 -match "rerun: survived\.") 'AF-0011'
        Check 'another auditor''s re-audit is marked and closes nothing' ($c11 -match '\| reproduces: no \| \(re-audited by security\) looked fine') 'AF-0011'
        Check 'two consecutive "no"s on two audits close an unreliable auditor''s finding' ($c12 -match '(?m)^status: closed$' -and $c12 -match '(?m)^closed-how: consecutive$' -and $c12 -match '(?m)^closed-by: 2026-10-07_0844.md, 2026-10-14_0930.md$') 'AF-0012'
        Check 'closed-how is inserted in template order into a finding from before ADR-0422' ($c12 -match '(?s)\nclosed: 2026-10-14\nclosed-how: consecutive\nclosed-by: ') 'AF-0012'
        Check 'a "no" after a "yes" from an unreliable auditor stays open' ($c13 -match '(?m)^status: accepted$') 'AF-0013'
        Check 'open findings with one key: the later is closed as a duplicate of the earlier' ($c15 -match '(?m)^status: closed$' -and $c15 -match '(?m)^closed-how: duplicate$' -and $c15 -match '(?m)^duplicate-of: AF-0014$' -and $c14 -match '(?m)^tasks: BL-6, BL-7$' -and $c14 -match '(?m)^status: accepted$') 'AF-0015'
        Check 'the same mutant in other words is a repeat of the open finding' ($c14 -match 'reproduces: yes \| still reported' -and -not ((Get-ChildItem -LiteralPath $FindingsDirectory -Filter 'AF-*.md' | ForEach-Object { [IO.File]::ReadAllText($_.FullName) }) -match 'title: NoDelay can become false')) 'AF-0014'
        Check 'a new surviving mutant gets the mechanical key, reproduction and command' ($c16 -match '(?m)^key: quality:Curl\.Net\.UnitLibrary/C\.cs:Check-eq:surviving-mutant\r?$' -and $c16 -match '(?m)^reproduction: mutation Curl\.Net\.UnitLibrary/C\.cs:6:==\r?$' -and $c16 -match 'Invoke-MutationTest\.ps1 -Site Curl\.Net\.UnitLibrary/C\.cs:6:== -Member Check -ExcludeBaselineFailures') 'AF-0019'
        Check 'a re-audit in a project that depends on a planted defect''s project is set aside, not counted' ($c17 -match '\| not re-audited \| overlaps planted defect PD-103 in Curl\.Dep\.UnitLibrary/Reader\.cs, so the auditor''s verdict \(reproduces yes\) is set aside: fails with IndexOutOfRange' -and $c17 -notmatch 'reproduces: yes \| fails') 'AF-0017'
        Check 'a re-audit whose evidence cites a planted file is set aside' ($c18 -match 'overlaps planted defect PD-104' -and $c18 -match '(?m)^status: accepted\r?$') 'AF-0018'
        Check 'a set-aside line is neither yes nor no' (@(Get-ReauditLines $c17).Count -eq 2) "$(@(Get-ReauditLines $c17).Count) parsed"        Check 'a re-audit with reproduces null is "not re-audited", not a no' ($c14 -match '\| 2026-10-14_0930\.md \| not re-audited \| site not sampled' -and $c14 -match '(?m)^status: accepted\r?$') 'AF-0014'
        Check 'closure summary line' ($line2 -eq 'findings: new 1, still open 1, closed 3, catches 0') $line2
        $Tree = ''
        Check 'with no tree the member is the key''s <what> without its operator' ((Get-SiteMember ([pscustomobject]@{ File = 'X.UnitLibrary/Y.cs'; Line = 3; Operator = 'true' }) 'quality:X.UnitLibrary/Y.cs:Turn-true:surviving-mutant') -eq 'Turn') 'Turn'
        exit $(if ($failed) { 1 } else { 0 })
    }
    finally { Remove-Item -Recurse -Force -LiteralPath $work -ErrorAction SilentlyContinue }
}

if (-not $ReportDirectory -or -not $Scorecard -or -not $Commit) { throw 'Give -ReportDirectory, -Scorecard and -Commit (and -Manifest, -Unreliable as the audit has them).' }
if (-not $FindingsDirectory) { $FindingsDirectory = Join-Path $repo 'Audit\Findings' }
Invoke-WriteFindings
