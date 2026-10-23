# Curl.Mcp.Protocol.UnitLibrary

The protocol core of the hand-written MCP server (decision BL-3373, ADR-0799), in a library
that uses the base class library only: the JSON-RPC and Model Context Protocol message shapes,
read and written with source-generated JSON, and the server logic over them.

## Status

The project is a scaffold. It holds no code yet; the later tasks of the plan that replaced the
`ModelContextProtocol` and `Microsoft.Extensions.Hosting` packages add the protocol types here,
and [`Curl.Mcp`](../Curl.Mcp) will use them in place of those packages.

## Rules

- Namespace `Curl.Mcp.Protocol`. Base class library only: no package and no reference to any
  other Curl project.
- Source-generated JSON only (AOT-compatible, as every production project is).
- Held to the root `CLAUDE.md` gates: 100% line and branch coverage, cyclomatic complexity at
  most 10 per method, CRAP score at most 30.
- Its tests are in [`Curl.Mcp.Protocol.UnitTests`](../Curl.Mcp.Protocol.UnitTests), which has
  `InternalsVisibleTo`. `Curl.Mcp` and `Curl.Mcp.UnitTests` reference it.
- See [CLAUDE.md](CLAUDE.md) for the rules for changing it.
