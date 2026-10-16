# Curl.Mcp

The Model Context Protocol server host (ADR-0548): an executable that serves Curl to AI agents
over standard input and output. It is a naming exception, with `Curl.Console`, `Curl.Api` and
`Libcurl`, because it is not a library. Namespace `Curl.Mcp`.

It contains only attribute wiring and forwarding calls: the generic host, the stdio transport,
`[McpServerToolType]` classes whose methods each forward to one `Curl.Mcp.UnitLibrary` call
(`CurlCommandTool`, the `curl` tool), the runner adapters over `Curl.Console` and the engine
(`InProcessCurlCommandRunner`), and the source-generated JSON context. Tool logic - checking,
capping, turning a result into an error - lives in `Curl.Mcp.UnitLibrary`, never here; a branch
added here is a sign it belongs there. The host makes `CURL_MCP_ROOT` the process's current
directory once at start, so curl's relative paths resolve inside it (ADR-0575). Tools are registered one by one (`WithTools<T>()`), never
by scanning an assembly, so the host stays ahead-of-time compatible.

Standard output is the protocol channel: nothing else may write to it. Every log line goes to
standard error.

This is the only project allowed the two MCP SDK packages, `ModelContextProtocol` and
`Microsoft.Extensions.Hosting` (approved by Stewart on 2026-10-15). No other project may
reference them.

Held to the four quality gates in the root `CLAUDE.md`. Its tests live in `Curl.Mcp.UnitTests`,
which tests both this project and `Curl.Mcp.UnitLibrary`.
