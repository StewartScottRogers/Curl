---
id: BL-1918
title: Emulate SFTP transfers of upstream's test sshd in the case runner
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1954]
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests, Curl.Conformance.SshServer.UnitLibrary, Curl.Conformance.SshServer.UnitTests]
requirement: none
created: 2026-10-09
completed: 2026-10-10
---
# BL-1918 — Emulate SFTP transfers of upstream's test sshd in the case runner

## Goal

The SSH stand-in serves SFTP (protocol version 3: open, read, write, readdir, stat, mkdir, rmdir, rename, realpath) from the case's files, so upstream's SFTP cases are measured.

## Context

Harness: Curl.Conformance.UnitLibrary (UpstreamCaseRunner.cs runs a case; UpstreamCaseScreening.cs decides what it skips and why; SwsHttpServerConnector.cs is the in-memory IConnector stand-in for upstream's sws HTTP server). Read Curl.Conformance.UnitLibrary\CLAUDE.md first. The library opens no socket: servers are in-memory IConnector/IDatagramConnector stand-ins, hand-written in the BCL only, platform-neutral, held to 100% line and branch coverage and complexity at most 10. Skip counts are from gap run 2026-10-08_2029. Builds on BL-1916. Implement the SFTP server over the sftp subsystem channel, backed by a per-case virtual file tree built from <data>, <file> and <client><file> parts plus the log directory, never the real disk outside the case's log directory. Record uploaded bytes for <verify><upload>. Reference: the upstream curl 8.21.0 release tarball (curl-8_21_0). The cases are vendored in Curl.Conformance.UnitTests\UpstreamTestData (all 2,016 of curl 8.21.0's tests/data files); use them, never a cache under %LOCALAPPDATA%\Curl\gap, which the audit guard refuses a lane. Read tests/server/*.c, tests/*.pl and tests/*.py from the tarball (https://curl.se/download/curl-8.21.0.tar.xz) into an empty scratch folder, never into the repository.

## Acceptance criteria

- [x] At least 15 named SFTP cases run through UpstreamCaseRunner and get Passed or a real Curl difference.
- [x] UpstreamCaseScreening no longer returns a skip reason of the form "the harness has no value for %SFTP_PWD" for those cases (a screening test in Curl.Conformance.UnitTests pins it); any case still skipped for another reason says that reason.
- [x] Failures the newly measured cases reveal in Curl itself are not fixed here; the next gap run files them. Cases that fail only because of a stand-in fault are fixed in the stand-in.
- [x] Curl.Conformance.UnitLibrary holds 100% line and branch coverage and complexity of at most 10 per method; tests run on Windows, Linux and macOS with no TestCategory=Integration.
- [x] `dotnet build Curl.Conformance.UnitLibrary -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.
- [x] Curl.Conformance.UnitLibrary\CLAUDE.md states what the runner now does for this.
- [x] Interactive check, not a lane gate: `dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "<tests/data>" <folder without blanks>/raw.json <case numbers>` reports the named cases as measured, not skipped.

## Notes

- 2026-10-10 (interactive): depends-on BL-1916 became BL-1954; BL-1916 was split into BL-1953 and BL-1954 and deferred.
- 2026-10-10 (lane 1): touches widened to Curl.Conformance.SshServer.UnitLibrary and .UnitTests: the stand-in sshd lives there (ADR-0456), and no task in Doing on origin/work/dark-factory named them.
- Decision: the SFTP server works on the real files under the case's log directory, as BL-1917's scp does, not on a virtual tree: the cases' paths are absolute %LOGDIR paths and their postchecks read the disk. Same path mapping as scp (a slash before a drive is dropped).
- New: SshServerSftpProcess (SFTP v3: open, close, read, write, stat/lstat/fstat, setstat/fsetstat (no-op), opendir/readdir, remove, mkdir, rmdir, realpath, rename; others OP_UNSUPPORTED); SshServerConnector runs it for subsystem sftp. Screening drops the SFTP payload skip and records <verify><upload> for sftp.
- Measured: 27 SFTP cases pass and joined the ratchet (600, 602, 604, 608, 609, 611, 612, 615, 616, 618, 620, 622, 626, 627, 633-640, 642, 664, 1583, 2007, 3021). Measured but differing: 614 (listing; postcheck), 1446 (postcheck), 2004 (protocol "filename = /2004"), 624 and 625 (runner reads the upload before the postcheck moves it; filed BL-1955). Still skipped for other reasons: 582, 583 (<tool>), 610, 613 (%SRCDIR), 2604, 2605 (unittest feature).
- Coverage: Curl.Conformance.SshServer.UnitLibrary and Curl.Conformance.UnitLibrary 100% line and branch, 0 failing members (Measure-CodeQuality). Interactive check: a lane may not run the gap office tools (audit guard), so the same fact is pinned in the fast suite by UpstreamConformanceTests.SftpTransferCase_RunThroughCurl_IsMeasuredNotSkipped (32 named cases, none skipped, none hung); an interactive session may still run Measure-UpstreamCases to confirm.

## Log

- 2026-10-09: Created.
- 2026-10-10: Backlog -> Doing.
- 2026-10-10: Doing -> Done. SSH stand-in serves SFTP v3; 27 upstream SFTP cases pass and joined the ratchet
