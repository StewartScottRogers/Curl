---
id: BL-2384
title: Expose the transfer tool from Curl.Mcp over the managed engine
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2321, BL-2383, BL-2382]
touches: [Curl.Mcp, Curl.Mcp.UnitTests]
requirement: none
created: 2026-10-15
completed:
---

## Goal

Add the transfer method to the Curl.Mcp tool classes and compose the real ICurlEngine for it.

## Context

ADR-0548 (`Documentation/Planning/Decisions/ADR-0548-curl-mcp-an-mcp-server-over-the-api-seam-on-microsofts-sdk.md`) and the plan `Documentation/Planning/Curl-Mcp.md` decide the design: `Curl.Mcp` is the thin host holding the Microsoft MCP SDK wiring, `Curl.Mcp.UnitLibrary` holds all tool logic (base class library only), `Curl.Mcp.UnitTests` tests both. The two packages `ModelContextProtocol` 2.2.0 and `Microsoft.Extensions.Hosting` 10.0.x are approved for `Curl.Mcp` only.

The real engine comes from `Curl.Engine.UnitLibrary` (`EngineComposition`, `ManagedCurlEngine`); read how `Curl.Api` or `Libcurl` obtains it and use the same composition with production connectors. The tool description tells an agent when to prefer `transfer` (one HTTP request with a structured result) over `curl` (any curl option).

## Acceptance criteria

- [ ] The server lists `curl` and `transfer`; a test asserts both names, descriptions and the `transfer` input schema (required `url`).
- [ ] A unit test runs `transfer` through the real composition against a loopback listener the test opens and closes itself and asserts status and body; if that cannot be done without a real network resource, the test lives in `Curl.Mcp.IntegrationTests` and Notes says so.
- [ ] - `dotnet build Curl.Mcp -warnaserror` and `dotnet build Curl.Mcp.UnitLibrary -warnaserror` are clean; `dotnet test Curl.Mcp.UnitTests --filter "TestCategory!=Integration"` is green; `powershell -NoProfile -File Measure-CodeQuality.ps1` reports no member of `Curl.Mcp.UnitLibrary` or `Curl.Mcp` outside the four gates (100% line, 100% branch, complexity at most 10, CRAP at most 30).

## Notes

Plan: Documentation/Planning/Curl-Mcp.md. Decision: ADR-0548.

## Log

- 2026-10-15: Created.
