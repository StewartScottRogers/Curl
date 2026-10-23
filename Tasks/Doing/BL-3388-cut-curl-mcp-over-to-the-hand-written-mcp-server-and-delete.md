---
id: BL-3388
title: Cut Curl.Mcp over to the hand-written MCP server and delete the SDK-attributed tool classes
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3374, BL-3380, BL-3381, BL-3382, BL-3383, BL-3384, BL-3385, BL-3386, BL-3387]
touches: [Curl.Mcp, Curl.Mcp.UnitTests, Curl.Mcp.IntegrationTests/CurlTransferToolIntegrationTests.cs]
model: opus
requirement: none
created: 2026-10-23
completed:
---
# BL-3388 — Cut Curl.Mcp over to the hand-written MCP server and delete the SDK-attributed tool classes

## Goal

`Curl.Mcp` runs on the hand-written server end to end: `Program` starts `McpStdioHost` with all five tools and the ai-help resources registered, no source file in `Curl.Mcp`, `Curl.Mcp.UnitTests` or `Curl.Mcp.IntegrationTests` uses `ModelContextProtocol` or `Microsoft.Extensions.*`, and the golden conversations of BL-3374 pass against it.

## Context

- Rewrite `Curl.Mcp/CurlMcpHost.cs` (its `Build` now returns the composed `McpStdioHost`; keep the environment reading, `McpConfinementRoot`, `McpResponseCap`, current-directory change and the single shared `ManagedCurlEngine` exactly as they are, ADR-0575) and `Curl.Mcp/Program.cs`. Registration is one explicit line per tool and provider in the host, never a scan.
- Delete the SDK-attributed `CurlCommandTool`, `CurlTransferTool` (with `RefuseWrongArgumentShapes`), `CurlUrlTool`, `CurlVersionTool`, `CurlAiHelpResourceHandlers` and `CurlMcpJsonContext` (its `ToolOptions` and `McpJsonUtilities` dependency are SDK-only; the results' contexts now live beside each ported tool). Delete their old tests only where the ported tests of the port tasks cover the same cases (`CurlCommandToolTests`, `CurlTransferToolTests`, `McpTransferToolShapeTests`, `CurlUrlToolTests`, `CurlVersionToolTests`, `CurlAiHelpResourceHandlersTests`, `CurlMcpJsonContextTests`); rewrite `CurlMcpHostTests` for the new host. Remove the "old class equals new class" comparison tests together with the old classes. List each deleted test file and the file that now covers it in Notes.
- Update `Curl.Mcp.IntegrationTests/CurlTransferToolIntegrationTests.cs`, which references the SDK types. Do not remove the two PackageReference lines in `Curl.Mcp.csproj` yet: the next task does, so the removal is its own commit.
- Any difference from the golden files that the port tasks listed as deliberate is re-listed in Notes; any other difference is a bug to fix here.

## Acceptance criteria

- [ ] `grep -rn "ModelContextProtocol\|Microsoft.Extensions" Curl.Mcp Curl.Mcp.UnitTests Curl.Mcp.IntegrationTests --include=*.cs` finds nothing.
- [ ] `dotnet test Curl.Mcp.IntegrationTests --filter "TestCategory=Integration&FullyQualifiedName~Golden"` passes, and so do the existing server and native-AOT integration tests (`CurlMcpServerIntegrationTests`, `CurlMcpNativeAotIntegrationTests`, `CurlTransferToolIntegrationTests`).
- [ ] `initialize`, `tools/list`, `tools/call` for each of the five tools, and `resources/read` produce the golden responses; Notes lists every deliberate difference with its reason.
- [ ] Notes lists each deleted test file and the file covering its cases.
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Mcp,Curl.Mcp.UnitLibrary` reports 0 failing members (100% line, 100% branch, complexity at most 10, CRAP at most 30), measured from the fast tests only.
- [ ] `dotnet build` of every project named in touches is clean with `-warnaserror`, and `dotnet test --filter "TestCategory!=Integration"` passes for the test projects named in touches, with no test deleted, skipped or weakened.

## Notes

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: f8c57a3e c1ec6728 b4692e39 208afdc2 96ffe31b 6d2cec38.
