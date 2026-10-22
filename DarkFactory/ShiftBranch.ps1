# Putting the checkout back on the shift's branch, and -TestShiftBranch. Dot-sourced by
# RunDarkFactory.ps1 in its order; see the loader there.

function Restore-ShiftBranch {
    # Switches -Repo back to -Branch when something else checked out another branch under
    # a running shift (BL-809). Returns '' when it is on -Branch, or why it was left alone.
    param([string]$Branch, [string]$Repo = $Root)
    $current = "$(git -C $Repo rev-parse --abbrev-ref HEAD 2>$null)".Trim()
    if ($current -eq $Branch) { return '' }
    if (@(git -C $Repo status --porcelain) | Where-Object { $_ }) {
        return "checkout is on $current, not $Branch, and has uncommitted changes; left alone"
    }
    git -C $Repo switch -q $Branch 2>&1 | Out-Null
    $now = "$(git -C $Repo rev-parse --abbrev-ref HEAD 2>$null)".Trim()
    if ($now -ne $Branch) { return "checkout is on $current and could not be switched to $Branch" }
    Write-Trace '-' 'branch' "checkout was on $current; switched back to $Branch" 'Yellow'
    return ''
}

if ($TestShiftBranch) {
    $repo = Join-Path ([IO.Path]::GetTempPath()) "df-shift-branch-$PID"
    New-Item -ItemType Directory -Force -Path $repo | Out-Null
    git -C $repo init -q -b master 2>&1 | Out-Null
    git -C $repo -c user.name=t -c user.email=t@t commit -q --allow-empty -m one 2>&1 | Out-Null
    git -C $repo branch work 2>&1 | Out-Null
    $failed = 0
    $cases = @(
        ,@('clean switch-back', "'' on work", { $r = Restore-ShiftBranch -Branch work -Repo $repo; "'$r' on $((git -C $repo rev-parse --abbrev-ref HEAD).Trim())" })
        ,@('already on it', "'' on work", { $r = Restore-ShiftBranch -Branch work -Repo $repo; "'$r' on $((git -C $repo rev-parse --abbrev-ref HEAD).Trim())" })
        ,@('dirty refusal', "left alone on master", {
            git -C $repo switch -q master 2>&1 | Out-Null
            Set-Content -Path (Join-Path $repo 'x.txt') -Value 'x'
            $r = Restore-ShiftBranch -Branch work -Repo $repo
            "$(if ($r -match 'left alone$') { 'left alone' } else { $r }) on $((git -C $repo rev-parse --abbrev-ref HEAD).Trim())" }))
    foreach ($case in $cases) {
        $got = & $case[2]
        if ($case[1] -ceq $got) { Write-Host "PASS $($case[0]): $got" -ForegroundColor Green }
        else { Write-Host "FAIL $($case[0]): expected $($case[1]), got $got" -ForegroundColor Red; $failed++ }
    }
    Remove-Item -Recurse -Force -Path $repo -ErrorAction SilentlyContinue
    exit $(if ($failed) { 1 } else { 0 })
}

# This file ran to its end; RunDarkFactory.ps1 goes on to the next one.
$AreaFileFinished = $true
