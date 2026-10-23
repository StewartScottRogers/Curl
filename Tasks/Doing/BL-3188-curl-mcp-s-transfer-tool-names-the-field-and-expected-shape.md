---
id: BL-3188
title: Curl.Mcp's transfer tool names the field and expected shape for a wrong argument shape (BL-3171 item 6)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Mcp/CurlTransferTool.cs, Curl.Mcp/CurlMcpJsonContext.cs, Curl.Mcp.UnitLibrary/McpTransferTool.cs, Curl.Mcp.UnitLibrary/McpTransferRequest.cs, Curl.Mcp.UnitLibrary/McpTransferRequestCheck.cs, Curl.Mcp.UnitTests/McpTransferToolShapeTests.cs]
requirement: none
created: 2026-10-22
completed:
---
# BL-3188 — Curl.Mcp's transfer tool names the field and expected shape for a wrong argument shape (BL-3171 item 6)

## Goal

A wrong argument shape sent to Curl.Mcp's `transfer` tool is rejected with an error naming the field and its expected shape, never `An error occurred invoking 'transfer'`.

## Context

BL-3171 item 6. The SDK binds arguments in `Curl.Mcp/CurlTransferTool.cs` (source-generated JSON in `CurlMcpJsonContext.cs`) before `Curl.Mcp.UnitLibrary/McpTransferTool.cs` sees them, so a shape mismatch throws there; the likely fix is to take the raw arguments (JsonElement) and check shapes in `McpTransferRequestCheck.cs`. Also see `McpTransferRequest.cs`, `McpTransferHeader.cs`, `McpTransferBody.cs`.

Split from BL-3171 (one sub-criterion per subtask); read BL-3171's Context for the full item text. Measure curl 8.22.0 with Record-CurlExchange.ps1 before pinning (curl 8.22.0 Schannel is on PATH on the lane machines) and record the measurement in Notes. Build gate: `dotnet build <library> -warnaserror` clean, the matching .UnitTests fast tests pass, and Measure-CodeQuality.ps1 for the changed library is inside every gate.

## Acceptance criteria

- [ ] Tests in `Curl.Mcp.UnitTests/McpTransferToolShapeTests.cs` send `headers` as an object and `body` as a string (and at least one more wrong shape each for `url`, `method` and `options`) and expect an error text naming the field and its expected shape; none yields `An error occurred invoking 'transfer'`, and the right shapes still work.
- [ ] The changed library builds clean with -warnaserror, its .UnitTests fast tests pass, and Measure-CodeQuality.ps1 for it is inside every gate.

## Notes

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: 31e292f6 6361b4ee.
