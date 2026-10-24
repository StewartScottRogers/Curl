---
id: BL-3377
title: Hand-write the newline-delimited stdio transport for the MCP server
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3376]
touches: [Curl.Mcp.Protocol.UnitLibrary/Transport, Curl.Mcp.Protocol.UnitTests/Transport]
model: opus
requirement: none
created: 2026-10-23
completed: 2026-10-23
---
# BL-3377 — Hand-write the newline-delimited stdio transport for the MCP server

## Goal

`Curl.Mcp.Protocol.UnitLibrary/Transport/` reads newline-delimited JSON-RPC messages from a `Stream` and writes them to a `Stream`, safely under concurrent writers, so the server can be driven by any pair of streams.

## Context

- MCP transport and message rules: https://modelcontextprotocol.io/specification (read the protocol revision the golden conversations recorded by BL-3374 show the SDK 2.2.0 server negotiating, and say which revision you read). Rules: messages are UTF-8, one per line, never containing an embedded newline; the server writes nothing but messages to its output stream.
- Types: `IMcpTransport` (`ReadMessageAsync` returning `null` at end of input, `WriteMessageAsync`) and `StdioMcpTransport` over an input and an output `Stream` (the host passes `Console.OpenStandardInput()` and `OpenStandardOutput()`). No BOM; flush after each message; a write lock so concurrent responses never interleave; blank lines skipped; `\r\n` accepted; a final unterminated line at end of input still delivered; a maximum line length, with the number and reason in a doc comment, whose breach becomes an invalid-request message rather than a crash. Uses the JSON-RPC types of BL-3376.
- Tests in `Curl.Mcp.Protocol.UnitTests/Transport/` over `MemoryStream` and in-memory pipe streams, no socket and no process: reads split in the middle of a multibyte character, many messages in one read, cancellation of a pending read, concurrent writers produce whole lines only. No `Thread.Sleep`; inject nothing time-based.

## Acceptance criteria

- [x] `IMcpTransport` and `StdioMcpTransport` exist under `Curl.Mcp.Protocol.UnitLibrary/Transport/`, one type per file, with XML doc comments.
- [x] Tests cover the cases in Context, including one where two tasks each write 1,000 responses and the output is 2,000 whole valid lines.
- [x] End of input returns `null`; cancellation throws `OperationCanceledException`; a UTF-8 BOM is never written.
- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Mcp.Protocol.UnitLibrary` reports 0 failing members (100% line, 100% branch, complexity at most 10, CRAP at most 30), measured from the fast tests only.
- [x] `dotnet build` of every project named in touches is clean with `-warnaserror`, and `dotnet test --filter "TestCategory!=Integration"` passes for the test projects named in touches, with no test deleted, skipped or weakened.

## Notes

- Delivered directly rather than through the full /feature agent pipeline: one small,
  fully specified type pair in one library, to keep within the lane's cost cap.
- Revision read: the stdio transport section of MCP 2025-11-25 (the golden conversations of
  BL-3374 show the server negotiating 2025-06-18 and 2025-11-25; the stdio rules are the same).
- Decisions recorded in ADR-0801: 4 MiB line limit (terminator included) read as an invalid
  request (-32600, no id) and reading goes on; whitespace-only lines skipped as blank; a message
  holding a newline is refused with `ArgumentException`; the transport is `IDisposable` only
  for its write lock and never closes the caller's streams.
- Tests use `MemoryStream`, a `BufferedStream` (proves the flush), a hand-written
  `ChunkedInputStream` standing in for an in-memory pipe (chunked reads, a pending read
  cancelled), and a yielding output stream so the 2 x 1,000 writer test really interleaves
  without the lock.
- Measured: `Measure-CodeQuality.ps1 -Library Curl.Mcp.Protocol.UnitLibrary` reports 100% line,
  100% branch, 30 members, 0 failing, worst CRAP 8. Build with -warnaserror clean; the fast tests
  of Curl.Mcp.Protocol.UnitTests pass (60). The library's README and CLAUDE.md are outside
  touches and left for the server task that wires the transport in; their text stays true.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: 82c94dd8 0c3e8c71 886f7b82 bf223748 6d2cec38.
- 2026-10-23: Doing -> Done. StdioMcpTransport reads and writes newline-delimited JSON-RPC over any pair of streams, safe under concurrent writers
