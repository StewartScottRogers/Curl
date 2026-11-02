---
id: BL-4482
title: Conformance: MCP 2025-06-18 lifecycle, tools and Streamable HTTP for mcp:// and mcps:// (McpClient part 1 of 2)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4500, BL-4505, BL-4506, BL-4507, BL-4502, BL-4503]
touches: [Curl.Conformance.Extended.UnitLibrary/McpClient, Curl.Conformance.Extended.UnitTests/McpClient, Documentation/Conformance/Extended/McpClient.md, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed:
---
# BL-4482 — Conformance: MCP 2025-06-18 lifecycle, tools and Streamable HTTP for mcp:// and mcps:// (McpClient part 1 of 2)

## Goal

Curl's `mcp://` and `mcps://` client is measured against the Model Context Protocol 2025-06-18 base protocol,
lifecycle, tools and Streamable HTTP transport through the real curl command line, every client requirement is in a
matrix mapped to a test, and every failure is fixed or listed with its fix task.

## Context

Specification: Model Context Protocol, revision 2025-06-18 (modelcontextprotocol.io/specification/2025-06-18):
"Base Protocol > Overview" (JSON-RPC 2.0 requests, responses, notifications, ids, errors), "Lifecycle" (initialize,
version negotiation, capabilities, `notifications/initialized`, shutdown), "Transports > Streamable HTTP" (POST with
`Accept: application/json, text/event-stream`, JSON and SSE responses, `Mcp-Session-Id`, `MCP-Protocol-Version`,
202 for notifications, 404 on an expired session, DELETE to end), "Server Features > Tools" (`tools/list` with
pagination cursors, `tools/call`, content types, `isError`). Part 2 (BL-4483) covers the stdio transport.

Library: `Curl.Extended.Protocol.McpClient.UnitLibrary` (no `CLAUDE.md`; `Wire/`, `StreamableHttp/`,
`Client/McpClientFlow`, `McpHttpSession`, `McpProtocolHandler`; `McpClientFlow.LatestProtocolVersion` is
`2025-06-18`). Without `-d` it lists tools (name, tab, description per line); with
`-d '{"name":"tool","arguments":{...}}'` it calls one and writes each content item; exit 22 when the tool reports an
error.

Part of the extended-protocol conformance plan Stewart approved on 2026-11-02. Follow
`Documentation/Conformance/Extended/README.md` and the ADR BL-4462 records: the harness, `[SpecRequirement]` IDs
(`McpClient:<source>-<locator>`), the matrix format and the `KnownFailures.txt` rule. Measure against the
specification, never against upstream curl. The reference server implements the protocol from the specification
itself and references no `Curl.Extended.Protocol.*` project (and not `Curl.Mcp`'s server code either). One file per
case group; nothing added to `Harness/`. This task touches only its own conformance files so protocols run in
parallel: a fault in its own server or cases is fixed here, but a defect in the extension library is listed in
`KnownFailures.txt` with a filed High task `Fix McpClient conformance: <requirement>` (pipeline feature; touches the
library, its `.UnitTests` and `KnownFailures.txt`; criteria: the case passes, a focused unit test in the library's
`.UnitTests` pins the fix (ADR-0482), the `KnownFailures.txt` line is removed).

## Acceptance criteria

- [ ] `Documentation/Conformance/Extended/McpClient.md` gives every MUST, MUST NOT, SHOULD and SHOULD NOT of this
      scope that binds a client a `McpClient:` ID mapped to a test or to `n/a: <reason>`, links to
      `McpClient-Stdio.md` (BL-4483), and the harness's matrix check passes.
- [ ] A reference MCP server in `Curl.Conformance.Extended.UnitLibrary/McpClient/` keeps its JSON-RPC and tool logic
      apart from its transport, so BL-4483 can serve it over stdio from `McpClient/Stdio/` without editing this
      task's files.
- [ ] Cases in `Curl.Conformance.Extended.UnitTests/McpClient/` run curl in process and assert HTTP requests,
      stdout, stderr and exit code for at least: initialize and the initialized notification, a server that answers
      with an older or unsupported protocol version, `Mcp-Session-Id` and `MCP-Protocol-Version` on later requests,
      a JSON reply and an SSE reply (with unrelated messages before the response), a paginated `tools/list`, a call
      returning text, image and resource content, `isError`, a JSON-RPC error, a 404 for an expired session, and
      `mcps://` over the TLS route.
- [ ] Every failing case is fixed in this task or listed in `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`
      with the ID of its filed High `Fix McpClient conformance: ...` task; the known-failures check passes.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is
      green; no test needs `TestCategory=Integration`.
- [ ] The code added under `Curl.Conformance.Extended.UnitLibrary/McpClient/` is at 100% line and branch coverage,
      complexity at most 10 and CRAP at most 30 per method (`Measure-CodeQuality.ps1`).
- [ ] No package added; every test is platform-neutral (no drive letters, Windows-only paths or text).

## Notes

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: d316a8f7 cd294115 6ada1d6e 14e1c865 d8e3fb0e 79cbadb2 ef6c9026.
