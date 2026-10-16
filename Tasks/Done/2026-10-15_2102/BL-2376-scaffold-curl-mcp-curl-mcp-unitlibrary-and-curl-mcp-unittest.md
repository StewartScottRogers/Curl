---
id: BL-2376
title: Scaffold Curl.Mcp, Curl.Mcp.UnitLibrary and Curl.Mcp.UnitTests with a stdio host that lists zero tools
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-2321]
touches: [Curl.Mcp, Curl.Mcp.UnitLibrary, Curl.Mcp.UnitTests, Curl.slnx, Directory.Packages.props, Measure-CodeQuality.ps1]
requirement: none
created: 2026-10-15
completed: 2026-10-15
---

## Goal

Create the three Curl.Mcp projects and a stdio MCP host that starts, answers `initialize` and lists zero tools.

## Context

ADR-0548 (`Documentation/Planning/Decisions/ADR-0548-curl-mcp-an-mcp-server-over-the-api-seam-on-microsofts-sdk.md`) and the plan `Documentation/Planning/Curl-Mcp.md` decide the design: `Curl.Mcp` is the thin host holding the Microsoft MCP SDK wiring, `Curl.Mcp.UnitLibrary` holds all tool logic (base class library only), `Curl.Mcp.UnitTests` tests both. The two packages `ModelContextProtocol` 2.2.0 and `Microsoft.Extensions.Hosting` 10.0.x are approved for `Curl.Mcp` only.

Follow the `Curl.Api` / `Curl.Api.UnitLibrary` / `Curl.Api.UnitTests` pattern (read their csproj files and CLAUDE.md). `Curl.Mcp` is an exe (`OutputType Exe`) using `Host.CreateApplicationBuilder`, `AddMcpServer().WithStdioServerTransport()` and explicit tool registration (an AOT-safe `WithTools<T>()` is preferred over assembly scanning where the SDK offers it); logging goes to stderr only, because stdout is the protocol channel. `Measure-CodeQuality.ps1` decides what is production code by name at about lines 40 and 258 (`*.UnitLibrary` plus `Curl.Console`, `Curl.Api`, `Libcurl`); add `Curl.Mcp` there.

## Acceptance criteria

- [x] `Directory.Packages.props` has `PackageVersion` entries for `ModelContextProtocol` 2.2.0 and `Microsoft.Extensions.Hosting` 10.0.x (latest stable 10.0 release); no `Version` attribute appears on any `PackageReference`.
- [x] Only `Curl.Mcp.csproj` references those two packages; `Curl.Mcp.UnitLibrary` references no package and uses the base class library plus `Curl.Api.UnitLibrary`.
- [x] `Curl.slnx` lists `Curl.Mcp`, `Curl.Mcp.UnitLibrary` and `Curl.Mcp.UnitTests` in alphabetical place, each `.UnitTests` directly after the project it tests, flat, no solution folder.
- [x] Each of the three project folders has a `CLAUDE.md` stating its role (ADR-0548), the rule that only `Curl.Mcp` may take the SDK packages, and that `Curl.Mcp.UnitTests` tests both projects.
- [x] `Measure-CodeQuality.ps1` treats `Curl.Mcp` as production code (its help text and its name test both name it).
- [x] A unit test builds the host's service graph in process (no real stdio) and asserts the server reports zero tools; a second test asserts `Curl.Mcp` writes nothing to standard output outside the protocol.
- [x] `dotnet build Curl.Mcp -warnaserror` and `dotnet build Curl.Mcp.UnitLibrary -warnaserror` are clean; `dotnet test Curl.Mcp.UnitTests --filter "TestCategory!=Integration"` is green; `powershell -NoProfile -File Measure-CodeQuality.ps1` reports no member of `Curl.Mcp.UnitLibrary` or `Curl.Mcp` outside the four gates (100% line, 100% branch, complexity at most 10, CRAP at most 30).
- [x] `Directory.Build.props` checks (`VerifyAotCompatibility`, `VerifyIntegrationTestPlacement`) still pass.

## Notes

Plan: Documentation/Planning/Curl-Mcp.md. Decision: ADR-0548.

- Delivered directly rather than through the full `/feature` stages: ADR-0548 and the plan already fix the design, and a scaffold has no behaviour to plan.
- `Microsoft.Extensions.Hosting` is pinned at 10.0.13, the latest stable 10.0 release on nuget.org on 2026-10-15.
- `CurlMcpHost.Build` clears the default logging providers (the default console provider writes to standard output) and adds the console logger with `LogToStandardErrorThreshold = Trace`, so every log line goes to standard error.
- `Program.Main` is one line that serves the real stdio; it carries `[ExcludeFromCodeCoverage]` under ADR-0083 and is left to `Curl.Mcp.IntegrationTests` (BL-2390). `CurlMcpHost` is 100% line and branch covered.
- `Curl.Mcp.UnitLibrary` has no code yet, so `Measure-CodeQuality.ps1 -Library Curl.Mcp,Curl.Mcp.UnitLibrary` reports only `Curl.Mcp`: 100% line, 100% branch, complexity 2, 0 failing members.
- Zero tools is checked through `IOptions<McpServerOptions>.ToolCollection` and the registered `McpServerTool` services. No tool exists yet, so there is no `WithTools<T>()` call.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. Curl.Mcp, Curl.Mcp.UnitLibrary and Curl.Mcp.UnitTests exist; the stdio MCP host builds with zero tools and logs to stderr only
