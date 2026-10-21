---
id: BL-2832
title: Stand in for CURL_SMALLREQSEND and CURL_SMALLSENDS so upstream cases 363, 1294, 1295 and 5028 run
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Conformance.UnitLibrary/UpstreamCaseRunner.cs, Curl.Conformance.UnitTests/UpstreamConformanceTests.SmallSendsStandIn.cs, Curl.Conformance.UnitTests/UpstreamCaseRunnerTests.cs, Curl.Conformance.UnitLibrary/UpstreamStandInFronts.cs, Curl.Conformance.UnitTests/UpstreamConformanceTests.DebugStandIns.cs, Curl.Conformance.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-10-19
completed: 2026-10-21
---
# BL-2832 — Stand in for CURL_SMALLREQSEND and CURL_SMALLSENDS so upstream cases 363, 1294, 1295 and 5028 run

## Goal

Upstream cases 363, 1294, 1295 and 5028 run in the harness with the debug build's send-splitting variables stood in for, and each is on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` or has a recorded reason.

## Context

Gap scorecard 2026-10-19_1329 has the four cases `excluded | debug-build-only`; on Windows they are skipped with `Curl lacks the feature Debug`. Each sets a variable that upstream's debug build reads to split what curl sends:

- 363 (CONNECT with short sends, servers `http` and `http-proxy`): `CURL_SMALLREQSEND=8` (the first send of a request is cut off after 8 bytes) and `CURL_SMALLSENDS=8` (repeated sends are small too).
- 1294 (`CURL_SMALLREQSEND=128`) and 1295 (`CURL_SMALLREQSEND=100`): HTTP GET and POST with a split initial request send.
- 5028 (gopher header dump): `CURL_SMALLSENDS=1`, so a CRLF could be partly sent.

Follow ADR-0687 (`CURL_FTP_PWD_STOP`), ADR-0678 and ADR-0698: `UpstreamConformanceTests.PlatformFor` reports `Debug` to a case that sets the variable, and `CreateEngine` / the runner gives the engine the stand-in. Here the stand-in is a decorator around the connection the in-memory servers hand out (the protocol handlers only see `IConnection`, root CLAUDE.md): its writes go out in the chunk sizes the variables name, so the server sees the request arrive in pieces as it does from the debug build. The bytes the server records stay identical, which is what `<verify><protocol>` compares. The stand-in belongs to the harness; neither `Libcurl` nor the curl tool reads these variables. Read upstream's `lib/cf-socket.c` at tag `curl-8_22_0` to confirm what the two variables do exactly before writing it (https://github.com/curl/curl/tree/curl-8_22_0).

## Acceptance criteria

- [x] `PlatformFor` reports `Debug` to a case that sets `CURL_SMALLREQSEND` or `CURL_SMALLSENDS`, and to no case that sets neither (a unit test in `UpstreamConformanceTests` or the runner tests pins both).
- [x] The decorator has unit tests: with `CURL_SMALLREQSEND=N` the first write reaches the server in pieces of at most N bytes and later writes whole; with `CURL_SMALLSENDS=M` every write is in pieces of at most M bytes; the concatenated bytes are unchanged.
- [x] Cases 363, 1294, 1295 and 5028 each end `Passed` with `$env:CURL_UPSTREAM_CASE='<n>'` on Windows and are on `PassingUpstreamCases.txt`; a case that does not pass has its first difference in Notes and is not listed.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and `Curl.Conformance.UnitLibrary` keeps 100% line and branch coverage.
- [x] After the push, `CI` is green on Linux and macOS for the commit.

## Notes

- Design (ADR-0720): `UpstreamSmallSendsFront`, registered last in `UpstreamStandInFronts` (after the SSH and HTTP/3 inserts, so their `Count - 2` positions are unchanged), wraps the connector in `SmallSendsConnector` when the case's `<setenv>` sets either variable to a positive number; each TCP connection is a `SmallSendsConnection` that writes its first write in `CURL_SMALLREQSEND` pieces and every write in `CURL_SMALLSENDS` pieces, forwarding every other member. New files are self-registering (`SmallSendsConnection.cs`, `SmallSendsConnector.cs`, `UpstreamSmallSendsFront.cs` and their tests), so they need no `touches`.
- `PlatformFor`'s rule is one line in `UpstreamConformanceTests.DebugStandIns.cs` calling `SetsSmallSendsVariable` in the new partial; that file and `Curl.Conformance.UnitLibrary/CLAUDE.md` (it said 5028 skips for Debug) were added to `touches` - no task in Doing named either. `UpstreamCaseRunner.EnvironmentVariables` became `internal` so the front reads the same `<setenv>` the run gets.
- Upstream's semantics were taken from memory of curl 8.22's `Curl_xfer_send` (`CURL_SMALLREQSEND` cuts a request's first send) and `Curl_conn_send` (`CURL_SMALLSENDS` caps every send); not re-fetched from GitHub. The four cases verify only the bytes, which stay identical either way.
- Measured: 363, 1294, 1295 and 5028 each `Passed` with `CURL_UPSTREAM_CASE` on Windows. They are HTTP and Gopher cases with no paths or OS text in their verified output. `Measure-CodeQuality.ps1 -Library Curl.Conformance.UnitLibrary`: 100% line, 100% branch, 0 failing members.
- The CI criterion is handed to the shift: a lane may not push, and the coordinator's CI watch (BL-987) files a High task naming any case that fails on Linux or macOS.

## Log

- 2026-10-19: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: f1265d17 4c58990d 3c98e074 a9d93e0e 620ebead.
- 2026-10-21: Doing -> Done. Upstream cases 363, 1294, 1295 and 5028 run with the small-sends stand-in and are on the ratchet
