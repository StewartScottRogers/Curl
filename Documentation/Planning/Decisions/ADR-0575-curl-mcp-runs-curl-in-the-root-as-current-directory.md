# ADR-0575: `Curl.Mcp` runs the `curl` tool with the root as the process's current directory

- Status: Accepted
- Date: 2026-10-15
- Decided by Claude under Stewart's delegation (BL-2382)

## Context

The `curl` tool's logic (ADR-0569) checks every path against `CURL_MCP_ROOT`, resolving relative
paths against the root, and hands the runner the root as its working directory. `Curl.Mcp` adapts
`Curl.Console.InProcessCurl` to `ICurlCommandRunner`, but `InProcessCurl` takes no working
directory and no cancellation token: curl resolves relative paths against the process's current
directory through `PhysicalFileSystem`. If that directory were not the root, `-o out.txt` would
pass the check and be written outside it. The tool's result must also reach the MCP client as a
tool error when curl fails, and nothing may serialize by reflection.

## Decision

- `CurlMcpHost.Build` reads `CURL_MCP_ROOT` and `CURL_MCP_MAX_RESPONSE_BYTES` once, so a bad value
  stops the server before it serves, and when a root is set makes it the process's current
  directory, once, before the host is built. One server process serves one root, so this is the
  directory every run should resolve against; no run changes it afterwards.
- `InProcessCurlCommandRunner` calls the `InProcessCurl` overload that builds the production
  `TcpConnector` (`TcpDialer` with default `TcpSocketOptions`, `SystemDnsResolver`,
  `UdpDatagramConnector`). It refuses, with `InvalidOperationException`, a working directory that
  is not the current directory, so a mismatch can never let relative paths escape the root. The
  request's cancellation token is not passed on, because `InProcessCurl` takes none;
  `McpCurlTool` still reports a cancelled request as cancelled once the run returns.
- `CurlCommandTool.RunCurlAsync` (`[McpServerTool(Name = "curl")]`) forwards to `McpCurlTool` and
  returns a `CallToolResult` whose one text block is the `McpCurlToolResult` as camel-case JSON
  (enums as names) and whose `IsError` is the result's. The JSON comes from the source-generated
  `CurlMcpJsonContext`, which also registers the tool (combined with the SDK's own resolver), so
  the schema of `args` (required string array) and `stdin` (optional string) needs no reflection.
  The generated members are excluded from coverage: the generator, not this project, writes them.
- `Curl.Mcp` references `Curl.Console` for `InProcessCurl`; the reference builds cleanly.

## Consequences

- A test that builds the host with a root changes the test process's current directory and must
  restore it.
- Cancelling a long transfer does not stop it early. A cancellable `InProcessCurl` overload would
  let the adapter pass the token on.
