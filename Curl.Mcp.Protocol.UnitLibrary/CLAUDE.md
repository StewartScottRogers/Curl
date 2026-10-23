# Curl.Mcp.Protocol.UnitLibrary

The protocol core of the hand-written MCP server (decision BL-3373, ADR-0799). Namespace
`Curl.Mcp.Protocol`. `JsonRpc/` (namespace `Curl.Mcp.Protocol.JsonRpc`) holds the JSON-RPC 2.0
message model, parser and response writer (ADR-0800). `Model/` (namespace
`Curl.Mcp.Protocol.Model`) holds `IMcpTool`, `IMcpResourceProvider`, their wire shapes, their
JSON context and `McpServerRegistry`, which the host fills one `Add` per tool or provider
(ADR-0801); the later tasks of the plan add the server logic.

Base class library only: no package, no reference to any other Curl project (a test in
`Curl.Mcp.Protocol.UnitTests` checks this). JSON is source-generated only, never reflection.
Only `Curl.Mcp` and `Curl.Mcp.UnitTests` reference it.

Held to the four quality gates in the root `CLAUDE.md`. Its tests live in
`Curl.Mcp.Protocol.UnitTests`.
