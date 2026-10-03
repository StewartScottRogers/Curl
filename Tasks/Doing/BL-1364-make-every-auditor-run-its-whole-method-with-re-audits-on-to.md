---
id: BL-1364
title: Make every auditor run its whole method, with re-audits on top, and report per-step counts
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Audit/RunAudit.ps1, Audit/Instructions, Audit/Tools/Write-AuditScorecard.ps1]
lane: no
requirement: none
created: 2026-10-03
completed:
---
# BL-1364 — Make every auditor run its whole method, with re-audits on top, and report per-step counts

## Goal

Every auditor runs every step of its method on every audit; re-audits are added to the method, never a replacement, and an auditor that skips its method is marked unreliable.

## Context

- Diagnosed 2026-10-03 across the audits of 2026-10-02 14:00 (run Z:\repos\Curl.audit\20261002-140002, planted commit 5232c4f8) and 2026-10-03 06:23 (run 20261003-062343, planted commit 5089bd0d): quality, truthfulness, process and conformance caught 0 of their planted defects. No auditor ran out of budget. Audit paths: interactive only, done on the `audit` branch and merged by pull request once CI is green.
- Truthfulness ran 6 turns both times ("I ran only the two re-audits and sampled nothing new"; "I did not file it, because this was a re-audit only"), and process 7 turns, because RunAudit.ps1's prompt (lines ~319-321) says "Re-audit these open findings" and Truthfulness.md says "The prompt may limit you to some steps; do only those". PD-401, PD-402 and PD-403 were all where its steps would have looked.

## Acceptance criteria

- [ ] RunAudit.ps1's auditor prompt says: do every step of your method in full; the re-audits come on top of it, never instead.
- [ ] Auditor-Rules.md rule 5 adds that re-audits never replace the method; Truthfulness.md, and any other method that lets the prompt limit its steps, says only an explicit list of steps does, and a re-audit list is not one.
- [ ] Report-Format.md defines per-step counts in the report's `metrics` for each auditor (for example truthfulness `sampled.names`, `sampled.docComments`, `sampled.adrs`, `sampled.scriptHelp`; process `rulesChecked`; quality `mutants`, `testsRead`; conformance `cases`; security and performance theirs), and each method says to report them.
- [ ] Write-AuditScorecard.ps1 marks an auditor unreliable when its report lacks a required count, and its -SelfTest covers it.
- [ ] A rerun of truthfulness, process and quality on the run 2 planted tree reports the counts, runs over 20 turns each, and truthfulness catches PD-402 and PD-403 (Notes record it).
- [ ] `dotnet build` is clean and the fast tests are green.

## Notes

## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
