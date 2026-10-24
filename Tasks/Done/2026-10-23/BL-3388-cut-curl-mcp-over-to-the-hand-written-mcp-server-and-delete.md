---
id: BL-3388
title: Cut Curl.Mcp over to the hand-written MCP server and delete the SDK-attributed tool classes
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3374, BL-3380, BL-3381, BL-3382, BL-3383, BL-3384, BL-3385, BL-3386, BL-3387]
touches: [Curl.Mcp, Curl.Mcp.UnitTests, Curl.Mcp.IntegrationTests/CurlTransferToolIntegrationTests.cs, Curl.Mcp.IntegrationTests/Golden/tools-call-transfer-missing-url.json, Curl.Mcp.IntegrationTests/CLAUDE.md]
model: opus
requirement: none
created: 2026-10-23
completed: 2026-10-23
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

- [x] `grep -rn "ModelContextProtocol\|Microsoft.Extensions" Curl.Mcp Curl.Mcp.UnitTests Curl.Mcp.IntegrationTests --include=*.cs` finds nothing.
- [x] `dotnet test Curl.Mcp.IntegrationTests --filter "TestCategory=Integration&FullyQualifiedName~Golden"` passes, and so do the existing server and native-AOT integration tests (`CurlMcpServerIntegrationTests`, `CurlMcpNativeAotIntegrationTests`, `CurlTransferToolIntegrationTests`).
- [x] `initialize`, `tools/list`, `tools/call` for each of the five tools, and `resources/read` produce the golden responses; Notes lists every deliberate difference with its reason.
- [x] Notes lists each deleted test file and the file covering its cases.
- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Mcp,Curl.Mcp.UnitLibrary` reports 0 failing members (100% line, 100% branch, complexity at most 10, CRAP at most 30), measured from the fast tests only.
- [x] `dotnet build` of every project named in touches is clean with `-warnaserror`, and `dotnet test --filter "TestCategory!=Integration"` passes for the test projects named in touches, with no test deleted, skipped or weakened.

## Notes

- Plan: `CurlMcpHost.Build` now returns the composed `McpStdioHost` (env reading, root as current directory and the stop signals stay in `McpStdioHost`, ADR-0575); `CreateRegistry` registers the five tools and `AiHelpMcpResourceProvider` one line each over one shared `ManagedCurlEngine`; `CreateHandlers` gives tools/list, tools/call, resources/list, resources/read and `logging/setLevel`. `Program.Main` runs the host and returns its exit code. ServerInfo is the assembly name and version (`Curl.Mcp`, `0.1.0.0`), as the SDK answered.
- Decided: tools are registered in the order the SDK host listed them (parse_url, transfer, curl, build_url, version), because the golden comparison keeps array order. Decided: `LoggingSetLevelMcpHandler` (new, in Curl.Mcp) advertises the `logging` capability the SDK advertised and answers `logging/setLevel` with `{}`; the server sends no log notifications, so the level changes nothing.
- Descriptions moved from the deleted SDK classes into each ported tool's `Description`; the unit tests pin each with StartsWith/EndsWith, and the golden tools-list pins the full text.
- Deliberate difference from the golden files (listed by BL-3384): `tools/call transfer {}` answers the structured `InvalidRequest` "Invalid url: it is empty." instead of the SDK binder's "An error occurred invoking 'transfer'."; `Golden/tools-call-transfer-missing-url.json` was updated to that answer (added to touches: no task in Doing on origin/work/dark-factory names Curl.Mcp; also `Curl.Mcp.IntegrationTests/CLAUDE.md` to keep it true). The other BL-3384 difference, `"method": null` sending a GET, has no golden conversation. No other differences: all 28 golden conversations pass.
- Deleted test files and what covers their cases: `CurlCommandToolTests.cs` -> `Tools/CurlCommandMcpToolTests.cs`; `CurlTransferToolTests.cs` -> `Tools/TransferMcpToolTests.cs`; `McpTransferToolShapeTests.cs` -> `Tools/TransferMcpToolTests.cs` (CallAsync_WrongShape_*) and two new `McpTransferRequestCheckTests` CheckArgumentShapes tests (null arguments, unknown argument), which restored 100% branch coverage; `CurlUrlToolTests.cs` -> `Tools/ParseUrlMcpToolTests.cs` and `Tools/BuildUrlMcpToolTests.cs`; `CurlVersionToolTests.cs` -> `Tools/VersionMcpToolTests.cs`; `CurlAiHelpResourceHandlersTests.cs` -> `Tools/AiHelpMcpResourceProviderTests.cs`; `CurlMcpJsonContextTests.cs` -> the serialization tests of each ported tool (their JSON contexts live beside them). Removed with the old classes: the five "serializes like the SDK tool" comparison tests and `Provider_ListAndReadEqualTheSdkHandlersForEveryCatalogueResource`. `CurlMcpHostTests` rewritten for the new host.
- Measured: Golden 30/30, CurlMcpServerIntegrationTests, CurlMcpNativeAotIntegrationTests and CurlTransferToolIntegrationTests pass (41 run, the one golden failure fixed as above); Curl.Mcp.UnitTests 526 passed; Measure-CodeQuality -Library Curl.Mcp,Curl.Mcp.UnitLibrary: 100/100, 0 failing members. Platform-neutral: no paths or OS text in the new tests.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: f8c57a3e c1ec6728 b4692e39 208afdc2 96ffe31b 6d2cec38.
- 2026-10-23: Doing -> Done. Curl.Mcp runs on the hand-written MCP server end to end; SDK-attributed tool classes deleted and no Curl.Mcp source uses the SDK
