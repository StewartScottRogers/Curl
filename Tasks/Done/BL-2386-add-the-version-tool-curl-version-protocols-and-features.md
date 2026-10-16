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
completed: 2026-10-16
---

## Goal

Provide MCP tool version that reports what curl -V reports.

## Context

ADR-0548 (`Documentation/Planning/Decisions/ADR-0548-curl-mcp-an-mcp-server-over-the-api-seam-on-microsofts-sdk.md`) and the plan `Documentation/Planning/Curl-Mcp.md` decide the design: `Curl.Mcp` is the thin host holding the Microsoft MCP SDK wiring, `Curl.Mcp.UnitLibrary` holds all tool logic (base class library only), `Curl.Mcp.UnitTests` tests both. The two packages `ModelContextProtocol` 2.2.0 and `Microsoft.Extensions.Hosting` 10.0.x are approved for `Curl.Mcp` only.

`CurlVersionInfo` is in `Curl.Api.UnitLibrary/EngineContracts` and is returned by `ICurlEngine`; the `curl -V` text is produced in `Curl.Cli.UnitLibrary`. Return structured fields (version string, protocols list, features list, SSL backend text) and, as a convenience, the exact first line of `curl -V`. The platform rule applies: whatever the engine reports (Schannel build on Windows, OpenSSL elsewhere).

## Acceptance criteria

- [x] A fake-engine test asserts the version, protocols (in the order curl prints them) and features come through unchanged.
- [x] A real-engine test asserts the version equals the one `curl --version` prints from Curl itself.
- [x] The server lists `version` with a description.
- [x] - `dotnet build Curl.Mcp -warnaserror` and `dotnet build Curl.Mcp.UnitLibrary -warnaserror` are clean; `dotnet test Curl.Mcp.UnitTests --filter "TestCategory!=Integration"` is green; `powershell -NoProfile -File Measure-CodeQuality.ps1` reports no member of `Curl.Mcp.UnitLibrary` or `Curl.Mcp` outside the four gates (100% line, 100% branch, complexity at most 10, CRAP at most 30).

## Notes

Plan: Documentation/Planning/Curl-Mcp.md. Decision: ADR-0548.

- 2026-10-16 (lane 6): `McpVersionTool` (Curl.Mcp.UnitLibrary) answers an `McpVersionToolResult` with the engine's `CurlVersionInfo` fields unchanged (version, protocols, features, TLS backend) and `versionLine`, the platform's first `CurlVersionText` line. `CurlVersionTool` (Curl.Mcp) registers it as `version` (read-only, idempotent, no arguments) over the same `ManagedCurlEngine` the `transfer` tool uses.
- Default taken: the tool passes the engine's lists through as the engine orders them, not re-sorted, so it reports exactly what `curl_version_info` says. Today the managed engine reports no features and no TLS backend and lists `smb`/`smbs`, unlike `curl -V`'s lines; BL-2465 (Backlog) makes `ManagedCurlEngine.VersionInfo` report what `curl -V` does, and the tool follows it with no change.
- Measured: Curl.Mcp.UnitTests 442 passed; Measure-CodeQuality -Library Curl.Mcp.UnitLibrary,Curl.Mcp: 0 failing members; `dotnet build -warnaserror` of the solution clean.

## Log

- 2026-10-15: Created.
- 2026-10-16: Backlog -> Doing.
- 2026-10-16: Doing -> Done. MCP version tool reports the engine's version, protocols, features, TLS backend and curl -V's first line
