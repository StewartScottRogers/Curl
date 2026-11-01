# Curl.Mcp.IntegrationTests

The Integration tests of `Curl.Mcp` (ADR-0421): tests that touch something real outside the
process, such as a loopback socket that carries bytes. Every test carries
`[TestCategory("Integration")]`, so the fast test run skips them all. Namespace `Curl.Mcp`.

It holds:

- `CurlTransferToolIntegrationTests`: the `transfer` tool from `CurlMcpHost.CreateRegistry` (the
  production `ManagedCurlEngine`) against a loopback listener the test opens and closes itself (BL-2384).
- `CurlMcpServerIntegrationTests`: the built `Curl.Mcp` started as a child process with
  `CURL_MCP_ROOT` set to a temporary folder, driven over stdio by `McpStdioClient` - `initialize`,
  `tools/list`, `resources/list`, `version`, `curl` against a loopback listener, and `-o` refused
  outside the root and written inside it (BL-2390).
- `CurlMcpPackIntegrationTests`: `dotnet pack Curl.Mcp`, in the configuration the test assembly was
  built in, into a temporary folder; the tool package holds `.mcp/server.json` and the McpServer package
  type (BL-2390, BL-2561). `Curl.Mcp.csproj` sets `PublishAot`, so the pack makes only the tool package;
  the test passes no `-p:PublishAot=true`, a global property that recompiled every referenced project
  and took minutes on a CI runner (BL-3512).
- `CurlMcpNativeAotIntegrationTests`: `dotnet publish Curl.Mcp -r <this machine's runtime>
  -p:PublishAot=true` once for the class, then the native binary over stdio - `initialize`,
  `tools/list` and a `transfer` call with one request header against a loopback listener (BL-2486,
  ADR-0621).

Both tests that run the SDK pass `--disable-build-servers`. Otherwise the MSBuild nodes and compiler
server the child starts inherit its redirected output and hold it open for about 15 minutes (BL-2561).

- `CurlMcpGoldenConversationIntegrationTests`: replays the 28 recorded conversations in `Golden/` (recorded from the SDK-based server, `CURL_MCP_RECORD_GOLDEN=1` re-records; `tools-call-transfer-missing-url` holds the hand-written server's deliberate answer, BL-3384) against the built server (BL-3374).

`McpStdioClient` is a hand-written newline-delimited JSON-RPC client (`System.Text.Json`; the SDK
stays in `Curl.Mcp`, ADR-0548). It runs `Curl.Mcp.dll` from this project's output through
`dotnet exec` with this project's runtime configuration and dependency file, which carry every
assembly the server needs, or, through `StartNative`, a published native binary directly. `LoopbackHttpResponder` answers one request on a loopback listener.
