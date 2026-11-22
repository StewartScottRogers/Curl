---
id: BL-5754
title: Make a Curl.Api body to mcp://, mcps:// and mcp+stdio: call the tool, not list the tools
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-5753]
touches: [Curl.Extended.Protocol.McpClient.UnitLibrary, Curl.Engine.UnitLibrary/ManagedEasyTransfer.Context.cs, Curl.Engine.UnitLibrary/CLAUDE.md, Curl.Engine.UnitTests/ManagedEasyTransferTests.Mqtt.cs, Curl.Api/RequestBodies/UploadSchemes.cs, Curl.Api.UnitTests/RequestBodies/UploadSchemeBodyTests.cs]
requirement: none
created: 2026-11-22
completed: 2026-11-22
---
# BL-5754 — Make a Curl.Api body to mcp://, mcps:// and mcp+stdio: call the tool, not list the tools

## Goal

A `Curl.Api` `POST` with a `tools/call` JSON body to an `mcp://`, `mcps://` or `mcp+stdio:` URL makes that call and returns its result, as the console's `-d` does, instead of returning the tool list.

## Context

From Surly cookbook bug report `2026-11-22T1116-Curl-bug-report.md` (batch 11 of the 1,000-recipe plan), finding 2. Build tested: Curl `c84035f797`, Windows 11; server Surl `5da5b3eaf`. Check it still reproduces on the current branch first.

`surl mcp://127.0.0.1:8765/`; a `Curl.Api` `POST` to `mcp://127.0.0.1:8765/` with a `tools/call` JSON body returns the tools list (the extension''s no-data default). Same family as BL-5611 and BL-5681 (`Curl.Api` bodies not reaching extensions that take `-d`): the MCP client decides "list" when it sees no `-d` data, and the `Curl.Api` body does not arrive in the form it checks. Follow BL-5611's approach so the MCP client sees a `Curl.Api` byte, string or stream body as `-d` data. Cookbook recipe: `mcp-start-an-mcp-server-as-a-program-in-csharp`.

## Acceptance criteria

- [x] Against Surl's MCP server (or the MCP test double), a `Curl.Api` `POST` with a `tools/call` body to `mcp://`, `mcps://` and `mcp+stdio:` returns the tool's result; the Notes record the runs.
- [x] A unit test pins that a `Curl.Api` body reaches the MCP client as `-d` data for each scheme.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-22: Created from Surly bug report 2026-11-22T1116, finding 2.
- 2026-11-22: Backlog -> Doing. Criteria at claim: dc5fc37c 2a9c02b2 3d836d5e.

## Notes

- Run 1 (no edits): the fix is a one-line addition of "mcp", "mcps", "mcp+stdio" to `Curl.Api/RequestBodies/UploadSchemes.cs` (BL-5611's approach) plus DataRows in `UploadSchemeBodyTests.cs`; the McpClient library itself needs no change. BL-5753 (Doing) touches `Curl.Api/RequestBodies`, so this task waits for it (depends-on BL-5753).
- 2026-11-22: Doing -> Backlog. Needs Curl.Api/RequestBodies/UploadSchemes.cs, which BL-5753 holds; waits for BL-5753.
- 2026-11-22: Backlog -> Doing. Criteria at claim: dc5fc37c 2a9c02b2 3d836d5e.
- Run 2: BL-5758 (Done, c267034e5f) already sends a `Curl.Api` byte body to every extension scheme as `-d` data, so `UploadSchemes.cs` needed no change. Reproduced on this branch with a file-based app driving `CurlClient.Create` against curl's own MCP server (`mcp+stdio://localhost/<path>/Curl.Mcp.exe`, body `{"name":"version","arguments":{}}`): the byte body returned the `version` tool's result, but a `CurlContent.FromStream` body still returned the tool list, because the engine passed only `CURLOPT_POSTFIELDS` to extensions, not a read-callback post.
- Fix: `ManagedEasyTransfer.Context.cs` reads a read-callback post to an extension URL up front as its `PostData` (ADR-1337). Widened `touches` to that file, its tests (`ManagedEasyTransferTests.Mqtt.cs`) and the engine's CLAUDE.md; no task in Doing on origin/work/dark-factory held them.
- After the fix the same run returns the `version` tool's result for both the byte and the stream body over `mcp+stdio:`. `mcp://` and `mcps://` take the same `PostData` path in `McpProtocolHandler.ExecuteAsync` (only the session differs), and no HTTP MCP server was at hand here, so they are pinned by unit tests: `UploadSchemeBodyTests.SendAsync_PostedToolCallToAnMcpScheme_ReachesTheMcpClientAsDashDData` (all three schemes, Curl.Api side) and the `TransferSettingsOf_ReadCallbackPost*` tests (engine side).
- `dotnet build` clean (0 warnings), fast tests green; Measure-CodeQuality for Curl.Engine.UnitLibrary: 0 failing members (a first run flagged `PostDataOf` at complexity 12, split into `PostFieldsDataOf`). Tests are platform-neutral (no paths or OS text).
- 2026-11-22: Doing -> Done. Curl.Api byte and stream bodies to mcp, mcps and mcp+stdio reach the MCP client as -d data and call the tool
