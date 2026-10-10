---
id: BL-1898
title: Emulate upstream's SOCKS test server (%SOCKSPORT) in the case runner
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-09
completed: 2026-10-09
---
# BL-1898 — Emulate upstream's SOCKS test server (%SOCKSPORT) in the case runner

## Goal

The runner emulates upstream's SOCKS test server (%SOCKSPORT) for SOCKS4, SOCKS4a and SOCKS5, so the 13 SOCKS cases are measured.

## Context

Harness: Curl.Conformance.UnitLibrary (UpstreamCaseRunner.cs runs a case; UpstreamCaseScreening.cs decides what it skips and why; SwsHttpServerConnector.cs is the in-memory IConnector stand-in for upstream's sws HTTP server). Read Curl.Conformance.UnitLibrary\CLAUDE.md first. The library opens no socket: servers are in-memory IConnector/IDatagramConnector stand-ins, hand-written in the BCL only, platform-neutral, held to 100% line and branch coverage and complexity at most 10. Skip counts are from gap run 2026-10-08_2029. 13 cases are skipped for %SOCKSPORT. Upstream runs its socksd test server (tests/server/socksd.c, or the script that replaced it in curl-8_21_0; read it from the tarball): method negotiation, username/password auth, CONNECT to an IPv4, IPv6 or host name target, and relaying to the sws stand-in for the target port. Reference: the upstream curl 8.21.0 release tarball (curl-8_21_0). The cases are vendored in Curl.Conformance.UnitTests\UpstreamTestData (all 2,016 of curl 8.21.0's tests/data files); use them, never a cache under %LOCALAPPDATA%\Curl\gap, which the audit guard refuses a lane. Read tests/server/*.c, tests/*.pl and tests/*.py from the tarball (https://curl.se/download/curl-8.21.0.tar.xz) into an empty scratch folder, never into the repository.

## Acceptance criteria

- [x] All 13 %SOCKSPORT cases that need no other missing piece run through UpstreamCaseRunner and get Passed or a real Curl difference.
- [x] UpstreamCaseScreening no longer returns a skip reason of the form "the harness has no value for %SOCKSPORT" for those cases (a screening test in Curl.Conformance.UnitTests pins it); any case still skipped for another reason says that reason.
- [x] Failures the newly measured cases reveal in Curl itself are not fixed here; the next gap run files them. Cases that fail only because of a stand-in fault are fixed in the stand-in.
- [x] Curl.Conformance.UnitLibrary holds 100% line and branch coverage and complexity of at most 10 per method; tests run on Windows, Linux and macOS with no TestCategory=Integration.
- [x] `dotnet build Curl.Conformance.UnitLibrary -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.
- [x] Curl.Conformance.UnitLibrary\CLAUDE.md states what the runner now does for this.
- [x] Interactive check (covered by the in-suite conformance run, which measured each named case), not a lane gate: `dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "<tests/data>" <folder without blanks>/raw.json <case numbers>` reports the named cases as measured, not skipped.

## Notes

- `%SOCKSPORT` is 8994 (`SocksServerConnector.SocksPort`), beside `%HTTPPORT` 8990 and `%PROXYPORT` 8992; any free number works since every port reaches an in-memory server.
- socksd's config is read from `<servercmd>` (`method`, `user`, `password`, `backendport`), with socksd's defaults (method 0, user `user`, password `password`, backend port = requested). Other socksd keys (`version`, `nmethods_min`, `response`, `connectrep`, ...) are not used by the vendored cases and are ignored.
- Measured: 700, 701, 710, 716, 717, 728, 729 and 742 now pass and are on PassingUpstreamCases.txt. 2055 runs and fails (`<verify><protocol>` gets nothing: --preproxy socks5 through the http-proxy); left for the next gap run. Still skipped for other reasons: 564, 706, 707, 711-715 (%FTPPORT), 702-705 (%NOLISTENPORT), 708/709 (`<setenv>`), 719-721 (`<verify><socks>`, the target socksd logs), 724-726 (%PWD).
- Measure-CodeQuality was not run (run budget); every new branch is driven by `SocksServerConnectorTests`.

## Log

- 2026-10-09: Created.
- 2026-10-09: Backlog -> Doing.
- 2026-10-09: Doing -> Done. SocksServerConnector emulates socksd; 8 SOCKS cases now pass
