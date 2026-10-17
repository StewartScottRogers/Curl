---
id: BL-2634
title: Make upstream case 678 run: the runner interprets a libtest client precheck line
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-17
completed:
---
# BL-2634 — Make upstream case 678 run: the runner interprets a libtest client precheck line

## Goal

Upstream case 678 runs through the runner and, when it passes, holds the ratchet.

## Context

Found by BL-2535, which ported the libtest program and unit tested it over a fake engine; the port matches upstream's C, so the difference is in the engine or Libcurl. In the first pass find the project at fault and add it to `touches` before any edit. test678 is skipped: `the harness does not interpret the <client><precheck> line ./libtest/libtests lib678 check`. The runner in `Curl.Conformance.UnitLibrary` should run a `libtests libNNN args` precheck through the ported program (`Lib678` answers the `check` URL) and skip the case only when the precheck prints or fails. 678 uses `CURLOPT_CAINFO_BLOB` with `test-localhost.pem` over HTTPS, so check it is platform-neutral before holding it (Schannel on Windows, OpenSSL elsewhere).

## Acceptance criteria

- [ ] Each named case that passes through the runner on Windows is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; one that still fails is filed with the board script with its differing lines.
- [ ] The fix adds a focused unit test in the fixed library's own `.UnitTests` project (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-17: Created.
