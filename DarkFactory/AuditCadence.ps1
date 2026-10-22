# Whether an audit is due (master's Test-AuditDue.ps1), the shift-start decision while an
# audit runs, the audit notice, and -TestAuditCadence. Dot-sourced by RunDarkFactory.ps1 in
# its order; see the loader there.

function Get-AuditDueFromMaster {
    # The audit office's view of whether an audit is due (BL-1019), from master's copy of
    # Audit/Tools/Test-AuditDue.ps1 - never this branch's, so the factory never runs its own copy
    # of an audit tool. Returns its -Json answer, or $null when master has no copy yet or it fails.
    param([string]$Branch, [string]$MasterRef = 'origin/master')
    $tool = Join-Path ([IO.Path]::GetTempPath()) "Test-AuditDue-$PID.ps1"
    $copy = git -C $Root show "${MasterRef}:Audit/Tools/Test-AuditDue.ps1" 2>$null
    if ($LASTEXITCODE -ne 0 -or -not $copy) { return $null }
    try {
        [IO.File]::WriteAllText($tool, ($copy -join "`r`n"))
        $json = & powershell -NoProfile -ExecutionPolicy Bypass -File $tool -Json -Repository $Root -Ref "origin/$Branch" -ScorecardsRef $MasterRef 2>$null
        if ($LASTEXITCODE -ne 0 -or -not $json) { return $null }
        return ($json -join '') | ConvertFrom-Json
    } catch { return $null }
    finally { Remove-Item -LiteralPath $tool -ErrorAction SilentlyContinue }
}

function Get-AuditCadence {
    # What the shift's end does with an audit-due answer: a report line, and whether to hold the
    # merge (only for a roadmap milestone, which is audited before it reaches master).
    param($Due)
    $result = [pscustomobject]@{ Line = ''; HoldMerge = $false; MergeMessage = '' }
    if (-not $Due -or -not $Due.due) { return $result }
    $result.Line = "Audit due: $(@($Due.reasons) -join ', ')"
    $milestone = @($Due.reasons | Where-Object { $_ -match '^milestone:(\d+)$' })[0]
    if ($milestone -and $milestone -match '^milestone:(\d+)$') {
        $result.HoldMerge = $true
        $result.MergeMessage = "not merged: audit due before the Milestone $($Matches[1]) merge (run Audit\RunAudit.cmd)"
    }
    return $result
}

function Get-ShiftStartDecision {
    # Whether a shift may start while an audit runs: start, refuse, or (with -Continuous) wait.
    # A waiting "Fix CI failure" task starts the shift anyway: CI is red and the audit reads a
    # fixed commit, so waiting for it only lengthens the red spell (AF-0203, BL-2196). An audit
    # started with -AlongsideShift was meant to share the machine with a shift, so it holds none
    # back, not even one a -Restart starts while it runs (BL-2989).
    param([object[]]$Processes, [bool]$IsContinuous, [bool]$CiFixWaiting = $false)
    $audits = @($Processes | Where-Object { $_.CommandLine -match 'RunAudit\.ps1' -and $_.CommandLine -notmatch '\s-(NewTab|DryRun|SelfTest|AlongsideShift)\b' })
    if (-not $audits.Count -or $CiFixWaiting) { return 'start' }
    if ($IsContinuous) { return 'wait' }
    return 'refuse'
}

function Show-AuditNotice {
    # A one-off notice, not the alarm: an audit is due, said once at the end of the shift.
    param([string]$Line)
    Write-Host ("  $Line  ".PadRight(78)) -ForegroundColor Black -BackgroundColor Cyan
    Write-Trace '-' 'AUDIT' $Line 'Cyan'
    if (Test-AlarmSilent) { return }
    Invoke-Chime
    $voice = Get-Voice
    if ($voice) { $voice.SpeakAsyncCancelAll(); [void]$voice.SpeakAsync("Curl dark factory: $($Line.ToLowerInvariant() -replace ':', '.' -replace 'milestone\.', 'milestone ')") }
}

if ($TestAuditCadence) {
    $failed = 0
    $check = { param([string]$Name, [bool]$Ok, [string]$Detail) if (-not $Ok) { $script:auditCadenceFailed++ }; Write-Host "$(if ($Ok) { 'PASS' } else { 'FAIL' }) ${Name}: $Detail" -ForegroundColor $(if ($Ok) { 'Green' } else { 'Red' }) }
    $script:auditCadenceFailed = 0
    $none = Get-AuditDueFromMaster -Branch 'work/dark-factory' -MasterRef 'refs/heads/no-such-branch-for-the-test'
    $c = Get-AuditCadence $none
    & $check 'no copy on master skips silently' (($null -eq $none) -and -not $c.Line -and -not $c.HoldMerge) 'nothing reported, merge proceeds'
    $c = Get-AuditCadence ([pscustomobject]@{ due = $true; reasons = @('factory-script') })
    & $check 'factory-script is reported and the merge proceeds' ($c.Line -eq 'Audit due: factory-script' -and -not $c.HoldMerge) $c.Line
    $c = Get-AuditCadence ([pscustomobject]@{ due = $true; reasons = @('factory-script', 'milestone:3') })
    & $check 'milestone:3 is reported and the merge is skipped' ($c.Line -eq 'Audit due: factory-script, milestone:3' -and $c.HoldMerge -and $c.MergeMessage -eq 'not merged: audit due before the Milestone 3 merge (run Audit\RunAudit.cmd)') $c.MergeMessage
    $c = Get-AuditCadence ([pscustomobject]@{ due = $false; reasons = @() })
    & $check 'No audit due adds nothing' (-not $c.Line -and -not $c.HoldMerge) 'nothing'
    $audit = [pscustomobject]@{ CommandLine = 'powershell -NoProfile -File Z:\repos\Curl\Audit\RunAudit.ps1 -Auditors truthfulness' }
    $dry = [pscustomobject]@{ CommandLine = 'powershell -File Z:\repos\Curl\Audit\RunAudit.ps1 -DryRun' }
    & $check 'a running audit refuses a shift start' ((Get-ShiftStartDecision @($audit) $false) -eq 'refuse') 'refuse'
    & $check 'with -Continuous it waits instead' ((Get-ShiftStartDecision @($audit) $true) -eq 'wait') 'wait'
    & $check 'a dry run or no audit lets the shift start' ((Get-ShiftStartDecision @($dry) $false) -eq 'start') 'start'
    & $check 'a waiting CI fix task starts the shift despite a running audit' ((Get-ShiftStartDecision @($audit) $true $true) -eq 'start') 'start'
    $alongside = [pscustomobject]@{ CommandLine = 'powershell -NoProfile -ExecutionPolicy Bypass -File Z:\repos\Curl\Audit\RunAudit.ps1 -AlongsideShift' }
    & $check 'an audit run -AlongsideShift lets the shift start' ((Get-ShiftStartDecision @($alongside) $true) -eq 'start') 'start'
    & $check 'beside a plain audit it still waits' ((Get-ShiftStartDecision @($alongside, $audit) $true) -eq 'wait') 'wait'
    exit $(if ($script:auditCadenceFailed) { 1 } else { 0 })
}

# This file ran to its end; RunDarkFactory.ps1 goes on to the next one.
$AreaFileFinished = $true
