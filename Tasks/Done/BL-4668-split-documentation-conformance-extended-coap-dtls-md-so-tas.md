---
id: BL-4668
title: Split Documentation/Conformance/Extended/Coap-Dtls.md so tasks touching it can run in parallel
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Documentation/Conformance/Extended/Coap-Dtls.md, Documentation/Conformance/Extended/Coap-Dtls-Handshake.md, Documentation/Conformance/Extended/Coap.md, Tasks/Backlog]
requirement: none
created: 2026-11-03
completed: 2026-11-03
---
# BL-4668 — Split Documentation/Conformance/Extended/Coap-Dtls.md so tasks touching it can run in parallel

## Goal

`Documentation/Conformance/Extended/Coap-Dtls.md` no longer serialises the tasks that touch it: each unit it holds lives in its own file, so those tasks can run in parallel lanes.

## Context

Filed by the dark factory's coordinator (ADR-0130, 2026-10-21 amendment): the board's capacity was the binding limit on -Lanes Auto two steps running while 8 ready task(s) could not start because of overlapping touches.

`task-board.ps1 hotspots` counts 6 unfinished lane-eligible tasks touching `Documentation/Conformance/Extended/Coap-Dtls.md`: BL-4514, BL-4515, BL-4516, BL-4574, BL-4575, BL-4646. Tasks that touch the same file never run side by side, so they wait for one another.

## Acceptance criteria

- [x] The per-case or per-stand-in content of `Documentation/Conformance/Extended/Coap-Dtls.md` moves into one file per unit, and each unit registers itself, so adding or changing one touches only its own file.
- [x] After the waiting tasks' `touches` (BL-4514, BL-4515, BL-4516, BL-4574, BL-4575, BL-4646) are narrowed to their own files by editing their front matter, `task-board.ps1 hotspots` counts fewer than 5 tasks for `Documentation/Conformance/Extended/Coap-Dtls.md`.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Coap-Dtls.md renamed to Coap-Dtls-Handshake.md (the loader reads every *.md, parts are `<Name>-<Part>.md`). BL-4574 now writes Coap-Dtls-Faults.md and BL-4575 Coap-Dtls-Remaining.md; their touches and text narrowed. Coap.md links updated (touches widened by that file; no other Doing task holds it). BL-4646 (touches `Documentation`) is not mine to narrow; BL-4514/4515/4516 touch DnsQuery.md, not this file. Conformance.Extended tests: 1494 pass.

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: caa8cc5f 17a3c0e1 a0b49a29.
- 2026-11-03: Doing -> Done. Coap-Dtls.md split into per-part files
