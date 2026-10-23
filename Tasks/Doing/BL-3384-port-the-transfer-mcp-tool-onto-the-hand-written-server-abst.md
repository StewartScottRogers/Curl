---
id: BL-3384
title: Port the transfer MCP tool onto the hand-written server abstractions
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3375, BL-3378, BL-3374]
touches: [Curl.Mcp/Tools/TransferMcpTool.cs, Curl.Mcp/Tools/TransferMcpToolJson.cs, Curl.Mcp.UnitTests/Tools/TransferMcpToolTests.cs]
requirement: none
created: 2026-10-23
completed:
---
# BL-3384 — Port the transfer MCP tool onto the hand-written server abstractions

## Goal

`Curl.Mcp/Tools/TransferMcpTool.cs` implements `IMcpTool` for the `transfer` tool with the same name, description, input schema and results as the SDK-attributed `CurlTransferTool`, including the argument-shape refusal that the SDK needed a call-tool filter for.

## Context

- Read `Curl.Mcp/CurlTransferTool.cs`, `Curl.Mcp.UnitTests/CurlTransferToolTests.cs`, `McpTransferToolShapeTests.cs`, ADR-0593 and ADR-0771. Do not edit or delete the old class or tests (the cut-over task does). `McpTransferTool`, `McpTransferRequest` and `McpTransferRequestCheck` stay in `Curl.Mcp.UnitLibrary`.
- The input schema is written by hand and must equal the golden one for `transfer` from BL-3374 as a JSON value: `url` required, `method`, `headers` (array of `{name, value}` objects, `McpTransferHeader`), `body` (`McpTransferBody` object with its fields, including `isBase64`), `followRedirects`, `timeoutSeconds`, `insecure`, `proxy`, `userAgent`, each with the descriptions of the current parameters.
- No SDK binder exists any more, so the tool reads the `JsonElement` arguments itself: first `McpTransferRequestCheck.CheckArgumentShapes(arguments)` (its failure becomes the same `McpTransferToolResult.Failed` result the filter produced), then each field with the defaults of the current method signature (`method` "GET", `followRedirects` false, `insecure` false, headers empty). Small source-generated context in `TransferMcpToolJson.cs` for `McpTransferHeader[]`, `McpTransferBody` and the result; no reflection serialization (ADR-0621).
- A missing or wrongly-typed `url`, and every other bad-shape case in `McpTransferToolShapeTests.cs`, must give the answer the golden files of BL-3374 show or a clearly better one listed in Notes as a deliberate difference.

## Acceptance criteria

- [ ] `TransferMcpTool` exists with the same name, description and annotations as `CurlTransferTool` and an input schema equal to the golden `transfer` schema as a JSON value.
- [ ] Every test case of `CurlTransferToolTests.cs` and `McpTransferToolShapeTests.cs` has an equivalent in `TransferMcpToolTests.cs`, including each wrong-shape argument, defaults, a header with an empty value, base64 and text bodies, and cancellation reaching `McpTransferTool`.
- [ ] A test proves the serialized result equals `CurlTransferTool`'s for the same `McpTransferToolResult`.
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Mcp,Curl.Mcp.UnitLibrary` reports 0 failing members (100% line, 100% branch, complexity at most 10, CRAP at most 30), measured from the fast tests only.
- [ ] `dotnet build` of every project named in touches is clean with `-warnaserror`, and `dotnet test --filter "TestCategory!=Integration"` passes for the test projects named in touches, with no test deleted, skipped or weakened.

## Notes

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: a0fcb2dc 52c3f2f5 a7cd6620 96ffe31b 6d2cec38.
