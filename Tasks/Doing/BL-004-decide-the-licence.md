---
id: BL-004
title: Decide the licence - keep GPL-3.0 or relicense to MIT/Apache-2.0
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions, LICENSE.txt, Documentation/Product/Product-Overview.md]
requirement: none
created: 2026-09-25
completed:
---
# BL-004 — Decide the licence: keep GPL-3.0 or relicense to MIT/Apache-2.0

## Goal

Curl has a chosen licence, recorded as an Architecture Decision Record, and
`LICENSE.txt` matches it.

## Context

Open question 1 in `Documentation/Product/Product-Overview.md`. This blocks the
first public release. The choice is Stewart's; once it is made, `align-and-document` records
it.

## Acceptance criteria

- [x] Stewart has chosen the licence.
- [ ] An ADR under `Documentation/Planning/Decisions/` records the choice and why.
- [ ] `LICENSE.txt` holds the chosen licence text.
- [ ] Open question 1 in `Documentation/Product/Product-Overview.md` is marked answered, pointing at the ADR.

## Notes

**Decision (Stewart, 2026-09-26):** Relicense to MIT. `LICENSE.txt` becomes the standard MIT text, copyright 2026 Stewart Scott Rogers.

## Log

- 2026-09-25: Migrated from Documentation/Planning/Backlog.md (Ready). Assigned to Stewart because the decision is his.
- 2026-09-26: Stewart chose MIT. Reassigned to Claude to record the ADR and replace LICENSE.txt.
- 2026-09-26: Backlog -> Doing.
