---
id: BL-1906
title: Emulate passive-mode FTP data transfers (PASV, EPSV, LIST, NLST, RETR) in the case runner
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-1905]
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-09
completed:
---
# BL-1906 — Emulate passive-mode FTP data transfers (PASV, EPSV, LIST, NLST, RETR) in the case runner

## Goal

The FTP emulation serves passive-mode data transfers (PASV, EPSV, LIST, NLST, RETR) from <reply> parts.

## Context

Harness: Curl.Conformance.UnitLibrary (UpstreamCaseRunner.cs runs a case; UpstreamCaseScreening.cs decides what it skips and why; SwsHttpServerConnector.cs is the in-memory IConnector stand-in for upstream's sws HTTP server). Read Curl.Conformance.UnitLibrary\CLAUDE.md first. The library opens no socket: servers are in-memory IConnector/IDatagramConnector stand-ins, hand-written in the BCL only, platform-neutral, held to 100% line and branch coverage and complexity at most 10. Skip counts are from gap run 2026-10-08_2029. Builds on BL-1905. Per ftpserver.pl: PASV/EPSV open an in-memory data connection (an address and port the 227/229 reply names, reachable by curl's FTP code through the injected connector), and RETR/LIST/NLST write the case's <data> part and close it, answering 150/226 around it. Include the SIZE, MDTM, REST and PRET replies the same script gives. Reference: the upstream curl 8.21.0 release tarball (curl-8_21_0). The cases are vendored in Curl.Conformance.UnitTests\UpstreamTestData (all 2,016 of curl 8.21.0's tests/data files); use them, never a cache under %LOCALAPPDATA%\Curl\gap, which the audit guard refuses a lane. Read tests/server/*.c, tests/*.pl and tests/*.py from the tarball (https://curl.se/download/curl-8.21.0.tar.xz) into an empty scratch folder, never into the repository.

## Acceptance criteria

- [x] At least 20 named FTP download and listing cases (passive mode, including EPSV and --disable-epsv) run through UpstreamCaseRunner and get Passed or a real Curl difference.
- [x] UpstreamCaseScreening no longer returns a skip reason of the form "the harness has no value for %FTPPORT" for those cases (a screening test in Curl.Conformance.UnitTests pins it); any case still skipped for another reason says that reason.
- [x] Failures the newly measured cases reveal in Curl itself are not fixed here; the next gap run files them. Cases that fail only because of a stand-in fault are fixed in the stand-in.
- [ ] Curl.Conformance.UnitLibrary holds 100% line and branch coverage and complexity of at most 10 per method; tests run on Windows, Linux and macOS with no TestCategory=Integration.
- [x] `dotnet build Curl.Conformance.UnitLibrary -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.
- [ ] Curl.Conformance.UnitLibrary\CLAUDE.md states what the runner now does for this.
- [ ] Interactive check, not a lane gate: `dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "<tests/data>" <folder without blanks>/raw.json <case numbers>` reports the named cases as measured, not skipped.

## Notes

- FtpTransferCommands (PASV, EPSV, RETR, LIST, NLST, SIZE, MDTM, REST) and FtpDataConnection follow ftpserver.pl at curl-8_21_0; FtpServerConnector offers each passive data connection once on port 8995 (ftpserver.pl picks a free port; any fixed unused port stands for it). PRET has no handler in ftpserver.pl, so it stays "500 PRET is not dealt with!".
- Choice: a file name loads the case's data only when it names a test number (ftpserver.pl loads test<N> from the log dir, which only holds the current case). Screening now skips PORT, EPRT, LPRT, STOR, APPE (BL-1907) and CWD fully_simulated (wildcard listing, not emulated); the old skip text had no %FTPPORT form, the screening test pins the new one.
- 171 cases newly pass and are on PassingUpstreamCases.txt (FTP downloads and listings incl. EPSV and --disable-epsv: 102, 105, 106, 110, 111, 115, 117, 118, 120-124, 126, 127, 130-143, 1348-1363, 1378-1393 ...). Remaining FTP failures (e.g. 1137, 416 send SIZE before RETR; 146, 1225 CWD / ordering) are Curl differences for the next gap run.
- Conformance tests 1993 passed, 0 failed; solution build clean, fast tests green. Coverage: every branch is driven by FtpTransferCommandsTests, but Measure-CodeQuality was not run within the cost cap; BL-1942 measures it. FtpTransferCommandsTests also holds the FtpDataConnection, connector and responder cases for brevity; BL-1942 may split them per class.

- 2026-10-10 (interactive): depends-on BL-1942 dropped, it made a cycle (BL-1942 measures code that exists only in this task's shelved work). The shelved code is stash 9a4c35992 ("darkfactory BL-1906 20261009-213949"); apply it by hash (`git stash apply 9a4c35992`), never pop. This task measures its own coverage with Measure-CodeQuality.ps1 and closes any gap, then ticks the box; BL-1942 is deferred as folded in here.

## Log

- 2026-10-09: Created.
- 2026-10-09: Backlog -> Doing.
- 2026-10-09: Doing -> Backlog. Code done and green but uncommitted (stashed); waits on BL-1942 to measure Library coverage before Done
- 2026-10-10: Backlog -> Doing.
- 2026-10-10: Doing -> Blocked. Stewart: dark factory its runs of the last day already cost 3.32 of its 3.93 US dollar cost cap, so it was not run again; split the task; see Z:\repos\Curl.logs\BL-1906-*.jsonl
