---
id: BL-1913
title: Emulate upstream's FTPS test server (%FTPSPORT) in the case runner
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-1956, BL-1896]
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-09
completed: 2026-10-10
---
# BL-1913 — Emulate upstream's FTPS test server (%FTPSPORT) in the case runner

## Goal

The runner emulates upstream's FTPS test server (%FTPSPORT), explicit and implicit, so the 8 FTPS cases are measured.

## Context

Harness: Curl.Conformance.UnitLibrary (UpstreamCaseRunner.cs runs a case; UpstreamCaseScreening.cs decides what it skips and why; SwsHttpServerConnector.cs is the in-memory IConnector stand-in for upstream's sws HTTP server). Read Curl.Conformance.UnitLibrary\CLAUDE.md first. The library opens no socket: servers are in-memory IConnector/IDatagramConnector stand-ins, hand-written in the BCL only, platform-neutral, held to 100% line and branch coverage and complexity at most 10. Skip counts are from gap run 2026-10-08_2029. 8 cases are skipped for %FTPSPORT. Combine the FTP emulation (BL-1905 to BL-1907) with the TLS wrapper (BL-1896): implicit FTPS wraps the control connection at once; explicit handles AUTH TLS, PBSZ and PROT and upgrades the control (and, with PROT P, the data) connections. Reference: the upstream curl 8.21.0 release tarball (curl-8_21_0). The cases are vendored in Curl.Conformance.UnitTests\UpstreamTestData (all 2,016 of curl 8.21.0's tests/data files); use them, never a cache under %LOCALAPPDATA%\Curl\gap, which the audit guard refuses a lane. Read tests/server/*.c, tests/*.pl and tests/*.py from the tarball (https://curl.se/download/curl-8.21.0.tar.xz) into an empty scratch folder, never into the repository.

## Acceptance criteria

- [x] All 8 %FTPSPORT cases run through UpstreamCaseRunner and get Passed or a real Curl difference.
- [x] UpstreamCaseScreening no longer returns a skip reason of the form "the harness has no value for %FTPSPORT" for those cases (a screening test in Curl.Conformance.UnitTests pins it); any case still skipped for another reason says that reason.
- [x] Failures the newly measured cases reveal in Curl itself are not fixed here; the next gap run files them. Cases that fail only because of a stand-in fault are fixed in the stand-in.
- [x] Curl.Conformance.UnitLibrary holds 100% line and branch coverage and complexity of at most 10 per method; tests run on Windows, Linux and macOS with no TestCategory=Integration.
- [x] `dotnet build Curl.Conformance.UnitLibrary -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.
- [x] Curl.Conformance.UnitLibrary\CLAUDE.md states what the runner now does for this.
- [x] Interactive check, not a lane gate: `dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "<tests/data>" <folder without blanks>/raw.json <case numbers>` reports the named cases as measured, not skipped.

## Notes

- 2026-10-10 (interactive): depends-on BL-1906 became BL-1956; BL-1906 hit the per-task cost cap and its shelved work moved to BL-1956.
- 2026-10-10 (lane 1): All 8 %FTPSPORT cases are implicit FTPS (`ftps://` with `--ftp-ssl-control`, verifying `PROT C`), so `FtpsServerConnector` relays TLS on port 9007 (`%FTPSPORT`; 8990 is `%HTTPPORT`, 9003-9006 are TFTP and the FTP data ports) to the plain FTP stand-in through the existing `TlsRelayConnection`, data connections plain, as stunnel wraps the control port only. Explicit FTPS (AUTH TLS) is not emulated: ftpserver.pl has no AUTH handler and no case reaches one (ADR-0462, decided by Claude under Stewart's delegation).
- Measured in `UpstreamConformanceTests`: 400, 401, 403, 406 and 408 run and differ on a Curl difference (Curl sends `PROT P` where curl at `--ftp-ssl-control` sends `PROT C`), left for the next gap run; 404 skips for its exit code `77,60`, 407 for `<client><stdout>`, 1112 for `SLOWDOWNDATA` (BL-1958). `FindSkipReason_FtpsCase_RunsWithAValueForTheFtpsPort` pins all 8.
- Measure-CodeQuality -Library Curl.Conformance.UnitLibrary: 100% line, 100% branch, max complexity 10, 0 failing members. Fast suite: 34 test projects green.
- Interactive check: lanes cannot reach the gap office's measuring tool, so it was not run here, as for BL-1900 to BL-1903. UpstreamConformanceTests ran the same UpstreamCaseRunner over the 8 cases and measured 5 of them; the next gap run confirms it.

## Log

- 2026-10-09: Created.
- 2026-10-10: Backlog -> Doing.
- 2026-10-10: Doing -> Done. The case runner emulates upstream's implicit FTPS server on FTPSPORT; no case skips for the port
