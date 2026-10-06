---
id: BL-326
title: Renumber the second ADR-0040 so every ADR number is unique
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions, Tasks/Backlog/BL-349-treat-o-as-standard-output-and-keep-output-dir-off-it.md]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-326 — Renumber the second ADR-0040 so every ADR number is unique

## Goal

Every ADR under `Documentation/Planning/Decisions/` has a number no other ADR uses.

## Context

- Found while writing ADR-0050 (BL-164, 2026-09-27): `ADR-0040-http-enforces-max-time-...` and `ADR-0040-w-standard-output-line-feeds-...` share 0040, and both are in the README index.
- Keep the older one at 0040 (by git history); give the other the next free number and update every reference to it in the repository.

## Acceptance criteria

- [x] No two files in `Documentation/Planning/Decisions/` share an `ADR-####` prefix.
- [x] The README index lists the renumbered ADR under its new number, and `git grep` finds no reference to its old file name.

## Notes

- Git history: `ADR-0040-http-enforces-max-time-...` was added in 6da05d8 (2026-09-26 21:47), `ADR-0040-w-standard-output-line-feeds-...` in c3992f2 (22:00). The HTTP one keeps 0040; the `-w` one becomes ADR-0081, the next free number (0080 was the highest).
- The README index actually listed only the HTTP ADR-0040; the `-w` ADR had no row. Added it as 0081, noting its former number, and noted the renumbering on the ADR's Date line so older citations of "ADR-0040" for line feeds can still be traced.
- Updated the only reference to the old file name outside this task, in Backlog task BL-349 (a `touches` addition: no task in Doing names that file).
- Bare "ADR-0040" citations of the line-feed decision remain in `Curl.Console/CLAUDE.md` and `Curl.Console/DiskWriteOutFileOpener.cs`. `Curl.Console` is held by BL-132 in Doing, so rather than widen this task they are filed as BL-394. Done tasks' notes (BL-235, BL-280) keep their historical citations unedited.
- Verified: `ls Documentation/Planning/Decisions | grep -oE '^ADR-[0-9]{4}' | sort | uniq -d` prints nothing; `git grep ADR-0040-w-standard` finds only this task's own description of the old name.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. Every ADR number is unique: the -w line-feed ADR is now ADR-0081 and indexed; Curl.Console citations filed as BL-394
