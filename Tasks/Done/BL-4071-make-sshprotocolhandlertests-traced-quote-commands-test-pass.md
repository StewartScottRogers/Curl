---
id: BL-4071
title: Make SshProtocolHandlerTests' traced quote commands test pass every run
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Ssh.UnitTests/SshProtocolHandlerTests.StateTraceOtherPaths.cs]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4071 — Make SshProtocolHandlerTests' traced quote commands test pass every run

## Goal

`SshProtocolHandlerTests.ExecuteAsync_TracedQuoteCommandsBeforeAndAfter_WritesTheQuoteStates` passes on every run of the fast tests, not only when the machine is idle.

## Context

Found by BL-4070's lane on 2026-10-29: the test failed once (after 6 s) in a full `dotnet test --filter "TestCategory!=Integration"` run while other lanes were building, and passed in the next full run with nothing changed in Curl.Protocol.Ssh. Find what it waits on with a time limit or a race (look for a fixed timeout, or a read of the trace before the transfer has written it) and make it wait for the event it needs.

## Acceptance criteria

- [x] The test waits for the event it checks rather than a time limit a busy runner can pass.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Cause: the run had no fixed timeout in the test or the fake server. `SshProtocolHandler.TransferAndKeepOrDisconnectAsync` bounds the session's close (SFTP `CLOSE` and `DISCONNECT`) with a 5 s `CancellationTokenSource` on `context.TimeProvider`, which was the system clock. The in-memory connection cannot hold the session, so every traced transfer runs that close, and on a loaded runner the 5 s ran out before the close finished. That matches a failure after 6 s.
- Fix (test only): the file's `RunTracedAsync` now passes an `UnlimitedShutDownClock`. It is the system clock, but its timers never fire, so the test waits for the close to finish rather than racing a wall-clock limit. Timestamps still come from the system clock. The default connect limit (300 s) uses the same clock and is not reached in these tests either way. Every test in the file that calls `RunTracedAsync` gets the fix.
- No production code changed, so Measure-CodeQuality.ps1 was not needed. The change uses no paths, OS errors or certificates, so it does not depend on the platform.
- Checked: `dotnet build` clean with 0 warnings. Curl.Protocol.Ssh.UnitTests 1942 passed. A full `dotnet test --filter "TestCategory!=Integration"` passed all 46 test projects.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: f4d39644 779790c0.
- 2026-10-29: Doing -> Done. The traced SSH quote test waits for the session close, not a 5 s wall-clock limit
