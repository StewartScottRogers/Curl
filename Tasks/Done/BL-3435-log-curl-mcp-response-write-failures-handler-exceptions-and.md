---
id: BL-3435
title: Log Curl.Mcp response write failures, handler exceptions and discarded invalid messages to stderr
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Mcp.Protocol.UnitLibrary/Server, Curl.Mcp.Protocol.UnitLibrary/Tools/ToolsCallHandler.cs, Curl.Mcp/McpStdioHost.cs, Curl.Mcp.Protocol.UnitTests, Curl.Mcp.UnitTests]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---
# BL-3435 — Log Curl.Mcp response write failures, handler exceptions and discarded invalid messages to stderr

## Goal

Every failure the hand-written MCP server now hides reaches the operator's stderr log, and a broken stdout stops the server. The JSON-RPC wire text stays pinned to the SDK's golden files.

## Context

Found by the shift-end review of 2026-10-23 (ADR-0710). The silent-failure hunter found it in the diff `79ec91e50...7f4970f66`; the type-design analyzer confirmed the write-failure part. All of it is in `Curl.Mcp.Protocol.UnitLibrary/Server/McpServer.cs` unless noted.

- **A failed response write is never seen.** Line 67, `running.RemoveAll(task => task.IsCompleted)`, also drops faulted tasks, and only `Task.WhenAll(running)` at line 57 would have observed them. When the client's stdout pipe breaks, `WriteAsync` (lines 124 and 150) throws `IOException`, and the request task faults. The next message removes it unobserved, and the server keeps running requests whose answers can never be delivered. Only a failure in the last few requests reaches `McpStdioHost.cs:149`, which then says "reading its input failed" for a write failure.
- **A handler exception leaves no trace.** `catch (Exception)` at lines 112-115 answers `-32603 "An error occurred."`. `Tools/ToolsCallHandler.cs:73-76` answers `isError` "An error occurred invoking '<tool>'.". Neither logs the exception. A failing notification handler (lines 139-142) is also swallowed with no log.
- **A discarded message is not logged.** A line over the transport's 4 MiB cap, or one that is not valid JSON, becomes a `JsonRpcInvalidMessage` with a null id, and `AnswerInvalidMessageAsync` (lines 147-148) drops it. Sending no answer matches the SDK golden files (BL-3374) and stays. Dropping it without a log line is the defect: a client that sends a 5 MiB `stdin` argument hangs with nothing to show why.

The host already owns a stderr log (`Curl.Mcp/McpStandardErrorLog.cs`). Pass a sink for failures from it into `McpServer` and `ToolsCallHandler`, through their constructors and dependency injection, not a static.

## Acceptance criteria

- [x] A unit test shows that when a response write throws for a request that is not the last one, `McpServer.RunAsync` stops reading and ends faulted, and the write failure is not lost.
- [x] `McpStdioHost` logs a write failure as a write failure, not as "reading its input failed", and logs the exception in full, not only `InnerException!.Message`. A unit test pins the line.
- [x] Unit tests show that a throwing request handler, a throwing tool and a throwing notification handler each write the exception's type and message to the failure sink. The JSON-RPC response bytes stay unchanged from today's.
- [x] A unit test shows that an over-long line and an unparseable line each write a "discarded" line to the failure sink, and that no response is written.
- [x] `dotnet build` is clean, the fast tests pass, and `Curl.Mcp` and `Curl.Mcp.Protocol.UnitLibrary` stay at 100% line and branch coverage.

## Notes

- Decided by Claude: the failure sink is a plain `Action<string>` passed to `McpServer` and `ToolsCallHandler` (the host passes `McpStandardErrorLog.WriteLine`); a failed response write is recorded, cancels the read loop and `RunAsync` ends with the new `McpResponseWriteException` (inner = transport's exception), which the host logs as a write failure with the full exception. Sink lines carry the exception type and message only. Measured: Curl.Mcp and Curl.Mcp.Protocol.UnitLibrary at 100% line and branch, fast tests of both test projects pass.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: 6c43e9ea 24260786 1e990932 5e636157 a43672c6.
- 2026-10-23: Doing -> Done. Curl.Mcp logs write failures, handler/tool exceptions and discarded messages to stderr; a broken stdout stops the server
