---
id: BL-4664
title: Split Documentation/Conformance/Extended/CurlSurface-Transfer-MessagingStreams.md so tasks touching it can run in parallel
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Documentation/Conformance/Extended/CurlSurface-Transfer-MessagingStreams.md]
requirement: none
created: 2026-11-03
completed:
---
# BL-4664 — Split Documentation/Conformance/Extended/CurlSurface-Transfer-MessagingStreams.md so tasks touching it can run in parallel

## Goal

`Documentation/Conformance/Extended/CurlSurface-Transfer-MessagingStreams.md` no longer serialises the tasks that touch it: each unit it holds lives in its own file, so those tasks can run in parallel lanes.

## Context

Filed by the dark factory's coordinator (ADR-0130, 2026-10-21 amendment): the board's capacity was the binding limit on -Lanes Auto two steps running while 4 ready task(s) could not start because of overlapping touches.

`task-board.ps1 hotspots` counts 7 unfinished lane-eligible tasks touching `Documentation/Conformance/Extended/CurlSurface-Transfer-MessagingStreams.md`: BL-4514, BL-4515, BL-4516, BL-4624, BL-4627, BL-4646, BL-4656. Tasks that touch the same file never run side by side, so they wait for one another.

## Acceptance criteria

- [x] The per-case or per-stand-in content of `Documentation/Conformance/Extended/CurlSurface-Transfer-MessagingStreams.md` moves into one file per unit, and each unit registers itself, so adding or changing one touches only its own file.
- [x] After the waiting tasks' `touches` (BL-4514, BL-4515, BL-4516, BL-4624, BL-4627, BL-4646, BL-4656) are narrowed to their own files by editing their front matter, `task-board.ps1 hotspots` counts fewer than 5 tasks for `Documentation/Conformance/Extended/CurlSurface-Transfer-MessagingStreams.md`.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Split by scheme family, as the Brokers pages are: `CurlSurface-Transfer-MessagingStreams-{Redis,Xmpp,Mqtt5,Coap}.md`, each with the shared intro and its own rows; the old page is gone (the matrix check requires rows in every page, so no index page). BL-4624 now touches `...-Dns.md` (new page for its rows), BL-4656 `...-Redis.md`, BL-4646 its one ADR. BL-4514, BL-4515 and BL-4516 lacked their front matter closing `---`, so the board read them as touching everything; fixed. `hotspots` now lists nothing for these pages. ADR-1222 citation updated. Fast tests of Curl.Conformance.Extended.UnitTests green.

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: efb1682a 3430a6d1 a0b49a29.
