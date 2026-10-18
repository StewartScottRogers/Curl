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
completed: 2026-10-17
---
# BL-2617 — Emulate the RTSP server in the upstream case runner so cases 567 to 572, 577 and 689 run

## Goal

The upstream case runner serves `<servercmd>`/`<reply>` RTSP exchanges, so the libtest cases 567, 568, 569, 570, 571, 572, 577 and 689 run instead of being skipped with "the harness does not emulate the rtsp server".

## Context

Found by BL-2532, which ported lib567 to lib572 (`Curl.Conformance.LibTest.UnitLibrary`); every case running them is screened out in `Curl.Conformance.UnitLibrary` today. Upstream's `tests/rtspserver.pl` (curl 8.22.0) is the model: CSeq echo, session IDs, interleaved RTP (`RTP:` lines in `<reply>`) for 571.

## Acceptance criteria

- [x] Each case that passes through the runner on Windows is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; one that still fails is filed with the board script with its differing lines.
- [x] No added case's output depends on the platform.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.

## Notes

- Built `RtspServerConnector`, `RtspServerConnection` and `RtspInterleavedPackets` in `Curl.Conformance.UnitLibrary` after upstream's `tests/server/rtspd.c` at `curl-8_22_0` (ADR-0674, which supersedes ADR-0457's decision 2). The runner puts it on 8996 just inside the Gopher stand-in and merges its dump into `<verify><protocol>`; screening lets `rtsp` cases run. rtspd neither echoes CSeq nor tracks sessions (the replies carry them), so neither does the stand-in. Its auth part-number rules, `idle`, `stream`, `pipe`, `skip`, 404 document and `<postcmd>` are left out, since no `rtsp` case uses them.
- Measured on Windows: 567 to 572, 577, 689 and 3100 now run (none skips). All of them differ in the `Libcurl` engine, not the server: every RTSP perform goes out as the tool's `OPTIONS * RTSP/1.0`, because `EasyRtspOptions` (request, stream URI, transport, interleave callback) never reach `RtspProtocolHandler`. Filed as BL-2704 with each case's first differing line, so no case is added to `PassingUpstreamCases.txt` here. 3100 stays with BL-2699.
- `UpstreamCaseScreeningTests`' unemulated-server row now uses `httptls`, since `rtsp` is emulated. `RunAsync_RtspPortCase_IsSkippedForItsToolNotForRtspPort` still holds, since that runner has no libtests.
- The stand-in writes no file, so its output does not depend on the platform. An `RTSP/` status line keeps its bare LF under `crlf="headers"`, as upstream's `subnewlines` keeps it.
- Quality: `Measure-CodeQuality.ps1 -Library Curl.Conformance.UnitLibrary` gives 100% line, 100% branch, complexity at most 10, 0 failing members. Fast tests: all green (Conformance 3347 passed).

## Log

- 2026-10-17: Created.
- 2026-10-17: Backlog -> Doing. Criteria at claim: 57abe33b 21e5ddeb 4272a6b9.
- 2026-10-17: Doing -> Done. The case runner emulates rtspd on %RTSPPORT, so the RTSP libtest cases run; their Libcurl client differences are BL-2704
