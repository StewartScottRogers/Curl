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
completed:
---
# BL-3377 — Hand-write the newline-delimited stdio transport for the MCP server

## Goal

`Curl.Mcp.Protocol.UnitLibrary/Transport/` reads newline-delimited JSON-RPC messages from a `Stream` and writes them to a `Stream`, safely under concurrent writers, so the server can be driven by any pair of streams.

## Context

- MCP transport and message rules: https://modelcontextprotocol.io/specification (read the protocol revision the golden conversations recorded by BL-3374 show the SDK 2.2.0 server negotiating, and say which revision you read). Rules: messages are UTF-8, one per line, never containing an embedded newline; the server writes nothing but messages to its output stream.
- Types: `IMcpTransport` (`ReadMessageAsync` returning `null` at end of input, `WriteMessageAsync`) and `StdioMcpTransport` over an input and an output `Stream` (the host passes `Console.OpenStandardInput()` and `OpenStandardOutput()`). No BOM; flush after each message; a write lock so concurrent responses never interleave; blank lines skipped; `\r\n` accepted; a final unterminated line at end of input still delivered; a maximum line length, with the number and reason in a doc comment, whose breach becomes an invalid-request message rather than a crash. Uses the JSON-RPC types of BL-3376.
- Tests in `Curl.Mcp.Protocol.UnitTests/Transport/` over `MemoryStream` and in-memory pipe streams, no socket and no process: reads split in the middle of a multibyte character, many messages in one read, cancellation of a pending read, concurrent writers produce whole lines only. No `Thread.Sleep`; inject nothing time-based.

## Acceptance criteria

- [ ] `IMcpTransport` and `StdioMcpTransport` exist under `Curl.Mcp.Protocol.UnitLibrary/Transport/`, one type per file, with XML doc comments.
- [ ] Tests cover the cases in Context, including one where two tasks each write 1,000 responses and the output is 2,000 whole valid lines.
- [ ] End of input returns `null`; cancellation throws `OperationCanceledException`; a UTF-8 BOM is never written.
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Mcp.Protocol.UnitLibrary` reports 0 failing members (100% line, 100% branch, complexity at most 10, CRAP at most 30), measured from the fast tests only.
- [ ] `dotnet build` of every project named in touches is clean with `-warnaserror`, and `dotnet test --filter "TestCategory!=Integration"` passes for the test projects named in touches, with no test deleted, skipped or weakened.

## Notes

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: 82c94dd8 0c3e8c71 886f7b82 bf223748 6d2cec38.
