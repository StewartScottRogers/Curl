---
id: BL-2617
title: Emulate the RTSP server in the upstream case runner so cases 567 to 572, 577 and 689 run
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-17
completed:
---
# BL-2617 — Emulate the RTSP server in the upstream case runner so cases 567 to 572, 577 and 689 run

## Goal

The upstream case runner serves `<servercmd>`/`<reply>` RTSP exchanges, so the libtest cases 567, 568, 569, 570, 571, 572, 577 and 689 run instead of being skipped with "the harness does not emulate the rtsp server".

## Context

Found by BL-2532, which ported lib567 to lib572 (`Curl.Conformance.LibTest.UnitLibrary`); every case running them is screened out in `Curl.Conformance.UnitLibrary` today. Upstream's `tests/rtspserver.pl` (curl 8.22.0) is the model: CSeq echo, session IDs, interleaved RTP (`RTP:` lines in `<reply>`) for 571.

## Acceptance criteria

- [ ] Each case that passes through the runner on Windows is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; one that still fails is filed with the board script with its differing lines.
- [ ] No added case's output depends on the platform.
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.

## Notes

## Log

- 2026-10-17: Created.
