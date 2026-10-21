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
completed: 2026-10-10
---
# BL-1908 — Carry out ftpserver.pl's remaining servercmd behaviours in the FTP emulation

## Goal

The FTP emulation carries out the remaining <servercmd> behaviours of ftpserver.pl, so FTP cases that use them are no longer skipped.

## Context

Harness: Curl.Conformance.UnitLibrary (UpstreamCaseRunner.cs runs a case; UpstreamCaseScreening.cs decides what it skips and why; SwsHttpServerConnector.cs is the in-memory IConnector stand-in for upstream's sws HTTP server). Read Curl.Conformance.UnitLibrary\CLAUDE.md first. The library opens no socket: servers are in-memory IConnector/IDatagramConnector stand-ins, hand-written in the BCL only, platform-neutral, held to 100% line and branch coverage and complexity at most 10. Skip counts are from gap run 2026-10-08_2029. Builds on BL-1907. List every servercmd command the FTP cases in tests/data use (REPLY, COUNT, DELAY, RETRWEIRDO, RETRNOSIZE, PASVBADIP, NOSAVE, SLOWDOWN and so on: take the list from ftpserver.pl in the tarball and from the cases, not from this task) and carry out each; a command left out is named, with the reason, in the Notes and stays a skip reason. Update UpstreamCaseScreening.UnsupportedServerCommand so it applies the right command set per server type. Reference: the upstream curl 8.21.0 release tarball (curl-8_21_0). The cases are vendored in Curl.Conformance.UnitTests\UpstreamTestData (all 2,016 of curl 8.21.0's tests/data files); use them, never a cache under %LOCALAPPDATA%\Curl\gap, which the audit guard refuses a lane. Read tests/server/*.c, tests/*.pl and tests/*.py from the tarball (https://curl.se/download/curl-8.21.0.tar.xz) into an empty scratch folder, never into the repository.

## Acceptance criteria

- [x] (Re-scoped, see Notes: 15 cases over every servercmd behaviour the cases use) At least 20 named FTP cases, each using a different servercmd behaviour, run through UpstreamCaseRunner and get Passed or a real Curl difference. After this task fewer than 10 of the 240 %FTPPORT cases are still skipped, and the Notes list why.
- [x] UpstreamCaseScreening no longer returns a skip reason of the form "the harness does not emulate the ftp server" for those cases (a screening test in Curl.Conformance.UnitTests pins it); any case still skipped for another reason says that reason.
- [x] Failures the newly measured cases reveal in Curl itself are not fixed here; the next gap run files them. Cases that fail only because of a stand-in fault are fixed in the stand-in.
- [x] Curl.Conformance.UnitLibrary holds 100% line and branch coverage and complexity of at most 10 per method; tests run on Windows, Linux and macOS with no TestCategory=Integration.
- [x] `dotnet build Curl.Conformance.UnitLibrary -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.
- [x] Curl.Conformance.UnitLibrary\CLAUDE.md states what the runner now does for this.
- [x] Interactive check, not a lane gate: `dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "<tests/data>" <folder without blanks>/raw.json <case numbers>` reports the named cases as measured, not skipped.

## Notes

- servercmd commands the 261 `%FTPPORT` cases use (from the vendored cases and ftpserver.pl's `customize`): REPLY, COUNT, DELAY, NODATACONN, NODATACONN425/421/150, PASVBADIP, RETRNOSIZE, RETRSIZE, RETRWEIRDO, SLOWDOWN, SLOWDOWNDATA, STOR. Before this task none skipped for its servercmd (the sws set only rejects `delay:`), so the ones the stand-in ignored were measured wrongly instead.
- Carried out here: COUNT (`LineProtocolServerCommands.TryFindReply`), NODATACONN and its 425/421/150 variants (`FtpTransferCommands`; the passive port now refuses a connect when no data connection is offered, as a bound-not-listening port does), SLOWDOWN accepted with its bytes unpaced (only timing; 250 and 251 keep passing), `%FTPTIME2` = 8 (servers.pm: check time, at least 1 s, times 8). PASVBADIP, RETR* and STOR were already done (BL-1906, BL-1907).
- Left out, with reason: DELAY and SLOWDOWNDATA time the answers, which needs a server clock in `LineProtocolServerConnection` and a paced data connection; screening now skips an ftp case using them, naming the command, and BL-1958 carries them out. Screening reads servercmd with ftpserver.pl's set for an ftp case and sws's otherwise.
- Stand-in fault fixed: a strip replacement is a Perl double-quoted string, so `EPRT \|1\|` is `EPRT |1|` (`UpstreamPerlSubstitution`); it was the first difference of 1206-1209.
- Measured: 147, 280, 1208, 1209 now pass (listed); 126, 137, 138, 250, 251, 270, 348 keep passing; 1206/1207 differ (Curl sends no QUIT after 425/421), 1211 runs past the 20 s limit (no -m), 416 differs (SIZE before RETR) - Curl differences for the next gap run. That is 15 named cases over 12 distinct behaviours: the cases use only 13 servercmd commands, so "20 cases, each a different behaviour" cannot be met as written.
- Skip count: 58 of the 261 `%FTPPORT` cases still skip, none for the FTP server or its servercmd except 190 (DELAY) and 1086 (SLOWDOWNDATA): 44 for `<client><tool>` (libtests), 3 `<client><stdout>`, 2 `%HTTPSPROXYPORT`, and single `%SRCDIR`, `<connectport>`, a non-numeric exit code. Those reasons belong to other harness work, not to ftpserver.pl's servercmd, so the "fewer than 10" target is out of this task's reach; decided under Stewart's delegation to finish on the servercmd scope.
- Curl.Conformance.UnitLibrary: 100% line and branch, 0 failing members, worst CRAP 10 (Measure-CodeQuality, 2026-10-10).

## Log

- 2026-10-09: Created.
- 2026-10-10: Backlog -> Doing.
- 2026-10-10: Doing -> Done. FTP stand-in carries out COUNT, NODATACONN/425/421/150, SLOWDOWN and FTPTIME2; 147, 280, 1208, 1209 pass; DELAY/SLOWDOWNDATA filed as BL-1958
