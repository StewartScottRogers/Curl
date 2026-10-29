# Proves a one-task dark factory shift runs end to end (BL-3933): a coordinator starts a lane,
# the lane claims the only ready task, runs a stand-in for claude -p that makes a trivial
# change and moves the task to Done, integrates (rebase, build, push) and the shift ends.
# Nothing reaches the real remote and no Claude tokens are spent:
#  - origin is a bare repository made here, holding a scratch board whose only open task is
#    the scratch task (every Backlog, Doing, Blocked and Deferred task is removed);
#  - a stand-in claude, first on PATH, answers every claude -p the shift starts with one
#    result event, as a headless run does (TaskRun.ps1 reads stream-json), and for the lane's
#    task prompt also commits ShiftProof.txt and moves the task to Done with the board script.
# Exits 0 when the scratch task ends in Done on origin with the stand-in's change pushed, else
# 1, printing the shift's logs. Run by .github/workflows/dark-factory-unix-shift.yml on Linux;
# runs the same way on Windows: pwsh -File DarkFactory/ShiftProof.ps1. Not dot-sourced by
# RunDarkFactory.ps1.
param(
    # Where the bare origin, the scratch checkout, its logs and lanes, and the stand-in go.
    [string]$WorkDir = (Join-Path ([IO.Path]::GetTempPath()) "df-shift-proof-$PID"),
    # Passed to RunDarkFactory.ps1 -Lanes: 2 has a coordinator start the lanes the board has
    # work for, as a real shift does.
    [string]$Lanes = '2'
)
$ErrorActionPreference = 'Stop'
$onWindows = ($PSVersionTable.PSVersion.Major -lt 6) -or [bool]$IsWindows
$psExe = if ($onWindows) { 'powershell' } else { 'pwsh' }
$source = Split-Path $PSScriptRoot -Parent
$branch = 'work/dark-factory'
$origin = Join-Path $WorkDir 'origin.git'
$repo = Join-Path $WorkDir 'Curl'
$bin = Join-Path $WorkDir 'bin'
$check = Join-Path $WorkDir 'check'

function Invoke-ProofGit {
    param([string]$Repo, [string[]]$Arguments)
    $out = & git -C $Repo @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) { throw "git $($Arguments -join ' ') failed: $($out -join ' ')" }
    return $out
}

New-Item -ItemType Directory -Force -Path $WorkDir, $bin | Out-Null
Write-Host "shift proof in $WorkDir"

# The scratch origin and checkout, on the shift branch, at the commit under test.
& git init -q --bare $origin
& git clone -q $source $repo
if ($LASTEXITCODE -ne 0) { throw "git clone $source failed" }
$sourceHead = "$(git -C $source rev-parse HEAD)".Trim()
Invoke-ProofGit $repo @('checkout', '-q', '-B', $branch, $sourceHead) | Out-Null
# A new origin, so no remote-tracking ref of the source's is left: with its master gone, the
# shift's merge to master finds nothing to merge instead of waiting for GitHub's CI.
Invoke-ProofGit $repo @('remote', 'remove', 'origin') | Out-Null
Invoke-ProofGit $repo @('remote', 'add', 'origin', $origin) | Out-Null
Invoke-ProofGit $repo @('config', 'user.name', 'dark-factory-shift-proof') | Out-Null
Invoke-ProofGit $repo @('config', 'user.email', 'shift-proof@example.invalid') | Out-Null

# The scratch board: no open task but the scratch one.
$open = @(foreach ($state in 'Backlog', 'Doing', 'Blocked', 'Deferred') { Get-ChildItem (Join-Path $repo "Tasks/$state") -Filter 'BL-*.md' -ErrorAction SilentlyContinue })
if ($open.Count) { Invoke-ProofGit $repo (@('rm', '-q', '--') + @($open | ForEach-Object { $_.FullName })) | Out-Null }
$board = Join-Path $repo '.claude/skills/task-board/task-board.ps1'
# The board script works on $env:CLAUDE_PROJECT_DIR's board when it is set, as it is inside a
# Claude session; the shift, as on a headless host, starts with it unset.
$env:CURL_DARK_FACTORY_LANE = $null
$env:CLAUDE_PROJECT_DIR = $repo
Push-Location $repo
try { $created = "$(& $psExe -NoProfile -ExecutionPolicy Bypass -File $board new -Title 'Write the shift proof stand-in change' -Pipeline direct -Touches 'ShiftProof.txt' 2>&1 | Select-Object -Last 1)".Trim() }
finally { Pop-Location; $env:CLAUDE_PROJECT_DIR = $null }
$taskFile = Get-ChildItem (Join-Path $repo 'Tasks/Backlog') -Filter 'BL-*.md' | Select-Object -First 1
if (-not $taskFile) { throw "the board script filed no scratch task: $created" }
$id = [regex]::Match($taskFile.Name, '^BL-\d+').Value
# Its criteria are ticked already: the stand-in only commits a change and moves it to Done.
$text = (Get-Content -Raw -LiteralPath $taskFile.FullName).Replace('- [ ]', '- [x]')
[IO.File]::WriteAllText($taskFile.FullName, $text)
Invoke-ProofGit $repo @('add', '-A', 'Tasks') | Out-Null
Invoke-ProofGit $repo @('commit', '-q', '-m', "chore(tasks): scratch board for the shift proof, holding only $id") | Out-Null
Invoke-ProofGit $repo @('push', '-q', '-u', 'origin', "HEAD:refs/heads/$branch") | Out-Null
Write-Host "scratch task $id on origin/$branch"

# The stand-in claude, first on PATH for the shift and everything it starts.
$standIn = Join-Path $bin 'StandInClaude.ps1'
Set-Content -LiteralPath $standIn -Encoding UTF8 -Value @'
# Stands in for claude -p in DarkFactory/ShiftProof.ps1: reads the prompt from stdin, and for a
# lane's task prompt commits ShiftProof.txt and moves the task to Done; always answers with one
# stream-json result event.
$onWindows = ($PSVersionTable.PSVersion.Major -lt 6) -or [bool]$IsWindows
# A claude session points the board script at the checkout it runs in.
$env:CLAUDE_PROJECT_DIR = (Get-Location).Path
$prompt = [Console]::In.ReadToEnd()
$result = 'OK'
if ($prompt -match 'Run /task-run (BL-\d+)\. The shift has already claimed' -and (Get-ChildItem 'Tasks/Doing' -Filter "$($Matches[1])-*.md")) {
    $id = $Matches[1]
    Set-Content -Path 'ShiftProof.txt' -Value "Written by the stand-in claude for $id."
    git add ShiftProof.txt 2>&1 | Out-Null
    git commit -q -m "chore: write the shift proof stand-in change for $id" 2>&1 | Out-Null
    & $(if ($onWindows) { 'powershell' } else { 'pwsh' }) -NoProfile -ExecutionPolicy Bypass -File '.claude/skills/task-board/task-board.ps1' move -Id $id -To Done -Reason 'the stand-in change is committed' 2>&1 | Out-Null
    git add -A Tasks 2>&1 | Out-Null
    git commit -q -m "chore(tasks): complete $id" 2>&1 | Out-Null
    $result = "FACTORY: DONE $id the stand-in change is committed"
}
$event = [ordered]@{ type = 'result'; subtype = 'success'; is_error = $false; num_turns = 1; duration_ms = 1000; total_cost_usd = 0; result = $result }
[Console]::Out.WriteLine(($event | ConvertTo-Json -Compress))
'@
if ($onWindows) {
    Set-Content -LiteralPath (Join-Path $bin 'claude.cmd') -Encoding ASCII -Value "@powershell -NoProfile -ExecutionPolicy Bypass -File `"$standIn`""
} else {
    $wrapper = Join-Path $bin 'claude'
    [IO.File]::WriteAllText($wrapper, "#!/bin/sh`nexec pwsh -NoProfile -File '$standIn'`n")
    & chmod +x $wrapper
}
$env:PATH = $bin + [IO.Path]::PathSeparator + $env:PATH
# No herdr tabs and no sound: lanes start as detached processes, as on a headless host.
$env:HERDR_ENV = $null
$env:CI = 'true'

# The shift.
Write-Host "starting a $Lanes-lane shift of one task"
& $psExe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $repo 'RunDarkFactory.ps1') -Lanes $Lanes -MaxTasks 1 -Hours 1 -TaskMinutes 30 -Model sonnet -QuietAlarm -HeartbeatMinutes 0
$shiftExit = $LASTEXITCODE
Write-Host "the shift ended, exit $shiftExit"

# What reached origin.
& git clone -q -b $branch $origin $check
$failed = 0
$done = @(Get-ChildItem (Join-Path $check 'Tasks/Done') -Recurse -Filter "$id-*.md" -ErrorAction SilentlyContinue)
$proof = Join-Path $check 'ShiftProof.txt'
$log = @(git -C $check log --format=%s "$sourceHead..HEAD")
foreach ($result in @(
        @("$id is in Done on origin/$branch", ($done.Count -eq 1)),
        @("the stand-in's change is on origin/$branch", ((Test-Path $proof) -and (Get-Content -Raw $proof) -match [regex]::Escape($id))),
        @("the stand-in's commit is on origin/$branch", [bool]($log -contains "chore: write the shift proof stand-in change for $id")),
        @("$id is no longer in Backlog or Doing on origin/$branch", -not (Get-ChildItem (Join-Path $check 'Tasks/Backlog'), (Join-Path $check 'Tasks/Doing') -Filter "$id-*.md" -ErrorAction SilentlyContinue)))) {
    if (-not $result[1]) { $failed++ }
    Write-Host "$(if ($result[1]) { 'PASS' } else { 'FAIL' }) $($result[0])" -ForegroundColor $(if ($result[1]) { 'Green' } else { 'Red' })
}
Write-Host "origin/$branch since the commit under test:`n  $($log -join "`n  ")"
if ($failed) {
    foreach ($file in Get-ChildItem "$repo.logs" -Recurse -File -Include '*.log', '*.txt', '*.jsonl' -ErrorAction SilentlyContinue) {
        Write-Host "----- $($file.FullName)"
        Get-Content -LiteralPath $file.FullName -Tail 60 | ForEach-Object { Write-Host $_ }
    }
}
exit $(if ($failed) { 1 } else { 0 })
