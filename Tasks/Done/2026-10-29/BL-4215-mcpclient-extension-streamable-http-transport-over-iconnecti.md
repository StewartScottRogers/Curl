---
id: BL-4215
title: McpClient extension: Streamable HTTP transport over IConnection
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4214]
touches: [Curl.Extension.McpClient.UnitLibrary/StreamableHttp, Curl.Extension.McpClient.UnitTests/StreamableHttp, Curl.Extension.McpClient.UnitLibrary/Curl.Extension.McpClient.UnitLibrary.csproj]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4215 — McpClient extension: Streamable HTTP transport over IConnection

## Goal

`Curl.Extension.McpClient.UnitLibrary` carries MCP messages over the Streamable HTTP transport on an `IConnection` the engine's `ProtocolHandlerServices` connector (and `TlsProvider` for TLS) gives it.

## Context

Second of BL-4159's split. Specification: Model Context Protocol revision 2025-06-18, "Base Protocol > Transports > Streamable HTTP": each message is an HTTP POST to the MCP endpoint with `Accept: application/json, text/event-stream`; the reply is `application/json` or a `text/event-stream` whose `data:` events carry JSON-RPC messages; a notification gets 202 Accepted; the `Mcp-Session-Id` header from the initialize response is sent on every later request; `MCP-Protocol-Version` is sent after initialization; DELETE ends the session. The extension may not reference `Curl.Protocol.Http` or another extension (`Curl.Extension.S3` wrote its own small HTTP/1.1 client for the same reason): write a minimal HTTP/1.1 client here (request line, headers, Content-Length and chunked response bodies, connection close). Builds on BL-4214's `Wire/` messages.

## Acceptance criteria

- [x] `StreamableHttp/` sends one JSON-RPC message as a POST with the headers the specification names and returns the awaited response from a JSON or an event-stream reply, carrying `Mcp-Session-Id` forward.
- [x] A notification answered by 202 Accepted completes; any other HTTP status fails with a typed failure carrying the status.
- [x] Unit tests use a fake `IConnection` with recorded bytes (no network) and name the specification section each checks, including a chunked event-stream reply split across reads.
- [x] The library meets the four quality gates by `Measure-CodeQuality.ps1`; `dotnet build` is clean and the fast tests pass.

## Notes

- Added the library's `.csproj` to `touches`: the transport needs a reference to `Curl.Protocol.Abstractions.UnitLibrary` (for `IConnection`) and `InternalsVisibleTo` for the tests. No other Doing task on `origin/work/dark-factory` names a McpClient file.
- Decision (ADR-1033): `StreamableHttp/` holds its own minimal HTTP/1.1 client adapted from `Curl.Extension.S3`'s (ADR-0999 forbids referencing it or `Curl.Protocol.Http`). `McpStreamableHttpTransport` takes a `Func<CancellationToken, ValueTask<IConnection>>`, so the later handler composes the engine's connector and `TlsProvider`, and the transport is tested with recorded bytes only. A request needs 200 with `application/json` or `text/event-stream`; a notification needs 202; any other status throws `McpHttpException` (`UnexpectedStatus`, `StatusCode`). DELETE counts 2xx or 405 as ended.
- Default taken: once the awaited response arrives on an event stream, the connection is closed rather than drained, because a server may keep the stream open. A stale kept connection is not retried.
- Coverlet counted the unreachable branches of `await` inside `finally` as uncovered, so the connection is closed without a try block (`CloseConnectionUnlessKeptAsync`, using `ConfigureAwaitOptions.SuppressThrowing`).
- Measured: Measure-CodeQuality.ps1 -Library Curl.Extension.McpClient.UnitLibrary gives 100% line, 100% branch, max complexity 10, no failing member. `dotnet build` is clean and the fast tests are green on Windows. The tests are platform-neutral (no paths, OS text or certificates), which meets the CI criterion.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: b1a1a1ef cb855ea9 166b8c16 14de81cc.
- 2026-10-29: Doing -> Done. McpClient carries MCP messages over Streamable HTTP on an IConnection: POST with JSON or event-stream replies, 202 notifications, session id and protocol version headers, DELETE to end
