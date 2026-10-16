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
completed:
---

## Goal

Make dotnet pack Curl.Mcp produce a NuGet package that dnx can run and nuget.org lists as an MCP server.

## Context

ADR-0548 (`Documentation/Planning/Decisions/ADR-0548-curl-mcp-an-mcp-server-over-the-api-seam-on-microsofts-sdk.md`) and the plan `Documentation/Planning/Curl-Mcp.md` decide the design: `Curl.Mcp` is the thin host holding the Microsoft MCP SDK wiring, `Curl.Mcp.UnitLibrary` holds all tool logic (base class library only), `Curl.Mcp.UnitTests` tests both. The two packages `ModelContextProtocol` 2.2.0 and `Microsoft.Extensions.Hosting` 10.0.x are approved for `Curl.Mcp` only.

Follow Microsoft's guidance (https://learn.microsoft.com/dotnet/ai/quickstarts/build-mcp-server): `PackAsTool` true, `ToolCommandName`, `PackageType` `McpServer`, `PackageReadmeFile`, `RuntimeIdentifiers` for the self-contained per-runtime packages (win-x64, win-arm64, linux-x64, linux-arm64, osx-arm64, osx-x64, as the SDK template does), and `.mcp/server.json` packed into the package. `server.json` follows the MCP registry schema the template generates: `name` `io.github.StewartScottRogers/Curl`, a `description`, `version` equal to the package version, a `packages` entry with `registryType` `nuget`, `identifier` `Curl.Mcp`, and `environmentVariables` declaring `CURL_MCP_ROOT` required (description: every file curl reads or writes must lie inside it) and `CURL_MCP_MAX_RESPONSE_BYTES` optional. Check the schema URL and field names against the template output on that page before writing; do not invent fields. Package metadata (authors, licence, repository URL) comes from `Directory.Build.props` where it exists.

## Acceptance criteria

- [ ] `Curl.Mcp.csproj` sets the properties above; a unit test parses the csproj XML and asserts `PackAsTool`, `PackageType` McpServer, a non-empty `RuntimeIdentifiers` and the `.mcp/server.json` pack item.
- [ ] `Curl.Mcp/.mcp/server.json` is valid JSON and a unit test asserts name, `registryType` nuget, identifier `Curl.Mcp`, `CURL_MCP_ROOT` required, and that its version matches the project version.
- [ ] `dotnet pack Curl.Mcp -c Release -o <temp>` succeeds locally and Notes records the package file name and size; the pack run itself is covered by the integration task, not a fast test.
- [ ] No package other than the two approved ones is added, and `Curl.Mcp` is the only project that gains package references.
- [ ] - `dotnet build Curl.Mcp -warnaserror` and `dotnet build Curl.Mcp.UnitLibrary -warnaserror` are clean; `dotnet test Curl.Mcp.UnitTests --filter "TestCategory!=Integration"` is green; `powershell -NoProfile -File Measure-CodeQuality.ps1` reports no member of `Curl.Mcp.UnitLibrary` or `Curl.Mcp` outside the four gates (100% line, 100% branch, complexity at most 10, CRAP at most 30).

## Notes

Plan: Documentation/Planning/Curl-Mcp.md. Decision: ADR-0548.

## Log

- 2026-10-15: Created.
- 2026-10-16: Backlog -> Doing.
