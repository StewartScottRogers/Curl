# ADR-0799: Curl.Mcp drops ModelContextProtocol and Microsoft.Extensions.Hosting for a hand-written MCP server

- Status: Accepted
- Date: 2026-10-23
- Task: BL-3373
- Supersedes in intent: ADR-0548's decision 1 (packages) and the SDK wording of its decisions 2 and 5
- Decided by Claude under Stewart's delegation.

## Context

ADR-0548 approved two packages for `Curl.Mcp` alone: `ModelContextProtocol` (2.2.0) and
`Microsoft.Extensions.Hosting` (10.0.13). Stewart asked on 2026-10-23 whether they can be removed
yet and to file the work. The standing rule (root `CLAUDE.md`) is base class library only, and
whatever the BCL lacks is built by hand in its own `UnitLibrary`, held to the same gates. ADR-0621
(BL-2486) already named a hand-written server as the fallback and sketched a
`Curl.Mcp.Protocol.UnitLibrary`.

Today the SDK supplies the generic host (startup, standard-error logging, cancellation, lifetime),
the stdio transport, JSON-RPC and the `initialize`, `ping`, `tools/*` and `resources/*` methods,
tool discovery by `[McpServerToolType]` attributes, and each tool's input schema generated from
method signatures and `[Description]` attributes. All of that is replaced.

## Decision

1. **A new project pair holds the protocol core.** `Curl.Mcp.Protocol.UnitLibrary` and
   `Curl.Mcp.Protocol.UnitTests`, namespace `Curl.Mcp.Protocol`, with no dependency on any other
   Curl project. It holds JSON-RPC 2.0 messages and error codes, newline-delimited stdio framing,
   and the dispatcher with `initialize`, `ping`, notifications, `tools/list`, `tools/call`,
   `resources/list` and `resources/read`. *Why:* the protocol is independent of curl, so it is
   unit tested without a network or a process, and held to the 100% line and branch gates like
   every `*.UnitLibrary`.
2. **Tools and resources are one file each in `Curl.Mcp/Tools/`,** implementing the core's
   `IMcpTool` and resource-provider interfaces, with each input schema written out by hand, never
   reflected. *Why:* reflection over method signatures is what breaks native AOT (ADR-0598), and a
   hand-written schema is the exact text a client sees, so it is reviewable and testable. One file
   per tool lets lanes add tools without touching a central switch.
3. **The host is a hand-written composition root in `Curl.Mcp`:** no DI container, no reflection
   scanning, constructor injection. It cancels on stdin close, Ctrl+C and SIGTERM, and logs to
   standard error only. *Why:* standard output carries the protocol, so any log line there corrupts
   the stream; the generic host is the second package and the one that brings the container.
4. **JSON uses `System.Text.Json` source generation only,** one small context per area. *Why:* it
   keeps native AOT warning-free (ADR-0621) with no options built by reflection.
5. **Behaviour parity is proved by golden conversations** recorded from the SDK-based server
   before it is replaced (BL-3374). Any deliberate difference is listed here. Known one: the SDK's
   generic "An error occurred invoking" text for a failing tool is not reproduced; the hand-written
   server returns the tool's own error text as an `isError` result. BL-3374 may add to this list,
   and the final task proves every other byte of the conversations unchanged. *Why:* a client must
   not be able to tell the servers apart beyond what is listed.
6. **The supported MCP protocol versions are exactly those the SDK 2.2.0 server negotiated in the
   golden recording,** per <https://modelcontextprotocol.io/specification>. *Why:* no version is
   claimed that was not measured, and none is dropped.

## Follow-up tasks

BL-3374 golden recording; BL-3375 scaffold; BL-3376 JSON-RPC; BL-3377 transport; BL-3378
abstractions; BL-3379 dispatcher; BL-3380 `tools/*` handlers; BL-3381 `resources/*` handlers;
BL-3382 host; BL-3383 to BL-3387 the tool and resource ports; BL-3388 cut-over, deleting the
SDK-based code; BL-3389 package removal and documents.

## Consequences

- The two packages stay in `Curl.Mcp` until BL-3389, and leave only when that final task proves the
  golden conversations unchanged. Until then root `CLAUDE.md`, `DarkFactory/LaneRules.md` and
  `README.md` keep their package-exception wording, which stays true; BL-3389 rewrites it so the
  only packages left are `MSTest`.
- ADR-0548's other decisions (project split, tools, file-access root, transport, packing,
  publishing) stand.
