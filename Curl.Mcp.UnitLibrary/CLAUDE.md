# Curl.Mcp.UnitLibrary

All of the MCP server's tool logic (ADR-0548): path confinement, result and error plumbing,
and the tool implementations over `Curl.Api.UnitLibrary`'s seam. Namespace `Curl.Mcp`.
It holds the shared result plumbing (ADR-0555): `McpToolFailure` for tool errors,
`McpReturnedBytes` for text-or-base64 output cut to `McpResponseCap`, and `McpToolRunner`,
which passes the request's cancellation token through. `McpConfinementRoot` (ADR-0557) checks
that a path, with `..` and symbolic links resolved, lies inside `CURL_MCP_ROOT`, answering with
an `McpPathCheck`. `McpOptionPathTable` (ADR-0559) says how every curl option uses a local path
(`McpOptionPathUse`); an option it does not name is refused. `McpArgumentChecker` (ADR-0564)
checks a whole curl argument array, paths embedded in values, `file:` URLs and `-K` files,
answering with an `McpArgumentCheck`. The later tasks of `Documentation/Planning/Curl-Mcp.md` add the rest.

Base class library only. Only `Curl.Mcp` may take the MCP SDK packages (`ModelContextProtocol`
and `Microsoft.Extensions.Hosting`); this library references no package. Reference
`Curl.Api.UnitLibrary`, and `Curl.Cli.UnitLibrary` for its option table's rows (ADR-0564);
never reference `Curl.Mcp`.

Held to the four quality gates in the root `CLAUDE.md`. Its tests live in `Curl.Mcp.UnitTests`,
which tests both this library and `Curl.Mcp`.
