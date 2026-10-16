# Curl.Mcp.UnitLibrary

All of the MCP server's tool logic (ADR-0548): path confinement, result and error plumbing,
and the tool implementations over `Curl.Api.UnitLibrary`'s seam. Namespace `Curl.Mcp`.
Empty of logic until the tool tasks (BL-2377 onwards) fill it.

Base class library only. Only `Curl.Mcp` may take the MCP SDK packages (`ModelContextProtocol`
and `Microsoft.Extensions.Hosting`); this library references no package. Reference
`Curl.Api.UnitLibrary` only; never reference `Curl.Mcp`.

Held to the four quality gates in the root `CLAUDE.md`. Its tests live in `Curl.Mcp.UnitTests`,
which tests both this library and `Curl.Mcp`.
