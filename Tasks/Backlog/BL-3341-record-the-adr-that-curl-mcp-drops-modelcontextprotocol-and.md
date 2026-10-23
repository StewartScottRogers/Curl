---
id: BL-3341
title: Record the ADR that Curl.Mcp drops ModelContextProtocol and Microsoft.Extensions.Hosting for a hand-written MCP server
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions/ADR-0798-curl-mcp-drops-its-two-packages-for-a-hand-written-server.md, Documentation/Planning/Decisions/ADR-0548-curl-mcp-an-mcp-server-over-the-api-seam-on-microsofts-sdk.md, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-10-23
completed:
---
# BL-3341 — Record the ADR that Curl.Mcp drops ModelContextProtocol and Microsoft.Extensions.Hosting for a hand-written MCP server

## Goal

An ADR, "Decided by Claude under Stewart's delegation", records that `Curl.Mcp` will drop `ModelContextProtocol` (2.2.0) and `Microsoft.Extensions.Hosting` (10.0.13) for a hand-written, base-class-library MCP server, and the work that follows is planned from it.

## Context

- Stewart, 2026-10-23: "Can we remove these yet" ... "file it". The two packages were approved for `Curl.Mcp` alone by ADR-0548 (2026-10-15). The standing rule (root `CLAUDE.md`) is BCL only, and the default answer is to hand-build what the BCL lacks, isolated in its own `UnitLibrary`. BL-2486 / ADR-0621 already sketched the fallback: a `Curl.Mcp.Protocol.UnitLibrary` with its own `.UnitTests`.
- Read ADR-0548, ADR-0555, ADR-0589, ADR-0593, ADR-0596, ADR-0598, ADR-0621 and `Curl.Mcp/CLAUDE.md` first. Today the SDK supplies: the generic host (startup, stderr logging, cancellation, lifetime), the stdio transport, JSON-RPC and the `initialize`/`ping`/`tools/*`/`resources/*` methods, tool discovery by `[McpServerToolType]` attributes, and each tool's input schema generated from method signatures and `[Description]` attributes.
- Decide and record: (1) a new project pair `Curl.Mcp.Protocol.UnitLibrary` + `Curl.Mcp.Protocol.UnitTests` (namespace `Curl.Mcp.Protocol`, no dependency on any other Curl project) holds the protocol core: JSON-RPC 2.0 messages and error codes, newline-delimited stdio framing, the dispatcher with `initialize`, `ping`, notifications, `tools/list`, `tools/call`, `resources/list`, `resources/read`; (2) tools and resources are one file each in `Curl.Mcp/Tools/` implementing the core's `IMcpTool` / resource-provider interfaces, with the input schema written out by hand per tool, never reflected; (3) the host is a hand-written composition root in `Curl.Mcp` (no DI container, no reflection scanning, constructor injection), cancelling on stdin close, Ctrl+C and SIGTERM, logging to standard error only; (4) JSON uses `System.Text.Json` source generation only, one small context per area, so native AOT stays warning-free (ADR-0621); (5) behaviour parity is proved by golden conversations recorded from the SDK-based server before it is replaced, and any deliberate difference (for example the SDK's generic "An error occurred invoking" text) is listed in the ADR; (6) the supported MCP protocol versions are exactly those the SDK 2.2.0 server negotiated in the golden recording, per https://modelcontextprotocol.io/specification.
- The plan filed with this task: BL-3342 golden recording, BL-3343 scaffold, BL-3344 JSON-RPC, BL-3345 transport, BL-3346 abstractions, BL-3347 dispatcher, BL-3348 tools methods, BL-3349 resources methods, BL-3350 host, BL-3351 to BL-3355 tool and resource ports, BL-3356 cut-over, BL-3357 package removal and documents.
- Do NOT edit `CLAUDE.md`, `DarkFactory/LaneRules.md` or `README.md` here: their package-exception wording stays true until the last task of this plan removes the packages, and that task rewrites it. State the intent in the ADR only.

## Acceptance criteria

- [ ] The ADR file `Documentation/Planning/Decisions/ADR-0798-curl-mcp-drops-its-two-packages-for-a-hand-written-server.md` exists (use the next free ADR number if 0798 is taken, and rename the touches entry), is marked "Decided by Claude under Stewart's delegation", and states decisions (1) to (6) above with the reason for each.
- [ ] The ADR names the follow-up tasks by ID (the plan filed alongside this task) and says the packages leave only when the final task proves the golden conversations unchanged.
- [ ] ADR-0548 gains a status note that its decision 1 (packages) and the SDK wording in decisions 2 and 5 are superseded in intent by the new ADR, with a link; the rest of ADR-0548 is untouched.
- [ ] `Documentation/Planning/Decisions/README.md` lists the new ADR in the same form as its neighbours.
- [ ] Every statement in the ADR is true of the repository as it is now, or is written as intent.

## Notes

## Log

- 2026-10-23: Created.
