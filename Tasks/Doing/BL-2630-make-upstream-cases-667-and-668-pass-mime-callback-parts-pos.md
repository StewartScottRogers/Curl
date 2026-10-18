---
id: BL-2630
title: Make upstream cases 667 and 668 pass: MIME callback parts post their data in curl's chunks
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-17
completed:
---
# BL-2630 — Make upstream cases 667 and 668 pass: MIME callback parts post their data in curl's chunks

## Goal

Upstream cases 667 and 668 pass through the runner.

## Context

Found by BL-2535, which ported the libtest program and unit tested it over a fake engine; the port matches upstream's C, so the difference is in the engine or Libcurl. In the first pass find the project at fault and add it to `touches` before any edit. test667 (lib667, base64 part read one byte a call, unknown size): `<verify><protocol> differs at byte 302 (line 14): expected "4\r\n" ("ZHVt"), got "8\r\n" ("ZHVtbXk=")` - curl encodes and sends each callback read as it comes. test668 (lib668): `differs at byte 181 (line 8): expected "cd\r\n", got "63\r\n"` - the known-size callback part `field1` posts no data. Related: BL-2624 (645 chunk sizes).

## Acceptance criteria

- [ ] Each named case that passes through the runner on Windows is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; one that still fails is filed with the board script with its differing lines.
- [ ] The fix adds a focused unit test in the fixed library's own `.UnitTests` project (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-17: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: 92cbe6ef c59fcab3 3d836d5e.
