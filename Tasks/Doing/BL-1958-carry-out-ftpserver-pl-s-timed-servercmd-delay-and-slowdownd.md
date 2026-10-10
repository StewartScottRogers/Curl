---
id: BL-1958
title: Carry out ftpserver.pl's timed servercmd DELAY and SLOWDOWNDATA in the FTP emulation
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-10
completed:
---
# BL-1958 — Carry out ftpserver.pl's timed servercmd DELAY and SLOWDOWNDATA in the FTP emulation

## Goal

The FTP emulation carries out ftpserver.pl's `DELAY <COMMAND> <seconds>` and `SLOWDOWNDATA` (5 ms per data byte), so test190 and test1086 run instead of skipping.

## Context

Left by BL-1908. `UpstreamCaseScreening.UnsupportedFtpServerCommand` skips an ftp case whose `<servercmd>` has `DELAY` or `SLOWDOWNDATA`, naming the command. Both need timed answers: `LineProtocolServerConnection` answers synchronously and has no `TimeProvider`, and `FtpDataConnection` sends all bytes at once. Plumb the runner's server clock (`UpstreamCaseRunner.ServerClock`, real when `-m` races it) into `FtpServerConnector`, delay a reply on it, and pace data bytes. `SLOWDOWN` is already accepted with its bytes unpaced (no case depends on its timing). ftpserver.pl at curl-8_21_0, lines 2854-2875 and 3290-3298.

## Acceptance criteria

- [ ] test190 (`DELAY CWD 60`, `-m 10`, exit 28) and test1086 (`SLOWDOWNDATA`, `-m 5`, exit 28) run through UpstreamCaseRunner and pass or fail on a real Curl difference.
- [ ] Screening no longer skips an ftp case for `DELAY` or `SLOWDOWNDATA`; a screening test pins it.
- [ ] Curl.Conformance.UnitLibrary holds 100% line and branch coverage and complexity of at most 10; `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

## Log

- 2026-10-10: Created.
- 2026-10-10: Backlog -> Doing.
