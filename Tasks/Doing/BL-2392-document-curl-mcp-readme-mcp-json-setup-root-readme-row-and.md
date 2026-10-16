---
id: BL-2392
title: Document Curl.Mcp: README, mcp.json setup, root README row and glossary entries
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-2321, BL-2389, BL-2390]
touches: [Curl.Mcp, Documentation, README.md, CLAUDE.md]
requirement: none
created: 2026-10-15
completed:
---

## Goal

Write Curl.Mcp's README and bring the root README, glossary and root CLAUDE.md in line.

## Context

ADR-0548 (`Documentation/Planning/Decisions/ADR-0548-curl-mcp-an-mcp-server-over-the-api-seam-on-microsofts-sdk.md`) and the plan `Documentation/Planning/Curl-Mcp.md` decide the design: `Curl.Mcp` is the thin host holding the Microsoft MCP SDK wiring, `Curl.Mcp.UnitLibrary` holds all tool logic (base class library only), `Curl.Mcp.UnitTests` tests both. The two packages `ModelContextProtocol` 2.2.0 and `Microsoft.Extensions.Hosting` 10.0.x are approved for `Curl.Mcp` only.

The root README's "Use it from .NET" table (around line 51) lists `Curl.Api` and `Curl.Api.UnitLibrary`; `Documentation/Wiki/Glossary.md` owns the names; root `CLAUDE.md` holds the package rule ("Base class library only") and the exceptions to the project naming rule, which ADR-0548 says to extend. Every statement must be true of the code as it is: take tool names, parameters and environment variables from the code, not from the plan.

## Acceptance criteria

- [ ] `Curl.Mcp/README.md` states what it is, the tools and resources (names and one-line purposes), `CURL_MCP_ROOT` and `CURL_MCP_MAX_RESPONSE_BYTES`, how to run it with `dnx Curl.Mcp`, a VS Code `.vscode/mcp.json` example and a Visual Studio example (a `servers` entry with `type` stdio, `command` dnx, `args` and `env`), and the confinement behaviour.
- [ ] Root `README.md` "Use it from .NET" table has a `Curl.Mcp` row (and its UnitLibrary) and any project count stays accurate.
- [ ] `Documentation/Wiki/Glossary.md` has entries for MCP server, MCP tool, MCP resource and `CURL_MCP_ROOT`.
- [ ] Root `CLAUDE.md`: the Curl.Mcp exception to the `Curl.<Area>.UnitLibrary` naming rule, the quality-gate sentence naming `Curl.Mcp`, and the package rule naming the two packages and the one project that may use them.
- [ ] `Documentation/Planning/Curl-Mcp.md` status section says what is done.

## Notes

Plan: Documentation/Planning/Curl-Mcp.md. Decision: ADR-0548.

## Log

- 2026-10-15: Created.
- 2026-10-16: Backlog -> Doing.
