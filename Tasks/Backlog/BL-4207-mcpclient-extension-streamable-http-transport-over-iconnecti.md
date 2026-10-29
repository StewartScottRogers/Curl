---
id: BL-4207
title: McpClient extension: Streamable HTTP transport over IConnection
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4206]
touches: [Curl.Extension.McpClient.UnitLibrary/StreamableHttp, Curl.Extension.McpClient.UnitTests/StreamableHttp]
requirement: none
created: 2026-10-29
completed:
---
# BL-4207 — McpClient extension: Streamable HTTP transport over IConnection

## Goal

`Curl.Extension.McpClient.UnitLibrary` carries MCP messages over the Streamable HTTP transport on an `IConnection` the engine's `ProtocolHandlerServices` connector (and `TlsProvider` for TLS) gives it.

## Context

Second of BL-4159's split. Specification: Model Context Protocol revision 2025-06-18, "Base Protocol > Transports > Streamable HTTP": each message is an HTTP POST to the MCP endpoint with `Accept: application/json, text/event-stream`; the reply is `application/json` or a `text/event-stream` whose `data:` events carry JSON-RPC messages; a notification gets 202 Accepted; the `Mcp-Session-Id` header from the initialize response is sent on every later request; `MCP-Protocol-Version` is sent after initialization; DELETE ends the session. The extension may not reference `Curl.Protocol.Http` or another extension (`Curl.Extension.S3` wrote its own small HTTP/1.1 client for the same reason): write a minimal HTTP/1.1 client here (request line, headers, Content-Length and chunked response bodies, connection close). Builds on BL-4206's `Wire/` messages.

## Acceptance criteria

- [ ] `StreamableHttp/` sends one JSON-RPC message as a POST with the headers the specification names and returns the awaited response from a JSON or an event-stream reply, carrying `Mcp-Session-Id` forward.
- [ ] A notification answered by 202 Accepted completes; any other HTTP status fails with a typed failure carrying the status.
- [ ] Unit tests use a fake `IConnection` with recorded bytes (no network) and name the specification section each checks, including a chunked event-stream reply split across reads.
- [ ] The library meets the four quality gates by `Measure-CodeQuality.ps1`; `dotnet build` is clean and the fast tests pass.

## Log

- 2026-10-29: Created.
