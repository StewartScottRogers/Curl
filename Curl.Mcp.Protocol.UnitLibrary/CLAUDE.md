# Curl.Mcp.Protocol.UnitLibrary

The protocol core of the hand-written MCP server (decision BL-3373, ADR-0799). Namespace
`Curl.Mcp.Protocol`. `JsonRpc/` (namespace `Curl.Mcp.Protocol.JsonRpc`) holds the JSON-RPC 2.0
message model, parser and response writer (ADR-0800); the later tasks of the plan add the Model
Context Protocol types and the server logic.

Base class library only: no package, no reference to any other Curl project (a test in
`Curl.Mcp.Protocol.UnitTests` checks this). JSON is source-generated only, never reflection.
Only `Curl.Mcp` and `Curl.Mcp.UnitTests` reference it.

Held to the four quality gates in the root `CLAUDE.md`. Its tests live in
`Curl.Mcp.Protocol.UnitTests`.
