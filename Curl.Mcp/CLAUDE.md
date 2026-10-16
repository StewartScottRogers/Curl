# Curl.Mcp

The Model Context Protocol server host (ADR-0548): an executable that serves Curl to AI agents
over standard input and output. It is a naming exception, with `Curl.Console`, `Curl.Api` and
`Libcurl`, because it is not a library. Namespace `Curl.Mcp`.

It contains only attribute wiring and forwarding calls: the generic host, the stdio transport,
`[McpServerToolType]` classes whose methods each forward to one `Curl.Mcp.UnitLibrary` call
(`CurlCommandTool`, the `curl` tool; `CurlTransferTool`, the `transfer` tool over a production
`ManagedCurlEngine` the host creates once, as `Libcurl` does; `CurlVersionTool`, the `version` tool
over that same engine; `CurlUrlTool`, the `parse_url` and `build_url` tools over that engine's URL
handles), the `--ai-help` resource handlers over
`McpAiHelpResourceCatalogue` (`CurlAiHelpResourceHandlers`, ADR-0589), the runner adapters over `Curl.Console` and the engine
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
which tests both this project and `Curl.Mcp.UnitLibrary`; tests that send bytes over a socket
live in `Curl.Mcp.IntegrationTests` (ADR-0421).

It packs as a NuGet MCP server (ADR-0596): `dotnet pack` makes a `.NET` tool of `PackageType` `McpServer`
with `.mcp/server.json`, one self-contained package per runtime; the version in the csproj and in
`server.json` are held equal by `McpPackagingTests`.
