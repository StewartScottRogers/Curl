---
id: BL-2389
title: Attempt native AOT for Curl.Mcp and record the outcome in an ADR
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-2321, BL-2388]
touches: [Curl.Mcp, Documentation/Planning/Decisions]
requirement: none
created: 2026-10-15
completed:
---

## Goal

Try PublishAot for the packed Curl.Mcp; keep it if it builds and runs, otherwise keep self-contained packing and record why.

## Context

ADR-0548 (`Documentation/Planning/Decisions/ADR-0548-curl-mcp-an-mcp-server-over-the-api-seam-on-microsofts-sdk.md`) and the plan `Documentation/Planning/Curl-Mcp.md` decide the design: `Curl.Mcp` is the thin host holding the Microsoft MCP SDK wiring, `Curl.Mcp.UnitLibrary` holds all tool logic (base class library only), `Curl.Mcp.UnitTests` tests both. The two packages `ModelContextProtocol` 2.2.0 and `Microsoft.Extensions.Hosting` 10.0.x are approved for `Curl.Mcp` only.

ADR-0548 decision 6: native AOT where the SDK allows it, as Microsoft's `mcpserver` template does; if the SDK breaks the AOT build, publish self-contained and record why. ModelContextProtocol 2.2.0's AOT support (source-generated JSON, explicit `WithTools<T>()`) is the likely friction; `Directory.Build.props` already makes production projects AOT-compatible, so analyzers report trim problems at build. Build with `dotnet publish Curl.Mcp -r win-x64` (`C:\Program Files (x86)\Microsoft Visual Studio\Installer` on PATH for vswhere). Run the published binary, send `initialize` and `tools/list` over stdio with a PowerShell script, and compare with the self-contained build.

## Acceptance criteria

- [ ] If AOT works: `PublishAot` is set for the packed runtime identifiers, the published binary answers `initialize` and `tools/list` listing `curl`, `transfer`, `parse_url`, `build_url` and `version`, and a new ADR records the sizes measured.
- [ ] If AOT fails: the property stays off, a new ADR (`Documentation/Planning/Decisions/ADR-####-...`, number from the folder) names the exact trim or SDK error and the SDK version, and states that Curl.Mcp ships self-contained; the exact failing command is in the ADR.
- [ ] The ADR is marked "Decided by Claude under Stewart's delegation" and `Documentation/Planning/Curl-Mcp.md` links it.
- [ ] `dotnet build Curl.Mcp -warnaserror` and the fast tests of `Curl.Mcp.UnitTests` are green.

## Notes

Plan: Documentation/Planning/Curl-Mcp.md. Decision: ADR-0548.

## Log

- 2026-10-15: Created.
- 2026-10-16: Backlog -> Doing.
