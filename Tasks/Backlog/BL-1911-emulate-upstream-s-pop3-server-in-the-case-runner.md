---
id: BL-1911
title: Emulate upstream's POP3 server in the case runner
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-1895, BL-1927, BL-1905]
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-09
completed:
---
# BL-1911 — Emulate upstream's POP3 server in the case runner

## Goal

The runner emulates upstream's POP3 server (%POP3PORT), so the 51 POP3 cases are measured.

## Context

Harness: Curl.Conformance.UnitLibrary (UpstreamCaseRunner.cs runs a case; UpstreamCaseScreening.cs decides what it skips and why; SwsHttpServerConnector.cs is the in-memory IConnector stand-in for upstream's sws HTTP server). Read Curl.Conformance.UnitLibrary\CLAUDE.md first. The library opens no socket: servers are in-memory IConnector/IDatagramConnector stand-ins, hand-written in the BCL only, platform-neutral, held to 100% line and branch coverage and complexity at most 10. Skip counts are from gap run 2026-10-08_2029. 51 cases are skipped for %POP3PORT. Build on BL-1895; behaviour from the pop3 parts of tests/ftpserver.pl (USER/PASS, STAT, LIST, RETR, DELE, TOP, UIDL, CAPA, multi-line replies ending with a lone dot, APOP/AUTH exchanges). Reference: the upstream curl 8.21.0 release tarball (curl-8_21_0). The cases are vendored in Curl.Conformance.UnitTests\UpstreamTestData (all 2,016 of curl 8.21.0's tests/data files); use them, never a cache under %LOCALAPPDATA%\Curl\gap, which the audit guard refuses a lane. Read tests/server/*.c, tests/*.pl and tests/*.py from the tarball (https://curl.se/download/curl-8.21.0.tar.xz) into an empty scratch folder, never into the repository.

## Acceptance criteria

- [ ] At least 20 named POP3 cases run through UpstreamCaseRunner and get Passed or a real Curl difference.
- [ ] UpstreamCaseScreening no longer returns a skip reason of the form "the harness has no value for %POP3PORT" for those cases (a screening test in Curl.Conformance.UnitTests pins it); any case still skipped for another reason says that reason.
- [ ] Failures the newly measured cases reveal in Curl itself are not fixed here; the next gap run files them. Cases that fail only because of a stand-in fault are fixed in the stand-in.
- [ ] Curl.Conformance.UnitLibrary holds 100% line and branch coverage and complexity of at most 10 per method; tests run on Windows, Linux and macOS with no TestCategory=Integration.
- [ ] `dotnet build Curl.Conformance.UnitLibrary -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.
- [ ] Curl.Conformance.UnitLibrary\CLAUDE.md states what the runner now does for this.
- [ ] Interactive check, not a lane gate: `dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "<tests/data>" <folder without blanks>/raw.json <case numbers>` reports the named cases as measured, not skipped.

## Notes

- Split on 2026-10-09 (lane 2), following the SMTP and IMAP precedent (BL-1925/BL-1909, BL-1926/BL-1910): `UpstreamCaseRunner` routes only to the sws HTTP stand-in today (no `LineProtocolServerConnector` is wired in), and no POP3 responder exists. BL-1927 writes `Pop3Responder`; BL-1905 (FTP control channel in the runner) is the first wiring of a `LineProtocolServerConnector` into `UpstreamCaseRunner`, which BL-1909 and BL-1910 also wait on. This task then wires POP3 in and measures the 51 cases. Nothing was measured or written for it in this run.

## Log

- 2026-10-09: Created.
- 2026-10-09: Backlog -> Doing.
- 2026-10-09: Doing -> Backlog. Waits on BL-1927 (Pop3Responder) and BL-1905 (first line-protocol wiring in the runner)
