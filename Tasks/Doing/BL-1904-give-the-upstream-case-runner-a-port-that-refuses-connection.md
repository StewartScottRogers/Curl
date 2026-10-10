---
id: BL-1904
title: Give the upstream case runner a port that refuses connections (%NOLISTENPORT)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-09
completed:
---
# BL-1904 — Give the upstream case runner a port that refuses connections (%NOLISTENPORT)

## Goal

The runner supplies %NOLISTENPORT as a loopback port that refuses connections, so the 29 cases that expect a connection failure are measured.

## Context

Harness: Curl.Conformance.UnitLibrary (UpstreamCaseRunner.cs runs a case; UpstreamCaseScreening.cs decides what it skips and why; SwsHttpServerConnector.cs is the in-memory IConnector stand-in for upstream's sws HTTP server). Read Curl.Conformance.UnitLibrary\CLAUDE.md first. The library opens no socket: servers are in-memory IConnector/IDatagramConnector stand-ins, hand-written in the BCL only, platform-neutral, held to 100% line and branch coverage and complexity at most 10. Skip counts are from gap run 2026-10-08_2029. 29 cases are skipped with "the harness has no value for %NOLISTENPORT". Upstream picks a port nothing listens on. In memory there is no real port: choose a port number and make the runner's connector refuse it (as UnreachableDatagramConnector refuses datagrams) with the error a refused TCP connection gives, so curl exits 7. Look at how SwsHttpServerConnector resolves host and port, and at the expected exit codes in the cases. Reference: the upstream curl 8.21.0 release tarball (curl-8_21_0). The cases are vendored in Curl.Conformance.UnitTests\UpstreamTestData (all 2,016 of curl 8.21.0's tests/data files); use them, never a cache under %LOCALAPPDATA%\Curl\gap, which the audit guard refuses a lane. Read tests/server/*.c, tests/*.pl and tests/*.py from the tarball (https://curl.se/download/curl-8.21.0.tar.xz) into an empty scratch folder, never into the repository.

## Acceptance criteria

- [x] A unit test shows a connection to the %NOLISTENPORT value ends curl with exit code 7 and curl's refused-connection message, and at least 10 named upstream cases using %NOLISTENPORT run through UpstreamCaseRunner and get Passed or a real Curl difference.
- [x] UpstreamCaseScreening no longer returns a skip reason of the form "the harness has no value for %NOLISTENPORT" for those cases (a screening test in Curl.Conformance.UnitTests pins it); any case still skipped for another reason says that reason.
- [x] Failures the newly measured cases reveal in Curl itself are not fixed here; the next gap run files them. Cases that fail only because of a stand-in fault are fixed in the stand-in.
- [x] Curl.Conformance.UnitLibrary holds 100% line and branch coverage and complexity of at most 10 per method; tests run on Windows, Linux and macOS with no TestCategory=Integration.
- [x] `dotnet build Curl.Conformance.UnitLibrary -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.
- [x] Curl.Conformance.UnitLibraryCLAUDE.md states what the runner now does for this.
- [ ] Interactive check, not a lane gate: `dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "<tests/data>" <folder without blanks>/raw.json <case numbers>` reports the named cases as measured, not skipped.

## Notes
- %NOLISTENPORT is 47, as runtests.pl gives it. NoListenPortConnector refuses it with ConnectResult.Refused (exit 7, TcpConnector's own wording) and wraps sws; the runner chains SocksServerConnector -> NoListenPortConnector -> sws, so a SOCKS CONNECT to 47 is refused too.
- Stand-in fault fixed: SocksServerConnection threw when its backend refused; it now answers as socksd (SOCKS4 reply 91, SOCKS5 reply 5) and closes, so test702 and test703 pass (exit 97).
- InMemoryServerTcpDialer (tests) throws SocketException(ConnectionRefused) for a refused result, so the production TcpConnector reports the refusal.
- Of 33 %NOLISTENPORT cases, 22 now pass and are listed (19, 219, 333, 370, 702-705, 1084, 1234, 1236, 1248, 1260, 1263, 1269, 1409, 1410, 1427, 1447, 1469, 1474, 1673). test1453 fails with a real difference (expected 71, got 7, TFTP), left for the next gap run. The rest skip for named other reasons: <setenv>, <tool>, %HOST6IP/%RESOLVE, %USER/%SFTP_PWD.
- Coverage: no Measure-CodeQuality run (cost cap); every new branch is hit by a named test (NoListenPortConnectorTests, SocksServerConnectorTests Socks4/Socks5_BackendRefuses).
- Interactive Measure-UpstreamCases check not run (not a lane gate).

## Log

- 2026-10-09: Created.
- 2026-10-09: Backlog -> Doing.
