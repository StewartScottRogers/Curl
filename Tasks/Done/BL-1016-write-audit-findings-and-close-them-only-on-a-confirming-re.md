---
id: BL-1016
title: Write audit findings and close them only on a confirming re-audit
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1001]
touches: [Audit/Tools/Write-AuditFindings.ps1, Audit/Tools/Fixtures/findings]
lane: no
requirement: none
created: 2026-09-29
completed: 2026-09-30
---
# BL-1016 — Write audit findings and close them only on a confirming re-audit

## Goal

`Audit/Tools/Write-AuditFindings.ps1` turns the auditors' report blocks from one audit into finding files under `Audit/Findings/`, recognises findings it already has, excludes planted defects, appends re-audit results, and closes a finding only when a reliable re-audit says its reproduction no longer reproduces.

## Context

Interactive only (`lane: no`): it writes `Audit/`. Run it with `/task-run BL-1016`.
Formats and rules: `Audit/Findings/README.md`, `FINDING-TEMPLATE.md` and
`Audit/Instructions/Report-Format.md` (BL-1001). `RunAudit.ps1` (BL-1020) calls it.

Inputs: `-ReportDirectory` (one `<auditor>.md` per auditor holding its full reply; the
script extracts the last fenced `json` block), `-Manifest` (the seeder's manifest,
BL-1015), `-Unreliable` (auditor names the scorecard writer flagged; BL-1017 computes
this from the same inputs, so accept it as a parameter), `-Scorecard` (the name of this
audit's scorecard), `-Commit` (audited), `-FindingsDirectory` (default
`Audit/Findings`).

Behaviour:

- A reported finding that matches a planted defect (same file, and the manifest's
  `catch` text found in its title, key or evidence) is a catch, not a finding: skip it,
  and write the matches to `catches.json` in the report directory for BL-1017.
- A reported finding whose `key` equals an open (`proposed` or `accepted`) finding's
  `key` is the same finding: append a `Re-audits` line ("still reported") rather than
  filing it again.
- Otherwise file a new `AF-####` (next number after the highest in the folder) with
  `status: proposed`, every field filled, and the auditor's flag "reported by an
  auditor flagged unreliable in <scorecard>" in the Summary when it is.
- For each `reaudits` entry: append the line. If `reproduces` is false and the auditor is
  not unreliable, set `status: closed`, `closed`, `closed-by`. Nothing else ever closes
  a finding - not a task's state, not a missing report.
- `rejected` and `closed` findings are never changed, except that a new finding whose
  key matches a closed one names it ("reappeared; previously AF-####").
- Print a summary line: new, still open, closed, catches.

`-SelfTest` runs against `Audit/Tools/Fixtures/findings/` (a sample report set, a
manifest, and an existing findings folder copied to a temporary directory) and checks
every rule above.

## Acceptance criteria

- [x] A report key that matches a `rejected` finding is not filed again: the finding gains a Re-audits line saying it is still reported, and its status stays `rejected` (Decided by Claude, 2026-09-30, raised while writing the BL-1001 formats: otherwise every audit re-files what Stewart already turned down).
- [x] `-SelfTest` prints `PASS` and no `FAIL` for: a new finding filed as `proposed` with the next ID; a repeated key appends a re-audit line and files nothing; a planted-defect match is skipped and appears in `catches.json`; `reproduces: false` from a reliable auditor closes the finding with `closed-by`; `reproduces: false` from an unreliable auditor does not close it; a finding whose task is Done but has no re-audit stays open; a `rejected` finding is untouched; a key matching a closed finding files a new one that names it.
- [x] Output files match `FINDING-TEMPLATE.md`'s front matter and section order exactly.
- [x] Header help documents parameters and rules; ASCII only; runs under PowerShell 7 and Windows PowerShell 5.1.

## Notes

- Audit branch commits d4702810 and 86fd76c8. -SelfTest: 11 PASS, 0 FAIL under Windows PowerShell 5.1 and PowerShell 7.6.6 (new AF with next ID; repeat adds a line; planted match to catches.json; reliable re-audit closes; unreliable does not; Done task with no re-audit stays open; rejected stays rejected with a still-reported line; reappeared closed key files new naming it; unreliable flag in Summary; template fields and sections; summary line). ASCII only.
- Findings/README.md rule 8 updated for the rejected-finding decision.
- The catch rule is the same as BL-1017's: same auditor, same file, and the manifest's catch fragment in title, key or evidence; the seeder now writes catch as a short fragment.

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. Write-AuditFindings.ps1 files, repeats, re-audits and closes findings by the rules, with catches to catches.json; on the audit branch.
