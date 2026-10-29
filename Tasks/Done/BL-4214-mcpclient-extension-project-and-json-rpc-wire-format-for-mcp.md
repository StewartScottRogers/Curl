---
id: BL-4214
title: McpClient extension: project and JSON-RPC wire format for MCP
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.McpClient.UnitLibrary/Wire, Curl.Extension.McpClient.UnitTests/Wire, Curl.Extension.McpClient.UnitLibrary/Curl.Extension.McpClient.UnitLibrary.csproj, Curl.Extension.McpClient.UnitTests/Curl.Extension.McpClient.UnitTests.csproj, Curl.slnx]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4214 — McpClient extension: project and JSON-RPC wire format for MCP

## Goal

`Curl.Extension.McpClient.UnitLibrary` and `Curl.Extension.McpClient.UnitTests` exist, and the library writes and reads the Model Context Protocol's JSON-RPC 2.0 messages a client needs to list and call tools.

## Context

First of BL-4159's split (wire format, then the transports, then the handler). Extension rules: ADR-0999 and the lane rules' "Extensions". Specification: Model Context Protocol, revision 2025-06-18 (https://modelcontextprotocol.io/specification/2025-06-18), sections "Base Protocol > Overview" (JSON-RPC messages), "Lifecycle" (initialize, notifications/initialized, protocolVersion negotiation) and "Server Features > Tools" (tools/list with cursor pagination, tools/call, CallToolResult, isError). Use `System.Text.Json`'s `Utf8JsonWriter` and `JsonDocument` (BCL, AOT-safe without serializer reflection); no package. Never reference a `Curl.Protocol.*` library or another extension.

## Acceptance criteria

- [x] Both projects exist (use the `new-project` skill's conventions for an extension) and `Curl.slnx` lists them in one single-line `<Folder Name="/Extensions/McpClient/">` directly under the extensions marker comment; the build's `VerifyExtensionIsolation` passes.
- [x] `Wire/` writes the `initialize` request (protocolVersion, capabilities, clientInfo), the `notifications/initialized` notification, `tools/list` (with optional cursor) and `tools/call` (name, arguments object) requests with increasing ids.
- [x] `Wire/` reads a JSON-RPC response matched by id: the result (InitializeResult's protocolVersion, a tools/list result's tools and nextCursor, a CallToolResult's content and isError) or the error object (code, message), and skips server notifications and requests that are not the awaited response.
- [x] Unit tests name the specification section each one checks and use recorded message bytes only.
- [x] The library meets the four quality gates by `Measure-CodeQuality.ps1`; `dotnet build` is clean and the fast tests pass.

## Notes

- No handler, scheme registration or description here: BL-4159 adds those once the transports (BL-4215, BL-4216) exist.
- Plan: `Wire/McpRequestWriter` (Utf8JsonWriter, ids from 1, an id taken only when the request is written) and `Wire/McpResponseReader` (JsonDocument; skips messages with a `method` or another id; typed readers for InitializeResult, tools/list and CallToolResult; malformed messages throw `McpWireFormatException`). Decisions in ADR-1027.
- Default taken: `initialize` declares empty `capabilities` (no roots, sampling or elicitation) and asks for 2025-06-18; the handler (BL-4159) decides what to do with another version.
- Measured: Measure-CodeQuality.ps1 -Library Curl.Extension.McpClient.UnitLibrary: 100% line, 100% branch, 0 failing members. 51 unit tests; full build clean, fast tests green. Tests are pure in-memory byte checks, so platform-neutral for CI on Linux and macOS.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 15fea506 16e0e381 0687ad8e 18064adb 14de81cc.
- 2026-10-29: Doing -> Done. Curl.Extension.McpClient writes initialize, notifications/initialized, tools/list and tools/call and reads their JSON-RPC responses
