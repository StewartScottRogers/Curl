---
id: BL-992
title: Accept four-digit task IDs wherever RunDarkFactory.ps1 reads the board
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-09-29
completed: 2026-09-29
---
# BL-992 — Accept four-digit task IDs wherever RunDarkFactory.ps1 reads the board

## Goal

The dark factory claims, requeues and continues on tasks numbered `BL-1000` and above exactly as it does on `BL-999` and below.

## Context

`task-board.ps1` numbers tasks with `'BL-{0:D3}'`, so the ID after `BL-999` is `BL-1000`
(it was `BL-992` when this task was filed, and 30 tasks were filed with it). Five regexes in
`RunDarkFactory.ps1` expect exactly three digits followed by whitespace, so they silently
fail to match a four-digit ID:

- line ~1560 (`Invoke-Requeue`'s status reading): `'^\s+(BL-\d{3})\s.*\[needs Stewart\]'`
- line ~1581: `[regex]::Matches($reason, 'BL-\d{3}')` - matches `BL-100` inside `BL-1003`, a wrong ID
- line ~2435 (`Invoke-Claim`): `'(?m)^(BL-\d{3})\s'` - `next` printing `BL-1003  pipeline: ...` looks like "nothing ready", so a lane waits or ends its shift with work ready
- line ~3227 (`-Continuous`): `'(?m)^BL-\d{3}\s'`
- line ~3329 (single-lane loop): `'(?m)^(BL-\d{3})\s'`

Line numbers drift; search the file for `BL-\d{3}`. The fix is `BL-\d+` (or `BL-\d{3,}`)
in every one, plus a self-test. The script already has self-test switches of the form
`-TestCiWatch`, `-TestRestart` that print `PASS`/`FAIL` lines; follow that pattern.

## Acceptance criteria

- [x] `Select-String -Path RunDarkFactory.ps1 -SimpleMatch -Pattern 'BL-\d{3}'` finds no match.
- [x] A new switch `-TestTaskIds` prints one `PASS` line per case and no `FAIL` line for: `next` output `BL-1003  pipeline: direct  Tasks\Backlog\BL-1003-x.md` yields `BL-1003`; `BL-992  pipeline: ...` yields `BL-992`; a `-Reason` naming `BL-1003 and BL-999` yields exactly those two IDs; a status line `  BL-1005 Normal Stewart Title  [needs Stewart]` yields `BL-1005`.
- [x] Every existing `-Test*` switch in the script still prints no `FAIL` line.
- [x] The switch is listed in the script's comment-based help beside the other `-Test*` switches.

## Notes

- Every ID reading now goes through four small functions beside `Get-LastLogLine`:
  `Get-NextTaskId` (`next` output, used by `Invoke-Claim`, `-Continuous` and the
  single-lane loop), `Get-TaskIdsNamed` (`Invoke-Requeue`'s reason, `\bBL-\d+\b` so
  `BL-1003` is never read as `BL-100`), `Get-NeedsStewartId` (status lines) and
  `Get-TaskIdFromFileName`.
- Found beyond the five listed regexes: three `$f.Name.Substring(0, 6)` calls
  (`Get-WaitingOnStewart`, `Invoke-Requeue`, the shift's adoption of stopped lanes) cut
  `BL-1003-x.md` to `BL-100`. Same file and same goal, so fixed here with
  `Get-TaskIdFromFileName`; `-TestTaskIds` covers it with a sixth case.
- `-TestTaskIds` also checks that `No task is ready.` yields no ID.
- Checked with `-TestTaskIds`, `-TestAutoLanes`, `-TestMachineProbe`, `-TestFlakyTests`,
  `-TestShiftBranch`, `-TestCiWatch`, `-TestHeartbeat` and `-TestRestart`: all exit 0,
  no `FAIL`. `-TestAlarm` and `-TestOutOfTokens` print no PASS/FAIL lines and sound the
  alarm and notices, so they were not run unattended; neither reads a task ID.
- `dotnet build` clean; fast tests 20,977 passed across 33 test projects.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. RunDarkFactory.ps1 claims, requeues and continues on BL-1000 and above; -TestTaskIds proves it
