# Curl.Mcp

The Model Context Protocol server host (ADR-0548): an executable that serves Curl to AI agents
over standard input and output. It is a naming exception, with `Curl.Console`, `Curl.Api` and
`Libcurl`, because it is not a library. Namespace `Curl.Mcp`.

It runs on the hand-written server of `Curl.Mcp.Protocol.UnitLibrary` (BL-3388): `Program` runs the
`McpStdioHost` that `CurlMcpHost.Build` composes. `CurlMcpHost.CreateRegistry` registers each tool and
provider with one explicit line, never by scanning an assembly, so the host stays ahead-of-time
compatible: the `Tools/` classes `ParseUrlMcpTool`, `TransferMcpTool`, `CurlCommandMcpTool`,
`BuildUrlMcpTool` and `VersionMcpTool` (in the order `tools/list` answers), each forwarding to one
`Curl.Mcp.UnitLibrary` call over a production `ManagedCurlEngine` the host creates once, as `Libcurl`
does, and `AiHelpMcpResourceProvider` for the `--ai-help` resources (ADR-0589), including `curl://ai-help/extensions`,
which reads `ExtensionListing.Markdown` in the host's environment, as `curl --ai-help extensions` does (BL-4450). That engine's transfers reach
Curl's extensions through `Curl.Extension.Composition.UnitLibrary` unless `CURL_EXTENSIONS` is `0` (ADR-1071).
`LoggingSetLevelMcpHandler` advertises the `logging` capability the SDK host advertised. The runner
adapter over `Curl.Console` is `InProcessCurlCommandRunner`. Tool logic - checking, capping, turning
a result into an error - lives in `Curl.Mcp.UnitLibrary`, never here; a branch added here is a sign
it belongs there. The host makes `CURL_MCP_ROOT` the process's current directory once at start, so
curl's relative paths resolve inside it (ADR-0575).

Standard output is the protocol channel: nothing else may write to it. Every log line goes to
standard error through `McpStandardErrorLog`.

The server is hand-written (ADR-0799): the protocol core is `Curl.Mcp.Protocol.UnitLibrary`, the tool
adapters are in `Tools/` and the host is `McpStdioHost`. This project references no package.

Held to the four quality gates in the root `CLAUDE.md`. Its tests live in `Curl.Mcp.UnitTests`,
which tests both this project and `Curl.Mcp.UnitLibrary`; tests that send bytes over a socket
live in `Curl.Mcp.IntegrationTests` (ADR-0421).

It packs as a NuGet MCP server (ADR-0596): `dotnet pack` makes a `.NET` tool of `PackageType` `McpServer`
with `.mcp/server.json`, and one native-AOT package per runtime, packed with `-r <rid>` on its own OS: the csproj sets `PublishAot` (ADR-0621, BL-2517); the version in the csproj and in
`server.json` are held equal by `McpPackagingTests`.
