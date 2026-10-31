# The CI watch (BL-987): reading the shift branch's CI runs, filing a task for each new
# failure, filing split tasks for hotspots, and -TestCiWatch. Dot-sourced by
# RunDarkFactory.ps1 in its order; see the loader there.

# ---------------------------------------------------------------------------- CI watch

# Lanes test only on Windows, so a Linux- or macOS-only break used to sit unnoticed until
# the shift-end merge refused it (BL-987). The coordinator reads every finished CI run on
# the shift's branch and files a High task for each new failure. Runs are cached by id;
# Settled holds "<key>|<run id>" pairs already filed or found covered, so each is decided once.
$CiWatchDir = Join-Path $LanesDir 'ci-watch'
# Settled maps each pair to what became of it ("filed BL-###", "covered by a task"); Logged
# holds the last line traced for each run, so a run is traced again only when that changes.
$script:CiWatch = @{ CheckedAt = [datetime]::MinValue; Failures = @{}; Capacity = @{}; Settled = @{}; Logged = @{}; AuditTasks = @{}; Off = $false }
# The shift's branch once the coordinator knows it, so a wait for tokens keeps watching CI.
$script:CiWatchBranch = ''
# How many finished runs the flaky and regression verdicts look back over, and how long a
# failure seen on one platform in only the newest run waits for the next run to confirm it.
$CiWindowRuns = 6
$CiConfirmMinutes = 5

function Get-CiPlatform {
    # "Build and test (ubuntu-latest)" -> Linux; the platform's name, or the job's when unknown.
    param([string]$Job)
    if ($Job -match 'ubuntu|linux') { return 'Linux' }
    if ($Job -match 'macos') { return 'macOS' }
    if ($Job -match 'windows') { return 'Windows' }
    return $Job
}

function Get-CiFailures {
    # What `gh run view <id> --log-failed` says failed: one object per failing test ("Failed
    # <TestName> [..]") or, when the build broke, per compiler error ("error CS1002 in X.cs"),
    # with the platforms it failed on, its test project (or the project that did not build)
    # and the first line of its error message.
    # A data row whose display name is not its method's ("Failed -4 http://[::1]:47500/ [1 ms]")
    # is keyed by the method its test logger's START line names, with the row's display name in
    # Rows, so every failing row of one method is one failure (BL-3998). An upstream case
    # ("Failed test1034: ...") keeps its case number: that is the name its tasks use.
    param([string[]]$Log)
    $found = [ordered]@{}
    $current = @{}
    $awaitingMessage = @{}
    # Each job's failing test waits in $current until its block ends, then joins $found.
    $settle = {
        param([string]$Job)
        $pending = $current[$Job]
        $current[$Job] = $null
        if (-not $pending) { return }
        if (-not $found.Contains($pending.Key)) { $found[$pending.Key] = $pending; return }
        $entry = $found[$pending.Key]
        foreach ($platform in $pending.Platforms) { if ($entry.Platforms -notcontains $platform) { $entry.Platforms += $platform } }
        foreach ($row in $pending.Rows) { if ($entry.Rows -notcontains $row) { $entry.Rows += $row } }
        if (-not $entry.Project) { $entry.Project = $pending.Project }
        if (-not $entry.Message) { $entry.Message = $pending.Message }
    }
    foreach ($raw in $Log) {
        $parts = "$raw" -split "`t", 3
        if ($parts.Count -lt 3) { continue }
        $job = $parts[0]
        $text = $parts[2] -replace '^\d{4}-\d\d-\d\dT[\d:.]+Z ?', ''
        $platform = Get-CiPlatform $job
        $item = $null
        if ($text -match '^\s+Failed\s+(\S+)\s') {
            $key = $Matches[1]
            & $settle $job
            $display = ($text.Trim() -replace '^Failed\s+', '') -replace '\s+\[[^\]]*\]$', ''
            $current[$job] = [pscustomobject]@{ Key = $key; Kind = 'test'; Platforms = @($platform); Project = ''; Message = ''; Rows = @(); Display = $display }
            $awaitingMessage[$job] = $false
            continue
        } elseif ($text -match '(?:^|\s)(?:[^\s(]*[/\\])?([^\s/\\(]+)\(\d+,\d+\):\s+error\s+(\w+):\s*(.*?)(?:\s+\[(?:[^\]]*[/\\])?([^\]/\\]+)\.\w+proj\])?\s*$') {
            $item = [pscustomobject]@{ Key = "error $($Matches[2]) in $($Matches[1])"; Kind = 'build'; Platforms = @(); Project = "$($Matches[4])"; Message = $Matches[3] }
        } elseif ($text -match '^Audit guard: (\S+) changed on ') {
            # The audit guard (BL-998) found the factory's own change to an audit path or a guard.
            $item = [pscustomobject]@{ Key = "audit guard $($Matches[1])"; Kind = 'audit'; Platforms = @(); Project = ''; Message = $text.Trim() }
        }
        if ($item) {
            if (-not $found.Contains($item.Key)) { $found[$item.Key] = $item }
            $entry = $found[$item.Key]
            if ($entry.Platforms -notcontains $platform) { $entry.Platforms += $platform }
            continue
        }
        $entry = $current[$job]
        if (-not $entry) { continue }
        if ($text -match '^\s*START\s+[\w.`+]*\.(\w+)\s+\((.+)\)\s*$' -and $Matches[2] -ceq $entry.Display -and $entry.Key -notmatch '^test\d+:$') {
            $entry.Key = $Matches[1]
            $entry.Rows = @($entry.Display)
            continue
        }
        if ($text -match '^\s+Error Message:') { $awaitingMessage[$job] = $true; continue }
        if ($awaitingMessage[$job] -and $text.Trim()) {
            if (-not $entry.Message) { $entry.Message = $text.Trim() }
            $awaitingMessage[$job] = $false
            continue
        }
        # A stack-trace frame under a test project's folder, either suffix: *.UnitTests or *.IntegrationTests.
        if (-not $entry.Project -and $text -match '\sin\s.*?[/\\](Curl[\w.]*\.(?:UnitTests|IntegrationTests))[/\\]') { $entry.Project = $Matches[1] }
        if ($text -match '^Failed!\s.*-\s+([\w.]+)\.dll') {
            if (-not $entry.Project) { $entry.Project = $Matches[1] }
            & $settle $job
        }
    }
    foreach ($job in @($current.Keys)) { & $settle $job }
    # A run that failed with no test, compiler error or audit-guard line in its log (a crashed
    # test host, a timeout, a job step that broke) is still red: file it by the job and step
    # the log names, so the spell does not stay open unfiled (AF-0171). That holds for every
    # failed job, not only when the whole run names nothing: a Linux test failure beside a
    # failed Quality-gates job left the quality failure unfiled for 301 minutes (AF-0150).
    $unknownOnly = -not $found.Count
    $namedJobs = @{}
    foreach ($item in $found.Values) {
        foreach ($platform in $item.Platforms) { $namedJobs[$platform] = $true }
    }
    foreach ($raw in $Log) {
        $parts = "$raw" -split "`t", 3
        if ($parts.Count -lt 3 -or -not $parts[0].Trim()) { continue }
        # gh prints UNKNOWN STEP where it cannot tie a line to a job. Such lines file the run only
        # when nothing else in it is named, so a run that names no test is never dropped (AF-0178).
        if ($parts[0] -eq 'UNKNOWN STEP') {
            if (-not $unknownOnly) { continue }
            $parts = @('unknown job', $parts[1], $parts[2])
        }
        if ($namedJobs.ContainsKey((Get-CiPlatform $parts[0].Trim()))) { continue }
        $key = "job $($parts[0].Trim()) step $($parts[1].Trim())"
        $text = ($parts[2] -replace '^\d{4}-\d\d-\d\dT[\d:.]+Z ?', '').Trim()
        if (-not $found.Contains($key)) {
            $found[$key] = [pscustomobject]@{ Key = $key; Kind = 'job'; Platforms = @(Get-CiPlatform $parts[0].Trim()); Project = ''; Message = ''; GateMembers = @(); Files = @() }
        }
        # The quality gate annotates each member outside a gate ("X is outside a gate: ..."). Those
        # lines are the message, not the step's echoed script (AF-0244: BL-2466 read the script
        # line and never named CurlMulti.SetOption, so the real gap waited for a Normal task).
        # A step's ::error file=<path> annotation names the file at fault (BL-3028).
        if ($text -match '::error file=(.+?)(?:,|::)' -and @($found[$key].Files).Count -lt 5) { $found[$key].Files += $Matches[1].Trim() }
        if ($text -match '(?:^|\s)(?:##\[error\])?(\S.* is outside a gate: .*)$') {
            if (@($found[$key].GateMembers).Count -lt 5) { $found[$key].GateMembers += $Matches[1].Trim() }
            $found[$key].Message = (@($found[$key].GateMembers) -join '; ')
        } elseif (-not $found[$key].Message -and $text -match '(?i)error|fail|timed? ?out|cancel|exit code') { $found[$key].Message = $text }
    }
    return @($found.Values)
}

function Get-CiVerdicts {
    # Which failures to file, from -Runs: the finished CI runs, newest first, each with Id,
    # Sha, FinishedAt and Failures (Get-CiFailures; empty for a green run). A failure is a
    # regression when it fails in the newest run and either broke the build, failed on two
    # platforms, failed in the run before too, or has waited -ConfirmMinutes for another run;
    # it is flaky when it failed once and a run on each side of that one passed it, or failed
    # in the newest run after passing in the one before. Anything else waits or is already fixed.
    param([object[]]$Runs, [datetime]$Now = (Get-Date), [int]$ConfirmMinutes = $CiConfirmMinutes)
    $Runs = @($Runs)
    $verdicts = @()
    $keys = @($Runs | ForEach-Object { @($_.Failures) } | Where-Object { $_ } | ForEach-Object { $_.Key } | Select-Object -Unique)
    foreach ($key in $keys) {
        $failing = @(0..($Runs.Count - 1) | Where-Object { @($Runs[$_].Failures | Where-Object { $_.Key -ceq $key }).Count })
        $at = $failing[0]
        $failure = @($Runs[$at].Failures | Where-Object { $_.Key -ceq $key })[0]
        $verdict = ''
        if ($at -eq 0) {
            if ($failure.Kind -in 'build', 'audit' -or @($failure.Platforms).Count -ge 2 -or $failing -contains 1) { $verdict = 'regression' }
            elseif ($failing.Count -gt 1) { $verdict = 'flaky' }
            elseif (($Now - $Runs[0].FinishedAt).TotalMinutes -ge $ConfirmMinutes) { $verdict = 'regression' }
        } elseif ($failing.Count -eq 1 -and $at -lt $Runs.Count - 1) { $verdict = 'flaky' }
        if (-not $verdict) { continue }
        # A regression's first failing commit is the oldest run of its unbroken red streak.
        $first = 0
        $platforms = @($failure.Platforms)
        $rows = @($failure.Rows)
        if ($verdict -eq 'regression') {
            while ($failing -contains ($first + 1)) {
                $first++
                $earlier = @($Runs[$first].Failures | Where-Object { $_.Key -ceq $key })[0]
                $platforms += @($earlier.Platforms)
                $rows += @($earlier.Rows)
            }
        } else { $first = $at }
        $verdicts += [pscustomobject]@{
            Key = $key; Kind = $failure.Kind; Verdict = $verdict; RunId = $Runs[$at].Id; FirstSha = $Runs[$first].Sha
            Platforms = @($platforms | Select-Object -Unique | Sort-Object); Project = $failure.Project; Message = $failure.Message
            GateMembers = @($failure.GateMembers | Where-Object { $_ }); Files = @($failure.Files | Where-Object { $_ })
            Rows = @($rows | Where-Object { $_ } | Select-Object -Unique)
        }
    }
    return $verdicts
}

function Get-CiTaskTitle {
    # The task's title; each names the test or error exactly, which is how a later run finds it.
    param($Verdict)
    $on = ($Verdict.Platforms -join ' and ')
    if ($Verdict.Kind -eq 'audit') { return "Revert the dark factory's change to $(Get-CiAuditPath $Verdict.Key)" }
    if ($Verdict.Kind -eq 'build') { return "Fix CI build $($Verdict.Key) on $on" }
    if ($Verdict.Verdict -eq 'flaky') { return "Fix flaky CI test $($Verdict.Key) that failed once on $on" }
    return "Fix CI failure $($Verdict.Key) on $on"
}

function Get-CiAuditPath {
    # The path an audit-guard failure's key names: "audit guard Audit/x.md" -> Audit/x.md.
    param([string]$Key)
    return ($Key -replace '^audit guard ', '')
}

function Get-CiTaskNewArgs {
    # The task-board.ps1 new arguments that file -Verdict. An audit-guard failure is filed
    # -NoLane: a lane cannot touch the path (BL-997) and the board lets a factory process file
    # an audit-path task only that way (BL-996).
    param($Verdict, [string[]]$Touches)
    if ($Verdict.Kind -eq 'audit') {
        return @('new', '-Title', (Get-CiTaskTitle $Verdict), '-Priority', 'High', '-Pipeline', 'direct',
            '-Touches', (Get-CiAuditPath $Verdict.Key), '-NoLane')
    }
    # Never empty touches: a task with none is unready and no lane takes it (BL-3028).
    $Touches = @($Touches | Where-Object { $_ })
    if (-not $Touches.Count) { $Touches = @(Get-CiFallbackTouches $Verdict) }
    return @('new', '-Title', (Get-CiTaskTitle $Verdict), '-Priority', 'High', '-Pipeline', 'feature', '-Touches', ($Touches -join ','))
}

function Get-CiFallbackTouches {
    # The narrow touches of a CI task whose failure names no project or file: the script a
    # quality-gate step runs, Measure-CodeQuality.ps1, and otherwise Curl.slnx, which the build
    # and test steps build. Never .github/workflows/ci.yml: it is a guard file, so a lane could
    # not take the task.
    param($Verdict)
    if ("$($Verdict.Key)" -match '(?i)quality|coverage|complexity|crap') { return @('Measure-CodeQuality.ps1') }
    return @('Curl.slnx')
}

function Get-CiJobTouches {
    # A job failure's touches, as far as they exist in -Repo: the files its ::error file=
    # annotations name, and the source files that declare each member outside a gate
    # ("Type.Method is outside a gate" -> */Type.cs and its */Type.*.cs partials). Empty when
    # the log names neither; Get-CiTaskNewArgs then falls back to Get-CiFallbackTouches.
    param($Verdict, [string]$Repo)
    $touches = @()
    foreach ($file in @($Verdict.Files | Where-Object { $_ })) {
        $relative = ("$file" -replace '\\', '/') -replace '^(?:[A-Za-z]:)?/.*?/Curl/Curl/', ''
        if (Test-Path (Join-Path $Repo $relative) -PathType Leaf) { $touches += $relative }
    }
    foreach ($member in @($Verdict.GateMembers | Where-Object { $_ })) {
        $name = @(("$member" -replace ' is outside a gate:.*$', '').Trim() -split '\s+')[-1] -replace '\(.*$', ''
        $parts = @($name -split '\.' | Where-Object { $_ })
        if ($parts.Count -lt 2) { continue }
        $type = ($parts[-2] -split '\+')[-1] -replace '[<`].*$', ''
        $touches += @(git -C $Repo ls-files -- "*/$type.cs" "*/$type.*.cs" 2>$null | Select-Object -First 3)
    }
    return @($touches | Where-Object { $_ } | Select-Object -Unique)
}

function Get-CiTouches {
    # The test project and the library it tests, or for a build error the project that did not
    # build and its twin, as far as they exist in -Repo. Empty when the project is unknown.
    # Both test-project suffixes map to their library: X.UnitTests and X.IntegrationTests each
    # give X.UnitLibrary and X (the latter for Curl.Console).
    param([string]$Project, [string]$Repo)
    if (-not $Project) { return @() }
    $pair = @($Project)
    if ($Project -match '^(.+)\.(?:UnitTests|IntegrationTests)$') { $pair += @("$($Matches[1]).UnitLibrary", $Matches[1]) }
    elseif ($Project -match '^(.+)\.UnitLibrary$') { $pair += "$($Matches[1]).UnitTests" }
    else { $pair += "$Project.UnitTests" }
    return @($pair | Where-Object { Test-Path (Join-Path $Repo $_) -PathType Container })
}

function Set-CiTaskBody {
    # Fills the new task file's Goal, Context and Acceptance criteria.
    param([string]$Path, $Verdict, [string]$RunUrl)
    if ($Verdict.Kind -eq 'audit') { Set-CiAuditTaskBody -Path $Path -Verdict $Verdict -RunUrl $RunUrl; return }
    $on = $Verdict.Platforms -join ' and '
    $what = if ($Verdict.Kind -eq 'build') { "the build ($($Verdict.Key))" } else { "``$($Verdict.Key)``" }
    $goal = if ($Verdict.Verdict -eq 'flaky') {
        "$what passes on every run on Windows, Linux and macOS; it failed once on $on in the ``CI`` workflow and passed on the runs either side."
    } else { "$what passes on Windows, Linux and macOS, so the ``CI`` workflow on the shift's branch is green again." }
    $message = if ($Verdict.Message) { $Verdict.Message } else { '(no error message in the log)' }
    $first = if ($Verdict.FirstSha) { "First failing commit: $($Verdict.FirstSha.Substring(0, [math]::Min(8, $Verdict.FirstSha.Length)))." } else { 'First failing commit: not found.' }
    # A data-row test's failing rows by display name (BL-3998).
    $rows = @(@($Verdict.Rows | Where-Object { $_ }) | ForEach-Object { '`' + $_ + '`' })
    $context = @(
        "Filed by the dark factory's CI watch (BL-987). $what failed on $on in CI run $($Verdict.RunId) ($RunUrl). $first"
        ''
        "    $message"
        ''
        if ($rows.Count) { "Failing data rows: $($rows -join ', ')."; '' }
        "Lanes test only on Windows, so reproduce with ``gh run view $($Verdict.RunId) --log-failed`` and fix it platform-neutrally (CLAUDE.md, ""Tests pass on Windows, Linux and macOS"")."
    ) -join "`n"
    $criteria = if ($Verdict.Verdict -eq 'flaky') {
        "- [ ] What made $what fail intermittently is named under Notes and removed.`n- [ ] $what passes locally, and in the ``CI`` workflow on Windows, Linux and macOS for the commit that lands the fix."
    } else { "- [ ] $what passes locally, and the ``CI`` workflow passes on Windows, Linux and macOS for the commit that lands the fix." }
    $text = [IO.File]::ReadAllText($Path)
    $text = $text.Replace('<!-- One sentence: the observable outcome once this task is done. -->', $goal)
    $text = $text -replace '<!-- Why this matters.*?-->', $context.Replace('$', '$$')
    $text = $text.Replace('- [ ] <!-- A statement someone else can check from the repository without asking a question. -->', $criteria)
    [IO.File]::WriteAllText($Path, $text, (New-Object Text.UTF8Encoding $false))
}

function Set-CiAuditTaskBody {
    # Fills an audit-guard task's Goal, Context and Acceptance criteria. The Context names the
    # watch's key, "audit guard <path>", which is how a later run finds the task.
    param([string]$Path, $Verdict, [string]$RunUrl)
    $target = Get-CiAuditPath $Verdict.Key
    $first = if ($Verdict.FirstSha) { "First failing commit: $($Verdict.FirstSha.Substring(0, [math]::Min(8, $Verdict.FirstSha.Length)))." } else { 'First failing commit: not found.' }
    $goal = "The ``audit-guard`` job passes on work/dark-factory again: ``$target`` there matches master, or its change reaches master through the audit branch."
    $context = @(
        "Filed by the dark factory's CI watch (BL-999) for $($Verdict.Key): the ``audit-guard`` job failed in CI run $($Verdict.RunId) ($RunUrl). $first"
        ''
        "    $($Verdict.Message)"
        ''
        "Audit paths and their guards change only through the audit branch (ADR-0267). Interactive only: a dark factory lane cannot touch ``$target`` (BL-997). Red CI blocks the shift-end merge until this is fixed. Decide whether the change was wanted: if not, restore master's copy on work/dark-factory (``git checkout origin/master -- $target``); if it was, land it on master through the audit branch's pull request, then merge master into work/dark-factory."
    ) -join "`n"
    $criteria = "- [ ] The ``audit-guard`` job passes on work/dark-factory for the commit that lands the fix."
    $text = [IO.File]::ReadAllText($Path)
    $text = $text.Replace('<!-- One sentence: the observable outcome once this task is done. -->', $goal)
    $text = $text -replace '<!-- Why this matters.*?-->', $context.Replace('$', '$$')
    $text = $text.Replace('- [ ] <!-- A statement someone else can check from the repository without asking a question. -->', $criteria)
    [IO.File]::WriteAllText($Path, $text, (New-Object Text.UTF8Encoding $false))
}

function Test-CiFailureCovered {
    # Whether a task already covers -Key failing in the run on -Sha: a live task (Backlog, Doing,
    # Blocked) names it, or a finished one does whose last commit that run did not yet contain,
    # so the failure predates its fix. Read in the -Repo worktree.
    param([string]$Key, [string]$Sha, [string]$Repo)
    $tasks = Join-Path $Repo 'Tasks'
    foreach ($file in @(Get-ChildItem $tasks -Recurse -Filter 'BL-*.md' -ErrorAction SilentlyContinue)) {
        # The whole name only: a task for Parse_Empty does not cover Parse_Empty_Throws.
        if (-not (Select-String -LiteralPath $file.FullName -Pattern "(?<![\w.])$([regex]::Escape($Key))(?![\w])" -CaseSensitive -Quiet)) { continue }
        $state = (Split-Path (Split-Path $file.FullName -Parent) -Leaf)
        if ($state -in 'Backlog', 'Doing', 'Blocked') { return $true }
        $relative = $file.FullName.Substring($Repo.TrimEnd('\', '/').Length + 1)
        $last = "$(git -C $Repo log -1 --format=%H -- $relative 2>$null)".Trim()
        if (-not $last) { continue }
        git -C $Repo merge-base --is-ancestor $last $Sha 2>$null | Out-Null
        if ($LASTEXITCODE -ne 0) { return $true }
    }
    return $false
}

function Send-CiWhisper {
    # Has the whisper hook announce a filed task, as it would for a Claude session's
    # task-board.ps1 new, without waiting for the speech.
    param([string]$Line)
    $hook = Join-Path $Root '.claude\hooks\whisper-milestone.ps1'
    if (-not (Test-Path $hook)) { return }
    try {
        $stdinFile = Join-Path $LogDir "ci-whisper-$Stamp-$([guid]::NewGuid().ToString('N').Substring(0, 8)).json"
        $json = [pscustomobject]@{ cwd = $Root; tool_input = @{ command = 'task-board.ps1 new' }; tool_response = @{ stdout = $Line } } | ConvertTo-Json -Compress
        [IO.File]::WriteAllText($stdinFile, $json, (New-Object Text.UTF8Encoding $false))
        Start-Process -FilePath (Get-FactoryPowerShellExe) -ArgumentList @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$hook`"") `
            -RedirectStandardInput $stdinFile -WindowStyle Hidden | Out-Null
    } catch { }
}

function Test-CiRunWithoutRunner {
    # True when `gh run view <id> --json jobs` shows at least one job that did not succeed and
    # every such job was cancelled with no step run (GitHub never gave it a runner, BL-3944).
    param($JobsJson)
    if (-not $JobsJson) { return $false }
    try { $jobs = @(($JobsJson | ConvertFrom-Json).jobs) } catch { return $false }
    $bad = @($jobs | Where-Object { $_.conclusion -notin 'success', 'skipped', 'neutral' })
    if (-not $bad.Count) { return $false }
    foreach ($job in $bad) {
        if ($job.conclusion -ne 'cancelled') { return $false }
        if (@($job.steps | Where-Object { $_.conclusion -and $_.conclusion -ne 'skipped' }).Count) { return $false }
    }
    return $true
}

function Invoke-GhRunList {
    # Runs `gh run list -Arguments` and returns its JSON, or, when gh exits non-zero, an object
    # with its ExitCode and Error (the first line it wrote to stderr), so a trace can say why
    # gh could not list the runs (BL-4377) rather than discard its stderr.
    param([string[]]$Arguments)
    # Under Stop, Windows PowerShell would throw on gh's first stderr line rather than keep it.
    $ErrorActionPreference = 'Continue'
    $global:LASTEXITCODE = 0
    $lines = @(gh run list @Arguments 2>&1)
    $exit = $LASTEXITCODE
    $errors = @($lines | Where-Object { $_ -is [Management.Automation.ErrorRecord] } | ForEach-Object { "$_".Trim() } | Where-Object { $_ })
    if ($exit -ne 0) { return [pscustomobject]@{ ExitCode = $exit; Error = $(if ($errors.Count) { $errors[0] } else { 'no error text' }) } }
    return (@($lines | Where-Object { $_ -isnot [Management.Automation.ErrorRecord] } | ForEach-Object { "$_" }) -join "`n")
}

function Get-GhListFailure {
    # '' when -Listed is gh's JSON (Invoke-GhRunList or a stand-in succeeded); otherwise why
    # gh could not list the runs: "exit <code>: <first stderr line>".
    param($Listed)
    if ($Listed -is [string]) { return '' }
    if ($null -eq $Listed) { return 'gh gave no output and no exit code' }
    return "exit $($Listed.ExitCode): $(Get-Short "$($Listed.Error)" 160)"
}

function Write-GhListLine {
    # Traces gh failing to list the CI runs (BL-4377): the first failure with -Failure (gh's
    # exit code and first stderr line), again at most every -EveryMinutes while it keeps
    # failing with how long it has, and one "listing CI runs again" line on the first list
    # that works after it. -State keeps FailingSince and TracedAt between calls; -Failure is
    # '' when gh listed the runs.
    param([hashtable]$State, [string]$What, [string]$Failure, [datetime]$Now, [string]$Kind = 'ci', [int]$EveryMinutes = 30)
    if (-not $Failure) {
        if ($State.FailingSince) {
            Write-Trace '-' $Kind "listing CI runs again $What, after $([int]($Now - $State.FailingSince).TotalMinutes) min unable to" 'DarkGray'
            $State.FailingSince = $null
        }
        return
    }
    if (-not $State.FailingSince) {
        $State.FailingSince = $Now
        $State.TracedAt = $Now
        Write-Trace '-' $Kind "cannot list the CI runs $What with gh ($Failure)" 'DarkYellow'
        return
    }
    if (($Now - $State.TracedAt).TotalMinutes -lt $EveryMinutes) { return }
    $State.TracedAt = $Now
    Write-Trace '-' $Kind "still cannot list the CI runs $What with gh, for $([int]($Now - $State.FailingSince).TotalMinutes) min ($Failure)" 'DarkYellow'
}

function Get-CiRuns {
    # The newest finished CI runs on -Branch that passed or failed (cancelled ones prove
    # nothing), newest first, each with its failures; a failed run whose log names no
    # failure, or cannot be read, is left out and traced (Write-CiRunLine). $null when gh
    # cannot list them, traced with gh's exit code and error (Write-GhListLine, -Now its clock).
    # -ListRuns and -ReadLog stand in for gh in -TestCiWatch: the first returns `gh run list`'s
    # JSON or Invoke-GhRunList's failure, the second a run's `--log-failed` lines, $null if unreadable.
    param([string]$Branch, [scriptblock]$ListRuns, [scriptblock]$ReadLog, [scriptblock]$ReadJobs, [datetime]$Now = (Get-Date))
    # No --jq: Windows PowerShell strips the double quotes a jq filter needs from a native
    # command's arguments. Its ConvertFrom-Json turns "[]" into one empty element, hence the
    # databaseId filter.
    if (-not $ListRuns) {
        $ListRuns = {
            param([string]$Branch)
            Invoke-GhRunList -Arguments @('--workflow', 'CI', '--branch', $Branch, '--limit', '30', '--json', 'databaseId,status,conclusion,headSha,updatedAt')
        }
    }
    if (-not $ReadLog) {
        $ReadLog = {
            param([string]$Id)
            $log = @(gh run view $Id --log-failed 2>$null | ForEach-Object { "$_" })
            if ($LASTEXITCODE -ne 0) { return $null }
            return , $log
        }
    }
    if (-not $ReadJobs) {
        $ReadJobs = {
            param([string]$Id)
            $json = (gh run view $Id --json jobs 2>$null) -join "`n"
            if ($LASTEXITCODE -ne 0) { return $null }
            return $json
        }
    }
    $json = & $ListRuns $Branch
    if (-not $script:CiWatch.List) { $script:CiWatch.List = @{} }
    $failure = Get-GhListFailure $json
    Write-GhListLine -State $script:CiWatch.List -What "on $Branch" -Failure $failure -Now $Now
    if ($failure) { return $null }
    try { $listed = @($json | ConvertFrom-Json | ForEach-Object { $_ } | Where-Object { $_.databaseId }) }
    catch { Write-CiRunLine -Key 'list' -Line "cannot read gh's list of CI runs on $Branch"; return $null }
    # Kept for Get-CiRunningRun and Stop-CiRunWithoutFix, which read the runs not finished yet.
    $script:CiWatch.Listed = $listed
    $runs = @()
    foreach ($listedRun in $listed) {
        if ($runs.Count -ge $CiWindowRuns) { break }
        if ($listedRun.status -ne 'completed' -or $listedRun.conclusion -notin 'success', 'failure') { continue }
        $id = "$($listedRun.databaseId)"; $conclusion = $listedRun.conclusion; $sha = "$($listedRun.headSha)"
        $on = $sha.Substring(0, [math]::Min(8, $sha.Length))
        $finished = if ($listedRun.updatedAt -is [datetime]) { $listedRun.updatedAt.ToLocalTime() }
            else { [datetime]::Parse("$($listedRun.updatedAt)", [Globalization.CultureInfo]::InvariantCulture, [Globalization.DateTimeStyles]::AdjustToUniversal).ToLocalTime() }
        if ($conclusion -eq 'failure' -and -not $script:CiWatch.Failures.ContainsKey($id)) {
            $log = & $ReadLog $id
            if ($null -eq $log) { Write-CiRunLine -RunId $id -Line "failure on ${on}: cannot read its log with gh run view --log-failed; trying again next heartbeat"; continue }
            $named = @(Get-CiFailures $log)
            # A failed run whose --log-failed is empty is still red: file it by the run (AF-0178).
            if (-not $named.Count -and (Test-CiRunWithoutRunner (& $ReadJobs $id))) {
                $script:CiWatch.Capacity[$id] = $true
                $named = @()
            }
            elseif (-not $named.Count) {
                $named = @([pscustomobject]@{ Key = 'job unknown job step unknown step'; Kind = 'job'; Platforms = @('unknown job'); Project = ''; Message = 'the run failed and gh run view --log-failed printed no line' })
            }
            $script:CiWatch.Failures[$id] = $named
        }
        # Assigned apart from the if: an if statement's output is unrolled, so one failure came
        # back as a bare object, and Windows PowerShell gives a bare object no Count - a run
        # with a single failing test looked like one with none and was dropped (BL-1031).
        $failures = @()
        if ($conclusion -eq 'failure') { $failures = @($script:CiWatch.Failures[$id]) }
        if ($conclusion -eq 'failure' -and -not $failures.Count -and $script:CiWatch.Capacity[$id]) {
            Write-CiRunLine -RunId $id -Line "failure on ${on}: runner capacity - every job that did not succeed was never given a runner; nothing to file"
            continue
        }
        if ($conclusion -eq 'failure' -and -not $failures.Count) {
            Write-CiRunLine -RunId $id -Line "failure on ${on}: its log names no failing test or build error; nothing to file"
            continue
        }
        $runs += [pscustomobject]@{ Id = $id; Sha = $sha; FinishedAt = $finished; Failures = $failures }
    }
    return , $runs
}

function ConvertTo-CiDateTime {
    # gh's timestamp as local time, whether ConvertFrom-Json made it a [datetime] (PowerShell 7)
    # or left it a string (Windows PowerShell).
    param($Value)
    if ($Value -is [datetime]) { return $Value.ToLocalTime() }
    return [datetime]::Parse("$Value", [Globalization.CultureInfo]::InvariantCulture, [Globalization.DateTimeStyles]::AdjustToUniversal).ToLocalTime()
}

function Get-CiRunningRun {
    # The CI run on the branch that is still running, as a run Get-CiVerdicts reads (Id, Sha,
    # FinishedAt, Failures), once a job of it has failed with a failing test or build error in
    # its log; $null otherwise. A Build and test job fails minutes before the Quality gates job
    # lets the run finish (run 40034290028: test1173 failed at 22:48, the run ended at 22:57), so
    # the watch files the failure that much sooner and its fix can land before the red run ends
    # (BL-3454). Only tests and build errors count: a job's own key names a step, which the
    # finished run's --log-failed names differently. FinishedAt is the last failed job's end.
    # -Listed is gh's run list, newest first (Get-CiRuns keeps it). -ListJobs (a run id ->
    # `gh run view --json jobs` JSON) and -ReadJobLog (a job id -> its log lines, $null when
    # unreadable) stand in for gh in -TestCiWatch.
    param([object[]]$Listed, [scriptblock]$ListJobs, [scriptblock]$ReadJobLog)
    $running = @($Listed | Where-Object { $_ -and $_.status -eq 'in_progress' })[0]
    if (-not $running) { return $null }
    if (-not $ListJobs) {
        $ListJobs = { param([string]$Id) $json = (gh run view $Id --json jobs 2>$null) -join "`n"; if ($LASTEXITCODE -ne 0) { return $null }; return $json }
    }
    if (-not $ReadJobLog) {
        # gh run view --log-failed waits for the whole run; a finished job's log is readable at once.
        $ReadJobLog = {
            param([string]$Id)
            $log = @(gh api "repos/{owner}/{repo}/actions/jobs/$Id/logs" 2>$null | ForEach-Object { "$_" })
            if ($LASTEXITCODE -ne 0) { return $null }
            return , $log
        }
    }
    $id = "$($running.databaseId)"
    $json = & $ListJobs $id
    $key = "running $id"
    if (-not $json) { Write-CiRunLine -Key $key -Line "cannot read the jobs of running run $id with gh run view"; return $null }
    try { $failed = @(($json | ConvertFrom-Json).jobs | Where-Object { $_ -and $_.status -eq 'completed' -and $_.conclusion -eq 'failure' }) }
    catch { Write-CiRunLine -Key $key -Line "cannot parse the jobs of running run $id`: $(Get-Short "$($_.Exception.Message)" 60)"; return $null }
    if (-not $failed.Count) { return $null }
    $skipCache = $false; $failures = @()
    $cacheKey = "running $id $(@($failed | ForEach-Object { $_.databaseId } | Sort-Object) -join ',')"
    if (-not $script:CiWatch.Failures.ContainsKey($cacheKey)) {
        $lines = @()
        $unreadIds = @()
        foreach ($job in $failed) {
            $log = & $ReadJobLog "$($job.databaseId)"
            if ($null -eq $log) { $unreadIds += "$($job.databaseId)"; continue }
            $lines += @($log | ForEach-Object { "$($job.name)`tlog`t$(("$_").TrimStart([char]0xFEFF))" })
        }
        $unread = $unreadIds.Count -gt 0
        # One line for all unread jobs, so the same state is traced once, not once per job per heartbeat.
        if ($unread) { Write-CiRunLine -Key $key -Line "cannot read the log of failed job $($unreadIds -join ', ') of running run $id; skipped" }
        $read = @(Get-CiFailures $lines | Where-Object { $_.Kind -in 'test', 'build' })
        # A log that could not be read is tried again on the next heartbeat, so nothing is cached.
        if ($unread) { $failures = $read; $skipCache = $true } else { $script:CiWatch.Failures[$cacheKey] = $read }
    }
    if (-not $skipCache) { $failures = @($script:CiWatch.Failures[$cacheKey]) }
    if (-not $failures.Count -and $skipCache) { return $null }
    if (-not $failures.Count) { Write-CiRunLine -Key $key -Line "running run $id has failed jobs but no failing test or build error in their logs"; return $null }
    $finished = @($failed | ForEach-Object { ConvertTo-CiDateTime $_.completedAt } | Sort-Object)[-1]
    return [pscustomobject]@{ Id = $id; Sha = "$($running.headSha)"; FinishedAt = $finished; Failures = $failures }
}

function Get-CiFixTasksBetween {
    # The CI-fix tasks ("Fix CI failure ...", "Fix CI build ...", "Fix flaky CI test ...") that
    # reached Tasks/Done between commit -From and commit -To in -Repo, by file name; a task
    # already under Tasks/Done at -From and only archived since is not one. An empty array when the
    # commits compare and carry none; $null when git cannot compare them.
    param([string]$Repo, [string]$From, [string]$To)
    $added = @(git -C $Repo diff --no-renames --name-only --diff-filter=A $From $To -- Tasks/Done 2>$null | ForEach-Object { "$_" })
    if ($LASTEXITCODE -ne 0) { return $null }
    $before = @{}
    $tree = @(git -C $Repo ls-tree -r --name-only $From -- Tasks/Done 2>$null)
    if ($LASTEXITCODE -ne 0) { return $null }
    foreach ($path in $tree) { $before[(Split-Path "$path" -Leaf)] = $true }
    # The leading comma keeps an empty result an empty array: $null means git failed.
    return , @($added | ForEach-Object { Split-Path $_ -Leaf } |
        Where-Object { $_ -match '^BL-\d+-fix-(?:ci-failure|ci-build|flaky-ci-test)-' -and -not $before.ContainsKey($_) } |
        ForEach-Object { $_ -replace '^(BL-\d+).*$', '$1' })
}

function Stop-CiRunWithoutFix {
    # Cancels the CI run running on the branch when the run waiting behind it has a CI fix the
    # running commit lacks, so the fix is tested at once. On work/dark-factory a running run
    # always finishes (ADR-0693), so a fix that landed after a red run waited behind a run
    # doomed to fail the same way, and green came a whole run later: 37 to 77 minutes red where
    # the fix took 10 (AF-0388, AF-0407 to AF-0410; ADR-0811). Only while the newest finished run
    # of -Runs is red, so the red spell is already on record and the cancelled run hides none.
    # -Listed is gh's run list, newest first; -Repo a checkout that holds both commits;
    # -CancelRun (a run id -> whether gh cancelled it) stands in for gh in -TestCiWatch.
    # Returns the cancelled run's id, or ''.
    param([object[]]$Listed, [object[]]$Runs, [string]$Repo, [scriptblock]$CancelRun)
    $newest = @($Runs)[0]
    if (-not $newest -or -not @($newest.Failures | Where-Object { $_ }).Count) { return '' }
    $running = @($Listed | Where-Object { $_ -and $_.status -eq 'in_progress' })[0]
    $waiting = @($Listed | Where-Object { $_ -and $_.status -in 'queued', 'pending', 'waiting', 'requested' })[0]
    if (-not $running -or -not $waiting) { return '' }
    $id = "$($running.databaseId)"
    $decided = "cancel $id $($waiting.databaseId)"
    if ($script:CiWatch.Logged.ContainsKey($decided)) { return '' }
    $fixes = Get-CiFixTasksBetween -Repo $Repo -From "$($running.headSha)" -To "$($waiting.headSha)"
    if ($null -eq $fixes) {
        Write-CiRunLine -Key "cancel $id" -Line "cannot compare commits $($running.headSha) and $($waiting.headSha) with git: run $id is not cancelled"
        return ''
    }
    $script:CiWatch.Logged[$decided] = 'looked'
    if (-not @($fixes).Count) { return '' }
    if (-not $CancelRun) { $CancelRun = { param([string]$Id) gh run cancel $Id 2>&1 | Out-Null; return ($LASTEXITCODE -eq 0) } }
    $on = "$($running.headSha)".Substring(0, [math]::Min(8, "$($running.headSha)".Length))
    if (-not (& $CancelRun $id)) { Write-Trace '-' 'ci' "cannot cancel run $id on $on with gh run cancel" 'DarkYellow'; return '' }
    Write-Trace '-' 'ci' "cancelled run $id on ${on}: it lacks the CI fix $(@($fixes) -join ', ') that waiting run $($waiting.databaseId) carries" 'Yellow'
    return $id
}

function Write-CiRunLine {
    # Traces what the watch made of one run: the first time it examines it, and again whenever
    # that changes (waiting, then filed), not on every heartbeat that finds the same.
    # -Key, without -RunId, names what is traced when it is not a run, such as gh's run list.
    param([string]$RunId, [string]$Line, [string]$Key = $RunId)
    if ($script:CiWatch.Logged[$Key] -ceq $Line) { return }
    $script:CiWatch.Logged[$Key] = $Line
    $text = if ($RunId) { "run $RunId $Line" } else { $Line }
    Write-Trace '-' 'ci' $text 'DarkGray'
}

function Get-CiRunOutcome {
    # One line on what the watch did with -Run, one of -Runs (newest first): nothing for a
    # green run; for a red one, each failure and whether it was filed (-Settled holds
    # "<key>|<run id>" -> "filed BL-###" or "covered by a task"), is filed with a newer run,
    # waits for a run to confirm it, or has passed since.
    param($Run, [object[]]$Runs, [object[]]$Verdicts, [hashtable]$Settled, [int]$ConfirmMinutes = $CiConfirmMinutes)
    $on = "$($Run.Sha)".Substring(0, [math]::Min(8, "$($Run.Sha)".Length))
    $failures = @($Run.Failures | Where-Object { $_ })
    if (-not $failures.Count) { return "success on ${on}: nothing to file" }
    $said = foreach ($failure in $failures) {
        $verdict = @($Verdicts | Where-Object { $_ -and $_.Key -ceq $failure.Key })[0]
        $what = if ($verdict -and $verdict.RunId -ne $Run.Id) { "filed with run $($verdict.RunId)" }
            elseif ($verdict -and $Settled.ContainsKey("$($failure.Key)|$($Run.Id)")) { "$($verdict.Verdict), $($Settled["$($failure.Key)|$($Run.Id)"])" }
            elseif ($verdict) { "$($verdict.Verdict), not filed yet" }
            elseif (@($Runs)[0].Id -eq $Run.Id) { "on $($failure.Platforms -join '+') only, waiting for the next run or $($Run.FinishedAt.AddMinutes($ConfirmMinutes).ToString('HH:mm')) to confirm it" }
            else { 'passed since, not filed' }
        "$($failure.Key) $what"
    }
    return "failure on ${on}: $(@($said) -join '; ')"
}

function Invoke-CiWatch {
    # Once a heartbeat (-HeartbeatMinutes, 3 when publishing is off): reads the finished CI
    # runs on -Branch and files one High task per new failing test or build error, in the
    # ci-watch worktree at origin/-Branch, pushed straight to -Branch, and traces what it made
    # of each run (Write-CiRunLine). Never stops the shift.
    # -Final: the shift's last look, after the merge has waited for CI on its last commit. It
    # runs whatever the heartbeat, and files a failure seen on one platform in only the newest
    # run at once rather than waiting for a run this shift will not be there to read (BL-1031).
    param([string]$Branch, [switch]$Final)
    if ($script:CiWatch.Off) { return }
    $every = if ($HeartbeatMinutes -gt 0) { $HeartbeatMinutes } else { 3 }
    if (-not $Final -and ((Get-Date) - $script:CiWatch.CheckedAt).TotalMinutes -lt $every) { return }
    $script:CiWatch.CheckedAt = Get-Date
    if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
        $script:CiWatch.Off = $true
        Write-Trace '-' 'ci' 'CI watch off: gh is not installed' 'DarkYellow'
        return
    }
    $runs = Get-CiRuns -Branch $Branch
    if ($null -eq $runs) { return }
    $listed = @($script:CiWatch.Listed)
    $cancelled = ''
    if (@($listed | Where-Object { $_ -and $_.status -in 'queued', 'pending', 'waiting', 'requested' }).Count) {
        git -C $Root fetch -q origin $Branch 2>&1 | Out-Null
        if ($LASTEXITCODE -ne 0) { Write-CiRunLine -Key 'fetch' -Line "git fetch origin $Branch failed (exit $LASTEXITCODE): a run is waiting but cannot be compared with the running one" }
        else { $script:CiWatch.Logged.Remove('fetch') }
        $cancelled = Stop-CiRunWithoutFix -Listed $listed -Runs $runs -Repo $Root
    }
    # A failure in a job of the run still running is read now, not when the run ends (BL-3454).
    $running = if (-not $cancelled) { Get-CiRunningRun -Listed $listed }
    if ($running) { $runs = @($running) + @($runs) }
    $confirm = if ($Final) { 0 } else { $CiConfirmMinutes }
    $all = @(Get-CiVerdicts -Runs $runs -ConfirmMinutes $confirm)
    $verdicts = @($all | Where-Object { -not $script:CiWatch.Settled.ContainsKey("$($_.Key)|$($_.RunId)") })
    if ($verdicts.Count) { New-CiFailureTasks -Branch $Branch -Runs $runs -Verdicts $verdicts }
    Move-FixedCiTasksToDeferred -Branch $Branch -Runs $runs
    foreach ($run in $runs) {
        Write-CiRunLine -RunId $run.Id -Line (Get-CiRunOutcome -Run $run -Runs $runs -Verdicts $all -Settled $script:CiWatch.Settled -ConfirmMinutes $confirm)
    }
}

function New-CiFailureTasks {
    # Files one High task per -Verdicts entry not already covered by a task, in the ci-watch
    # worktree at origin/-Branch, and pushes them straight to -Branch; up to three tries when
    # the push races a lane. Records each in $script:CiWatch.Settled.
    param([string]$Branch, [object[]]$Runs, [object[]]$Verdicts)
    $saved = $env:CLAUDE_PROJECT_DIR
    try {
        foreach ($attempt in 1..3) {
            if (-not (Reset-CiWatchWorktree -Branch $Branch)) { return }
            $env:CLAUDE_PROJECT_DIR = $CiWatchDir
            $board = Join-Path $CiWatchDir '.claude\skills\task-board\task-board.ps1'
            $filed = @()
            foreach ($verdict in $verdicts) {
                $sha = @($runs | Where-Object { $_.Id -eq $verdict.RunId })[0].Sha
                if (Test-CiFailureCovered -Key $verdict.Key -Sha $sha -Repo $CiWatchDir) { $script:CiWatch.Settled["$($verdict.Key)|$($verdict.RunId)"] = 'covered by a task'; continue }
                $touches = if ($verdict.Kind -eq 'job') { @(Get-CiJobTouches -Verdict $verdict -Repo $CiWatchDir) } else { @(Get-CiTouches -Project $verdict.Project -Repo $CiWatchDir) }
                $newArgs = Get-CiTaskNewArgs -Verdict $verdict -Touches $touches
                $out = (& (Get-FactoryPowerShellExe) -NoProfile -ExecutionPolicy Bypass -File $board @newArgs 2>&1 | ForEach-Object { "$_" }) -join "`n"
                if ($out -notmatch '(?m)^(BL-\d+)\s+(Tasks\S+\.md)') { Write-Trace '-' 'ci' "cannot file $($verdict.Key): $(Get-Short $out 60)" 'DarkYellow'; continue }
                $id = $Matches[1]; $line = $Matches[0]
                $url = "https://github.com/$(gh repo view --json nameWithOwner --jq .nameWithOwner 2>$null)/actions/runs/$($verdict.RunId)"
                Set-CiTaskBody -Path (Join-Path $CiWatchDir $Matches[2]) -Verdict $verdict -RunUrl $url
                $filed += [pscustomobject]@{ Id = $id; Line = $line; Verdict = $verdict }
                if ($verdict.Kind -eq 'audit') { $script:CiWatch.AuditTasks["$id"] = Get-CiTaskTitle $verdict }
            }
            if (-not $filed.Count) { return }
            git -C $CiWatchDir add -A -- Tasks 2>&1 | Out-Null
            git -C $CiWatchDir commit -q -m "chore(tasks): file $(($filed | ForEach-Object { $_.Id }) -join ', ') for CI failures" -m "Filed by the dark factory's CI watch (BL-987)." -m $SkipCiNote 2>&1 | Out-Null
            git -C $CiWatchDir push -q origin "HEAD:$Branch" 2>&1 | Out-Null
            if ($LASTEXITCODE -ne 0) { continue }
            foreach ($task in $filed) {
                $script:CiWatch.Settled["$($task.Verdict.Key)|$($task.Verdict.RunId)"] = "filed $($task.Id)"
                Write-Trace $task.Id 'ci' "filed: $($task.Verdict.Verdict) $($task.Verdict.Key) on $($task.Verdict.Platforms -join '+'), run $($task.Verdict.RunId)" 'Yellow'
                Send-CiWhisper $task.Line
            }
            return
        }
        Write-Trace '-' 'ci' "could not push the CI failure tasks to $Branch; trying again next heartbeat" 'DarkYellow'
    } catch {
        Write-Trace '-' 'ci' "CI watch failed: $(Get-Short $_.Exception.Message 80)" 'DarkYellow'
    } finally { $env:CLAUDE_PROJECT_DIR = $saved }
}

function Reset-CiWatchWorktree {
    # Fetches -Branch and checks the ci-watch worktree out at origin/-Branch, adding it the
    # first time, with Tasks cleaned; $false, traced, when that fails.
    param([string]$Branch)
    git -C $Root fetch -q origin $Branch 2>&1 | Out-Null
    if (-not (Test-Path (Join-Path $CiWatchDir '.git'))) {
        New-Item -ItemType Directory -Force -Path $LanesDir | Out-Null
        git -C $Root worktree add -q --detach $CiWatchDir "origin/$Branch" 2>&1 | Out-Null
    }
    git -C $CiWatchDir checkout -q -f --detach "origin/$Branch" 2>&1 | Out-Null
    git -C $CiWatchDir clean -q -fd -- Tasks 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) { Write-Trace '-' 'ci' "cannot check out origin/$Branch in $CiWatchDir" 'DarkYellow'; return $false }
    return $true
}

function Get-FixedCiTaskIds {
    # The Backlog CI-fix tasks ("Fix CI failure ...", "Fix CI build ...", "Fix flaky CI test
    # ...") in -Repo that the newest of -Runs proves fixed: that run is green, so green for
    # the job or test each names, and its commit contains the task's filing. A red newest run
    # proves nothing, and a task in Doing is left to its lane (BL-3028).
    param([string]$Repo, [object[]]$Runs)
    $newest = @($Runs)[0]
    if (-not $newest -or @($newest.Failures | Where-Object { $_ }).Count) { return @() }
    $ids = @()
    foreach ($file in @(Get-ChildItem (Join-Path $Repo 'Tasks\Backlog') -Filter 'BL-*.md' -ErrorAction SilentlyContinue)) {
        $title = @(Get-Content -LiteralPath $file.FullName -TotalCount 6 | Where-Object { $_ -match '^title:\s*' })[0]
        if ($title -notmatch '^title:\s*Fix (?:CI failure|CI build|flaky CI test) ') { continue }
        $filedIn = "$(git -C $Repo log --diff-filter=A --format=%H -1 -- "Tasks/Backlog/$($file.Name)" 2>$null)".Trim()
        if (-not $filedIn) { continue }
        git -C $Repo merge-base --is-ancestor $filedIn $newest.Sha 2>$null | Out-Null
        if ($LASTEXITCODE -eq 0) { $ids += ($file.Name -replace '^(BL-\d+).*$', '$1') }
    }
    return $ids
}

function Move-FixedCiTasksToDeferred {
    # Once per green newest run: defers each task Get-FixedCiTaskIds finds with "already
    # fixed, CI green on <sha>" through task-board.ps1 move, in the ci-watch worktree at
    # origin/-Branch, pushed straight to -Branch like a filing; up to three tries when the
    # push races a lane. Deferred, not Done: the board moves only a task in Doing to Done,
    # and no lane did its work (ADR-0756).
    param([string]$Branch, [object[]]$Runs)
    $newest = @($Runs)[0]
    if (-not $newest -or @($newest.Failures | Where-Object { $_ }).Count -or $script:CiWatch.Logged.ContainsKey("deferred $($newest.Id)")) { return }
    $script:CiWatch.Logged["deferred $($newest.Id)"] = 'looked'
    $on = "$($newest.Sha)".Substring(0, [math]::Min(9, "$($newest.Sha)".Length))
    $saved = $env:CLAUDE_PROJECT_DIR
    try {
        foreach ($attempt in 1..3) {
            if (-not (Reset-CiWatchWorktree -Branch $Branch)) { return }
            $ids = @(Get-FixedCiTaskIds -Repo $CiWatchDir -Runs $Runs)
            if (-not $ids.Count) { return }
            $env:CLAUDE_PROJECT_DIR = $CiWatchDir
            $board = Join-Path $CiWatchDir '.claude\skills\task-board\task-board.ps1'
            foreach ($id in $ids) { & (Get-FactoryPowerShellExe) -NoProfile -ExecutionPolicy Bypass -File $board move -Id $id -To Deferred -Reason "already fixed, CI green on $on" 2>&1 | Out-Null }
            git -C $CiWatchDir add -A -- Tasks 2>&1 | Out-Null
            git -C $CiWatchDir commit -q -m "chore(tasks): defer $($ids -join ', '), already fixed, CI green on $on" -m "Moved by the dark factory's CI watch (BL-3028)." -m $SkipCiNote 2>&1 | Out-Null
            if ($LASTEXITCODE -ne 0) { return }
            git -C $CiWatchDir push -q origin "HEAD:$Branch" 2>&1 | Out-Null
            if ($LASTEXITCODE -ne 0) { continue }
            foreach ($id in $ids) { Write-Trace $id 'ci' "deferred: already fixed, CI green on $on, run $($newest.Id)" 'Yellow' }
            return
        }
        Write-Trace '-' 'ci' "could not push the deferral of fixed CI tasks to $Branch" 'DarkYellow'
    } catch {
        Write-Trace '-' 'ci' "CI watch failed: $(Get-Short $_.Exception.Message 80)" 'DarkYellow'
    } finally { $env:CLAUDE_PROJECT_DIR = $saved }
}

function Get-LiveTaskTitles {
    # The titles of the live tasks (Backlog, Doing, Blocked) in the -Repo worktree.
    param([string]$Repo)
    $titles = @()
    foreach ($state in 'Backlog', 'Doing', 'Blocked') {
        foreach ($file in @(Get-ChildItem (Join-Path $Repo "Tasks\$state") -Filter 'BL-*.md' -ErrorAction SilentlyContinue)) {
            $line = @(Get-Content -LiteralPath $file.FullName -TotalCount 6 | Where-Object { $_ -match '^title:\s*' })[0]
            if ($line) { $titles += ($line -replace '^title:\s*', '').Trim() }
        }
    }
    return $titles
}

function Set-SplitTaskBody {
    # Fills a split task's Goal, Context and Acceptance criteria from -Hotspot (Path, Count, Ids).
    param([string]$Path, $Hotspot, [int]$Waiting)
    $ids = $Hotspot.Ids -join ', '
    $goal = "``$($Hotspot.Path)`` no longer serialises the tasks that touch it: each unit it holds lives in its own file, so those tasks can run in parallel lanes."
    $context = @(
        "Filed by the dark factory's coordinator (ADR-0130, 2026-10-21 amendment): the board's capacity was the binding limit on -Lanes Auto two steps running while $Waiting ready task(s) could not start because of overlapping touches."
        ''
        "``task-board.ps1 hotspots`` counts $($Hotspot.Count) unfinished lane-eligible tasks touching ``$($Hotspot.Path)``: $ids. Tasks that touch the same file never run side by side, so they wait for one another."
    ) -join "`n"
    $criteria = @(
        "- [ ] The per-case or per-stand-in content of ``$($Hotspot.Path)`` moves into one file per unit, and each unit registers itself, so adding or changing one touches only its own file."
        "- [ ] After the waiting tasks' ``touches`` ($ids) are narrowed to their own files by editing their front matter, ``task-board.ps1 hotspots`` counts fewer than $SplitHotspotMinimum tasks for ``$($Hotspot.Path)``."
        '- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.'
    ) -join "`n"
    $text = [IO.File]::ReadAllText($Path)
    $text = $text.Replace('<!-- One sentence: the observable outcome once this task is done. -->', $goal)
    $text = $text -replace '<!-- Why this matters.*?-->', $context.Replace('$', '$$')
    $text = $text.Replace('- [ ] <!-- A statement someone else can check from the repository without asking a question. -->', $criteria)
    [IO.File]::WriteAllText($Path, $text, (New-Object Text.UTF8Encoding $false))
}

function Invoke-SplitHotspotFiling {
    # Called by an Auto step once the board's capacity has bound two steps running: when a
    # ready task waits on overlapping touches, asks task-board.ps1 hotspots for the files
    # that hold them back and files at most one High task to split the top one no live task
    # names (Select-SplitHotspot), in the ci-watch worktree at origin/-Branch, pushed straight
    # to -Branch like the CI-failure tasks; up to three tries, each retried when git fetch
    # fails or when the push races a lane.
    param([string]$Branch)
    $saved = $env:CLAUDE_PROJECT_DIR
    $pushFailed = $false
    try {
        foreach ($attempt in 1..3) {
            git -C $Root fetch -q origin $Branch 2>&1 | Out-Null
            if ($LASTEXITCODE -ne 0) { Write-CiRunLine -Key 'split-fetch' -Line "git fetch origin $Branch failed (exit $LASTEXITCODE): the split-hotspot filing skips this attempt rather than file from a stale origin/$Branch"; continue }
            $script:CiWatch.Logged.Remove('split-fetch')
            if (-not (Test-Path (Join-Path $CiWatchDir '.git'))) {
                New-Item -ItemType Directory -Force -Path $LanesDir | Out-Null
                git -C $Root worktree add -q --detach $CiWatchDir "origin/$Branch" 2>&1 | Out-Null
            }
            git -C $CiWatchDir checkout -q -f --detach "origin/$Branch" 2>&1 | Out-Null
            git -C $CiWatchDir clean -q -fd -- Tasks 2>&1 | Out-Null
            if ($LASTEXITCODE -ne 0) { Write-Trace '-' 'split' "cannot check out origin/$Branch in $CiWatchDir" 'DarkYellow'; return }
            $env:CLAUDE_PROJECT_DIR = $CiWatchDir
            $board = Join-Path $CiWatchDir '.claude\skills\task-board\task-board.ps1'
            $status = @(& (Get-FactoryPowerShellExe) -NoProfile -ExecutionPolicy Bypass -File $board status 2>&1 | ForEach-Object { "$_" })
            $capacityLine = (@(& (Get-FactoryPowerShellExe) -NoProfile -ExecutionPolicy Bypass -File $board capacity 2>&1 | ForEach-Object { "$_" }) -join "`n")
            $waiting = Get-OverlapWaitingCount -StatusLines $status -CapacityLine $capacityLine
            if (-not (Test-SplitHotspotDue -CapacityStreak $script:Auto.CapacityStreak -Waiting $waiting)) { return }
            $lines = @(& (Get-FactoryPowerShellExe) -NoProfile -ExecutionPolicy Bypass -File $board hotspots 2>&1 | ForEach-Object { "$_" })
            $exit = $LASTEXITCODE
            $choice = Select-SplitHotspot -Lines $lines -ExitCode $exit -LiveTitles (Get-LiveTaskTitles $CiWatchDir) -Filed $script:Auto.SplitFiled
            if ($choice.Action -ne 'file') { Write-Trace '-' 'split' $choice.Reason 'DarkGray'; return }
            $newArgs = @('new', '-Title', (Get-SplitTaskTitle $choice.Path), '-Priority', 'High', '-Pipeline', 'feature', '-Touches', $choice.Path)
            $out = (& (Get-FactoryPowerShellExe) -NoProfile -ExecutionPolicy Bypass -File $board @newArgs 2>&1 | ForEach-Object { "$_" }) -join "`n"
            if ($out -notmatch '(?m)^(BL-\d+)\s+(Tasks\S+\.md)') { Write-Trace '-' 'split' "cannot file a split task for $($choice.Path): $(Get-Short $out 60)" 'DarkYellow'; return }
            $id = $Matches[1]; $line = $Matches[0]
            Set-SplitTaskBody -Path (Join-Path $CiWatchDir $Matches[2]) -Hotspot $choice -Waiting $waiting
            git -C $CiWatchDir add -A -- Tasks 2>&1 | Out-Null
            git -C $CiWatchDir commit -q -m "chore(tasks): file $id to split $($choice.Path)" -m "Filed by the dark factory's coordinator: $($choice.Reason)." -m $SkipCiNote 2>&1 | Out-Null
            git -C $CiWatchDir push -q origin "HEAD:$Branch" 2>&1 | Out-Null
            if ($LASTEXITCODE -ne 0) { $pushFailed = $true; continue }
            $script:Auto.SplitFiled = @(@($script:Auto.SplitFiled) + $choice.Path)
            Write-Trace $id 'split' "filed: $($choice.Reason)" 'Yellow'
            Send-CiWhisper $line
            return
        }
        $failed = if ($pushFailed) { "push the split task to $Branch" } else { "fetch origin $Branch" }
        Write-Trace '-' 'split' "could not $failed; trying again next step" 'DarkYellow'
    } catch {
        Write-Trace '-' 'split' "split filing failed: $(Get-Short $_.Exception.Message 80)" 'DarkYellow'
    } finally { $env:CLAUDE_PROJECT_DIR = $saved }
}

function Invoke-MergeEndCiWatch {
    # The watch at the end of a shift's merge. After a hand-over during the merge's CI wait
    # (BL-3144) the next shift already uses the one shared ci-watch worktree and files the
    # branch's CI failures from it, so this shift neither checks it out, cleans it nor removes
    # it: that would delete task files the next shift wrote and has not pushed. Returns whether
    # it ran.
    param([string]$Branch, [bool]$HandedOver)
    if ($HandedOver) { return $false }
    Invoke-CiWatch -Branch $Branch -Final
    Remove-CiWatch
    return $true
}

function Remove-CiWatch {
    # Deletes the ci-watch worktree at shift end.
    if (-not (Test-Path $CiWatchDir)) { return }
    git -C $Root worktree remove --force $CiWatchDir 2>&1 | Out-Null
    git -C $Root worktree prune 2>&1 | Out-Null
}

if ($TestCiWatch) {
    $check = {
        param([string]$Name, [string]$Expected, [string]$Got)
        if ($Expected -ceq $Got) { Write-Host "PASS ${Name}: $Got" -ForegroundColor Green }
        else { Write-Host "FAIL ${Name}: expected $Expected, got $Got" -ForegroundColor Red; $script:ciTestFailed++ }
    }
    $script:ciTestFailed = 0
    $t = "`t"
    $log = @(
        "Build and test (ubuntu-latest)${t}Fast tests${t}2026-09-29T22:10:04.3646946Z Passed!  - Failed:     0, Passed:   257 - Curl.Zstandard.UnitTests.dll (net10.0)"
        "Build and test (ubuntu-latest)${t}Fast tests${t}2026-09-29T22:10:06.0746696Z   Failed FromComponents_ZeroCoefficient_Throws [54 ms]"
        "Build and test (ubuntu-latest)${t}Fast tests${t}2026-09-29T22:10:06.0765938Z   Error Message:"
        "Build and test (ubuntu-latest)${t}Fast tests${t}2026-09-29T22:10:06.0856851Z    Assertion failed. Expected exception of exact type CryptographicException but caught OpenSslCryptographicException."
        "Build and test (ubuntu-latest)${t}Fast tests${t}2026-09-29T22:10:06.1238521Z                      at Curl.Protocol.Ssh.Keys.RsaSshPrivateKey.FromPkcs1(ReadOnlySpan``1 der) in /home/runner/work/Curl/Curl/Curl.Protocol.Ssh.UnitLibrary/Keys/RsaSshPrivateKey.cs"
        "Build and test (ubuntu-latest)${t}Fast tests${t}2026-09-29T22:10:06.1352234Z                      at Curl.Protocol.Ssh.Keys.RsaSshPrivateKeyTests.<>c.b__5_0() in /home/runner/work/Curl/Curl/Curl.Protocol.Ssh.UnitTests/Keys/RsaSshPrivateKeyTests.cs:line 90"
        "Build and test (ubuntu-latest)${t}Fast tests${t}2026-09-29T22:10:07.6024310Z Failed!  - Failed:     1, Passed:   873, Skipped:     0, Total:   874, Duration: 16 s - Curl.Protocol.Ssh.UnitTests.dll (net10.0)"
        "Build and test (macos-latest)${t}Fast tests${t}2026-09-29T22:10:05.3235560Z   Failed FromComponents_ZeroCoefficient_Throws [73 ms]"
        "Build and test (macos-latest)${t}Fast tests${t}2026-09-29T22:10:05.3240640Z   Error Message:"
        "Build and test (macos-latest)${t}Fast tests${t}2026-09-29T22:10:05.3240641Z    Assertion failed. Expected exception of exact type CryptographicException but caught AppleCFErrorCryptographicException."
        "Build and test (macos-latest)${t}Fast tests${t}2026-09-29T22:10:07.6442400Z Failed!  - Failed:     1, Passed:   873 - Curl.Protocol.Ssh.UnitTests.dll (net10.0)")
    $parsed = @(Get-CiFailures $log)
    & $check 'test failure parsed' 'FromComponents_ZeroCoefficient_Throws|test|Linux,macOS|Curl.Protocol.Ssh.UnitTests|Assertion failed. Expected exception of exact type CryptographicException but caught OpenSslCryptographicException.' `
        (($parsed | ForEach-Object { "$($_.Key)|$($_.Kind)|$($_.Platforms -join ',')|$($_.Project)|$($_.Message)" }) -join ';')
    $buildLog = @(
        "Build and test (macos-latest)${t}Build${t}2026-09-29T22:00:00.0000000Z /Users/runner/work/Curl/Curl/Curl.Cli.UnitLibrary/Options/Parser.cs(12,5): error CS1002: ; expected [/Users/runner/work/Curl/Curl/Curl.Cli.UnitLibrary/Curl.Cli.UnitLibrary.csproj]"
        "Build and test (macos-latest)${t}Build${t}2026-09-29T22:00:01.0000000Z /Users/runner/work/Curl/Curl/Curl.Cli.UnitLibrary/Options/Parser.cs(12,5): error CS1002: ; expected [/Users/runner/work/Curl/Curl/Curl.Cli.UnitLibrary/Curl.Cli.UnitLibrary.csproj]")
    & $check 'build error parsed' 'error CS1002 in Parser.cs|build|macOS|Curl.Cli.UnitLibrary|; expected' `
        ((@(Get-CiFailures $buildLog) | ForEach-Object { "$($_.Key)|$($_.Kind)|$($_.Platforms -join ',')|$($_.Project)|$($_.Message)" }) -join ';')
    $now = [datetime]'2026-09-29T23:00:00'
    $f = { param([string]$Key, [string[]]$On, [string]$Kind = 'test') [pscustomobject]@{ Key = $Key; Kind = $Kind; Platforms = $On; Project = 'Curl.X.UnitTests'; Message = 'm' } }
    $run = { param([string]$Id, [int]$MinutesAgo, [object[]]$Failures) [pscustomobject]@{ Id = $Id; Sha = "sha$Id"; FinishedAt = $now.AddMinutes(-$MinutesAgo); Failures = @($Failures) } }
    $show = { param($Verdicts) (@($Verdicts | Where-Object { $_ }) | ForEach-Object { "$($_.Verdict) $($_.Key) run $($_.RunId) from $($_.FirstSha) on $($_.Platforms -join '+')" }) -join '; ' }
    & $check 'two platforms: regression at once' 'regression A run 9 from sha9 on Linux+macOS' `
        (& $show (Get-CiVerdicts -Now $now -Runs @((& $run 9 2 @(& $f A @('Linux', 'macOS'))), (& $run 8 20 @()))))
    & $check 'one platform, newest only: waits' '' `
        (& $show (Get-CiVerdicts -Now $now -Runs @((& $run 9 2 @(& $f A @('Linux'))), (& $run 8 20 @()))))
    & $check 'one platform, unconfirmed for 5 min: regression' 'regression A run 9 from sha9 on Linux' `
        (& $show (Get-CiVerdicts -Now $now -Runs @((& $run 9 31 @(& $f A @('Linux'))), (& $run 8 40 @()))))
    & $check 'two runs in a row: regression from the first' 'regression A run 9 from sha8 on Linux+macOS' `
        (& $show (Get-CiVerdicts -Now $now -Runs @((& $run 9 2 @(& $f A @('Linux'))), (& $run 8 9 @(& $f A @('macOS'))), (& $run 7 20 @()))))
    & $check 'failed once between passes: flaky' 'flaky A run 8 from sha8 on Linux' `
        (& $show (Get-CiVerdicts -Now $now -Runs @((& $run 9 2 @()), (& $run 8 9 @(& $f A @('Linux'))), (& $run 7 20 @()))))
    & $check 'failed again after passing: flaky' 'flaky A run 9 from sha9 on Linux' `
        (& $show (Get-CiVerdicts -Now $now -Runs @((& $run 9 2 @(& $f A @('Linux'))), (& $run 8 9 @()), (& $run 7 20 @(& $f A @('Linux'))))))
    & $check 'fixed regression: nothing' '' `
        (& $show (Get-CiVerdicts -Now $now -Runs @((& $run 9 2 @()), (& $run 8 9 @(& $f A @('Linux'))), (& $run 7 20 @(& $f A @('Linux'))))))
    & $check 'build error: regression at once' 'regression error CS1002 in X.cs run 9 from sha9 on Linux' `
        (& $show (Get-CiVerdicts -Now $now -Runs @((& $run 9 2 @(& $f 'error CS1002 in X.cs' @('Linux') 'build')))))
    & $check 'flaky title' 'Fix flaky CI test A that failed once on Linux' (Get-CiTaskTitle ([pscustomobject]@{ Key = 'A'; Kind = 'test'; Verdict = 'flaky'; Platforms = @('Linux') }))
    & $check 'CI fix title is claimable past the usage stop' 'True True True False' ("$(Test-CiFixTitle 'Fix CI failure A on Linux') $(Test-CiFixTitle 'Fix flaky CI test A') $(Test-CiFixTitle 'Fix CI build CS1002 on macOS') $(Test-CiFixTitle 'Add --foo option')")
    & $check 'a CI fix task file is told by its name' 'True True True False' ("$(Test-CiFixFileName 'BL-4052-fix-ci-failure-a.md') $(Test-CiFixFileName 'BL-4083-fix-flaky-ci-test-b.md') $(Test-CiFixFileName 'BL-9-fix-ci-build-c.md') $(Test-CiFixFileName 'BL-4093-add-extensions.md')")
    & $check 'touches' 'Curl.Protocol.Ssh.UnitTests,Curl.Protocol.Ssh.UnitLibrary' ((Get-CiTouches -Project 'Curl.Protocol.Ssh.UnitTests' -Repo $Root) -join ',')
    & $check 'integration touches' 'Curl.Networking.IntegrationTests,Curl.Networking.UnitLibrary' ((Get-CiTouches -Project 'Curl.Networking.IntegrationTests' -Repo $Root) -join ',')
    & $check 'console integration touches' 'Curl.Console.IntegrationTests,Curl.Console' ((Get-CiTouches -Project 'Curl.Console.IntegrationTests' -Repo $Root) -join ',')
    $integrationLog = @(
        "Build and test (ubuntu-latest)${t}Fast tests${t}2026-10-07T10:00:00.0000000Z   Failed Connect_Loopback_Succeeds [12 ms]"
        "Build and test (ubuntu-latest)${t}Fast tests${t}2026-10-07T10:00:00.0000001Z   Error Message:"
        "Build and test (ubuntu-latest)${t}Fast tests${t}2026-10-07T10:00:00.0000002Z    Assert.AreEqual failed."
        "Build and test (ubuntu-latest)${t}Fast tests${t}2026-10-07T10:00:00.0000003Z      at Curl.Networking.TcpDialerTests.Connect_Loopback_Succeeds() in /home/runner/work/Curl/Curl/Curl.Networking.IntegrationTests/TcpDialerTests.cs:line 12"
        "Build and test (ubuntu-latest)${t}Fast tests${t}2026-10-07T10:00:00.0000004Z Failed!  - Failed:     1, Passed:     3 - SomethingElse.dll (net10.0)")
    & $check 'integration test failure parsed' 'Connect_Loopback_Succeeds|Curl.Networking.IntegrationTests' `
        ((@(Get-CiFailures $integrationLog) | ForEach-Object { "$($_.Key)|$($_.Project)" }) -join ';')
    # BL-3998: data rows with their own display names, recorded from run 40774593052, are one
    # failure keyed by their method; an upstream case keeps its case number.
    $rowLog = @()
    foreach ($os in 'ubuntu', 'macos') {
        $j = "Build and test ($os-latest)${t}Fast tests${t}2026-10-28T22:00:51.0000000Z "
        $method = 'Curl.Networking.TcpConnectorTests.ConnectAsync_UnderTheOtherFamilyToAnAddressLiteral_DialsTheLiteral'
        foreach ($row in '-4 http://[::1]:47500/', '-4 bracketed', '-6 http://127.0.0.1:47500/') {
            $rowLog += @(
                "$j  Failed $row [1 ms]"
                "$j  Error Message:"
                "$j   Assertion failed. Expected value to not be null."
                "$j  Stack Trace:"
                "$j     at $method(String host, AddressFamily family) in /home/runner/work/Curl/Curl/Curl.Networking.UnitTests/TcpConnectorTests.AddressFamily.cs:line 120"
                "$j  Standard Output Messages:"
                "$j TestContext Messages:"
                "$j START $method ($row)"
                "$j END ${method}: Failed in 1 ms (arrange 1, act 2, assert 1)")
        }
        $rowLog += @(
            "$j  Failed ConnectAsync_UnderIPv6ThroughAnIPv4ProxyLiteral_DialsTheProxy [1 ms]"
            "$j START Curl.Networking.TcpConnectorTests.ConnectAsync_UnderIPv6ThroughAnIPv4ProxyLiteral_DialsTheProxy"
            "${j}Failed!  - Failed:     4, Passed:  1666 - Curl.Networking.UnitTests.dll (net10.0)"
            "$j  Failed test1034: HTTP over proxy with malformatted IDN host name [2 s]"
            "$j START Curl.Conformance.UnitTests.UpstreamConformanceTests.UpstreamCase_RunThroughCurl_HoldsTheRatchet (test1034: HTTP over proxy with malformatted IDN host name)"
            "${j}Failed!  - Failed:     1, Passed:  1000 - Curl.Conformance.UnitTests.dll (net10.0)")
    }
    $rowFailures = @(Get-CiFailures $rowLog)
    & $check 'data rows keyed by their method' 'ConnectAsync_UnderTheOtherFamilyToAnAddressLiteral_DialsTheLiteral|Linux,macOS|Curl.Networking.UnitTests|-4 http://[::1]:47500/,-4 bracketed,-6 http://127.0.0.1:47500/;ConnectAsync_UnderIPv6ThroughAnIPv4ProxyLiteral_DialsTheProxy|Linux,macOS|Curl.Networking.UnitTests|;test1034:|Linux,macOS|Curl.Conformance.UnitTests|' `
        (($rowFailures | ForEach-Object { "$($_.Key)|$($_.Platforms -join ',')|$($_.Project)|$($_.Rows -join ',')" }) -join ';')
    $rowVerdict = @(Get-CiVerdicts -Now $now -Runs @([pscustomobject]@{ Id = '40774593052'; Sha = 'sha1'; FinishedAt = $now; Failures = $rowFailures }))[0]
    & $check 'data-row task title' 'Fix CI failure ConnectAsync_UnderTheOtherFamilyToAnAddressLiteral_DialsTheLiteral on Linux and macOS' (Get-CiTaskTitle $rowVerdict)
    $rowBody = Join-Path ([IO.Path]::GetTempPath()) "df-ci-rows-$PID.md"
    Copy-Item (Join-Path $Root '.claude\skills\task-board\TASK-TEMPLATE.md') $rowBody
    Set-CiTaskBody -Path $rowBody -RunUrl 'https://github.com/o/r/actions/runs/40774593052' -Verdict $rowVerdict
    $rowText = [IO.File]::ReadAllText($rowBody)
    Remove-Item $rowBody -ErrorAction SilentlyContinue
    & $check 'data-row task context' 'True True' "$($rowText.Contains('`ConnectAsync_UnderTheOtherFamilyToAnAddressLiteral_DialsTheLiteral` failed on Linux and macOS')) $($rowText.Contains('Failing data rows: `-4 http://[::1]:47500/`, `-4 bracketed`, `-6 http://127.0.0.1:47500/`.'))"
    $body = Join-Path ([IO.Path]::GetTempPath()) "df-ci-body-$PID.md"
    Copy-Item (Join-Path $Root '.claude\skills\task-board\TASK-TEMPLATE.md') $body
    Set-CiTaskBody -Path $body -RunUrl 'https://github.com/o/r/actions/runs/9' -Verdict ([pscustomobject]@{
        Key = 'A_Throws'; Kind = 'test'; Verdict = 'regression'; RunId = '9'; FirstSha = '0729839c0000'; Platforms = @('Linux', 'macOS'); Message = 'costs $5' })
    $text = [IO.File]::ReadAllText($body)
    Remove-Item $body -ErrorAction SilentlyContinue
    $seen = @()
    $seen += $(if ($text -match '<!--') { 'placeholder left' } else { 'no placeholder' })
    if ($text -match 'CI run 9 \(https') { $seen += 'run 9' }
    if ($text -match 'First failing commit: 0729839c\.') { $seen += '0729839c' }
    if ($text.Contains('costs $5')) { $seen += '$5' }
    if ($text.Contains('- [ ] `A_Throws` passes locally')) { $seen += 'criteria' }
    & $check 'task body filled' 'no placeholder; run 9; 0729839c; $5; criteria' ($seen -join '; ')
    # Covered: a live task names the test; a Done one does for a run older than its last commit.
    $repo = Join-Path ([IO.Path]::GetTempPath()) "df-ci-covered-$PID"
    foreach ($state in 'Backlog', 'Done') { New-Item -ItemType Directory -Force -Path (Join-Path $repo "Tasks\$state") | Out-Null }
    $commit = { param([string]$Message) git -C $repo add -A 2>&1 | Out-Null; git -C $repo -c user.name=t -c user.email=t@t commit -q --allow-empty -m $Message 2>&1 | Out-Null; "$(git -C $repo rev-parse HEAD)".Trim() }
    git -C $repo init -q -b work 2>&1 | Out-Null
    Set-Content -Path (Join-Path $repo 'Tasks\Backlog\BL-001-fix-ci-failure-a.md') -Value 'title: Fix CI failure A_Throws on Linux'
    $oldRun = & $commit 'file BL-001'
    $live = Test-CiFailureCovered -Key 'A_Throws' -Sha $oldRun -Repo $repo
    $prefix = Test-CiFailureCovered -Key 'A' -Sha $oldRun -Repo $repo
    Move-Item (Join-Path $repo 'Tasks\Backlog\BL-001-fix-ci-failure-a.md') (Join-Path $repo 'Tasks\Done\')
    & $commit 'BL-001 Done' | Out-Null
    $newRun = & $commit 'a later push'
    $stale = Test-CiFailureCovered -Key 'A_Throws' -Sha $oldRun -Repo $repo
    $again = Test-CiFailureCovered -Key 'A_Throws' -Sha $newRun -Repo $repo
    Remove-Item -Recurse -Force -Path $repo -ErrorAction SilentlyContinue
    & $check 'covered' 'live True; other name False; before the fix True; after the fix False' "live $live; other name $prefix; before the fix $stale; after the fix $again"
    # BL-1031, the live path: gh's run list and --log-failed lines, recorded from runs
    # 36674691490 (two TcpDialerTests failures on Linux) and 36690174792 (one on macOS).
    $traced = New-Object Collections.Generic.List[string]
    function Write-Trace { param($Id, $Kind, $Text, $Color) $traced.Add($Text) }
    $script:CiWatch = @{ CheckedAt = [datetime]::MinValue; Failures = @{}; Capacity = @{}; Settled = @{}; Logged = @{}; AuditTasks = @{}; Off = $false }
    $ubuntu = "Build and test (ubuntu-latest)${t}Fast tests${t}"
    $macos = "Build and test (macos-latest)${t}UNKNOWN STEP${t}"
    $recorded = @{
        '36674691490' = @(
            "${ubuntu}2026-09-30T05:46:09.4012066Z   Skipped ConnectAsync_WithAnAbstractSocketOnWindows_FailsAsCurlsInvalidArguments"
            "${ubuntu}2026-09-30T05:46:09.4101921Z   Failed BindLocalEnd_WhenEveryPortIsInUse_ThrowsInterfaceFailed [12 ms]"
            "${ubuntu}2026-09-30T05:46:09.4131550Z   Error Message:"
            "${ubuntu}2026-09-30T05:46:09.4161939Z    Assertion failed. Expected exception of exact type LocalBindException but no exception was thrown."
            "${ubuntu}2026-09-30T05:46:09.4211716Z   Stack Trace:"
            "${ubuntu}2026-09-30T05:46:09.4242317Z      at Curl.Networking.TcpDialerTests.BindLocalEnd_WhenEveryPortIsInUse_ThrowsInterfaceFailed() in /home/runner/work/Curl/Curl/Curl.Networking.UnitTests/TcpDialerTests.cs:line 231"
            "${ubuntu}2026-09-30T05:46:09.4331989Z   Failed BindLocalEnd_WhenTheFirstPortIsInUse_BindsTheNext [22 ms]"
            "${ubuntu}2026-09-30T05:46:09.4332652Z   Error Message:"
            "${ubuntu}2026-09-30T05:46:09.4333071Z    Assertion failed. Expected values to be equal."
            "${ubuntu}2026-09-30T05:46:09.4382540Z   Stack Trace:"
            "${ubuntu}2026-09-30T05:46:09.4412190Z      at Curl.Networking.TcpDialerTests.BindLocalEnd_WhenTheFirstPortIsInUse_BindsTheNext() in /home/runner/work/Curl/Curl/Curl.Networking.UnitTests/TcpDialerTests.cs:line 219"
            "${ubuntu}2026-09-30T05:46:09.4992460Z Passed!  - Failed:     0, Passed:    98, Skipped:     0, Total:    98, Duration: 531 ms - Curl.Protocol.Mqtt.UnitTests.dll (net10.0)"
            "${ubuntu}2026-09-30T05:46:17.9032566Z Failed!  - Failed:     2, Passed:  1666, Skipped:    16, Total:  1684, Duration: 35 s - Curl.Networking.UnitTests.dll (net10.0)")
        '36690174792' = @(
            "${macos}2026-09-30T08:34:39.6519410Z   Failed BindLocalEnd_WhenTheFirstPortIsInUse_BindsTheNext [9 ms]"
            "${macos}2026-09-30T08:34:39.6519420Z   Error Message:"
            "${macos}2026-09-30T08:34:39.6519430Z    Test method Curl.Networking.TcpDialerTests.BindLocalEnd_WhenTheFirstPortIsInUse_BindsTheNext threw exception:"
            "${macos}2026-09-30T08:34:44.7941160Z Failed!  - Failed:     1, Passed:  1651, Skipped:    32, Total:  1684, Duration: 25 s - Curl.Networking.UnitTests.dll (net10.0)")
        '36671448420' = @("${ubuntu}2026-09-30T05:05:00.0000000Z ##[error]Process completed with exit code 1.")
    }
    $readLog = { param([string]$Id) if ($Id -eq '99') { return $null } return , @($recorded[$Id]) }
    $listed = { param([string]$Branch) $script:ciListJson }
    $script:ciListJson = '[{"conclusion":"failure","databaseId":36674691490,"headSha":"d0621052bba101113a0187570196267f13bc6b74","status":"completed","updatedAt":"2026-09-30T05:47:43Z"},{"conclusion":"success","databaseId":36668685198,"headSha":"7c9833f44d160cdb92e3bf3c9fda81b9cc9a0e5d","status":"completed","updatedAt":"2026-09-30T04:28:52Z"}]'
    $replay = Get-CiRuns -Branch 'work' -ListRuns $listed -ReadLog $readLog
    $replayAt = $replay[0].FinishedAt
    & $check 'replay 36674691490: new, waits to confirm' '' (& $show (Get-CiVerdicts -Now $replayAt.AddMinutes(4) -Runs $replay))
    & $check 'replay 36674691490: a task per failing test once confirmed' `
        'regression BindLocalEnd_WhenEveryPortIsInUse_ThrowsInterfaceFailed run 36674691490 from d0621052bba101113a0187570196267f13bc6b74 on Linux; regression BindLocalEnd_WhenTheFirstPortIsInUse_BindsTheNext run 36674691490 from d0621052bba101113a0187570196267f13bc6b74 on Linux' `
        (& $show (Get-CiVerdicts -Now $replayAt.AddMinutes(31) -Runs $replay))
    & $check 'replay 36674691490: the final look files at once' '2' "$(@(Get-CiVerdicts -Now $replayAt.AddMinutes(5) -Runs $replay -ConfirmMinutes 0).Count)"
    # The case that failed live: a run with one failing test was dropped as if it had none.
    $script:ciListJson = '[{"conclusion":"failure","databaseId":36690174792,"headSha":"b86b15dfc80d1dea7a5733c9b73fd263a17533b3","status":"completed","updatedAt":"2026-09-30T08:35:42Z"},{"conclusion":"cancelled","databaseId":1,"headSha":"x","status":"completed","updatedAt":"2026-09-30T08:31:26Z"},{"conclusion":"failure","databaseId":36671448420,"headSha":"bfd523f47edea0e8d7826a657606353aa2b6b462","status":"completed","updatedAt":"2026-09-30T05:05:30Z"},{"conclusion":"failure","databaseId":99,"headSha":"9999999999","status":"completed","updatedAt":"2026-09-30T05:00:00Z"},{"conclusion":"success","databaseId":36689969747,"headSha":"c4083da8fbe96c97b45499a41bab397f52fadce4","status":"completed","updatedAt":"2026-09-30T08:33:40Z"}]'
    $single = Get-CiRuns -Branch 'work' -ListRuns $listed -ReadLog $readLog
    & $check 'one failing test keeps its run; a nameless failure is kept by its job and step' '36690174792 1 macOS; 36671448420 1 Linux; 36689969747 0 ' `
        (($single | ForEach-Object { "$($_.Id) $(@($_.Failures).Count) $(@($_.Failures | ForEach-Object { $_.Platforms }) -join '+')" }) -join '; ')
    & $check 'unreadable failures traced' 'run 99 failure on 99999999: cannot read its log with gh run view --log-failed; trying again next heartbeat' `
        ((@($traced) | Sort-Object) -join '|')
    $nameless = @(Get-CiFailures @("${ubuntu}2026-09-30T05:05:00.0000000Z ##[error]Process completed with exit code 1."))
    & $check 'a failure naming no test is filed by its job and step (AF-0171)' 'job Build and test (ubuntu-latest) step Fast tests|job|Linux|##[error]Process completed with exit code 1.' `
        "$($nameless[0].Key)|$($nameless[0].Kind)|$($nameless[0].Platforms -join '+')|$($nameless[0].Message)"
    $mixed = @(Get-CiFailures @(
        "${ubuntu}2026-10-11T22:12:00.0000000Z   Failed test303 [5 ms]"
        "${ubuntu}2026-10-11T22:12:01.0000000Z Failed!  - Failed:     1, Passed:     9 - Curl.Cli.UnitTests.dll (net10.0)"
        "quality${t}Measure code quality${t}2026-10-11T22:13:00.0000000Z ##[error]Process completed with exit code 1."))
    & $check 'a red job beside a named test failure is filed too (AF-0150)' 'test303|test; job quality step Measure code quality|job' `
        (($mixed | ForEach-Object { "$($_.Key)|$($_.Kind)" }) -join '; ')
    $gate = @(Get-CiFailures @(
        "quality${t}Measure coverage${t}2026-10-16T09:02:13.0000000Z the step script echoed first"
        "quality${t}Measure coverage${t}2026-10-16T09:02:14.0000000Z ##[error]CurlMulti.SetOption is outside a gate: branch 87.5%"
        "quality${t}Measure coverage${t}2026-10-16T09:02:15.0000000Z ##[error]Process completed with exit code 1."))
    & $check 'a quality-gate failure names the members outside a gate (AF-0244)' 'CurlMulti.SetOption is outside a gate: branch 87.5%' "$($gate[0].Message)"
    $unknownJob = @(Get-CiFailures @("UNKNOWN STEP${t}Measure code quality${t}2026-10-11T23:15:00.0000000Z ##[error]Process completed with exit code 1."))
    & $check 'a failure whose job gh cannot name is still filed (AF-0178)' 'job unknown job step Measure code quality|job' `
        "$($unknownJob[0].Key)|$($unknownJob[0].Kind)"
    & $check 'an empty log names no failure' '0' "$(@(Get-CiFailures @()).Count)"
    $script:CiWatch = @{ CheckedAt = [datetime]::MinValue; Failures = @{}; Capacity = @{}; Settled = @{}; Logged = @{}; AuditTasks = @{}; Off = $false }
    $emptyRuns = @(Get-CiRuns -Branch 'b' -ListRuns { '[{"databaseId":901,"status":"completed","conclusion":"failure","headSha":"abcdef0123","updatedAt":"2026-10-11T23:15:00Z"}]' } -ReadLog { , @() })
    & $check 'a failed run with an empty log is filed by the run (AF-0178)' '1|job unknown job step unknown step' `
        "$($emptyRuns.Count)|$($emptyRuns[0].Failures[0].Key)"
    $noRunner = '{"jobs":[{"name":"ok","conclusion":"success","steps":[{"conclusion":"success"}]},{"name":"Pack","conclusion":"cancelled","steps":[]}]}'
    $ran = '{"jobs":[{"name":"Pack","conclusion":"cancelled","steps":[]},{"name":"T","conclusion":"failure","steps":[{"conclusion":"failure"}]}]}'
    $script:CiWatch = @{ CheckedAt = [datetime]::MinValue; Failures = @{}; Capacity = @{}; Settled = @{}; Logged = @{}; AuditTasks = @{}; Off = $false }
    $traced.Clear()
    $capRuns = @(Get-CiRuns -Branch 'b' -ListRuns { '[{"databaseId":902,"status":"completed","conclusion":"failure","headSha":"abcdef0123","updatedAt":"2026-10-11T23:15:00Z"}]' } -ReadLog { , @() } -ReadJobs { $noRunner })
    & $check 'a failed run whose only bad job never got a runner files nothing and says runner capacity (BL-3944)' '0|run 902 failure on abcdef01: runner capacity - every job that did not succeed was never given a runner; nothing to file' "$(@($capRuns[0]).Count)|$($traced -join ';')"
    $ranRuns = @(Get-CiRuns -Branch 'b' -ListRuns { '[{"databaseId":903,"status":"completed","conclusion":"failure","headSha":"abcdef0123","updatedAt":"2026-10-11T23:15:00Z"}]' } -ReadLog { , @() } -ReadJobs { $ran })
    & $check 'an empty log with a job that ran and failed is still filed (BL-3944)' '1|job unknown job step unknown step' "$($ranRuns.Count)|$($ranRuns[0].Failures[0].Key)"
    $script:CiWatch = @{ CheckedAt = [datetime]::MinValue; Failures = @{}; Capacity = @{}; Settled = @{}; Logged = @{}; AuditTasks = @{}; Off = $false }
    $traced.Clear()
    $script:ciListJson = $null
    $first = Get-CiRuns -Branch 'work' -ListRuns $listed -ReadLog $readLog
    $second = Get-CiRuns -Branch 'work' -ListRuns $listed -ReadLog $readLog
    & $check 'gh cannot list: no runs, traced once' 'True True; cannot list the CI runs on work with gh (gh gave no output and no exit code)' "$($null -eq $first) $($null -eq $second); $($traced -join '|')"
    # BL-4377: gh's exit code and first stderr line, traced again at most every 30 min while
    # listing keeps failing, and once when it lists again.
    $script:CiWatch = @{ CheckedAt = [datetime]::MinValue; Failures = @{}; Capacity = @{}; Settled = @{}; Logged = @{}; AuditTasks = @{}; Off = $false }
    $traced.Clear()
    $blindAt = [datetime]'2026-10-30 15:22'
    $refused = { param([string]$Branch) [pscustomobject]@{ ExitCode = 4; Error = 'To get started with GitHub CLI, please run:  gh auth login' } }
    $null = Get-CiRuns -Branch 'work' -ListRuns $refused -ReadLog $readLog -Now $blindAt
    & $check 'gh cannot list: its exit code and first stderr line (BL-4377)' 'cannot list the CI runs on work with gh (exit 4: To get started with GitHub CLI, please run: gh auth login)' ($traced -join '|')
    $null = Get-CiRuns -Branch 'work' -ListRuns $refused -ReadLog $readLog -Now $blindAt.AddMinutes(3)
    $null = Get-CiRuns -Branch 'work' -ListRuns $refused -ReadLog $readLog -Now $blindAt.AddMinutes(29)
    & $check 'gh still cannot list within 30 min: no new line (BL-4377)' '1' "$($traced.Count)"
    $null = Get-CiRuns -Branch 'work' -ListRuns $refused -ReadLog $readLog -Now $blindAt.AddMinutes(31)
    $null = Get-CiRuns -Branch 'work' -ListRuns $refused -ReadLog $readLog -Now $blindAt.AddMinutes(34)
    & $check 'gh still cannot list after 30 min: said again with how long (BL-4377)' '2|still cannot list the CI runs on work with gh, for 31 min (exit 4: To get started with GitHub CLI, please run: gh auth login)' "$($traced.Count)|$($traced[1])"
    $script:ciListJson = '[]'
    $null = Get-CiRuns -Branch 'work' -ListRuns $listed -ReadLog $readLog -Now $blindAt.AddMinutes(40)
    $null = Get-CiRuns -Branch 'work' -ListRuns $listed -ReadLog $readLog -Now $blindAt.AddMinutes(43)
    & $check 'gh lists again: one line (BL-4377)' '3|listing CI runs again on work, after 40 min unable to' "$($traced.Count)|$($traced[2])"
    $script:ciListJson = $null
    & $check 'Invoke-GhRunList: a failing gh gives its exit code and first stderr line (BL-4377)' 'exit 1: HTTP 401: Bad credentials' (Get-GhListFailure (& {
        function gh { Write-Error 'HTTP 401: Bad credentials'; Write-Error 'second line'; $global:LASTEXITCODE = 1 }
        Invoke-GhRunList -Arguments @('--workflow', 'CI') }))
    & $check 'Invoke-GhRunList: a gh that lists gives its JSON (BL-4377)' '[]' (& {
        function gh { '[]' }
        Invoke-GhRunList -Arguments @('--workflow', 'CI') })
    # What a coordinator log line says for each run it examined, and that it says it once.
    $verdicts = @(Get-CiVerdicts -Now $replayAt.AddMinutes(31) -Runs $replay)
    $waiting = Get-CiRunOutcome -Run $replay[0] -Runs $replay -Verdicts @() -Settled @{} -ConfirmMinutes 5
    & $check 'outcome: waiting' "failure on d0621052: BindLocalEnd_WhenEveryPortIsInUse_ThrowsInterfaceFailed on Linux only, waiting for the next run or $($replayAt.AddMinutes(5).ToString('HH:mm')) to confirm it; BindLocalEnd_WhenTheFirstPortIsInUse_BindsTheNext on Linux only, waiting for the next run or $($replayAt.AddMinutes(5).ToString('HH:mm')) to confirm it" $waiting
    $settled = @{ 'BindLocalEnd_WhenEveryPortIsInUse_ThrowsInterfaceFailed|36674691490' = 'filed BL-1040'; 'BindLocalEnd_WhenTheFirstPortIsInUse_BindsTheNext|36674691490' = 'covered by a task' }
    & $check 'outcome: filed and covered' 'failure on d0621052: BindLocalEnd_WhenEveryPortIsInUse_ThrowsInterfaceFailed regression, filed BL-1040; BindLocalEnd_WhenTheFirstPortIsInUse_BindsTheNext regression, covered by a task' `
        (Get-CiRunOutcome -Run $replay[0] -Runs $replay -Verdicts $verdicts -Settled $settled)
    & $check 'outcome: green run' 'success on 7c9833f4: nothing to file' (Get-CiRunOutcome -Run $replay[1] -Runs $replay -Verdicts $verdicts -Settled $settled)
    & $check 'outcome: not filed yet, older run, passed since' 'failure on sha8: A regression, not filed yet; failure on sha8: A filed with run 9; failure on sha8: A passed since, not filed' `
        ((@(
            Get-CiRunOutcome -Run (& $run 8 9 @(& $f A @('Linux'))) -Runs @((& $run 8 9 @())) -Verdicts @([pscustomobject]@{ Key = 'A'; Verdict = 'regression'; RunId = '8' }) -Settled @{}
            Get-CiRunOutcome -Run (& $run 8 9 @(& $f A @('Linux'))) -Runs @((& $run 9 2 @())) -Verdicts @([pscustomobject]@{ Key = 'A'; Verdict = 'regression'; RunId = '9' }) -Settled @{}
            Get-CiRunOutcome -Run (& $run 8 9 @(& $f A @('Linux'))) -Runs @((& $run 9 2 @())) -Verdicts @() -Settled @{})) -join '; ')
    $traced.Clear()
    Write-CiRunLine -RunId '7' -Line 'success on sha7: nothing to file'
    Write-CiRunLine -RunId '7' -Line 'success on sha7: nothing to file'
    Write-CiRunLine -RunId '8' -Line 'failure on sha8: A waiting'
    Write-CiRunLine -RunId '8' -Line 'failure on sha8: A regression, filed BL-1'
    & $check 'a run is traced once per outcome' 'run 7 success on sha7: nothing to file|run 8 failure on sha8: A waiting|run 8 failure on sha8: A regression, filed BL-1' ($traced -join '|')
    # BL-999: the audit guard's failure line files one interactive-only task, once.
    $auditLog = @("audit-guard${t}Run guard${t}2026-09-29T10:00:00.0000000Z Audit guard: Audit/Findings/x.md changed on work/dark-factory since its merge base with master")
    $audit = @(Get-CiFailures $auditLog)
    & $check 'audit guard line parsed' 'audit guard Audit/Findings/x.md|audit' (($audit | ForEach-Object { "$($_.Key)|$($_.Kind)" }) -join ';')
    $auditVerdicts = @(Get-CiVerdicts -Now $now -Runs @((& $run 9 2 $audit)))
    & $check 'audit guard: one red run files it' 'regression audit guard Audit/Findings/x.md run 9 from sha9 on audit-guard' (& $show $auditVerdicts)
    & $check 'audit guard title' "Revert the dark factory's change to Audit/Findings/x.md" (Get-CiTaskTitle $auditVerdicts[0])
    $board = Join-Path ([IO.Path]::GetTempPath()) "df-ci-audit-$PID"
    foreach ($state in 'Backlog', 'Doing', 'Blocked', 'Done') { New-Item -ItemType Directory -Force -Path (Join-Path $board "Tasks\$state") | Out-Null }
    $savedDir = $env:CLAUDE_PROJECT_DIR; $savedLane = $env:CURL_DARK_FACTORY_LANE
    try {
        $env:CLAUDE_PROJECT_DIR = $board
        $env:CURL_DARK_FACTORY_LANE = '0'
        $boardScript = Join-Path $Root '.claude\skills\task-board\task-board.ps1'
        $out = (& (Get-FactoryPowerShellExe) -NoProfile -ExecutionPolicy Bypass -File $boardScript @(Get-CiTaskNewArgs -Verdict $auditVerdicts[0] -Touches @()) 2>&1 | ForEach-Object { "$_" }) -join "`n"
        $filedPath = if ($out -match '(?m)^(BL-\d+)\s+(Tasks\S+\.md)') { Join-Path $board $Matches[2] } else { '' }
        & $check 'audit task filed with CURL_DARK_FACTORY_LANE=0' 'filed' $(if ($filedPath -and (Test-Path $filedPath)) { 'filed' } else { "not filed: $out" })
        if ($filedPath -and (Test-Path $filedPath)) {
            Set-CiTaskBody -Path $filedPath -Verdict $auditVerdicts[0] -RunUrl 'https://github.com/o/r/actions/runs/9'
            $text = [IO.File]::ReadAllText($filedPath)
            $fields = @(
                $(if ($text -match '(?m)^lane: no\s*$') { 'lane: no' })
                $(if ($text -match '(?m)^priority: High\s*$') { 'High' })
                $(if ($text -match '(?m)^touches: \[Audit/Findings/x\.md\]\s*$') { 'touches: [Audit/Findings/x.md]' })
                $(if ($text -notmatch '<!--') { 'no placeholder' })) -join '; '
            & $check 'audit task fields' 'lane: no; High; touches: [Audit/Findings/x.md]; no placeholder' $fields
            & $check 'audit guard: a second run files nothing' 'True' "$(Test-CiFailureCovered -Key $auditVerdicts[0].Key -Sha 'sha10' -Repo $board)"
        }
    } finally {
        $env:CLAUDE_PROJECT_DIR = $savedDir; $env:CURL_DARK_FACTORY_LANE = $savedLane
        Remove-Item -Recurse -Force -Path $board -ErrorAction SilentlyContinue
    }
    # BL-3028: a job failure is filed with touches the log names, never with none.
    $quality = @(Get-CiFailures @(
        "Quality gates (windows-latest)${t}Measure coverage, complexity and CRAP${t}2026-10-21T21:10:00.0000000Z ##[error]Unit2413ConnectionPeer.Mismatch is outside a gate: complexity 12"
        "Quality gates (windows-latest)${t}Measure coverage, complexity and CRAP${t}2026-10-21T21:10:01.0000000Z ::error file=D:\a\Curl\Curl\Curl.slnx::not a real annotation"
        "Quality gates (windows-latest)${t}Measure coverage, complexity and CRAP${t}2026-10-21T21:10:02.0000000Z ##[error]Process completed with exit code 1."))
    $qualityArgs = @(Get-CiTaskNewArgs -Verdict $quality[0] -Touches @(Get-CiJobTouches -Verdict $quality[0] -Repo $Root))
    & $check 'a quality-gate job failure names the file of the member outside a gate and the ::error file (BL-3028)' `
        'Curl.slnx,Curl.Conformance.UnitProgram.UnitLibrary/Dns/Easy/Unit2413ConnectionPeer.cs' $qualityArgs[[array]::IndexOf($qualityArgs, '-Touches') + 1]
    $bare = [pscustomobject]@{ Key = 'job unknown job step unknown step'; Kind = 'job'; Platforms = @('unknown job'); Project = ''; Message = '' }
    $bareArgs = @(Get-CiTaskNewArgs -Verdict $bare -Touches @(Get-CiJobTouches -Verdict $bare -Repo $Root))
    & $check 'an unknown step with no file in its log is filed with Curl.slnx, never empty touches' 'Curl.slnx' $bareArgs[[array]::IndexOf($bareArgs, '-Touches') + 1]
    $gateOnly = [pscustomobject]@{ Key = 'job quality step Measure code quality'; Kind = 'job'; Platforms = @('quality'); Project = ''; Message = '' }
    & $check 'a quality step naming no member falls back to Measure-CodeQuality.ps1' 'Measure-CodeQuality.ps1' "$(Get-CiFallbackTouches $gateOnly)"
    # BL-3028: a green run that contains a Backlog CI-fix task's filing defers it.
    $ciTemp = Join-Path ([IO.Path]::GetTempPath()) "df-ci-defer-$PID"
    $ciOrigin = Join-Path $ciTemp 'origin.git'; $ciRepo = Join-Path $ciTemp 'repo'
    $savedRoot = $Root; $savedWatchDir = $CiWatchDir; $savedLanesDir = $LanesDir; $savedDir = $env:CLAUDE_PROJECT_DIR; $savedLane = $env:CURL_DARK_FACTORY_LANE
    try {
        New-Item -ItemType Directory -Force -Path $ciTemp | Out-Null
        git init -q --bare -b work $ciOrigin 2>&1 | Out-Null
        git clone -q $ciOrigin $ciRepo 2>&1 | Out-Null
        git -C $ciRepo config user.name t; git -C $ciRepo config user.email t@t
        git -C $ciRepo checkout -q -b work 2>&1 | Out-Null
        foreach ($state in 'Doing', 'Backlog', 'Blocked', 'Deferred', 'Done') { New-Item -ItemType Directory -Force -Path (Join-Path $ciRepo "Tasks\$state") | Out-Null; Set-Content -Path (Join-Path $ciRepo "Tasks\$state\.gitkeep") -Value '' }
        Copy-Item -Recurse -Path (Join-Path $savedRoot '.claude\skills\task-board') -Destination (New-Item -ItemType Directory -Force -Path (Join-Path $ciRepo '.claude\skills')).FullName
        $ciTask = {
            param([string]$Id, [string]$State, [string]$Title)
            Set-Content -Path (Join-Path $ciRepo "Tasks\$State\$Id-task.md") -Encoding UTF8 -Value ((@('---', "id: $Id", "title: $Title", 'priority: High', 'assignee: Claude', 'pipeline: feature', 'depends-on: []',
                'touches: [Curl.slnx]', 'requirement: none', 'created: 2026-10-21', 'completed:', '---', "# $Id - $Title", '', '## Goal', '', 'x', '', '## Context', '', 'x', '',
                '## Acceptance criteria', '', '- [ ] x', '', '## Notes', '', '## Log', '', '- 2026-10-21: Created.')) -join "`n")
        }
        & $ciTask 'BL-001' 'Backlog' 'Fix CI failure job quality step Measure code quality on quality'
        & $ciTask 'BL-002' 'Backlog' 'Fix flaky CI test A_B that failed once on Linux'
        & $ciTask 'BL-003' 'Doing' 'Fix CI failure C_D on Linux'
        & $ciTask 'BL-004' 'Backlog' 'Change one file'
        git -C $ciRepo add -A 2>&1 | Out-Null; git -C $ciRepo commit -q -m 'file tasks' 2>&1 | Out-Null
        $sha1 = "$(git -C $ciRepo rev-parse HEAD)".Trim()
        & $ciTask 'BL-005' 'Backlog' 'Fix CI build error CS1002 in X.cs on macOS'
        git -C $ciRepo add -A 2>&1 | Out-Null; git -C $ciRepo commit -q -m 'file BL-005' 2>&1 | Out-Null
        git -C $ciRepo push -q origin work 2>&1 | Out-Null
        $sha2 = "$(git -C $ciRepo rev-parse HEAD)".Trim()
        $green = @([pscustomobject]@{ Id = '21'; Sha = $sha1; FinishedAt = (Get-Date); Failures = @() })
        $red = @([pscustomobject]@{ Id = '22'; Sha = $sha2; FinishedAt = (Get-Date); Failures = @([pscustomobject]@{ Key = 'A_B'; Kind = 'test' }) }) + $green
        & $check 'a green run defers the Backlog CI-fix and flaky tasks it contains, not the Doing one, another task or one filed after it' 'BL-001,BL-002' ((@(Get-FixedCiTaskIds -Repo $ciRepo -Runs $green)) -join ',')
        & $check 'a still-red newest run defers nothing' '' ((@(Get-FixedCiTaskIds -Repo $ciRepo -Runs $red)) -join ',')
        $script:Root = $ciRepo; $script:CiWatchDir = Join-Path $ciTemp 'ci-watch'; $script:LanesDir = $ciTemp; $env:CURL_DARK_FACTORY_LANE = '0'
        Move-FixedCiTasksToDeferred -Branch 'work' -Runs $red
        Move-FixedCiTasksToDeferred -Branch 'work' -Runs $green
        $pushed = @(git -C $ciOrigin ls-tree -r --name-only work -- Tasks 2>$null | Where-Object { $_ -match 'BL-00\d' } | ForEach-Object { $_ -replace '^Tasks/(\w+)/(BL-\d+).*$', '$2 $1' } | Sort-Object) -join ', '
        & $check 'the watch pushes the deferral; the Doing task and a task filed after the green commit stay' 'BL-001 Deferred, BL-002 Deferred, BL-003 Doing, BL-004 Backlog, BL-005 Backlog' $pushed
        $reason = "$(git -C $ciOrigin show "work:Tasks/Deferred/BL-001-task.md" 2>$null)"
        & $check 'the deferral says why' 'True' "$($reason -match "already fixed, CI green on $($sha1.Substring(0, 9))")"
    } finally {
        $script:Root = $savedRoot; $script:CiWatchDir = $savedWatchDir; $script:LanesDir = $savedLanesDir; $env:CLAUDE_PROJECT_DIR = $savedDir; $env:CURL_DARK_FACTORY_LANE = $savedLane
        Remove-Item -Recurse -Force -Path $ciTemp -ErrorAction SilentlyContinue
    }
    # BL-3144: a shift handed over during the merge's CI wait leaves the next shift's ci-watch worktree alone.
    $ciMarker = Join-Path ([IO.Path]::GetTempPath()) "df-ci-handover-$PID"
    New-Item -ItemType Directory -Force -Path $ciMarker | Out-Null
    $savedWatchDir2 = $CiWatchDir; $script:CiWatchDir = $ciMarker
    try { & $check 'a handed-over shift runs no final watch and removes no worktree' 'False,True' ((Invoke-MergeEndCiWatch -Branch 'none' -HandedOver $true), (Test-Path $ciMarker) -join ',') }
    finally { $script:CiWatchDir = $savedWatchDir2; Remove-Item -Recurse -Force -Path $ciMarker -ErrorAction SilentlyContinue }
    $script:CiWatch.AuditTasks = @{ 'BL-2001' = "Revert the dark factory's change to Audit/Findings/x.md" }
    & $check 'end report names an open audit task' "BL-2001 AUDIT    needs an interactive session: Revert the dark factory's change to Audit/Findings/x.md" `
        ((@(Get-OpenAuditTaskLines) | Where-Object { $_ -like 'BL-2001 *' }) -join '|')
    # BL-3454: a failed job of the run still running is read before the run ends.
    $listedRunning = @([pscustomobject]@{ databaseId = 77; status = 'in_progress'; conclusion = ''; headSha = 'abcdef1234' })
    $jobsJson = '{"jobs":[{"databaseId":501,"name":"Build and test (ubuntu-latest)","status":"completed","conclusion":"failure","completedAt":"2026-10-23T22:50:02Z"},{"databaseId":502,"name":"Quality gates (windows-latest)","status":"in_progress","conclusion":"","completedAt":"0001-01-01T00:00:00Z"},{"databaseId":503,"name":"Build and test (macos-latest)","status":"completed","conclusion":"success","completedAt":"2026-10-23T22:50:33Z"}]}'
    $jobLog = { param([string]$Id) if ($Id -ne '501') { return $null }; return , @("$([char]0xFEFF)2026-10-23T22:43:43.9183474Z Current runner version", '2026-10-23T22:48:58.5989616Z   Failed test1173: Man page syntax checks [40 s]', '2026-10-23T22:48:58.6Z   Error Message:', '2026-10-23T22:48:58.7Z    man page broke') }
    $early = Get-CiRunningRun -Listed $listedRunning -ListJobs { param($Id) $jobsJson } -ReadJobLog $jobLog
    & $check 'a running run with a failed job is read now' "77|abcdef1234|test1173:|Linux|man page broke|$(([datetime]'2026-10-23T22:50:02Z').ToUniversalTime().ToString('HH:mm'))" `
        "$($early.Id)|$($early.Sha)|$(($early.Failures | ForEach-Object { "$($_.Key)|$($_.Platforms -join '+')|$($_.Message)" }) -join ';')|$($early.FinishedAt.ToUniversalTime().ToString('HH:mm'))"
    $readOnce = { param([string]$Id) $script:flakyReads++; if ($script:flakyReads -eq 1) { return $null }; return , @('2026-10-23T22:48:58.5Z   Failed test1173: Man page syntax checks [40 s]', '2026-10-23T22:48:58.6Z   Error Message:', '2026-10-23T22:48:58.7Z    man page broke') }
    $script:flakyReads = 0; $listedRetry = @([pscustomobject]@{ databaseId = 78; status = 'in_progress'; conclusion = ''; headSha = 'abcdef1234' })
    $firstTry = Get-CiRunningRun -Listed $listedRetry -ListJobs { param($Id) $jobsJson } -ReadJobLog $readOnce
    $secondTry = Get-CiRunningRun -Listed $listedRetry -ListJobs { param($Id) $jobsJson } -ReadJobLog $readOnce
    & $check 'a failed job log that could not be read is read again, not cached' 'True|test1173:' "$($null -eq $firstTry)|$(@($secondTry.Failures)[0].Key)"
    $neverRead = { param([string]$Id) return $null }
    $listedUnread = @([pscustomobject]@{ databaseId = 79; status = 'in_progress'; conclusion = ''; headSha = 'abcdef1234' })
    $tracedBefore = $traced.Count
    $null = Get-CiRunningRun -Listed $listedUnread -ListJobs { param($Id) $jobsJson } -ReadJobLog $neverRead
    $tracedFirst = $traced.Count - $tracedBefore
    $null = Get-CiRunningRun -Listed $listedUnread -ListJobs { param($Id) $jobsJson } -ReadJobLog $neverRead
    & $check 'a failed job log that stays unreadable is traced on the first call only' '1|1|False' "$tracedFirst|$($traced.Count - $tracedBefore)|$($traced -like '*has failed jobs but no failing*' -as [bool])"
    & $check 'a running run with no failed job is not read' 'True' "$($null -eq (Get-CiRunningRun -Listed $listedRunning -ListJobs { param($Id) '{"jobs":[{"databaseId":9,"name":"x","status":"in_progress","conclusion":""}]}' } -ReadJobLog $jobLog))"
    & $check 'no running run, nothing read' 'True' "$($null -eq (Get-CiRunningRun -Listed @([pscustomobject]@{ databaseId = 1; status = 'completed'; conclusion = 'failure'; headSha = 'x' }) -ListJobs { param($Id) throw 'not called' } -ReadJobLog $jobLog))"
    # BL-3456: a running run without the CI fix a waiting run carries is cancelled, only while CI is red.
    $fixTemp = Join-Path ([IO.Path]::GetTempPath()) "df-ci-cancel-$PID"
    try {
        New-Item -ItemType Directory -Force -Path (Join-Path $fixTemp 'Tasks\Done\2026-10-22') | Out-Null
        git init -q -b work $fixTemp 2>&1 | Out-Null
        git -C $fixTemp config user.email t@t; git -C $fixTemp config user.name t
        Set-Content -Path (Join-Path $fixTemp 'Tasks\Done\2026-10-22\BL-0001-fix-ci-failure-old-on-linux.md') -Value 'old'
        git -C $fixTemp add -A 2>&1 | Out-Null; git -C $fixTemp commit -q -m one 2>&1 | Out-Null
        $shaRunning = "$(git -C $fixTemp rev-parse HEAD)".Trim()
        New-Item -ItemType Directory -Force -Path (Join-Path $fixTemp 'Tasks\Done\2026-10-23') | Out-Null
        git -C $fixTemp mv Tasks/Done/2026-10-22/BL-0001-fix-ci-failure-old-on-linux.md Tasks/Done/2026-10-23/BL-0001-fix-ci-failure-old-on-linux.md 2>&1 | Out-Null
        Set-Content -Path (Join-Path $fixTemp 'Tasks\Done\BL-0002-add-a-feature.md') -Value 'feature'
        git -C $fixTemp add -A 2>&1 | Out-Null; git -C $fixTemp commit -q -m two 2>&1 | Out-Null
        $shaNoFix = "$(git -C $fixTemp rev-parse HEAD)".Trim()
        Set-Content -Path (Join-Path $fixTemp 'Tasks\Done\BL-0003-fix-ci-failure-test1173-on-linux-and-macos.md') -Value 'fix'
        git -C $fixTemp add -A 2>&1 | Out-Null; git -C $fixTemp commit -q -m three 2>&1 | Out-Null
        $shaFix = "$(git -C $fixTemp rev-parse HEAD)".Trim()
        & $check 'a CI fix reaching Done counts; an archived one and another task do not' 'BL-0003' ((Get-CiFixTasksBetween -Repo $fixTemp -From $shaRunning -To $shaFix) -join ',')
        $redRuns = @([pscustomobject]@{ Id = '70'; Sha = $shaRunning; FinishedAt = $now; Failures = @(& $f A @('Linux')) })
        $greenRuns = @([pscustomobject]@{ Id = '70'; Sha = $shaRunning; FinishedAt = $now; Failures = @() })
        $pair = { param([string]$WaitingSha, [string]$WaitingId = '79') @([pscustomobject]@{ databaseId = [long]$WaitingId; status = 'pending'; headSha = $WaitingSha }, [pscustomobject]@{ databaseId = 78; status = 'in_progress'; headSha = $shaRunning }) }
        $script:cancelledIds = @()
        $cancel = { param([string]$Id) $script:cancelledIds += $Id; return $true }
        & $check 'red, and the waiting run carries the fix: the running run is cancelled' '78' (Stop-CiRunWithoutFix -Listed (& $pair $shaFix) -Runs $redRuns -Repo $fixTemp -CancelRun $cancel)
        & $check 'the same pair is decided once' '' (Stop-CiRunWithoutFix -Listed (& $pair $shaFix) -Runs $redRuns -Repo $fixTemp -CancelRun $cancel)
        & $check 'the waiting run carries no CI fix: nothing cancelled' '' (Stop-CiRunWithoutFix -Listed (& $pair $shaNoFix '80') -Runs $redRuns -Repo $fixTemp -CancelRun $cancel)
        $noFixes = Get-CiFixTasksBetween -Repo $fixTemp -From $shaRunning -To $shaNoFix
        & $check 'no fix found is an empty array, not $null' 'True' ($null -ne $noFixes -and $noFixes.Count -eq 0).ToString()
        & $check 'git failing is $null' 'True' ($null -eq (Get-CiFixTasksBetween -Repo $fixTemp -From 'nosuchcommit' -To $shaNoFix)).ToString()
        $script:gitCalls = 0
        function global:git { $script:gitCalls++; & (Get-Command git -CommandType Application | Select-Object -First 1) @args }
        try { & $check 'a pair with no fix is decided once: the second call runs no git' '' (Stop-CiRunWithoutFix -Listed (& $pair $shaNoFix '80') -Runs $redRuns -Repo $fixTemp -CancelRun $cancel) }
        finally { Remove-Item function:\global:git -ErrorAction SilentlyContinue }
        & $check 'no git ran for the decided pair' '0' "$script:gitCalls"
        & $check 'green: nothing cancelled, the red spell would be hidden' '' (Stop-CiRunWithoutFix -Listed (& $pair $shaFix '81') -Runs $greenRuns -Repo $fixTemp -CancelRun $cancel)
        & $check 'no waiting run: nothing cancelled' '' (Stop-CiRunWithoutFix -Listed @((& $pair $shaFix '82')[1]) -Runs $redRuns -Repo $fixTemp -CancelRun $cancel)
        & $check 'gh cancelled only run 78' '78' ($script:cancelledIds -join ',')
    } finally { Remove-Item -Recurse -Force -Path $fixTemp -ErrorAction SilentlyContinue }
    exit $(if ($script:ciTestFailed) { 1 } else { 0 })
}

# This file ran to its end; RunDarkFactory.ps1 goes on to the next one.
$AreaFileFinished = $true
