---
id: BL-324
title: Renumber the second ADR-0040 so every ADR number is unique
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions]
requirement: none
created: 2026-09-27
completed:
---
# BL-324 — Renumber the second ADR-0040 so every ADR number is unique

## Goal

Every ADR under `Documentation/Planning/Decisions/` has a number no other ADR uses.

## Context

- Found while writing ADR-0050 (BL-164, 2026-09-27): `ADR-0040-http-enforces-max-time-...` and `ADR-0040-w-standard-output-line-feeds-...` share 0040, and both are in the README index.
- Keep the older one at 0040 (by git history); give the other the next free number and update every reference to it in the repository.

## Acceptance criteria

- [ ] No two files in `Documentation/Planning/Decisions/` share an `ADR-####` prefix.
- [ ] The README index lists the renumbered ADR under its new number, and `git grep` finds no reference to its old file name.

## Notes

## Log

- 2026-09-27: Created.
