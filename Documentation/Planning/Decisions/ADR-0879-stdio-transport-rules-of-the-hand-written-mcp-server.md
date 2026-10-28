# ADR-0879: The stdio transport rules of the hand-written MCP server

- Status: Accepted
- Date: 2026-10-23
- Task: BL-3377
- Decided by Claude under Stewart's delegation.

## Context

BL-3377 adds `IMcpTransport` and `StdioMcpTransport` to `Curl.Mcp.Protocol.UnitLibrary`
(ADR-0799). The golden conversations of BL-3374 show the server negotiating revisions
2025-06-18 and 2025-11-25; the stdio transport section of revision 2025-11-25 (unchanged in
substance since 2024-11-05) says messages are UTF-8 JSON-RPC, one per line, never holding an
embedded newline, and the server writes nothing to its output that is not a message. It sets no
line length and says nothing about blank lines or `\r\n`.

## Decision

1. Reading is lenient: blank and whitespace-only lines are skipped, a line ending `\r\n` is read
   as is (JSON whitespace includes `\r`), and a final line left unterminated at the end of the
   input is still delivered. `ReadMessageAsync` returns `null` at the end of the input, every
   time it is called after that.
2. A line is at most 4 MiB (`StdioMcpTransport.MaximumLineLength`, terminator included). The
   server's requests carry a curl command line and tool arguments, far below it, so the limit only
   bounds the memory one line takes. A longer line is discarded up to its newline and read as a
   JSON-RPC invalid request (-32600) with no id, which the server answers; reading goes on with
   the next line.
3. Writing appends `\n`, never a byte order mark, and flushes after each message, all under one
   `SemaphoreSlim` so concurrent writers never interleave. A message holding a newline is
   refused with `ArgumentException` rather than written as two lines.
4. The transport does not own its streams: the host opens and closes standard input and output.

## Consequences

The server can be driven by any pair of streams, and its tests use in-memory streams only. The
final parity task (BL-3374's plan) compares this behaviour with the golden conversations.
