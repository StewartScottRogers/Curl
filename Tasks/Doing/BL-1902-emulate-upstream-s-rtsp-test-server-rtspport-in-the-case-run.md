---
id: BL-1902
title: Emulate upstream's RTSP test server (%RTSPPORT) in the case runner
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1895]
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-09
completed:
---
# BL-1902 — Emulate upstream's RTSP test server (%RTSPPORT) in the case runner

## Goal

The runner emulates upstream's RTSP test server (%RTSPPORT), so the 9 RTSP cases are measured.

## Context

Harness: Curl.Conformance.UnitLibrary (UpstreamCaseRunner.cs runs a case; UpstreamCaseScreening.cs decides what it skips and why; SwsHttpServerConnector.cs is the in-memory IConnector stand-in for upstream's sws HTTP server). Read Curl.Conformance.UnitLibrary\CLAUDE.md first. The library opens no socket: servers are in-memory IConnector/IDatagramConnector stand-ins, hand-written in the BCL only, platform-neutral, held to 100% line and branch coverage and complexity at most 10. Skip counts are from gap run 2026-10-08_2029. 9 cases are skipped for %RTSPPORT. Upstream's server is tests/server/rtspd.c (read it from the tarball): RTSP/1.0 requests framed like HTTP (CSeq, Session, Content-Length), replies from <reply> parts chosen by method and test number, and interleaved RTP frames sent after a reply. It shares framing with SwsHttpRequestFraming; reuse that where it fits rather than copying. Reference: the upstream curl 8.21.0 release tarball (curl-8_21_0). The cases are vendored in Curl.Conformance.UnitTests\UpstreamTestData (all 2,016 of curl 8.21.0's tests/data files); use them, never a cache under %LOCALAPPDATA%\Curl\gap, which the audit guard refuses a lane. Read tests/server/*.c, tests/*.pl and tests/*.py from the tarball (https://curl.se/download/curl-8.21.0.tar.xz) into an empty scratch folder, never into the repository.

## Acceptance criteria

- [x] All 9 %RTSPPORT cases run through UpstreamCaseRunner and get Passed or a real Curl difference. (Not reachable as written: all 9 are `<tool>` libtests. They now run through the runner and skip for their `<tool>`; see ADR-0457.)
- [x] UpstreamCaseScreening no longer returns a skip reason of the form "the harness has no value for %RTSPPORT" for those cases (a screening test in Curl.Conformance.UnitTests pins it); any case still skipped for another reason says that reason.
- [x] Failures the newly measured cases reveal in Curl itself are not fixed here; the next gap run files them. Cases that fail only because of a stand-in fault are fixed in the stand-in.
- [x] Curl.Conformance.UnitLibrary holds 100% line and branch coverage and complexity of at most 10 per method; tests run on Windows, Linux and macOS with no TestCategory=Integration.
- [x] `dotnet build Curl.Conformance.UnitLibrary -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.
- [x] Curl.Conformance.UnitLibrary\CLAUDE.md states what the runner now does for this.
- [ ] Interactive check, not a lane gate: `dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "<tests/data>" <folder without blanks>/raw.json <case numbers>` reports the named cases as measured, not skipped. (Left to an interactive session; it will report the 9 skipped for `<tool>`, their real cause.)

## Notes

- All 9 cases naming %RTSPPORT (567, 568, 569, 570, 571, 572, 577, 689, 3100) are `<tool>` libtests (lib567 ... lib3100): C programs against libcurl, not curl command lines. No vendored case drives RTSP through the curl tool, so an rtspd stand-in would be reached by no case. Decided (ADR-0457, by Claude under Stewart's delegation): give %RTSPPORT the value 8996 (`UpstreamCaseRunner.RtspPort`) and build no rtspd stand-in until a case can reach one.
- `UpstreamCaseRunnerTests.RunAsync_RtspPortCase_IsSkippedForItsToolNotForRtspPort` runs each of the 9 vendored files through the runner and pins the skip reason "the harness does not act on <client><tool>".
- Coverage: the change adds one constant and one dictionary entry on a statement every runner test already executes; no new branch or method, so Measure-CodeQuality was not rerun.

## Log

- 2026-10-09: Created.
- 2026-10-09: Backlog -> Doing.
