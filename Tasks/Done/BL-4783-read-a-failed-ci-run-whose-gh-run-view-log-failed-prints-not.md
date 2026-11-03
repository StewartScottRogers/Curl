---
id: BL-4783
title: Read a failed CI run whose gh run view --log-failed prints nothing as an empty log, not an unreadable one, so the CI watch files or traces it
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [DarkFactory/CiWatch.ps1]
lane: no
requirement: none
created: 2026-11-03
completed: 2026-11-03
---
# BL-4783 — Read a failed CI run whose gh run view --log-failed prints nothing as an empty log, not an unreadable one, so the CI watch files or traces it

## Goal

A failed CI run whose `gh run view --log-failed` exits 0 and prints nothing is read by the CI watch as an empty log, so it reaches the AF-0178 path (filed by the run) or the BL-3944 path (runner capacity, nothing to file) instead of being traced "cannot read its log" on every heartbeat.

## Context

Found while measuring BL-4647/BL-4715/BL-4778 over the shifts of 2026-11-03 13:46-16:14 MST. Runs 41688300433, 41715208450 and 41723085402 failed with no failed step: the `Audit paths untouched by the dark factory` job ran past its 5-minute `timeout-minutes` in `actions/checkout`, or the macOS `Build and test` job was "not acquired by Runner of type hosted". `gh run view <id> --log-failed` exits 0 with no output for them. The coordinator logged "failure on <sha>: cannot read its log with gh run view --log-failed; trying again next heartbeat" for each (DarkFactory-20261103-134646.log 13:51, DarkFactory-20261103-145254.log 14:58, 14:59, 15:22) and never classified them, and 41660275371, 41669804192 and 41671248257 had stayed unread since 08:25.

Cause: `Get-CiRuns`' default `-ReadLog` (DarkFactory/CiWatch.ps1) assigned an `if` statement's output, `$log = if ($JobId) { @(...) } else { @(...) }`. A statement's output is unrolled, so an empty array became `$null`, and `return , $null` read as "unreadable". The `-TestCiWatch` cases for AF-0178 and BL-3944 pass a stand-in `-ReadLog { , @() }`, so the default reader was never exercised.

## Acceptance criteria

- [x] `Read-CiRunLog` (DarkFactory/CiWatch.ps1) returns an empty array, not `$null`, when gh exits 0 with no output, `$null` when gh exits non-zero, and the lines otherwise; `Get-CiRuns`' default `-ReadLog` uses it.
- [x] `-TestCiWatch` has a case for each of those three answers through a stand-in gh, and passes under Windows PowerShell 5.1 and PowerShell 7.

## Notes

- Fix: `Read-CiRunLog` (DarkFactory/CiWatch.ps1) assigns `$log` inside each branch and takes `-Gh`, a stand-in for gh whose default keeps `2>$null` where it was; `Get-CiRuns`' default `-ReadLog` calls it. Checked live: under the old reader runs 41723085402, 41715208450 and 41688300433 came back `$null` with gh exit 0 (Windows PowerShell 5.1, `-ErrorAction Stop`), and 41655977963 (905 lines) read fine.
- `-TestCiWatch`: 96/96 PASS under Windows PowerShell 5.1 and pwsh 7 (three new cases: empty with exit 0 is an empty array, exit 1 is ``, a job log is read by its job id).
- What it changes live: such a run now reaches `Test-CiRunWithoutRunner` (41715208450: runner capacity, nothing to file) or the AF-0178 filing by the run (41688300433, 41723085402: the audit-guard job's `actions/checkout` ran past its 5-minute `timeout-minutes`, filed as BL-4782 for ci.yml).

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: 5602f0c2 945c9c21.
- 2026-11-03: Doing -> Done. The CI watch reads a failed run whose gh run view --log-failed prints nothing as an empty log (Read-CiRunLog), so it files it by the run or traces runner capacity; -TestCiWatch 96/96 under 5.1 and 7.
