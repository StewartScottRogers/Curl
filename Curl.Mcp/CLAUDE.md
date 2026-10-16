# Curl.Mcp

The Model Context Protocol server host (ADR-0548): an executable that serves Curl to AI agents
over standard input and output. It is a naming exception, with `Curl.Console`, `Curl.Api` and
`Libcurl`, because it is not a library. Namespace `Curl.Mcp`.

It stays thin: the generic host, the stdio transport, and `[McpServerToolType]` classes whose
methods each forward to one `Curl.Mcp.UnitLibrary` call. Tool logic lives in
`Curl.Mcp.UnitLibrary`, never here. Tools are registered one by one (`WithTools<T>()`), never
by scanning an assembly, so the host stays ahead-of-time compatible.

Standard output is the protocol channel: nothing else may write to it. Every log line goes to
standard error.

This is the only project allowed the two MCP SDK packages, `ModelContextProtocol` and
`Microsoft.Extensions.Hosting` (approved by Stewart on 2026-10-15). No other project may
reference them.

Held to the four quality gates in the root `CLAUDE.md`. Its tests live in `Curl.Mcp.UnitTests`,
which tests both this project and `Curl.Mcp.UnitLibrary`.
