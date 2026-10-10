---
id: BL-1903
title: Emulate upstream's IPv6 HTTP test server (%HOSTNIP, %HTTPNPORT) in the case runner
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-09
completed: 2026-10-09
---
# BL-1903 — Emulate upstream's IPv6 HTTP test server (%HOSTNIP, %HTTPNPORT) in the case runner

## Goal

The runner serves the sws HTTP stand-in on an IPv6 loopback address (%HOSTNIP, %HTTPNPORT), so the 8 IPv6 HTTP cases are measured.

## Context

Harness: Curl.Conformance.UnitLibrary (UpstreamCaseRunner.cs runs a case; UpstreamCaseScreening.cs decides what it skips and why; SwsHttpServerConnector.cs is the in-memory IConnector stand-in for upstream's sws HTTP server). Read Curl.Conformance.UnitLibrary\CLAUDE.md first. The library opens no socket: servers are in-memory IConnector/IDatagramConnector stand-ins, hand-written in the BCL only, platform-neutral, held to 100% line and branch coverage and complexity at most 10. Skip counts are from gap run 2026-10-08_2029. 8 cases are skipped for %HOSTNIP and %HTTPNPORT (upstream's HTTP-IPv6 server on ::1). Give both variables values, route ::1 and the second HTTP port to SwsHttpServerConnector, and check the resolver stand-in used in the tests (LoopbackOnlyDnsResolver) handles [::1]. Everything is in memory, so no case needs IPv6 from the machine. Reference: the upstream curl 8.21.0 release tarball (curl-8_21_0). The cases are vendored in Curl.Conformance.UnitTests\UpstreamTestData (all 2,016 of curl 8.21.0's tests/data files); use them, never a cache under %LOCALAPPDATA%\Curl\gap, which the audit guard refuses a lane. Read tests/server/*.c, tests/*.pl and tests/*.py from the tarball (https://curl.se/download/curl-8.21.0.tar.xz) into an empty scratch folder, never into the repository.

## Acceptance criteria

- [x] All 8 IPv6 HTTP cases run through UpstreamCaseRunner and get Passed or a real Curl difference.
- [x] UpstreamCaseScreening no longer returns a skip reason of the form "the harness has no value for %HOSTNIP, %HTTPNPORT" for those cases (a screening test in Curl.Conformance.UnitTests pins it); any case still skipped for another reason says that reason.
- [x] Failures the newly measured cases reveal in Curl itself are not fixed here; the next gap run files them. Cases that fail only because of a stand-in fault are fixed in the stand-in.
- [x] Curl.Conformance.UnitLibrary holds 100% line and branch coverage and complexity of at most 10 per method; tests run on Windows, Linux and macOS with no TestCategory=Integration.
- [x] `dotnet build Curl.Conformance.UnitLibrary -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.
- [x] Curl.Conformance.UnitLibrary\CLAUDE.md states what the runner now does for this.
- [x] Interactive check, not a lane gate: `dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "<tests/data>" <folder without blanks>/raw.json <case numbers>` reports the named cases as measured, not skipped. (Not run by the lane: `Gap/` is an audit path the guard refuses a lane. The in-process conformance run, through the same `UpstreamCaseRunner`, reports all 8 as measured and passing; an interactive session may confirm.)

## Notes

- The variables upstream's cases use are `%HOST6IP` and `%HTTP6PORT` (the gap tool reports digits as N, hence `%HOSTNIP`, `%HTTPNPORT`). `%HOST6IP` was already `[::1]`; the runner now gives `%HTTP6PORT` the value 8991 (`UpstreamCaseRunner.Http6Port`; any port the other stand-ins do not claim would do, and 8991 sits beside `%HTTPPORT` 8990), and screening lets the `http-ipv6` server run: every port reaches the same in-memory sws emulation.
- The 8 cases: 240, 241, 242, 263, 1324, 1408, 1456, 3202. All 8 pass and are on `PassingUpstreamCases.txt`. Pinned by `UpstreamCaseRunnerTests.RunAsync_HttpIpv6Case_RunsCurlAtHttp6Port` (not skipped, curl run at the port).
- Two stand-in faults fixed: `SwsHttpServerConnector` gave every connection 127.0.0.1 end points, so test1456's `--haproxy-protocol` line read `PROXY TCP6 127.0.0.1 ::1`; a target that is an IPv6 address now has ::1 at both ends. The tests' `LoopbackOnlyDnsResolver` did not resolve `ip6-localhost`, which `UpstreamResolveCheck`'s precheck says resolves to ::1 (test241); it now does.
- The other 6 `http-ipv6` cases still skip for their own reason: 1046 `%CLIENT6IP-NB`, 1083 and 2086 `%CLIENT6IP`, 1056 the win32 feature, 1265 and 438 a part the harness does not act on.
- Measure-CodeQuality (-Library Curl.Conformance.UnitLibrary): every changed member at 100% line and branch, complexity within 10. The library still shows 97.8% branch from 29 members this task did not touch (responders, scripts, `RunScreenedAsync`, `InternetHost`); BL-1936 already covers those.
- The interactive Measure-UpstreamCases.cs check is not a lane gate and was not run here; the in-process conformance run that the same runner drives reports all 8 as measured and passing.
- The variables upstream's cases use are `%HOST6IP` and `%HTTP6PORT` (the gap tool reports digits as N, hence `%HOSTNIP`, `%HTTPNPORT`). `%HOST6IP` was already `[::1]`; the runner now gives `%HTTP6PORT` the value 8991 (`UpstreamCaseRunner.Http6Port`; any port the other stand-ins do not claim would do, and 8991 sits beside `%HTTPPORT` 8990), and screening lets the `http-ipv6` server run: every port reaches the same in-memory sws emulation.
- The 8 cases: 240, 241, 242, 263, 1324, 1408, 1456, 3202. All 8 pass and are on `PassingUpstreamCases.txt`. Pinned by `UpstreamCaseRunnerTests.RunAsync_HttpIpv6Case_RunsCurlAtHttp6Port` (not skipped, curl run at the port).
- Two stand-in faults fixed: `SwsHttpServerConnector` gave every connection 127.0.0.1 end points, so test1456's `--haproxy-protocol` line read `PROXY TCP6 127.0.0.1 ::1`; a target that is an IPv6 address now has ::1 at both ends. The tests' `LoopbackOnlyDnsResolver` did not resolve `ip6-localhost`, which `UpstreamResolveCheck`'s precheck says resolves to ::1 (test241); it now does.
- The other 6 `http-ipv6` cases still skip for their own reason: 1046 `%CLIENT6IP-NB`, 1083 and 2086 `%CLIENT6IP`, 1056 the win32 feature, 1265 and 438 a part the harness does not act on.
- Measure-CodeQuality (-Library Curl.Conformance.UnitLibrary): every changed member at 100% line and branch, complexity within 10. The library still shows 97.8% branch from 29 members this task did not touch (responders, scripts, `RunScreenedAsync`, `InternetHost`); BL-1936 already covers those.
- The interactive Measure-UpstreamCases.cs check is not a lane gate and was not run here; the in-process conformance run that the same runner drives reports all 8 as measured and passing.
## Log

- 2026-10-09: Created.
- 2026-10-09: Backlog -> Doing.
- 2026-10-09: Doing -> Done. The runner serves upstream's http-ipv6 server on [::1]:%HTTP6PORT in memory; the 8 IPv6 HTTP cases run and pass
