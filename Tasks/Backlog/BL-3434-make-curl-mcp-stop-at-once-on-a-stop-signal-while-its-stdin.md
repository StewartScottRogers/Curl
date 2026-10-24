---
id: BL-3434
title: Make Curl.Mcp stop at once on a stop signal while its stdin read is blocked
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Mcp/McpStdioHost.cs, Curl.Mcp.Protocol.UnitLibrary/Server/McpServer.cs, Curl.Mcp.UnitTests, Curl.Mcp.Protocol.UnitTests]
requirement: none
created: 2026-10-23
completed:
---
# BL-3434 — Make Curl.Mcp stop at once on a stop signal while its stdin read is blocked

## Goal

An idle Curl.Mcp server that gets SIGTERM, SIGINT or SIGHUP while stdin stays open stops at once and exits 0, with no 30-second wait.

## Context

Found by the shift-end review of 2026-10-23 (ADR-0710). The code reviewer and the type-design analyzer both found it in the diff `79ec91e50...7f4970f66`, where the hand-written server replaced the SDK host.

- `Curl.Mcp/CurlMcpHost.cs:29` passes `Console.OpenStandardInput()` to `StdioMcpTransport`, which reads with `input.ReadAsync(readBuffer, cancellationToken)` (`Curl.Mcp.Protocol.UnitLibrary/Transport/StdioMcpTransport.cs:101`).
- The console stdin stream does not override `ReadAsync`. The base `Stream.ReadAsync` checks the token only before the read starts, so a read already blocked on an open pipe is never cancelled.
- `McpServer.RunAsync` (`Curl.Mcp.Protocol.UnitLibrary/Server/McpServer.cs:55`) waits for `reading` to end before it cancels the tracker, so `serving` never completes.
- `McpStdioHost.RunAsync` (`Curl.Mcp/McpStdioHost.cs:115-122`) therefore logs "stopping...", waits the whole `ShutdownTimeout` (30 s), then `ExitCodeFor` logs "stopped with requests still in flight" and returns 1. Both are false when nothing was running.
- The SDK's generic host this replaced exited 0 on a stop signal.
- The unit tests miss it because their input stream honours the token.

Suggested fix: in `RunAsync`, stop waiting on the read once the token fires, e.g. `await reading.WaitAsync(cancellationToken)` with `SuppressThrowing`. Then cancel the tracker and wait for `running` only. The abandoned read dies with the process.

## Acceptance criteria

- [ ] A unit test in `Curl.Mcp.Protocol.UnitTests` runs `McpServer.RunAsync` over an input stream whose `ReadAsync` ignores its cancellation token and blocks. It shows `RunAsync` completes promptly after the token is cancelled, with no request in flight.
- [ ] A unit test in `Curl.Mcp.UnitTests` shows `McpStdioHost.RunAsync` returns 0 and logs "Curl.Mcp stopped." after `StopOnSignal` over such a stream, under a fake `TimeProvider`, without advancing time by `ShutdownTimeout`.
- [ ] A request still in flight at the stop signal still gets up to `ShutdownTimeout` to finish, and an existing test shows it.
- [ ] `dotnet build` is clean, the fast tests pass, and `Curl.Mcp` and `Curl.Mcp.Protocol.UnitLibrary` stay at 100% line and branch coverage.

## Notes

## Log

- 2026-10-23: Created.
