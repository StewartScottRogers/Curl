---
id: BL-1901
title: Emulate upstream's TFTP test server (%TFTPPORT) in the case runner
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-09
completed:
---
# BL-1901 — Emulate upstream's TFTP test server (%TFTPPORT) in the case runner

## Goal

The runner emulates upstream's TFTP test server (%TFTPPORT) over datagrams, so the 15 TFTP cases are measured.

## Context

Harness: Curl.Conformance.UnitLibrary (UpstreamCaseRunner.cs runs a case; UpstreamCaseScreening.cs decides what it skips and why; SwsHttpServerConnector.cs is the in-memory IConnector stand-in for upstream's sws HTTP server). Read Curl.Conformance.UnitLibrary\CLAUDE.md first. The library opens no socket: servers are in-memory IConnector/IDatagramConnector stand-ins, hand-written in the BCL only, platform-neutral, held to 100% line and branch coverage and complexity at most 10. Skip counts are from gap run 2026-10-08_2029. 15 cases are skipped for %TFTPPORT. Upstream's server is tests/server/tftpd.c (read it from the tarball): RRQ/WRQ, DATA/ACK/OACK with options (blksize, tsize), ERROR, timeouts. Use IDatagramConnector (see UnreachableDatagramConnector for the current stand-in and how UpstreamCurlInvocation receives it); data comes from <data>, uploads are recorded for <verify><upload>. Reference: the upstream curl 8.21.0 release tarball (curl-8_21_0). The cases are vendored in Curl.Conformance.UnitTests\UpstreamTestData (all 2,016 of curl 8.21.0's tests/data files); use them, never a cache under %LOCALAPPDATA%\Curl\gap, which the audit guard refuses a lane. Read tests/server/*.c, tests/*.pl and tests/*.py from the tarball (https://curl.se/download/curl-8.21.0.tar.xz) into an empty scratch folder, never into the repository.

## Acceptance criteria

- [ ] All 15 %TFTPPORT cases run through UpstreamCaseRunner and get Passed or a real Curl difference.
- [ ] UpstreamCaseScreening no longer returns a skip reason of the form "the harness has no value for %TFTPPORT" for those cases (a screening test in Curl.Conformance.UnitTests pins it); any case still skipped for another reason says that reason.
- [ ] Failures the newly measured cases reveal in Curl itself are not fixed here; the next gap run files them. Cases that fail only because of a stand-in fault are fixed in the stand-in.
- [ ] Curl.Conformance.UnitLibrary holds 100% line and branch coverage and complexity of at most 10 per method; tests run on Windows, Linux and macOS with no TestCategory=Integration.
- [ ] `dotnet build Curl.Conformance.UnitLibrary -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.
- [ ] Curl.Conformance.UnitLibrary\CLAUDE.md states what the runner now does for this.
- [ ] Interactive check, not a lane gate: `dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "<tests/data>" <folder without blanks>/raw.json <case numbers>` reports the named cases as measured, not skipped.

## Notes

- 2026-10-09 (lane 2, parked at the run's cost cap): the work in progress is uncommitted and stashed by the shift.
  Done: `TftpServerConnector` (port 8996, transfer port 8997, `ProtocolLog` = tftpd's `server.input` dump,
  `FindFile` per tftpd's `validate_access`, `writedelay: N` in seconds) and `TftpServerChannel` (no OACK, as
  tftpd never sends one; 512-byte blocks; netascii LF->CRLF, CR->CR NUL; ERROR 2 "Access violation", ERROR 4
  "Illegal TFTP operation"); the runner wires `%TFTPPORT`, passes the connector as the datagram connector and
  appends its dump to the protocol bytes; screening admits `tftp`. The library builds clean with -warnaserror.
  Left: `Curl.Conformance.UnitTests/TftpServerConnectorTests.cs` has split string literals at lines 44 and 92-95
  (a sed edit put a real newline after `hello` / `three`; each should end `\n"`). Fix them and run the tests,
  then measure coverage (`Measure-CodeQuality.ps1 -Library Curl.Conformance.UnitLibrary`), update
  `UnreachableDatagramConnector`'s summary (it no longer waits "until a TFTP emulation exists") and the library's
  CLAUDE.md. Upload cases (test285, test286, test1243) still skip, naming `<verify><upload>`: uploads are not
  recorded yet.

## Log

- 2026-10-09: Created.
- 2026-10-09: Backlog -> Doing.
- 2026-10-09: Doing -> Backlog. Parked at the run's cost cap: emulation built, tests need their split literals fixed, then coverage and CLAUDE.md (see Notes)
