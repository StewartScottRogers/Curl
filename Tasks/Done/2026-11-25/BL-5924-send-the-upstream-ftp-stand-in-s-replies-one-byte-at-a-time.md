---
id: BL-5924
title: Send the upstream FTP stand-in's replies one byte at a time under SLOWDOWN
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitLibrary/FtpControlChannelResponder.cs, Curl.Conformance.UnitLibrary/FtpServerConnector.cs, Curl.Conformance.UnitLibrary/OneBytePerReadConnection.cs, Curl.Conformance.UnitLibrary/CLAUDE.md, Curl.Conformance.UnitTests/FtpServerConnectorTests.cs]
requirement: none
created: 2026-11-24
completed: 2026-11-25
---
# BL-5924 — Send the upstream FTP stand-in's replies one byte at a time under SLOWDOWN

## Goal

For an FTP case whose `<servercmd>` says `SLOWDOWN`, the conformance harness's FTP stand-in sends each control-connection reply one byte per read, as upstream's `ftpserver.pl` does.

## Context

Found in BL-5905: upstream test 251 sat on the passing list while Curl hung against a server that really drips its replies, because only `DELAY` and `SLOWDOWNDATA` are carried out by the stand-in (`Curl.Conformance.UnitLibrary/FtpControlChannelResponder.cs`, `FtpServerConnector.cs`) and `SLOWDOWN` is ignored. Waits go on the stand-in's injected clock, as `SLOWDOWNDATA`'s do.

## Acceptance criteria

- [x] A unit test in `Curl.Conformance.UnitTests` shows each reply of a `SLOWDOWN` server arriving one byte per read, with its waits on the injected clock.
- [x] Every `SLOWDOWN` case on `PassingUpstreamCases.txt` still passes.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Added to `touches`: the new `OneBytePerReadConnection.cs` and the library's `CLAUDE.md`, which said `SLOWDOWN` was unpaced. No other task in Doing named either.
- The pacing is a connection wrapper that `FtpServerConnector` puts round each control connection, so each read waits and then takes one byte. A sender pushing bytes on a timer was rejected: on the wait-skipping clock they pile up and one read takes many. A read cancelled during its wait takes nothing.
- 10 ms a byte, as upstream's `docs/tests/FILEFORMAT.md` gives for `SLOWDOWN`. `SLOWDOWNDATA` keeps its 5 ms, unchanged here.
- `SLOWDOWN` counts only alone on its line, so `SLOWDOWNDATA` does not switch it on. `FtpControlChannelResponder.cs` needed no change.
- The three listed `SLOWDOWN` cases are 250, 251 (FTP) and 853 (POP3); all pass. The mail stand-ins still ignore `SLOWDOWN`: filed as BL-5937.
- Verification: `dotnet build` clean; one full fast run, whose only failures seen were two new tests with wrong test data, fixed, then `Curl.Conformance.UnitTests` rerun green (4379 passed); `Measure-CodeQuality.ps1 -Library Curl.Conformance.UnitLibrary`: 0 failing members.
- Platform: the change is in memory on an injected clock, with nothing platform-specific.
- No ADR: this follows upstream's server as the harness already does (ADR-0013).

## Log

- 2026-11-24: Created.
- 2026-11-24: Backlog -> Doing. Criteria at claim: 0bfd023c 1201ce2f 3d836d5e.
- 2026-11-25: Doing -> Done. The FTP stand-in drips control replies one byte per read under SLOWDOWN
