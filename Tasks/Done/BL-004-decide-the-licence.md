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
completed: 2026-09-26
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
- [x] An ADR under `Documentation/Planning/Decisions/` records the choice and why.
- [x] `LICENSE.txt` holds the chosen licence text.
- [x] Open question 1 in `Documentation/Product/Product-Overview.md` is marked answered, pointing at the ADR.

## Notes

**Decision (Stewart, 2026-09-26):** Relicense to MIT. `LICENSE.txt` becomes the standard MIT text, copyright 2026 Stewart Scott Rogers.

**Delivered (2026-09-26, dark factory lane 2):** ADR-0012 records the MIT decision and is indexed in `Documentation/Planning/Decisions/README.md`; `LICENSE.txt` is the standard MIT text; Product-Overview's licensing constraint now states MIT and open question 1 is marked answered with a link to ADR-0012.

**Choices made unattended:** done in-session rather than via `align-and-document` because it is three small document edits. The ADR number 0012 is the next free one on this lane; if another lane lands an ADR-0012 first, the rebase will need to renumber one of them. `README.md`'s Licence section only links `LICENSE.txt`, so it stays true and was left alone (it is outside `touches`). No project file declares a licence expression, so nothing else names GPL.

## Log

- 2026-09-25: Migrated from Documentation/Planning/Backlog.md (Ready). Assigned to Stewart because the decision is his.
- 2026-09-26: Stewart chose MIT. Reassigned to Claude to record the ADR and replace LICENSE.txt.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. Curl is MIT-licensed: ADR-0012 records it, LICENSE.txt holds the MIT text, open question 1 answered
