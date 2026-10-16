---
id: BL-2386
title: Add the version tool: curl version, protocols and features
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2321, BL-2376, BL-2380]
touches: [Curl.Mcp, Curl.Mcp.UnitLibrary, Curl.Mcp.UnitTests]
requirement: none
created: 2026-10-15
completed:
---

## Goal

Provide MCP tool version that reports what curl -V reports.

## Context

ADR-0548 (`Documentation/Planning/Decisions/ADR-0548-curl-mcp-an-mcp-server-over-the-api-seam-on-microsofts-sdk.md`) and the plan `Documentation/Planning/Curl-Mcp.md` decide the design: `Curl.Mcp` is the thin host holding the Microsoft MCP SDK wiring, `Curl.Mcp.UnitLibrary` holds all tool logic (base class library only), `Curl.Mcp.UnitTests` tests both. The two packages `ModelContextProtocol` 2.2.0 and `Microsoft.Extensions.Hosting` 10.0.x are approved for `Curl.Mcp` only.

`CurlVersionInfo` is in `Curl.Api.UnitLibrary/EngineContracts` and is returned by `ICurlEngine`; the `curl -V` text is produced in `Curl.Cli.UnitLibrary`. Return structured fields (version string, protocols list, features list, SSL backend text) and, as a convenience, the exact first line of `curl -V`. The platform rule applies: whatever the engine reports (Schannel build on Windows, OpenSSL elsewhere).

## Acceptance criteria

- [ ] A fake-engine test asserts the version, protocols (in the order curl prints them) and features come through unchanged.
- [ ] A real-engine test asserts the version equals the one `curl --version` prints from Curl itself.
- [ ] The server lists `version` with a description.
- [ ] - `dotnet build Curl.Mcp -warnaserror` and `dotnet build Curl.Mcp.UnitLibrary -warnaserror` are clean; `dotnet test Curl.Mcp.UnitTests --filter "TestCategory!=Integration"` is green; `powershell -NoProfile -File Measure-CodeQuality.ps1` reports no member of `Curl.Mcp.UnitLibrary` or `Curl.Mcp` outside the four gates (100% line, 100% branch, complexity at most 10, CRAP at most 30).

## Notes

Plan: Documentation/Planning/Curl-Mcp.md. Decision: ADR-0548.

## Log

- 2026-10-15: Created.
