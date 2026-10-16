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
completed: 2026-10-15
---

## Goal

Add the [McpServerToolType] class in Curl.Mcp whose curl method forwards to the UnitLibrary tool, and the real runner adapter over Curl.Console.InProcessCurl.

## Context

ADR-0548 (`Documentation/Planning/Decisions/ADR-0548-curl-mcp-an-mcp-server-over-the-api-seam-on-microsofts-sdk.md`) and the plan `Documentation/Planning/Curl-Mcp.md` decide the design: `Curl.Mcp` is the thin host holding the Microsoft MCP SDK wiring, `Curl.Mcp.UnitLibrary` holds all tool logic (base class library only), `Curl.Mcp.UnitTests` tests both. The two packages `ModelContextProtocol` 2.2.0 and `Microsoft.Extensions.Hosting` 10.0.x are approved for `Curl.Mcp` only.

`Curl.Mcp` references `Curl.Console` for its public `InProcessCurl` (read `Curl.Console/InProcessCurl.cs` and `CurlComposition.cs` for the connectors it needs; pick the overload that builds the production TCP connector). If referencing an exe project breaks the build or the AOT publish, stop and file a task to extract the smallest public surface instead of widening this one. The method carries `[McpServerTool(Name = "curl")]` and a `[Description]` that tells an agent that `args` are curl's arguments without the program name, that file access is confined to `CURL_MCP_ROOT`, and that `--ai-help` documents every option. The method holds no logic beyond forwarding.

## Acceptance criteria

- [x] The server lists exactly one tool, `curl`, with that description and a JSON schema for `args` (required string array) and `stdin` (optional string); a unit test over the service graph asserts it.
- [x] The runner adapter is covered by a unit test that runs `curl --version` through it with injected streams and gets exit 0 and a first line starting with `curl `; no network is used.
- [x] `Curl.Mcp` contains only attribute wiring and forwarding calls; `Curl.Mcp/CLAUDE.md` states this rule.
- [x] - `dotnet build Curl.Mcp -warnaserror` and `dotnet build Curl.Mcp.UnitLibrary -warnaserror` are clean; `dotnet test Curl.Mcp.UnitTests --filter "TestCategory!=Integration"` is green; `powershell -NoProfile -File Measure-CodeQuality.ps1` reports no member of `Curl.Mcp.UnitLibrary` or `Curl.Mcp` outside the four gates (100% line, 100% branch, complexity at most 10, CRAP at most 30).

## Notes

Plan: Documentation/Planning/Curl-Mcp.md. Decision: ADR-0548.

Decisions (ADR-0575, decided by Claude under Stewart's delegation):
- `InProcessCurl` takes no working directory and no cancellation token, so `CurlMcpHost.Build` reads `CURL_MCP_ROOT` and `CURL_MCP_MAX_RESPONSE_BYTES` once (a bad value stops the server) and makes the root the process's current directory once before serving. `InProcessCurlCommandRunner` refuses any other working directory, so relative paths cannot escape the root; the token is not passed on.
- The adapter uses the dialing overload: the production `TcpConnector` over `TcpDialer` with default `TcpSocketOptions`, `SystemDnsResolver` and `UdpDatagramConnector`. Referencing the `Curl.Console` exe from `Curl.Mcp` builds cleanly; the AOT publish is BL-2389's to try.
- `CurlCommandTool.RunCurlAsync` returns a `CallToolResult`: one text block holding the `McpCurlToolResult` as camel-case JSON (enum names), with `IsError` taken from the result. The JSON comes from the source-generated `CurlMcpJsonContext`, combined with `McpJsonUtilities.DefaultOptions` to register the tool. Its generated members are excluded from coverage: the first measurement counted 68 generated members against the gates.

Measured: `Measure-CodeQuality.ps1 -Library Curl.Mcp,Curl.Mcp.UnitLibrary` gives Curl.Mcp 100% line, 100% branch, worst CRAP 4, and Curl.Mcp.UnitLibrary 100%/100%, worst CRAP 10, with 0 failing members. Curl.Mcp.UnitTests: 343 passed. Full fast suite green.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. Curl.Mcp serves the curl tool over InProcessCurl: one tool with its schema, the root as current directory, results as JSON tool errors
