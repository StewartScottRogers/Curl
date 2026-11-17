---
id: BL-5343
title: Cap the mcp+stdio exit grace period at the time left on -m
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.McpClient.UnitLibrary/Stdio/McpStdioSession.cs, Curl.Extended.Protocol.McpClient.UnitTests]
requirement: none
created: 2026-11-16
completed: 2026-11-17
---
# BL-5343 — Cap the mcp+stdio exit grace period at the time left on -m

## Goal

`-m` caps the whole `mcp+stdio://` operation: a server process that does not exit is killed at once when the transfer has already failed or the `-m` deadline has passed, instead of being waited on for a further 5 seconds.

## Context

From the outside fitness review of 2026-11-16 (commit 64e9144, `preview` build), finding EXT-071 (Low, Confirmed, 4 runs; `-m` contract broken, the child is killed at the end so no orphan). Area: mcp+stdio; Windows. This is an extension, so it is judged against the port's own docs and ADRs, not curl.

**Repro (PowerShell; Git Bash's PATH breaks the program lookup).**
- `$t=Get-Date; curl.exe -sS -m 2 "mcp+stdio://localhost/C:/Windows/System32/PING.EXE?-n&30&127.0.0.1"; ((Get-Date)-$t).TotalSeconds`
- With a script `hang.ps1` (PowerShell, not Python) or any program that sleeps 60 s and never answers: `$t=Get-Date; curl.exe -sS -m 3 "mcp+stdio://localhost/<path to the program>?<args>"; ((Get-Date)-$t).TotalSeconds`

**Expected.** `-m` caps the whole operation, as for the port's other schemes: the second command ends about 3 s after start with exit 28.

**Actual.** The ping run prints `curl: (8) The message is not valid JSON.` and returns after about 5.1 s; the hang run prints `curl: (28) Operation timed out after 3005 milliseconds with 0 bytes received` but returns after about 8.1 s.

**Where to start.** `Curl.Extended.Protocol.McpClient.UnitLibrary/Stdio/McpStdioSession.cs` (lines 13 and 85-92): the 5 s `ExitGracePeriod` (McpClient-Stdio.md `Shutdown-2`) runs after the `-m` deadline or an error. Cap it at the time left on `-m` (zero once the deadline has passed), and kill at once when the transfer already failed. Inject `TimeProvider` (no `Thread.Sleep`); keep the normal clean-shutdown path (server exits within the grace period) unchanged, and update `McpClient-Stdio.md` `Shutdown-2` to state the new rule.

## Acceptance criteria

- [x] New tests in `Curl.Extended.Protocol.McpClient.UnitTests` (a stand-in `IMcpServerProcess` and a fake `TimeProvider`, no real process) prove: when the `-m` deadline has passed and the server has not exited, the session kills it without waiting the grace period; when 1 s is left on `-m` the wait is at most 1 s, not 5 s.
- [x] A test proves that when the transfer failed (for example the server wrote invalid JSON) the process is killed at once without the 5 s wait.
- [x] A test proves the normal path is unchanged: a server that exits cleanly within the grace period with time left is waited for and not killed, and with no `-m` the 5 s grace period still applies.
- [x] `McpClient-Stdio.md` `Shutdown-2` states the cap.
- [x] Tests are platform-neutral.
- [x] Review acceptance check recorded in the Log as a hand check (Windows only): the `-m 3` hang run returns within about 3.5 s with exit 28; the ping run returns within about 1 s with exit 8. If the lane cannot run it, the Log says so.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers and hang programs must be PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md).
- Review acceptance check: the `-m 3` hang run returns within about 3.5 s with exit 28; the ping run returns within about 1 s with exit 8.
- Add the `McpClient-Stdio.md` path to `touches` when found (its exact path was not confirmed at filing).
- Decision: McpStdioSession takes optional maxTime and operationStarted (from ITransferContext.MaxTime and OperationStarted); EndAsync waits min(5 s, time left on -m), and kills at once when none is left. DisposeAsync without a prior EndAsync (transfer failed or cancelled) kills at once. Also touched McpStdioClientSession.cs, McpProtocolHandler.cs and Documentation/Conformance/Extended/McpClient-Stdio.md.
- Hand check (Windows): the ping run returned exit 8 in 1.5 s, the same as this machine takes to start Curl (a failing file:// run took 1.6 s). The -m 3 hang run was not run (no hang program written). Measure-CodeQuality for the library: 0 failing members. Tests are platform-neutral; fast tests green. Committed in the fix(mcp) commit.

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing. Criteria at claim: 472ce3d2 7cfbd6a7 11a9bfa5 d49766c1 c1cf21f9 c5da08c0 a0b49a29.
- 2026-11-17: Doing -> Done. mcp+stdio exit grace period capped at the time left on -m and skipped when the transfer failed
