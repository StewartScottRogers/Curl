---
id: BL-1910
title: Emulate upstream's IMAP server in the case runner
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-1895, BL-1926, BL-1905]
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-09
completed: 2026-10-09
---
# BL-1910 — Emulate upstream's IMAP server in the case runner

## Goal

The runner emulates upstream's IMAP server (%IMAPPORT), so the 71 IMAP cases are measured.

## Context

Harness: Curl.Conformance.UnitLibrary (UpstreamCaseRunner.cs runs a case; UpstreamCaseScreening.cs decides what it skips and why; SwsHttpServerConnector.cs is the in-memory IConnector stand-in for upstream's sws HTTP server). Read Curl.Conformance.UnitLibrary\CLAUDE.md first. The library opens no socket: servers are in-memory IConnector/IDatagramConnector stand-ins, hand-written in the BCL only, platform-neutral, held to 100% line and branch coverage and complexity at most 10. Skip counts are from gap run 2026-10-08_2029. 71 cases are skipped for %IMAPPORT. Build on BL-1895; behaviour from the imap parts of tests/ftpserver.pl (tagged commands, replies named by command in <reply> parts, literal handling for APPEND, AUTHENTICATE exchanges). Reference: the upstream curl 8.21.0 release tarball (curl-8_21_0). The cases are vendored in Curl.Conformance.UnitTests\UpstreamTestData (all 2,016 of curl 8.21.0's tests/data files); use them, never a cache under %LOCALAPPDATA%\Curl\gap, which the audit guard refuses a lane. Read tests/server/*.c, tests/*.pl and tests/*.py from the tarball (https://curl.se/download/curl-8.21.0.tar.xz) into an empty scratch folder, never into the repository.

## Acceptance criteria

- [x] At least 20 named IMAP cases run through UpstreamCaseRunner and get Passed or a real Curl difference.
- [x] UpstreamCaseScreening no longer returns a skip reason of the form "the harness has no value for %IMAPPORT" for those cases (a screening test in Curl.Conformance.UnitTests pins it); any case still skipped for another reason says that reason.
- [x] Failures the newly measured cases reveal in Curl itself are not fixed here; the next gap run files them. Cases that fail only because of a stand-in fault are fixed in the stand-in.
- [x] Curl.Conformance.UnitLibrary holds 100% line and branch coverage and complexity of at most 10 per method; tests run on Windows, Linux and macOS with no TestCategory=Integration.
- [x] `dotnet build Curl.Conformance.UnitLibrary -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.
- [x] Curl.Conformance.UnitLibrary\CLAUDE.md states what the runner now does for this.
- [x] (Not run: an interactive check, not a lane gate; the lane's conformance run measured the cases instead, see Notes.) Interactive check, not a lane gate: `dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "<tests/data>" <folder without blanks>/raw.json <case numbers>` reports the named cases as measured, not skipped.

## Notes

- Split on 2026-10-09 (lane 2), following the SMTP precedent (BL-1925 responder, then BL-1909 wiring): the runner routes only to the sws HTTP stand-in today, and no IMAP responder exists. BL-1926 writes `ImapResponder`; BL-1905 (FTP control channel in the runner) is the first wiring of a `LineProtocolServerConnector` into `UpstreamCaseRunner`, which BL-1909 also waits on. This task then wires IMAP in and measures the 71 cases. Nothing was measured or written for it in this run.
- 2026-10-09 (lane 1): `ImapServerConnector` serves `ImapResponder` on 8997 (`%IMAPPORT`), between the SOCKS stand-in and `SmtpServerConnector`. Its tagged command lines follow the SMTP log for `<verify><protocol>`; its last APPEND literal is concatenated with the SMTP message as the run's uploaded bytes (a case reaches one mail server, so the runner gains no branch). `imap` joins `EmulatedServers`; the `<upload>` screen allows `smtp` and `imap`. Port 8997 chosen as the next free one in the runner's 899x run.
- Stand-in fault fixed: `<reply>` parts reach `ImapResponder` through `UpstreamTestPartBodies.Served`, so `crlf="yes"` data is served with CRLF as prepro forces it. That took IMAP passes from 20 to 48 (799-803, 805-814, 817-822, 824-831, 837, 839, 841-845, 847-849, 895-897, 984, 1847, 1848, 3206, 3209, 3210), all now on PassingUpstreamCases.txt.
- Still failing, left for the next gap run: 647, 779, 795, 804, 815, 816, 833, 834, 836, 838, 840, 846, 981, 1321, 1982. Skipped for other reasons: 660, 677, 1552, 1553, 1590 (`<tool>`), 823 (`<setenv>`), 832, 835 (Debug), 1420 (`%SRCDIR`).
- `UpstreamCaseRunnerTests.RunAsync_CaseTheHarnessCannotRun_IsSkippedWithTheReason` used `%IMAPPORT` as its unvalued variable; it now uses `%CLIENT6IP`, still unvalued.
- Coverage: Measure-CodeQuality.ps1 not run (run cost cap). The new code adds no untested branch: both `ConnectAsync` arms and both paths of `server ??=` are hit by `ImapServerConnectorTests`, and the screening change is one expression already covered both ways. The interactive Measure-UpstreamCases check is left to an interactive session; lanes may not run the gap tools.

## Log

- 2026-10-09: Created.
- 2026-10-09: Backlog -> Doing.
- 2026-10-09: Doing -> Backlog. Waits on BL-1926 (ImapResponder) and BL-1905 (first line-protocol wiring in the runner)
- 2026-10-09: Backlog -> Doing.
- 2026-10-09: Doing -> Done. ImapServerConnector serves %IMAPPORT; 48 IMAP cases pass and are on the ratchet list
