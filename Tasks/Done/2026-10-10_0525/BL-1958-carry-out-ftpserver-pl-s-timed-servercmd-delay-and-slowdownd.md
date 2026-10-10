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
completed: 2026-10-10
---
# BL-1958 — Carry out ftpserver.pl's timed servercmd DELAY and SLOWDOWNDATA in the FTP emulation

## Goal

The FTP emulation carries out ftpserver.pl's `DELAY <COMMAND> <seconds>` and `SLOWDOWNDATA` (5 ms per data byte), so test190 and test1086 run instead of skipping.

## Context

Left by BL-1908. `UpstreamCaseScreening.UnsupportedFtpServerCommand` skips an ftp case whose `<servercmd>` has `DELAY` or `SLOWDOWNDATA`, naming the command. Both need timed answers: `LineProtocolServerConnection` answers synchronously and has no `TimeProvider`, and `FtpDataConnection` sends all bytes at once. Plumb the runner's server clock (`UpstreamCaseRunner.ServerClock`, real when `-m` races it) into `FtpServerConnector`, delay a reply on it, and pace data bytes. `SLOWDOWN` is already accepted with its bytes unpaced (no case depends on its timing). ftpserver.pl at curl-8_21_0, lines 2854-2875 and 3290-3298.

## Acceptance criteria

- [x] test190 (`DELAY CWD 60`, `-m 10`, exit 28) and test1086 (`SLOWDOWNDATA`, `-m 5`, exit 28) run through UpstreamCaseRunner and pass or fail on a real Curl difference.
- [x] Screening no longer skips an ftp case for `DELAY` or `SLOWDOWNDATA`; a screening test pins it.
- [x] Curl.Conformance.UnitLibrary holds 100% line and branch coverage and complexity of at most 10; `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

- Done directly in the session (no sub-agents, to keep the run's cost down). ADR-0464 records the decisions.
- `LineProtocolReply.Delay` (set by `FtpControlChannelResponder` from `LineProtocolServerCommands.ReplyDelay`, the command matched as written, as `$delayreply{$FTPCMD}`) is waited on the connector's clock by `LineProtocolServerConnection`; a reply after a delayed one waits for it, keeping order.
- `SLOWDOWNDATA`: `FtpDataConnection.SendSlowlyAsync` sends one byte every 5 ms (the task's figure) and closes; it stops once the client disposes the connection. The control channel's 226 is not held back for the data (no case depends on it).
- The runner passes its server clock to `FtpServerConnector` (real under `-m`).
- Measured: test190 passes (exit 28 after 10 s), test1086 passes (exit 28 after 5 s); both added to `PassingUpstreamCases.txt`. test1112 (FTPS) now runs and differs on the known `PROT P` vs `PROT C` difference.
- `Measure-CodeQuality.ps1 -Library Curl.Conformance.UnitLibrary`: 100% line, 100% branch, worst CRAP 10, 0 failing members.

## Log

- 2026-10-10: Created.
- 2026-10-10: Backlog -> Doing.
- 2026-10-10: Doing -> Done. The FTP stand-in carries out DELAY and SLOWDOWNDATA on the server clock; test190 and test1086 pass
