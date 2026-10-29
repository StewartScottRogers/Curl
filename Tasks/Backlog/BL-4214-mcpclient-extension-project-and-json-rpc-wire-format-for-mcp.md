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
completed:
---
# BL-4214 — McpClient extension: project and JSON-RPC wire format for MCP

## Goal

`Curl.Extension.McpClient.UnitLibrary` and `Curl.Extension.McpClient.UnitTests` exist, and the library writes and reads the Model Context Protocol's JSON-RPC 2.0 messages a client needs to list and call tools.

## Context

First of BL-4159's split (wire format, then the transports, then the handler). Extension rules: ADR-0999 and the lane rules' "Extensions". Specification: Model Context Protocol, revision 2025-06-18 (https://modelcontextprotocol.io/specification/2025-06-18), sections "Base Protocol > Overview" (JSON-RPC messages), "Lifecycle" (initialize, notifications/initialized, protocolVersion negotiation) and "Server Features > Tools" (tools/list with cursor pagination, tools/call, CallToolResult, isError). Use `System.Text.Json`'s `Utf8JsonWriter` and `JsonDocument` (BCL, AOT-safe without serializer reflection); no package. Never reference a `Curl.Protocol.*` library or another extension.

## Acceptance criteria

- [ ] Both projects exist (use the `new-project` skill's conventions for an extension) and `Curl.slnx` lists them in one single-line `<Folder Name="/Extensions/McpClient/">` directly under the extensions marker comment; the build's `VerifyExtensionIsolation` passes.
- [ ] `Wire/` writes the `initialize` request (protocolVersion, capabilities, clientInfo), the `notifications/initialized` notification, `tools/list` (with optional cursor) and `tools/call` (name, arguments object) requests with increasing ids.
- [ ] `Wire/` reads a JSON-RPC response matched by id: the result (InitializeResult's protocolVersion, a tools/list result's tools and nextCursor, a CallToolResult's content and isError) or the error object (code, message), and skips server notifications and requests that are not the awaited response.
- [ ] Unit tests name the specification section each one checks and use recorded message bytes only.
- [ ] The library meets the four quality gates by `Measure-CodeQuality.ps1`; `dotnet build` is clean and the fast tests pass.

## Notes

- No handler, scheme registration or description here: BL-4159 adds those once the transports (BL-4215, BL-4216) exist.

## Log

- 2026-10-29: Created.
