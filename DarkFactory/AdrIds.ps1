# Renumbering the ADRs a lane added that another lane's ADR already numbers, and
# -TestAdrIds. Dot-sourced by RunDarkFactory.ps1 in its order; see the loader there.

# Each lane picks the next ADR number from its own worktree, as task IDs once did, so two
# lanes can add the same one (BL-3834: 14 numbers shared on 2026-10-26). The lane that
# integrates second renumbers its own, as `task-board.ps1 dedupe` does for tasks.

$AdrFolder = 'Documentation/Planning/Decisions'

function Get-AddedLineNumbers {
    # The line numbers (1-based) of -Path that the commits between -Base and HEAD added.
    param([string]$Repo, [string]$Base, [string]$Path)
    $numbers = [System.Collections.Generic.HashSet[int]]::new()
    foreach ($line in @(git -C $Repo diff --unified=0 --no-renames $Base HEAD -- $Path)) {
        if ("$line" -match '^@@ -\S+ \+(\d+)(?:,(\d+))? @@') {
            $count = if ($Matches[2] -ne $null -and $Matches[2] -ne '') { [int]$Matches[2] } else { 1 }
            for ($i = 0; $i -lt $count; $i++) { [void]$numbers.Add([int]$Matches[1] + $i) }
        }
    }
    return , $numbers
}

function Invoke-AdrRenumber {
    # Renumbers every ADR file the commits between -Base and HEAD of -Repo added whose number
    # another file already has: a file at -Base (which keeps it, even when it shares the number
    # with another file there; BL-3835 cleans that up) or an earlier-named file this lane added.
    # It gets the next number above every ADR in the tree; its file name changes, and so does
    # every reference to the old number on a line this lane added (its heading, the README row,
    # a task citing it). Changes are left staged-and-committed in one commit. Returns the
    # "ADR-0900 -> ADR-0901  <file>" lines, @() when nothing needed renumbering; throws, saying
    # why, when git or a file fails, so the caller can stop the push (BL-3491 style).
    param([string]$Repo, [string]$Base)
    $dir = Join-Path $Repo $AdrFolder
    if (-not (Test-Path -LiteralPath $dir)) { return @() }
    $pattern = '^ADR-(\d{4})-.+\.md$'
    $added = @(git -C $Repo diff --name-only --diff-filter=A --no-renames $Base HEAD -- $AdrFolder 2>$null)
    if ($LASTEXITCODE -ne 0) { throw "git diff against $Base failed" }
    $atBase = @(git -C $Repo ls-tree --name-only -r $Base -- $AdrFolder)
    if ($LASTEXITCODE -ne 0) { throw "git ls-tree of $Base failed" }
    $taken = [System.Collections.Generic.HashSet[int]]::new()
    foreach ($path in $atBase) { if ((Split-Path -Leaf $path) -match $pattern) { [void]$taken.Add([int]$Matches[1]) } }
    $max = 0
    foreach ($file in Get-ChildItem -LiteralPath $dir -File) { if ($file.Name -match $pattern) { $max = [Math]::Max($max, [int]$Matches[1]) } }
    $byOld = @{}
    $files = @{}
    foreach ($path in @($added | Sort-Object { Split-Path -Leaf $_ })) {
        $name = Split-Path -Leaf $path
        if ($name -notmatch $pattern) { continue }
        $number = [int]$Matches[1]
        if ($taken.Add($number)) { continue }
        $max++
        $byOld[$number] = $max
        $files[$name] = $max
    }
    if (-not $byOld.Count) { return @() }
    $format = { param([int]$n) 'ADR-{0:D4}' -f $n }
    $lines = @()
    foreach ($name in $files.Keys) {
        $old = [int]($name.Substring(4, 4))
        $new = $files[$name]
        $newName = 'ADR-{0:D4}{1}' -f $new, $name.Substring(8)
        git -C $Repo mv "$AdrFolder/$name" "$AdrFolder/$newName" 2>&1 | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "git mv of $name failed" }
        $lines += "$(& $format $old) -> $(& $format $new)  $newName"
    }
    # A number is renumbered once, so one pass over each line with the whole map never rewrites a number twice.
    $changed = @(git -C $Repo diff --name-only --diff-filter=AM --no-renames $Base HEAD)
    if ($LASTEXITCODE -ne 0) { throw "git diff --name-only against $Base failed" }
    foreach ($path in $changed) {
        # A file this lane added under the old name was renamed above; read it under the new one.
        $leaf = Split-Path -Leaf $path
        $current = $path
        if ($path.StartsWith("$AdrFolder/") -and $files.ContainsKey($leaf)) { $current = "$AdrFolder/ADR-{0:D4}{1}" -f $files[$leaf], $leaf.Substring(8) }
        $full = Join-Path $Repo $current
        if (-not (Test-Path -LiteralPath $full -PathType Leaf)) { continue }
        $mine = Get-AddedLineNumbers $Repo $Base $path
        if (-not $mine.Count) { continue }
        $text = [IO.File]::ReadAllText($full)
        $rows = $text -split "`n"
        $touched = $false
        for ($i = 0; $i -lt $rows.Count; $i++) {
            if (-not $mine.Contains($i + 1)) { continue }
            $row = [regex]::Replace($rows[$i], 'ADR-(\d{4})(?!\d)|\[(\d{4})\](?=\(ADR-)', {
                    param($m)
                    $digits = if ($m.Groups[1].Success) { $m.Groups[1].Value } else { $m.Groups[2].Value }
                    $n = [int]$digits
                    if ($byOld.ContainsKey($n)) { return $m.Value.Replace($digits, ('{0:D4}' -f $byOld[$n])) }
                    return $m.Value
                })
            if ($row -cne $rows[$i]) { $rows[$i] = $row; $touched = $true }
        }
        if ($touched) { [IO.File]::WriteAllText($full, ($rows -join "`n"), [Text.UTF8Encoding]::new($false)) }
    }
    git -C $Repo add -A -- $AdrFolder Tasks 2>&1 | Out-Null
    foreach ($path in $changed) { git -C $Repo add -A -- $path 2>&1 | Out-Null }
    git -C $Repo commit -q -m "chore(adr): renumber ADRs another lane took first`n`n$($lines -join "`n")" 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'git commit of the renumbered ADRs failed' }
    return $lines
}

if ($TestAdrIds) {
    $temp = Join-Path ([IO.Path]::GetTempPath()) "df-adr-ids-$PID"
    $origin = Join-Path $temp 'origin.git'
    New-Item -ItemType Directory -Force -Path $temp | Out-Null
    git init -q --bare -b work $origin 2>&1 | Out-Null
    $clone = {
        param([string]$Name)
        $dir = Join-Path $temp $Name
        git clone -q $origin $dir 2>&1 | Out-Null
        git -C $dir config user.name t; git -C $dir config user.email t@t
        git -C $dir checkout -q -B work 2>&1 | Out-Null
        return $dir
    }
    $write = { param($Dir, $Rel, $Text) $p = Join-Path $Dir $Rel; New-Item -ItemType Directory -Force -Path (Split-Path $p) | Out-Null; [IO.File]::WriteAllText($p, $Text.Replace("`r`n", "`n")) }
    $commit = { param($Dir, $Msg) git -C $Dir add -A 2>&1 | Out-Null; git -C $Dir commit -q -m $Msg 2>&1 | Out-Null }
    $seed = & $clone 'seed'
    & $write $seed "$AdrFolder/ADR-0850-base-a.md" "# ADR-0850 - base a`nbody`n"
    & $write $seed "$AdrFolder/ADR-0850-base-b.md" "# ADR-0850 - base b`nbody`n"
    & $write $seed "$AdrFolder/ADR-0899-last.md" "# ADR-0899 - last`nbody`n"
    & $write $seed "$AdrFolder/README.md" "| head |`n| [0899](ADR-0899-last.md) | last |`nm1`nm2`nm3`nm4`nend`n"
    & $commit $seed 'seed'
    git -C $seed push -q origin work 2>&1 | Out-Null
    $first = & $clone 'first'
    $second = & $clone 'second'
    $third = & $clone 'third'
    & $write $first "$AdrFolder/ADR-0900-first.md" "# ADR-0900 - first`nbody`n"
    & $write $first "$AdrFolder/README.md" "| head |`n| [0899](ADR-0899-last.md) | last |`n| [0900](ADR-0900-first.md) | first |`nm1`nm2`nm3`nm4`nend`n"
    & $write $first 'Tasks/Doing/BL-1-first.md' "Notes: see ADR-0900.`n"
    & $commit $first 'first lane'
    git -C $first push -q origin work 2>&1 | Out-Null
    & $write $second "$AdrFolder/ADR-0900-second.md" "# ADR-0900 - second`nSee ADR-0850 and ADR-0900.`n"
    & $write $second "$AdrFolder/README.md" "| head |`n| [0899](ADR-0899-last.md) | last |`nm1`nm2`nm3`nm4`nend`n| [0900](ADR-0900-second.md) | second |`n"
    & $write $second 'Tasks/Doing/BL-2-second.md' "Notes: ADR-0900-second.md and ADR-0850.`n"
    & $commit $second 'second lane'
    # The third lane adds an ADR at a number base already shares twice, edits one of the two
    # (which it does not own) and adds two files numbered 0905.
    & $write $third "$AdrFolder/ADR-0850-third.md" "# ADR-0850 - third`nbody`n"
    & $write $third "$AdrFolder/ADR-0850-base-a.md" "# ADR-0850 - base a`nbody`nedited`n"
    & $write $third "$AdrFolder/ADR-0905-x.md" "# ADR-0905 - x`n"
    & $write $third "$AdrFolder/ADR-0905-y.md" "# ADR-0905 - y`n"
    & $commit $third 'third lane'
    git -C $second fetch -q origin work 2>&1 | Out-Null
    git -C $second rebase -q origin/work 2>&1 | Out-Null
    $secondLines = @(Invoke-AdrRenumber -Repo $second -Base 'origin/work')
    git -C $third fetch -q origin work 2>&1 | Out-Null
    git -C $third rebase -q origin/work 2>&1 | Out-Null
    $thirdLines = @(Invoke-AdrRenumber -Repo $third -Base 'origin/work')
    $noneLines = @(Invoke-AdrRenumber -Repo $first -Base 'origin/work~1')
    $failure = ''
    try { Invoke-AdrRenumber -Repo $second -Base 'no-such-ref' | Out-Null } catch { $failure = "$_" }
    $read = { param($Dir, $Rel) $p = Join-Path $Dir $Rel; if (Test-Path -LiteralPath $p) { ([IO.File]::ReadAllText($p) -replace "`r`n", "`n") } else { 'MISSING' } }
    $secondNames = @(Get-ChildItem (Join-Path $second $AdrFolder) -Name | Where-Object { $_ -match '^ADR-09' }) -join ','
    $thirdNames = @(Get-ChildItem (Join-Path $third $AdrFolder) -Name | Where-Object { $_ -match '^ADR-(09|0850)' }) -join ','
    $cases = @(
        ,@('the later lane renumbers its ADR to the next free number', 'ADR-0900-first.md,ADR-0901-second.md', $secondNames)
        ,@('the trace line names old, new and file', 'ADR-0900 -> ADR-0901  ADR-0901-second.md', ($secondLines -join '|'))
        ,@('its heading and own citations say the new number, other ADRs keep theirs', "# ADR-0901 - second`nSee ADR-0850 and ADR-0901.`n", (& $read $second "$AdrFolder/ADR-0901-second.md"))
        ,@('its citing task says the new number', "Notes: ADR-0901-second.md and ADR-0850.`n", (& $read $second 'Tasks/Doing/BL-2-second.md'))
        ,@('the README keeps the other lane row and renumbers its own', "| head |`n| [0899](ADR-0899-last.md) | last |`n| [0900](ADR-0900-first.md) | first |`nm1`nm2`nm3`nm4`nend`n| [0901](ADR-0901-second.md) | second |`n", (& $read $second "$AdrFolder/README.md"))
        ,@('the first lane ADR is unchanged', "# ADR-0900 - first`nbody`n", (& $read $second "$AdrFolder/ADR-0900-first.md"))
        ,@('an ADR at a base number shared twice is renumbered, the base ones are not, and a later file of the lane with the same number too', 'ADR-0850-base-a.md,ADR-0850-base-b.md,ADR-0900-first.md,ADR-0905-x.md,ADR-0906-third.md,ADR-0907-y.md', $thirdNames)
        ,@('an edited base ADR is not renumbered', "# ADR-0850 - base a`nbody`nedited`n", (& $read $third "$AdrFolder/ADR-0850-base-a.md"))
        ,@('nothing added means nothing renumbered', '0', "$($noneLines.Count)")
        ,@('a failing git stops with a reason', 'True', "$($failure -match 'failed')")
    )
    Remove-Item -LiteralPath $temp -Recurse -Force -ErrorAction SilentlyContinue
    $failed = 0
    foreach ($case in $cases) {
        if ($case[1] -ceq $case[2]) { Write-Host "PASS $($case[0])" -ForegroundColor Green }
        else { Write-Host "FAIL $($case[0]): expected $($case[1]), got $($case[2])" -ForegroundColor Red; $failed++ }
    }
    exit $(if ($failed) { 1 } else { 0 })
}

# This file ran to its end; RunDarkFactory.ps1 goes on to the next one.
$AreaFileFinished = $true
