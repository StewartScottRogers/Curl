---
id: BL-1908
title: Carry out ftpserver.pl's remaining servercmd behaviours in the FTP emulation
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-1907]
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-09
completed:
---
# BL-1908 — Carry out ftpserver.pl's remaining servercmd behaviours in the FTP emulation

## Goal

The FTP emulation carries out the remaining <servercmd> behaviours of ftpserver.pl, so FTP cases that use them are no longer skipped.

## Context

Harness: Curl.Conformance.UnitLibrary (UpstreamCaseRunner.cs runs a case; UpstreamCaseScreening.cs decides what it skips and why; SwsHttpServerConnector.cs is the in-memory IConnector stand-in for upstream's sws HTTP server). Read Curl.Conformance.UnitLibrary\CLAUDE.md first. The library opens no socket: servers are in-memory IConnector/IDatagramConnector stand-ins, hand-written in the BCL only, platform-neutral, held to 100% line and branch coverage and complexity at most 10. Skip counts are from gap run 2026-10-08_2029. Builds on BL-1907. List every servercmd command the FTP cases in tests/data use (REPLY, COUNT, DELAY, RETRWEIRDO, RETRNOSIZE, PASVBADIP, NOSAVE, SLOWDOWN and so on: take the list from ftpserver.pl in the tarball and from the cases, not from this task) and carry out each; a command left out is named, with the reason, in the Notes and stays a skip reason. Update UpstreamCaseScreening.UnsupportedServerCommand so it applies the right command set per server type. Reference: the upstream curl 8.21.0 release tarball (curl-8_21_0). The cases are vendored in Curl.Conformance.UnitTests\UpstreamTestData (all 2,016 of curl 8.21.0's tests/data files); use them, never a cache under %LOCALAPPDATA%\Curl\gap, which the audit guard refuses a lane. Read tests/server/*.c, tests/*.pl and tests/*.py from the tarball (https://curl.se/download/curl-8.21.0.tar.xz) into an empty scratch folder, never into the repository.

## Acceptance criteria

- [ ] At least 20 named FTP cases, each using a different servercmd behaviour, run through UpstreamCaseRunner and get Passed or a real Curl difference. After this task fewer than 10 of the 240 %FTPPORT cases are still skipped, and the Notes list why.
- [ ] UpstreamCaseScreening no longer returns a skip reason of the form "the harness does not emulate the ftp server" for those cases (a screening test in Curl.Conformance.UnitTests pins it); any case still skipped for another reason says that reason.
- [ ] Failures the newly measured cases reveal in Curl itself are not fixed here; the next gap run files them. Cases that fail only because of a stand-in fault are fixed in the stand-in.
- [ ] Curl.Conformance.UnitLibrary holds 100% line and branch coverage and complexity of at most 10 per method; tests run on Windows, Linux and macOS with no TestCategory=Integration.
- [ ] `dotnet build Curl.Conformance.UnitLibrary -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.
- [ ] Curl.Conformance.UnitLibrary\CLAUDE.md states what the runner now does for this.
- [ ] Interactive check, not a lane gate: `dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "<tests/data>" <folder without blanks>/raw.json <case numbers>` reports the named cases as measured, not skipped.

## Notes

## Log

- 2026-10-09: Created.
