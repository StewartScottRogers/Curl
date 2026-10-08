---
id: BL-1731
title: Write gap findings and close or reopen them only on re-measurement with Gap/Tools/Write-GapFindings.ps1
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1720]
touches: [Gap/Tools/Write-GapFindings.ps1, Gap/Tools/Fixtures/findings]
requirement: none
created: 2026-10-08
completed: 2026-10-08
---
# BL-1731 — Write gap findings and close or reopen them only on re-measurement with Gap/Tools/Write-GapFindings.ps1

## Goal

`Gap/Tools/Write-GapFindings.ps1` turns one run's area measurements and analyst report
blocks into `Gap/Findings/GF-####-*.md` files. It files new gaps, records a measurement line
on every open finding, closes a finding only when the run measures all its items gone, and
reopens a closed finding as a regression when one of its items returns.

## Context

This is ADR-0433 decisions 3 and 5. The finding format, the report block and the closing
rule are in `Gap/Instructions/Gap-Format.md`, `Gap/Findings/README.md` and
`Gap/Findings/FINDING-TEMPLATE.md` (BL-1720). The audit office's
`Audit/Tools/Write-AuditFindings.ps1` is the model in spirit, but this script needs nothing
from it: its rules are mechanical, read from the measurement files, never from an analyst's
verdict.

**Parameters.** `-RunDirectory` (holds `measurements/<area>.json`, and
`reports/gap-<area>.md`, each analyst's full reply), `-Stamp` (the run's
`yyyy-MM-dd_HHmm`), `-FindingsDirectory` (default `Gap/Findings`), `-ReleaseDiff` (an
optional `release-<version>.json` from BL-1735; its items become `scope: newest` findings),
`-WhatIf`, `-SelfTest`.

**Rules.**

1. Take the last fenced `json` block of each report. A group whose `key` equals an existing
   finding's `key` is that finding. Otherwise it files a new `GF-####` (the next number
   after the highest in the folder) with `status: open`, `scope: target`, `opened: <stamp>`,
   every field filled from the group, and the evidence and suggestion in their sections.
2. A gap item in a measurement that no group covers, and no open finding lists, is filed in
   one catch-all finding per area, keyed `<area>:ungrouped`, with severity `Low`, so no gap
   is ever dropped. The script prints a warning naming the analyst.
3. Every open finding of a measured area gets a `Measurements` line:
   `<stamp>: <n> of <m> items still gaps`.
4. An open finding whose every item the run measures as `match`, or as `excluded` with a
   reason, gets `status: closed`, `closed: <stamp>` and a `Log` line. Nothing else closes a
   finding: not a task reaching Done, not a missing report, not an item absent from the
   measurement (an absent item leaves the finding open, with a `Measurements` line saying
   so).
5. A closed finding with any item measured as `gap` goes back to `status: open`, with
   `regression: true`, `closed:` cleared and a `Log` line naming the run. The script never
   files a new finding for it.
6. A `rejected` finding is never changed, apart from a `Measurements` line.
7. Areas this run did not measure (`-Areas` left them out) are not touched at all.
8. `-ReleaseDiff` items become one `scope: newest` finding per changed area, with
   `introduced-in` set to the diff's `toVersion` and the changed items listed. Severity
   follows ADR-0433 decision 3 from the change kind. A later release diff that lists the
   same item key adds it to that finding rather than filing again.
9. Print one summary line: new, still open, closed, reopened as regressions, ungrouped.

**Self-test.** Fixtures under `Gap/Tools/Fixtures/findings/`: a sample run directory (two
areas, two reports) and an existing findings folder, copied to a temporary folder before
each check.

## Acceptance criteria

- [x] `Gap/Tools/Write-GapFindings.ps1 -SelfTest` prints `PASS` lines and no `FAIL` under Windows PowerShell 5.1 and PowerShell 7, with one check per rule 1 to 9. That includes: a finding whose task is Done but whose items still measure `gap` stays open; an item absent from the measurement does not close its finding; a closed finding reopened has `regression: true`.
- [x] The files written match `Gap/Findings/FINDING-TEMPLATE.md`'s front matter and section order exactly.
- [x] `-WhatIf` prints what it would write and changes nothing.
- [x] The header help documents every parameter and rule. The script is ASCII only.

## Notes

- Group key: the report block (Gap-Format.md section 6) has no `key` field, so a group's
  key is its `key` when an analyst adds one, else `<area>:<slug of its title>`. Titles stay
  stable across runs, so the key does too.
- Measurements line uses FINDING-TEMPLATE.md's wording, `<stamp>: <n> of <m> items are
  gaps.`, plus `<k> not in this run's measurement.` for absent items; a rerun with the same
  stamp adds no second line.
- Release-diff findings are keyed `<area>:newest`, one per area; severity added High,
  changed Medium, removed Low (ADR-0433 decision 3), the finding taking the highest.
- Rule 2 counts an item as listed when any finding of the area lists it (open, closed or
  rejected): a closed one reopens by rule 5, a rejected one stays Stewart's call.
- The release diff is applied before the areas, so a `newest` finding of a measured area
  gets this run's Measurements line too.
- `-WhatIf` comes from `SupportsShouldProcess`: one "What if" line per file written.

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
- 2026-10-08: Doing -> Done. Write-GapFindings.ps1 files, measures, closes and reopens gap findings by rules 1 to 9; -SelfTest passes 15 checks on PowerShell 5.1 and 7.
