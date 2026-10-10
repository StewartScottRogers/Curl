---
id: BL-1909
title: Emulate upstream's SMTP server in the case runner
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-1895, BL-1925, BL-1905]
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-09
completed:
---
# BL-1909 — Emulate upstream's SMTP server in the case runner

## Goal

The runner emulates upstream's SMTP server (%SMTPPORT), so the 88 SMTP cases are measured.

## Context

Harness: Curl.Conformance.UnitLibrary (UpstreamCaseRunner.cs runs a case; UpstreamCaseScreening.cs decides what it skips and why; SwsHttpServerConnector.cs is the in-memory IConnector stand-in for upstream's sws HTTP server). Read Curl.Conformance.UnitLibrary\CLAUDE.md first. The library opens no socket: servers are in-memory IConnector/IDatagramConnector stand-ins, hand-written in the BCL only, platform-neutral, held to 100% line and branch coverage and complexity at most 10. Skip counts are from gap run 2026-10-08_2029. 88 cases are skipped for %SMTPPORT. Build on the line-protocol core of BL-1895; behaviour from the smtp parts of tests/ftpserver.pl in the tarball (greeting, EHLO/HELO, MAIL FROM, RCPT TO, DATA with the dot terminator, RSET, VRFY, EXPN, AUTH exchanges as the script does, replies from <reply> parts and servercmd). Record the message for <verify><upload>. Reference: the upstream curl 8.21.0 release tarball (curl-8_21_0). The cases are vendored in Curl.Conformance.UnitTests\UpstreamTestData (all 2,016 of curl 8.21.0's tests/data files); use them, never a cache under %LOCALAPPDATA%\Curl\gap, which the audit guard refuses a lane. Read tests/server/*.c, tests/*.pl and tests/*.py from the tarball (https://curl.se/download/curl-8.21.0.tar.xz) into an empty scratch folder, never into the repository.

## Acceptance criteria

- [ ] At least 20 named SMTP cases run through UpstreamCaseRunner and get Passed or a real Curl difference.
- [ ] UpstreamCaseScreening no longer returns a skip reason of the form "the harness has no value for %SMTPPORT" for those cases (a screening test in Curl.Conformance.UnitTests pins it); any case still skipped for another reason says that reason.
- [ ] Failures the newly measured cases reveal in Curl itself are not fixed here; the next gap run files them. Cases that fail only because of a stand-in fault are fixed in the stand-in.
- [ ] Curl.Conformance.UnitLibrary holds 100% line and branch coverage and complexity of at most 10 per method; tests run on Windows, Linux and macOS with no TestCategory=Integration.
- [ ] `dotnet build Curl.Conformance.UnitLibrary -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.
- [ ] Curl.Conformance.UnitLibrary\CLAUDE.md states what the runner now does for this.
- [ ] Interactive check, not a lane gate: `dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "<tests/data>" <folder without blanks>/raw.json <case numbers>` reports the named cases as measured, not skipped.

## Notes

- 2026-10-09 (lane 2): split, as BL-1905 was. This run's $2 cost cap cannot hold the SMTP responder, the runner wiring, the %SMTPPORT screening change, 20 sample cases and 100% coverage together. Measured: the line-protocol core (BL-1895) and `FtpControlChannelResponder` exist, but `UpstreamCaseRunner.RunScreenedAsync` is hard-wired to `SwsHttpServerConnector` (its `Abandon`, `ReceivedBytes`, `ProxyReceivedBytes`) and `LineProtocolServerConnector.EmulatedServers` is empty, so no line-protocol stand-in is reachable from a case yet. BL-1905 owns that runner wiring for FTP; SMTP should reuse it rather than build a second dispatch, so BL-1909 waits on BL-1905. The SMTP responder itself is now BL-1925 (same touches). BL-1909 keeps: choosing `SmtpResponder` for `<server>smtp</server>`, a %SMTPPORT value, adding "smtp" to `EmulatedServers`, comparing `<verify><upload>` with the recorded message, the screening test, 20 sample cases and the CLAUDE.md update. Nothing was coded in this run.

## Log

- 2026-10-09: Created.
- 2026-10-09: Backlog -> Doing.
- 2026-10-09: Doing -> Backlog. Waits on BL-1925 (the SMTP responder, split off to fit one lane run) and BL-1905 (the runner wiring for line-protocol stand-ins)
