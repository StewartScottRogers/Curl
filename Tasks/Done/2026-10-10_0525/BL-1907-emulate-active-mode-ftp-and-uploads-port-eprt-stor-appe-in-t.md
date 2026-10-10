---
id: BL-1907
title: Emulate active-mode FTP and uploads (PORT, EPRT, STOR, APPE) in the case runner
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-1956]
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests, Curl.Console]
requirement: none
created: 2026-10-09
completed: 2026-10-10
---
# BL-1907 — Emulate active-mode FTP and uploads (PORT, EPRT, STOR, APPE) in the case runner

## Goal

The FTP emulation serves active mode (PORT, EPRT) and uploads (STOR, APPE), recording the uploaded bytes for <verify><upload>.

## Context

Harness: Curl.Conformance.UnitLibrary (UpstreamCaseRunner.cs runs a case; UpstreamCaseScreening.cs decides what it skips and why; SwsHttpServerConnector.cs is the in-memory IConnector stand-in for upstream's sws HTTP server). Read Curl.Conformance.UnitLibrary\CLAUDE.md first. The library opens no socket: servers are in-memory IConnector/IDatagramConnector stand-ins, hand-written in the BCL only, platform-neutral, held to 100% line and branch coverage and complexity at most 10. Skip counts are from gap run 2026-10-08_2029. Builds on BL-1956. For PORT/EPRT the stand-in connects back to the address curl names, through an in-memory route to curl's listener (check how Curl.Protocol.Ftp.UnitLibrary listens for active mode and whether an injected listener abstraction exists; if not, say so in the Notes and use the abstraction that exists, without editing the protocol library here). STOR/APPE read the data connection until close and record it; compare with <verify><upload>. Reference: the upstream curl 8.21.0 release tarball (curl-8_21_0). The cases are vendored in Curl.Conformance.UnitTests\UpstreamTestData (all 2,016 of curl 8.21.0's tests/data files); use them, never a cache under %LOCALAPPDATA%\Curl\gap, which the audit guard refuses a lane. Read tests/server/*.c, tests/*.pl and tests/*.py from the tarball (https://curl.se/download/curl-8.21.0.tar.xz) into an empty scratch folder, never into the repository.

## Acceptance criteria

- [x] At least 15 named FTP upload and active-mode cases run through UpstreamCaseRunner and get Passed or a real Curl difference, and a unit test checks the recorded upload bytes.
- [x] UpstreamCaseScreening no longer returns a skip reason of the form "the harness has no value for %FTPPORT" for those cases (a screening test in Curl.Conformance.UnitTests pins it); any case still skipped for another reason says that reason.
- [x] Failures the newly measured cases reveal in Curl itself are not fixed here; the next gap run files them. Cases that fail only because of a stand-in fault are fixed in the stand-in.
- [x] Curl.Conformance.UnitLibrary holds 100% line and branch coverage and complexity of at most 10 per method; tests run on Windows, Linux and macOS with no TestCategory=Integration.
- [x] `dotnet build Curl.Conformance.UnitLibrary -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.
- [x] Curl.Conformance.UnitLibrary\CLAUDE.md states what the runner now does for this.
- Interactive check, not a lane gate (left to an interactive session, see Notes): `dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "<tests/data>" <folder without blanks>/raw.json <case numbers>` reports the named cases as measured, not skipped.

## Notes

- 2026-10-10 (interactive): depends-on BL-1906 became BL-1956; BL-1906 hit the per-task cost cap and its shelved work moved to BL-1956.
- 2026-10-10 (lane 1): Curl.Protocol.Ftp.UnitLibrary already takes an injected IConnectionListener, but CurlComposition always built a TcpConnectionListener, so the in-process curl could not reach an in-memory one. Added Curl.Console to touches (no task in Doing named it) to thread an optional ftpListener through the dialing CreateRunner, CreateProtocolHandlers and CreateFtpProtocolHandler (null keeps the TCP listener); the protocol library is unchanged. FtpActiveModeListener is the in-memory listener (a listen on port 0 takes 9006); PORT and EPRT connect to it by port, ignoring the address as ftpserver.pl does.
- STOR/APPE answer 125 and 226 together (the line-protocol responder is synchronous); curl reads the 226 only after closing the data connection, so it sees ftpserver.pl's order. Bytes curl writes into the data connection are kept; the last upload is compared with <verify><upload>. NODATACONN*, NOSAVE and the early stop after a `STOR <text>` servercmd are not emulated (none of the measured cases needs them).
- Measured: of the 46 FTP cases verifying PORT, EPRT, STOR or APPE, 22 newly pass and are listed (101 103 107 108 109 112 116 128 144 145 235 236 248 251 348 362 475 476 1038 1039 1055 1414); 119, 289 and 1120 already passed; 149, 216 and 1217 differ (Curl sends QUIT where curl sends CWD or EPSV) and 1211 differs after <strip> (EPRT \|1\|), all left for the next gap run; the rest skip for <tool>, <client><stdout> or %FTPTIME2. UpstreamCaseScreeningTests.FindSkipReason_FtpActiveModeOrUploadCase_IsNotSkippedForTheFtpStandIn pins the 26 measured cases.
- Measure-CodeQuality -Library Curl.Conformance.UnitLibrary,Curl.Console: 100% line and branch, 0 failing members, worst CRAP 10.
- Interactive check left open: a lane may not read the gap office's folder or file a task naming it. Its Measure-UpstreamCases.cs tool calls CurlComposition.CreateRunner without ftpListener, so active-mode cases there still listen on a real TCP port; an interactive session should pass `ftpListener: invocation.ConnectionListener` there, as UpstreamConformanceTests.RunCurlAsync does, then run the check.

## Log

- 2026-10-09: Created.
- 2026-10-10: Backlog -> Doing.
- 2026-10-10: Doing -> Done. The FTP stand-in serves active mode (PORT, EPRT) and uploads (STOR, APPE); 22 more upstream FTP cases pass
