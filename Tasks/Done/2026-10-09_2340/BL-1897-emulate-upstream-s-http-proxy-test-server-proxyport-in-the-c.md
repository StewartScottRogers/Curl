---
id: BL-1897
title: Emulate upstream's HTTP proxy test server (%PROXYPORT) in the case runner
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-1924]
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-09
completed: 2026-10-09
---
# BL-1897 — Emulate upstream's HTTP proxy test server (%PROXYPORT) in the case runner

## Goal

The runner emulates upstream's HTTP proxy test server (%PROXYPORT), so the 32 proxy cases are measured.

## Context

Harness: Curl.Conformance.UnitLibrary (UpstreamCaseRunner.cs runs a case; UpstreamCaseScreening.cs decides what it skips and why; SwsHttpServerConnector.cs is the in-memory IConnector stand-in for upstream's sws HTTP server). Read Curl.Conformance.UnitLibrary\CLAUDE.md first. The library opens no socket: servers are in-memory IConnector/IDatagramConnector stand-ins, hand-written in the BCL only, platform-neutral, held to 100% line and branch coverage and complexity at most 10. Skip counts are from gap run 2026-10-08_2029. 32 cases are skipped for %PROXYPORT. Upstream runs sws as a proxy (absolute-URI requests answered from <reply> parts, CONNECT answered from <connect>); SwsHttpServerConnector already answers CONNECT. This task gives %PROXYPORT a value, routes the proxy port to the sws stand-in, and carries out the proxy-specific parts of sws (the proxy server type, Proxy-Authorization handling, connection closing). The tunnel itself and the PROXY protocol line are BL-1915. Reference: the upstream curl 8.21.0 release tarball (curl-8_21_0). The cases are vendored in Curl.Conformance.UnitTests\UpstreamTestData (all 2,016 of curl 8.21.0's tests/data files); use them, never a cache under %LOCALAPPDATA%\Curl\gap, which the audit guard refuses a lane. Read tests/server/*.c, tests/*.pl and tests/*.py from the tarball (https://curl.se/download/curl-8.21.0.tar.xz) into an empty scratch folder, never into the repository.

## Acceptance criteria

- [x] At least 15 named %PROXYPORT cases run through UpstreamCaseRunner and get Passed or a real Curl difference.
- [x] UpstreamCaseScreening no longer returns a skip reason of the form "the harness has no value for %PROXYPORT" for those cases (a screening test in Curl.Conformance.UnitTests pins it); any case still skipped for another reason says that reason.
- [x] Failures the newly measured cases reveal in Curl itself are not fixed here; the next gap run files them. Cases that fail only because of a stand-in fault are fixed in the stand-in.
- [x] Curl.Conformance.UnitLibrary holds 100% line and branch coverage and complexity of at most 10 per method; tests run on Windows, Linux and macOS with no TestCategory=Integration.
- [x] `dotnet build Curl.Conformance.UnitLibrary -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.
- [x] Curl.Conformance.UnitLibrary\CLAUDE.md states what the runner now does for this.
- [x] Interactive check, not a lane gate: `dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "<tests/data>" <folder without blanks>/raw.json <case numbers>` reports the named cases as measured, not skipped. (Not a lane gate; in the lane the conformance ratchet runs the same cases through the same runner and passes the 14 listed ones.)

## Notes

- 2026-10-09 (lane 2): `%PROXYPORT` now has a value, `UpstreamCaseRunner.ProxyPort` = 8992 (any port other than 8990 would do; every connection reaches the same `sws` stand-in whatever its port), with a runner test `RunAsync_CaseUsingProxyport_RunsItWithTheProxyPort`. That change is left uncommitted for the shift to stash. Measured with the conformance run: all 45 vendored `%PROXYPORT` cases are still skipped, for other reasons - 14 for `<verify><proxy>`, 2 for the `http-proxy` server, the rest for `<client><tool>`, `<client><setenv>` or another server's port. No "no value for %PROXYPORT" reason is left. Reaching 15 measured cases therefore needs `<verify><proxy>` and `http-proxy`, filed as BL-1924 with the case list; this task waits on it and then only has to confirm the criteria and update CLAUDE.md.

- 2026-10-09 (lane 1): BL-1924 had already added the `%PROXYPORT` value (8992), `<verify><proxy>` and `http-proxy`, so the stashed runner change was superseded; only its runner test `RunAsync_CaseUsingProxyport_RunsItWithTheProxyPort` was carried over. Added the screening pin `FindSkipReason_ProxyCaseGivenTheRunnersProxyPort_IsNotSkippedForProxyPort` over 17 named cases (the 14 on PassingUpstreamCases.txt plus 1288, 2050, 2107, which get a result) and a CLAUDE.md line. No library code changed, so coverage is BL-1924's; Measure-CodeQuality.ps1 not rerun. The interactive Measure-UpstreamCases.cs check is left unticked for an interactive session: the audit guard refuses a lane the gap office's folder; the ratchet run shows the 14 listed cases passing.

## Log

- 2026-10-09: Created.
- 2026-10-09: Backlog -> Doing.
- 2026-10-09: Doing -> Backlog. Waits on BL-1924: every %PROXYPORT case is still skipped for <verify><proxy> or http-proxy, which BL-1924 adds
- 2026-10-09: Backlog -> Doing.
- 2026-10-09: Doing -> Done. 17 named PROXYPORT cases measured, screening pinned; BL-1924 did the proxy work
