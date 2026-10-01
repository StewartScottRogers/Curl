---
id: BL-1017
title: Write the audit scorecard with each auditor's planted-defect catch rate
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1001, BL-1002, BL-1015]
touches: [Audit/Tools/Write-AuditScorecard.ps1, Audit/Tools/Fixtures/scorecard]
lane: no
requirement: none
created: 2026-09-29
completed:
---
# BL-1017 — Write the audit scorecard with each auditor's planted-defect catch rate

## Goal

`Audit/Tools/Write-AuditScorecard.ps1` computes each auditor's planted-defect catch rate and reliability, and writes one scorecard in the fixed format, with the auditor fingerprint and the change since the previous scorecard.

## Context

Interactive only (`lane: no`): it writes `Audit/`. Run it with `/task-run BL-1017`.
Format: `Audit/Scorecards/README.md` and `SCORECARD-TEMPLATE.md` (BL-1001). Fingerprint:
`Audit/Tools/Get-AuditorFingerprint.ps1` (BL-1002). Manifest: BL-1015. `RunAudit.ps1`
(BL-1020) calls it twice: `-ReliabilityOnly` before `Write-AuditFindings.ps1` (BL-1016)
to get the unreliable list, and in full after it.

Inputs: `-ReportDirectory`, `-Manifest`, `-FindingsDirectory`, `-ScorecardsDirectory`,
`-Commit`, `-Branch`, `-Started`, `-Finished`, `-CostUsd`, `-Models` (auditor=model
pairs).

Rules:

- A planted defect is caught by its auditor when that auditor's report has a finding in
  the defect's file whose title, key or evidence contains the manifest's `catch` text.
  (A catch by a different auditor is noted but does not count for either.)
- Catch rate = caught / planted for that auditor. An auditor is **unreliable** when it
  missed at least one defect planted for it, or returned no parseable report block.
- `-ReliabilityOnly` prints the unreliable auditor names, one per line, and writes
  nothing.
- Full run writes `yyyy-MM-dd_HHmm.md` with every section of the template in order: the
  header with the fingerprint (it runs `Get-AuditorFingerprint.ps1`), the auditor table
  in the fixed order, the performance and process tables from the reports' `metrics`,
  and "Change since the previous scorecard" computed from the newest earlier scorecard
  in the folder ("first scorecard" when there is none). Numbers from an unreliable
  auditor are marked `(unreliable)`.

`-SelfTest` runs against `Audit/Tools/Fixtures/scorecard/` (six sample reports, one of
which misses its planted defect and one of which has no report block; a manifest; a
previous scorecard) and checks the rules.

## Acceptance criteria

- [ ] `-SelfTest` prints `PASS` and no `FAIL` for: catch rates per auditor; the auditor that missed a defect and the one with no block are unreliable and nobody else is; a catch by the wrong auditor does not count; `-ReliabilityOnly` prints exactly the two names; the written scorecard has the template's sections in order, the fingerprint, and a correct delta against the previous scorecard.
- [ ] Two runs on the same inputs produce byte-identical scorecards except the file name.
- [ ] Header help documents parameters and rules; ASCII only; runs under PowerShell 7 and Windows PowerShell 5.1.

## Notes

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
