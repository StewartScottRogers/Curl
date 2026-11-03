---
id: BL-4785
title: Stop the integration quality-gate check passing when it cannot measure or commit
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [DarkFactory/Integrate.ps1]
requirement: none
created: 2026-11-03
completed:
---
# BL-4785 — Stop the integration quality-gate check passing when it cannot measure or commit

## Goal

The lane's quality-gate check before integrating (BL-4649) never reports "gates met" when it could not list the changed files, read the report or commit the repair, and its trace says why.

## Context

Found by the shift-end review of 2026-11-03 (silent-failure-hunter, Important), in the shift merged at 7410f102c. All lines are in `DarkFactory/Integrate.ps1`:

- `Test-QualityGates` ~line 153: `catch { return '' }` treats a truncated or corrupt JSON report as "gates met", with no trace.
- ~line 139: `git diff --name-only "$Base...HEAD" 2>$null` ignores `$LASTEXITCODE`. A failed diff (a bad `FETCH_HEAD`, say) gives no files, which returns `''`, meaning "no gated library changed".
- ~line 148: the default `$Measure` pipes all of `Measure-CodeQuality.ps1`'s output to `Out-Null`, so when the measure writes no report the trace cannot say why.
- `Repair-QualityGates` ~lines 166-169: the results of `Invoke-Git add` and `commit` are dropped. If the commit fails, the next `Test-QualityGates` measures the working tree, which holds the fix, so it passes. The lane then integrates a HEAD without the fix.
- Each of these lets a lane push code that CI's quality job turns red, which BL-4649 / AF-0543 was meant to stop.

## Acceptance criteria

- [ ] A failed `git diff` in `Test-QualityGates` returns a non-empty reason, not `''`.
- [ ] An unreadable report returns a non-empty reason and writes a trace line.
- [ ] When the measure writes no report, the trace carries the last lines of its output.
- [ ] `Repair-QualityGates` returns a reason when `add` or `commit` fails.
- [ ] `-TestIntegrateLock` (or another `-Test*` self-test) covers each case.

## Notes

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: d6bb2f4f 3d5d9b50 bb98ace9 19b6af10 c809f201.
