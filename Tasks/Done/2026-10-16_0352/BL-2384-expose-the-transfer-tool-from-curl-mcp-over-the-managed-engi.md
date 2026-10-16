---
id: BL-2384
title: Expose the transfer tool from Curl.Mcp over the managed engine
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2321, BL-2383, BL-2382]
touches: [Curl.Mcp, Curl.Mcp.UnitTests, Curl.Mcp.IntegrationTests, Curl.slnx]
requirement: none
created: 2026-10-15
completed: 2026-10-16
---

## Goal

Add the transfer method to the Curl.Mcp tool classes and compose the real ICurlEngine for it.

## Context

ADR-0548 (`Documentation/Planning/Decisions/ADR-0548-curl-mcp-an-mcp-server-over-the-api-seam-on-microsofts-sdk.md`) and the plan `Documentation/Planning/Curl-Mcp.md` decide the design: `Curl.Mcp` is the thin host holding the Microsoft MCP SDK wiring, `Curl.Mcp.UnitLibrary` holds all tool logic (base class library only), `Curl.Mcp.UnitTests` tests both. The two packages `ModelContextProtocol` 2.2.0 and `Microsoft.Extensions.Hosting` 10.0.x are approved for `Curl.Mcp` only.

The real engine comes from `Curl.Engine.UnitLibrary` (`EngineComposition`, `ManagedCurlEngine`); read how `Curl.Api` or `Libcurl` obtains it and use the same composition with production connectors. The tool description tells an agent when to prefer `transfer` (one HTTP request with a structured result) over `curl` (any curl option).

## Acceptance criteria

- [x] The server lists `curl` and `transfer`; a test asserts both names, descriptions and the `transfer` input schema (required `url`).
- [x] A unit test runs `transfer` through the real composition against a loopback listener the test opens and closes itself and asserts status and body; if that cannot be done without a real network resource, the test lives in `Curl.Mcp.IntegrationTests` and Notes says so.
- [x] - `dotnet build Curl.Mcp -warnaserror` and `dotnet build Curl.Mcp.UnitLibrary -warnaserror` are clean; `dotnet test Curl.Mcp.UnitTests --filter "TestCategory!=Integration"` is green; `powershell -NoProfile -File Measure-CodeQuality.ps1` reports no member of `Curl.Mcp.UnitLibrary` or `Curl.Mcp` outside the four gates (100% line, 100% branch, complexity at most 10, CRAP at most 30).

## Notes

Plan: Documentation/Planning/Curl-Mcp.md. Decision: ADR-0548.

- Wiring: `Curl.Mcp/CurlTransferTool.cs` (`[McpServerToolType]`, tool `transfer`) forwards flat
  fields (`url` required; `method`, `headers`, `body`, `followRedirects`, `timeoutSeconds`,
  `insecure`, `proxy`, `userAgent`) as one `McpTransferRequest` to `McpTransferTool`.
  `CurlMcpHost` registers `new McpTransferTool(new ManagedCurlEngine(), cap)`: the public
  production constructor `Libcurl`'s `CurlGlobal` uses, created once for the process's life. It is
  not container-owned because `ICurlEngine` is `IAsyncDisposable` only and the host is disposed
  synchronously. `CurlMcpJsonContext` gains `McpTransferHeader[]`, `McpTransferBody` and
  `McpTransferToolResult`.
- The loopback test sends bytes over a socket, which ADR-0421 makes an Integration test, so it
  lives in a new `Curl.Mcp.IntegrationTests` project (`CurlTransferToolIntegrationTests`, through
  `CurlMcpHost.Build`'s real composition; asserts status 201, the body, `Content-Type`, and the
  request line and header the listener saw). `Curl.Mcp.IntegrationTests` and `Curl.slnx` were
  added to `touches` for it; no task in Doing on `origin/work/dark-factory` named either (only
  BL-2425, on Curl.Engine). BL-2390 now extends the project rather than creating it, and its Notes
  say so.
- Tests: Curl.Mcp.UnitTests 436 green (new `CurlTransferToolTests`; `CurlMcpHostTests` assert
  both tool names, the `transfer` description and its schema with `url` the only required field);
  Curl.Mcp.IntegrationTests 1 green; the whole fast run green; `dotnet build` 0 warnings.
- `Measure-CodeQuality.ps1 -Library Curl.Mcp,Curl.Mcp.UnitLibrary`: 0 failing members.

## Log

- 2026-10-15: Created.
- 2026-10-16: Backlog -> Doing.
- 2026-10-16: Doing -> Done. Curl.Mcp lists and serves the transfer tool over the production ManagedCurlEngine
