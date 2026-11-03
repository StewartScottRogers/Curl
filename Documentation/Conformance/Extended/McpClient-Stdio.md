# McpClient: MCP 2025-06-18 stdio transport and shutdown

Curl's `mcp+stdio://localhost/command?arg1&arg2` client (`Curl.Extended.Protocol.McpClient.UnitLibrary`) measured
against the Model Context Protocol, revision 2025-06-18 (modelcontextprotocol.io/specification/2025-06-18): "Base
Protocol > Transports" (its introduction and "stdio") and "Lifecycle > Shutdown" (stdio) (BL-4483). Only
requirements that bind a client are rows; the server's own (nothing but MCP messages on its stdout, embedded-newline
free answers) are kept by the stand-in in `Curl.Conformance.Extended.UnitLibrary/McpClient/Stdio/`. The harness hands
curl `McpStdioReferenceProcessStarter` (ADR-1184), so every case starts `McpStdioReferenceProcess`, which serves
`McpReferenceToolServer` over in-memory stdin and stdout pipes, logs one line per message to its own stderr and
records the program, the arguments and every line curl wrote to its stdin; no real process starts. Beyond the rows,
`McpStdioCases` pins what the specification leaves to the client: curl ignores the server's stderr, and ends with
exit 8 when the server exits before answering or writes a line that is not JSON. Cases listed in
`Curl.Conformance.Extended.UnitTests/KnownFailures.txt` fail today and name the task that fixes them; none does.

| ID | Level | Requirement | Covered by |
|----|:-----:|-------------|------------|
| McpClient:MCP20250618-Stdio-1 | SHOULD | Clients support stdio whenever possible. | McpStdioCases.List_StartsTheProgramWithItsArgumentsAndWritesEachTool |
| McpClient:MCP20250618-Stdio-2 | MUST | The client launches the MCP server as a subprocess, and reads its JSON-RPC messages from the server's stdout. | McpStdioCases.List_StartsTheProgramWithItsArgumentsAndWritesEachTool |
| McpClient:MCP20250618-Stdio-3 | MUST NOT | Messages are delimited by newlines and contain no embedded newlines. | McpStdioCases.EveryMessage_IsOneJsonRpcLineWithNoEmbeddedNewline |
| McpClient:MCP20250618-Stdio-4 | MUST NOT | The client writes nothing to the server's stdin that is not a valid MCP message. | McpStdioCases.EveryMessage_IsOneJsonRpcLineWithNoEmbeddedNewline |
| McpClient:MCP20250618-Shutdown-1 | SHOULD | The client starts shutdown by closing the server's input stream, then waits for the server to exit. | McpStdioCases.TransferEnds_CurlClosesTheServersStandardInputAndWaitsForItToExit |
| McpClient:MCP20250618-Shutdown-2 | SHOULD | If the server does not exit within a reasonable time, the client terminates it (SIGTERM, then SIGKILL). Curl ends it in one step after a 5-second grace period: .NET's `Process.Kill` has no separate SIGTERM. | McpStdioCases.ServerKeepsRunningAfterItsInputCloses_CurlEndsItAfterTheGracePeriod |
