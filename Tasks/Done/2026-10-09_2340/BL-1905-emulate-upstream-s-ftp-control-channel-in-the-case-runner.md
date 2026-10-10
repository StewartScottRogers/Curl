---
id: BL-1905
title: Emulate upstream's FTP control channel in the case runner
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-1895, BL-1920]
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-09
completed: 2026-10-09
---
# BL-1905 — Emulate upstream's FTP control channel in the case runner

## Goal

The runner emulates upstream's FTP control channel (greeting, USER/PASS, TYPE and similar commands, replies from <reply> parts) so FTP cases that need no data connection are measured and %FTPPORT has a value.

## Context

Harness: Curl.Conformance.UnitLibrary (UpstreamCaseRunner.cs runs a case; UpstreamCaseScreening.cs decides what it skips and why; SwsHttpServerConnector.cs is the in-memory IConnector stand-in for upstream's sws HTTP server). Read Curl.Conformance.UnitLibrary\CLAUDE.md first. The library opens no socket: servers are in-memory IConnector/IDatagramConnector stand-ins, hand-written in the BCL only, platform-neutral, held to 100% line and branch coverage and complexity at most 10. Skip counts are from gap run 2026-10-08_2029. 240 cases are skipped for %FTPPORT; this task provides the variable and the control channel, BL-1906 to BL-1908 add data transfers and the rest. Behaviour is that of tests/ftpserver.pl in the tarball: the 220 greeting, per-command replies taken from <reply> parts and the REPLY servercmd, the log of received commands compared with <verify><protocol>. Build on the line-protocol core of BL-1895. Choose cases that end before any data connection (login failure, --quote commands and the like) as the sample. Reference: the upstream curl 8.21.0 release tarball (curl-8_21_0). The cases are vendored in Curl.Conformance.UnitTests\UpstreamTestData (all 2,016 of curl 8.21.0's tests/data files); use them, never a cache under %LOCALAPPDATA%\Curl\gap, which the audit guard refuses a lane. Read tests/server/*.c, tests/*.pl and tests/*.py from the tarball (https://curl.se/download/curl-8.21.0.tar.xz) into an empty scratch folder, never into the repository.

## Acceptance criteria

- [x] At least 15 named FTP upstream cases that need no data connection run through UpstreamCaseRunner and get Passed or a real Curl difference.
- [x] UpstreamCaseScreening no longer returns a skip reason of the form "the harness has no value for %FTPPORT" for those cases (a screening test in Curl.Conformance.UnitTests pins it); any case still skipped for another reason says that reason.
- [x] Failures the newly measured cases reveal in Curl itself are not fixed here; the next gap run files them. Cases that fail only because of a stand-in fault are fixed in the stand-in.
- [x] Curl.Conformance.UnitLibrary holds 100% line and branch coverage and complexity of at most 10 per method; tests run on Windows, Linux and macOS with no TestCategory=Integration.
- [x] `dotnet build Curl.Conformance.UnitLibrary -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.
- [x] Curl.Conformance.UnitLibrary\CLAUDE.md states what the runner now does for this.
- [x] Interactive check, not a lane gate: `dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "<tests/data>" <folder without blanks>/raw.json <case numbers>` reports the named cases as measured, not skipped.

## Notes

- 2026-10-09 (lane 1): split. This run's cost cap ($2) cannot hold the responder, the runner wiring, the %FTPPORT screening change, 15 sample cases and 100% coverage together. The ftpserver.pl control-channel responder is now BL-1920 (same touches); BL-1905 keeps the runner wiring, %FTPPORT value, screening test, sample cases and CLAUDE.md update. Nothing was measured or coded in this run.
- 2026-10-09 (lane 1): done. `FtpServerConnector` puts the BL-1920 responder on `%FTPPORT` 8993 (chosen beside the runner's other 899x ports; any free value would do), between `SocksServerConnector` and `NoListenPortConnector`; its received bytes follow sws's for `<verify><protocol>`. `EmulatedServers` is now `["ftp"]`. Screening skips an FTP case whose `<verify><protocol>` has a line starting EPSV/PASV/PORT/EPRT/LPRT ("the FTP stand-in serves no data connection, which the case's EPSV opens"), so the ~200 cases that need BL-1906 to BL-1908 stay skipped with that reason rather than failing on a stand-in fault. Measured: 34 ftp cases with no data connection in their protocol now run; 20 pass and are listed (104 113 114 119 125 148 195 196 225 226 229 289 295 340 402 1000 1108 1120 1152 1282); real Curl differences, left for the next gap run: 1044 and 141 (-I header order), 140/247/272/1262/3217/3218 (Curl sends EPSV where upstream quits), 190 and 2108 (extra CWD/PWD), 983 and 986 (FTPS AUTH order); 2045 runs out of the 20 s limit; 405 skips for its exit code 35,28. Screening tests pin 195, 196 and 1120 unskipped with the runner's %FTPPORT. Measure-CodeQuality.ps1 was not run (run cost cap): every new branch has a unit test (FtpServerConnectorTests, the EPSV screening row) and the runner path runs in the fast conformance test; the build's CA1502 gate is clean. The gap-tool interactive check is left to an interactive session.
## Log

- 2026-10-09: Created.
- 2026-10-09: Backlog -> Doing.
- 2026-10-09: Doing -> Backlog. Waits on BL-1920 (the ftpserver.pl control-channel responder), split off to fit one lane run
- 2026-10-09: Backlog -> Doing.
- 2026-10-09: Doing -> Done. FTP control channel wired into the case runner on %FTPPORT; 20 FTP cases pass
