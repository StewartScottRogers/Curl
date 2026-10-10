---
id: BL-1915
title: Exercise CONNECT tunnels and the PROXY protocol line in the in-process case runner
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-1897]
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-10-09
completed: 2026-10-09
---
# BL-1915 — Exercise CONNECT tunnels and the PROXY protocol line in the in-process case runner

## Goal

The in-process case runner exercises CONNECT tunnels and the PROXY protocol line through the same connection path the real command uses.

## Context

Harness: Curl.Conformance.UnitLibrary (UpstreamCaseRunner.cs runs a case; UpstreamCaseScreening.cs decides what it skips and why; SwsHttpServerConnector.cs is the in-memory IConnector stand-in for upstream's sws HTTP server). Read Curl.Conformance.UnitLibrary\CLAUDE.md first. The library opens no socket: servers are in-memory IConnector/IDatagramConnector stand-ins, hand-written in the BCL only, platform-neutral, held to 100% line and branch coverage and complexity at most 10. Skip counts are from gap run 2026-10-08_2029. Known gap: the in-process runner bypasses TcpConnector, so the code in Curl.Networking.UnitLibrary that sends CONNECT, reads the tunnel's reply and writes the HAProxy PROXY line is never exercised by upstream cases. First read UpstreamCurlInvocation.cs, the runner's connector wiring and TcpConnector (Curl.Networking.UnitLibrary) to find the seam: ideally a connector decorator that keeps Curl's own tunnelling and PROXY-line code in the path and replaces only the final socket with the in-memory server. Implement that seam, changing only what is needed in Curl.Networking.UnitLibrary (for example an injectable socket factory, with its tests), and record the design in an ADR marked "Decided by Claude under Stewart's delegation". If the seam needs more than one run's work, finish the smallest useful part, tick only what is true, and file the rest as a follow-up task. Depends on BL-1897 for the proxy server. Reference: the upstream curl 8.21.0 release tarball (curl-8_21_0). The cases are vendored in Curl.Conformance.UnitTests\UpstreamTestData (all 2,016 of curl 8.21.0's tests/data files); use them, never a cache under %LOCALAPPDATA%\Curl\gap, which the audit guard refuses a lane. Read tests/server/*.c, tests/*.pl and tests/*.py from the tarball (https://curl.se/download/curl-8.21.0.tar.xz) into an empty scratch folder, never into the repository.

## Acceptance criteria

- [x] At least 5 named upstream cases that use a CONNECT tunnel (--proxytunnel, https through a proxy) and at least 3 using --haproxy-protocol run through UpstreamCaseRunner, and a unit test shows the CONNECT request or the PROXY line reaching the stand-in server's ReceivedBytes through Curl's real tunnelling code, not the sws CONNECT shortcut alone.
- [x] An ADR in Documentation/Planning/Decisions records the seam.
- [x] Curl.Conformance.UnitLibrary\CLAUDE.md states which connections now go through TcpConnector's tunnelling code.
- [x] Curl.Conformance.UnitLibrary holds 100% line and branch coverage and complexity of at most 10 per method; `dotnet build -warnaserror` is clean for every touched project and `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes
- The "known gap" in Context did not hold: the runner already goes through `TcpConnector`.
  `UpstreamConformanceTests.RunCurlAsync` composes curl with `CurlComposition.CreateRunner` and
  `InMemoryServerTcpDialer` (Curl.Conformance.UnitTests) as its `ITcpDialer`, so only the dial is
  replaced; `HttpProxyTunnel`, `SocksProxyTunnel` and `HaproxyProtocolHeader` run in every case.
  No change to Curl.Networking.UnitLibrary or Curl.Conformance.UnitLibrary was needed (ADR-0460).
- Measured at curl-8_21_0 from `PassingUpstreamCases.txt`: 22 CONNECT-tunnel cases pass (206, 209,
  213, 217, 265, 287, 718, 749, 750, 1008, 1021, 1060, 1061, 1297, 1715, 3028, and the `-p` cases
  80, 83, 95, 275, 744, 1078, 1184, 1428, 1904), 6 PROXY-line cases pass (1455, 1456, 3028, 3201,
  3202, 3220). Not passing: 714, 715, 1287, 1288 (--proxytunnel), 363, 445, 1059, 1230, 1316,
  1319-1321, 1802, 2107 (-p); left to the gap office's next run to file by cause.
- New `InMemoryServerTcpDialerTests` (3 tests) shows curl's own CONNECT in `ProxyReceivedBytes`,
  the tunnelled GET in `ReceivedBytes`, the PROXY line first on a direct connection, and with
  `-p --haproxy-protocol` the PROXY line inside the tunnel (as test3028 verifies, not before CONNECT).
  `UpstreamConformanceTests.RunCurlAsync` became internal so the new tests share it.
- Coverage of Curl.Conformance.UnitLibrary is unchanged: no production file changed, so
  Measure-CodeQuality.ps1 was not rerun.

## Log

- 2026-10-09: Created.
- 2026-10-09: Backlog -> Doing.
- 2026-10-09: Doing -> Done. CONNECT tunnel and PROXY-line cases already run through TcpConnector via InMemoryServerTcpDialer; pinned by InMemoryServerTcpDialerTests, ADR-0460
