# Curl.Mcp.IntegrationTests

The Integration tests of `Curl.Mcp` (ADR-0421): tests that touch something real outside the
process, such as a loopback socket that carries bytes. Every test carries
`[TestCategory("Integration")]`, so the fast test run skips them all. Namespace `Curl.Mcp`.

It holds:

- `CurlTransferToolIntegrationTests`: the `transfer` tool through the host's real composition (the
  production `ManagedCurlEngine`) against a loopback listener the test opens and closes itself (BL-2384).
- `CurlMcpServerIntegrationTests`: the built `Curl.Mcp` started as a child process with
  `CURL_MCP_ROOT` set to a temporary folder, driven over stdio by `McpStdioClient` - `initialize`,
  `tools/list`, `resources/list`, `version`, `curl` against a loopback listener, and `-o` refused
  outside the root and written inside it (BL-2390).
- `CurlMcpPackIntegrationTests`: `dotnet pack Curl.Mcp -c Release` into a temporary folder, and the
  package holds `.mcp/server.json` and the McpServer package type (BL-2390).

`McpStdioClient` is a hand-written newline-delimited JSON-RPC client (`System.Text.Json`; the SDK
stays in `Curl.Mcp`, ADR-0548). It runs `Curl.Mcp.dll` from this project's output through
`dotnet exec` with this project's runtime configuration and dependency file, which carry every
assembly the server needs. `LoopbackHttpResponder` answers one request on a loopback listener.
