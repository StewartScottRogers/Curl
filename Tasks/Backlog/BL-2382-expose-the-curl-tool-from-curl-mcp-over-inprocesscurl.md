---
id: BL-2382
title: Expose the curl tool from Curl.Mcp over InProcessCurl
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-2321, BL-2381]
touches: [Curl.Mcp, Curl.Mcp.UnitTests]
requirement: none
created: 2026-10-15
completed:
---

## Goal

Add the [McpServerToolType] class in Curl.Mcp whose curl method forwards to the UnitLibrary tool, and the real runner adapter over Curl.Console.InProcessCurl.

## Context

ADR-0548 (`Documentation/Planning/Decisions/ADR-0548-curl-mcp-an-mcp-server-over-the-api-seam-on-microsofts-sdk.md`) and the plan `Documentation/Planning/Curl-Mcp.md` decide the design: `Curl.Mcp` is the thin host holding the Microsoft MCP SDK wiring, `Curl.Mcp.UnitLibrary` holds all tool logic (base class library only), `Curl.Mcp.UnitTests` tests both. The two packages `ModelContextProtocol` 2.2.0 and `Microsoft.Extensions.Hosting` 10.0.x are approved for `Curl.Mcp` only.

`Curl.Mcp` references `Curl.Console` for its public `InProcessCurl` (read `Curl.Console/InProcessCurl.cs` and `CurlComposition.cs` for the connectors it needs; pick the overload that builds the production TCP connector). If referencing an exe project breaks the build or the AOT publish, stop and file a task to extract the smallest public surface instead of widening this one. The method carries `[McpServerTool(Name = "curl")]` and a `[Description]` that tells an agent that `args` are curl's arguments without the program name, that file access is confined to `CURL_MCP_ROOT`, and that `--ai-help` documents every option. The method holds no logic beyond forwarding.

## Acceptance criteria

- [ ] The server lists exactly one tool, `curl`, with that description and a JSON schema for `args` (required string array) and `stdin` (optional string); a unit test over the service graph asserts it.
- [ ] The runner adapter is covered by a unit test that runs `curl --version` through it with injected streams and gets exit 0 and a first line starting with `curl `; no network is used.
- [ ] `Curl.Mcp` contains only attribute wiring and forwarding calls; `Curl.Mcp/CLAUDE.md` states this rule.
- [ ] - `dotnet build Curl.Mcp -warnaserror` and `dotnet build Curl.Mcp.UnitLibrary -warnaserror` are clean; `dotnet test Curl.Mcp.UnitTests --filter "TestCategory!=Integration"` is green; `powershell -NoProfile -File Measure-CodeQuality.ps1` reports no member of `Curl.Mcp.UnitLibrary` or `Curl.Mcp` outside the four gates (100% line, 100% branch, complexity at most 10, CRAP at most 30).

## Notes

Plan: Documentation/Planning/Curl-Mcp.md. Decision: ADR-0548.

## Log

- 2026-10-15: Created.
