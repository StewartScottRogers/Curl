---
id: BL-2635
title: Make upstream case 694 pass: the NTLM type-1 message carries curl's flags
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-17
completed:
---
# BL-2635 — Make upstream case 694 pass: the NTLM type-1 message carries curl's flags

## Goal

Upstream case 694 passes through the runner.

## Context

Found by BL-2535, which ported the libtest program and unit tested it over a fake engine; the port matches upstream's C, so the difference is in the engine or Libcurl. In the first pass find the project at fault and add it to `touches` before any edit. test694 (lib694: `CURLAUTH_BASIC|DIGEST|NTLM` as `me:password`): `differs at byte 146 (line 7): expected "Authorization: NTLM TlRMTVNTUAABAAAABoIIAAAAAAAAAAAAAAAAAAAAAAA=", got "...AABAAAAB4IIogAAAAAAAAAAAAAAAAAAAAAKAPRlAAAADw=="` - Curl's type-1 message sets other flags and a version block; curl 8.22.0's carries `0x00088206` and no version.

## Acceptance criteria

- [ ] Each named case that passes through the runner on Windows is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; one that still fails is filed with the board script with its differing lines.
- [ ] The fix adds a focused unit test in the fixed library's own `.UnitTests` project (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-17: Created.
