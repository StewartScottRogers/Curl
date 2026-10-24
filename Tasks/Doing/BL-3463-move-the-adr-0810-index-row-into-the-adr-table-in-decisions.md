---
id: BL-3463
title: Move the ADR-0810 index row into the ADR table in Decisions README
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-10-24
completed:
---
# BL-3463 — Move the ADR-0810 index row into the ADR table in Decisions README

## Goal

The ADR-0810 row in `Documentation/Planning/Decisions/README.md` sits inside the ADR index table, directly after the ADR-0809 row.

## Context

The shift-end review of 2026-10-24 (review-20261024-050935, pr-review-toolkit code-reviewer) found that BL-3462 put ADR-0810's index row at `Documentation/Planning/Decisions/README.md:2`, between the `# Architecture Decision Records` heading and the intro paragraph. That is outside the `| ADR | Title | Status | Date |` table, so it renders as a stray pipe-separated line. The table itself still ends at 0809. An agent scanning the index for the newest ADR, or for the next free number, misses 0810.

## Acceptance criteria

- [ ] Line 2 of `Documentation/Planning/Decisions/README.md` is no longer the ADR-0810 row. The heading is followed by the intro text, as before BL-3462.
- [ ] The ADR-0810 row, with its text unchanged, is the row directly after the ADR-0809 row at the end of the table.
- [ ] `grep -c "ADR-0810-" Documentation/Planning/Decisions/README.md` prints 1.

## Notes

## Log

- 2026-10-24: Created.
- 2026-10-24: Backlog -> Doing. Criteria at claim: b0b4473a 65fdf62e 2cdc9c29.
