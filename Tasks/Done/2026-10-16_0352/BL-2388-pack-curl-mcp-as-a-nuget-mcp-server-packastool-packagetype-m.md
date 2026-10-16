---
id: BL-2388
title: Pack Curl.Mcp as a NuGet MCP server: PackAsTool, PackageType McpServer and .mcp/server.json
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2321, BL-2382, BL-2387]
touches: [Curl.Mcp, Curl.Mcp.UnitTests]
requirement: none
created: 2026-10-15
completed: 2026-10-16
---

## Goal

Make dotnet pack Curl.Mcp produce a NuGet package that dnx can run and nuget.org lists as an MCP server.

## Context

ADR-0548 (`Documentation/Planning/Decisions/ADR-0548-curl-mcp-an-mcp-server-over-the-api-seam-on-microsofts-sdk.md`) and the plan `Documentation/Planning/Curl-Mcp.md` decide the design: `Curl.Mcp` is the thin host holding the Microsoft MCP SDK wiring, `Curl.Mcp.UnitLibrary` holds all tool logic (base class library only), `Curl.Mcp.UnitTests` tests both. The two packages `ModelContextProtocol` 2.2.0 and `Microsoft.Extensions.Hosting` 10.0.x are approved for `Curl.Mcp` only.

Follow Microsoft's guidance (https://learn.microsoft.com/dotnet/ai/quickstarts/build-mcp-server): `PackAsTool` true, `ToolCommandName`, `PackageType` `McpServer`, `PackageReadmeFile`, `RuntimeIdentifiers` for the self-contained per-runtime packages (win-x64, win-arm64, linux-x64, linux-arm64, osx-arm64, osx-x64, as the SDK template does), and `.mcp/server.json` packed into the package. `server.json` follows the MCP registry schema the template generates: `name` `io.github.StewartScottRogers/Curl`, a `description`, `version` equal to the package version, a `packages` entry with `registryType` `nuget`, `identifier` `Curl.Mcp`, and `environmentVariables` declaring `CURL_MCP_ROOT` required (description: every file curl reads or writes must lie inside it) and `CURL_MCP_MAX_RESPONSE_BYTES` optional. Check the schema URL and field names against the template output on that page before writing; do not invent fields. Package metadata (authors, licence, repository URL) comes from `Directory.Build.props` where it exists.

## Acceptance criteria

- [x] `Curl.Mcp.csproj` sets the properties above; a unit test parses the csproj XML and asserts `PackAsTool`, `PackageType` McpServer, a non-empty `RuntimeIdentifiers` and the `.mcp/server.json` pack item.
- [x] `Curl.Mcp/.mcp/server.json` is valid JSON and a unit test asserts name, `registryType` nuget, identifier `Curl.Mcp`, `CURL_MCP_ROOT` required, and that its version matches the project version.
- [x] `dotnet pack Curl.Mcp -c Release -o <temp>` succeeds locally and Notes records the package file name and size; the pack run itself is covered by the integration task, not a fast test.
- [x] No package other than the two approved ones is added, and `Curl.Mcp` is the only project that gains package references.
- [x] `dotnet build Curl.Mcp -warnaserror` and `dotnet build Curl.Mcp.UnitLibrary -warnaserror` are clean; `dotnet test Curl.Mcp.UnitTests --filter "TestCategory!=Integration"` is green; `powershell -NoProfile -File Measure-CodeQuality.ps1` reports no member of `Curl.Mcp.UnitLibrary` or `Curl.Mcp` outside the four gates (100% line, 100% branch, complexity at most 10, CRAP at most 30).

## Notes

Plan: Documentation/Planning/Curl-Mcp.md. Decision: ADR-0548.

- Decision: ADR-0596. Fields copied from the local `dotnet new mcpserver` template output
  (schema `https://static.modelcontextprotocol.io/schemas/2025-10-17/server.schema.json`).
  RuntimeIdentifiers follow this task's list, not the template's (which has linux-musl-x64 and no osx-x64).
  Version 0.1.0 set in the csproj; metadata set there because Directory.Build.props has none.
- Added `Curl.Mcp/README.md` (PackageReadmeFile needs one; BL-2392 expands it).
- `Curl.Console` reference: `GlobalPropertiesToRemove="RuntimeIdentifier;SelfContained"` and
  `ValidateExecutableReferencesMatchSelfContained=false`; without them pack failed with NETSDK1150,
  then NETSDK1047 for every non-host runtime.
- `dotnet pack Curl.Mcp -c Release -o %TEMP%\mcppack` (2026-10-16): Curl.Mcp.0.1.0.nupkg 4,192 bytes
  (holds .mcp/server.json, README.md, LICENSE.txt; package types DotnetTool and McpServer), and
  Curl.Mcp.<rid>.0.1.0.nupkg: win-x64 43,101,731, win-arm64 41,444,138, linux-x64 43,115,144,
  linux-arm64 40,855,982, osx-arm64 39,705,704, osx-x64 42,062,177 bytes.
- Tests: McpPackagingTests (6) in Curl.Mcp.UnitTests, reading the csproj and server.json copied to
  the test output under Packaging. Curl.Mcp.UnitTests 431 passed. Measure-CodeQuality for
  Curl.Mcp,Curl.Mcp.UnitLibrary: 0 failing members. No package added.

## Log

- 2026-10-15: Created.
- 2026-10-16: Backlog -> Doing.
- 2026-10-16: Doing -> Done. dotnet pack Curl.Mcp makes an McpServer tool package with .mcp/server.json and six self-contained runtime packages
