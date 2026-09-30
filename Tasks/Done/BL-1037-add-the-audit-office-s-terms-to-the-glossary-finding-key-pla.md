---
id: BL-1037
title: Add the audit office's terms to the glossary: finding, key, planted defect, catch rate, reliable, scorecard, re-audit
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Wiki/Glossary.md]
requirement: none
created: 2026-09-30
completed: 2026-09-30
---
# BL-1037 — Add the audit office's terms to the glossary: finding, key, planted defect, catch rate, reliable, scorecard, re-audit

## Goal

`Documentation/Wiki/Glossary.md` defines each audit-office term in one sentence, in the words `Audit/Findings/README.md`, `Audit/Instructions/Report-Format.md` and `Audit/Scorecards/README.md` use.

## Context

BL-1001 (2026-09-30) defined the audit office's formats and introduced terms the glossary does not have yet: finding (and `AF-####`), key, severity levels, re-audit, planted defect, catch rate, reliable, scorecard, auditor fingerprint. CLAUDE.md: one concept has one name, the one in the glossary. This is a docs change in `Documentation/`, not an audit path, so a lane may do it; it reads the `Audit/` files as they are on `master` (a lane's hook refuses reading `Audit/`, so if it is run by a lane, the definitions below are the source).

Definitions from the BL-1001 formats: a finding is one issue an auditor reported, one file `Audit/Findings/AF-####-<slug>.md`; its key is the auditor's stable dedupe string `<auditor>:<where>:<what>:<kind>`, never containing a line number; a re-audit is an auditor rerunning a finding's reproduction; a planted defect is a known defect the seeder puts into the audited tree; the catch rate is planted defects an auditor caught over those assigned to it; an auditor is reliable in an audit when it caught every planted defect assigned to it, returned a parseable report and did not write to the audited tree; a scorecard is the fixed-format summary of one audit in `Audit/Scorecards/yyyy-MM-dd_HHmm.md`; the auditor fingerprint is the hash of the auditor definitions that ran.

## Acceptance criteria

- [x] The glossary has an entry for each term above, one sentence each, matching the formats' wording.
- [x] No other document uses a second name for any of these concepts.

## Notes

- Added a section, "The audit office", to `Documentation/Wiki/Glossary.md` with eight entries: auditor fingerprint, catch rate, finding (`AF-####`), key, planted defect, re-audit, reliable, scorecard. Worded from this task's Context and ADR-0267, since the lane's audit guard refuses reading `Audit/`.
- Severity levels are left out: the Context names them but gives no definition, and the lane cannot read `Audit/Findings/README.md` to take its wording. An interactive session can add them.
- Second names: searched `Documentation/` and `CLAUDE.md` for seeded/injected defect, report card and similar; none found. ADR-0267 already says finding, re-audit, planted defect, catch rate, unreliable and scorecard.

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. The glossary defines the audit office's terms: finding, key, planted defect, catch rate, reliable, scorecard, re-audit, auditor fingerprint
