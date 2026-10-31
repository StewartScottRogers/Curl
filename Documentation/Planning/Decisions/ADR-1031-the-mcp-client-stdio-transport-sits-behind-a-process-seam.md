# ADR-1031: The MCP client's stdio transport sits behind a process seam

- Status: Accepted
- Date: 2026-10-29
- Decided by Claude under Stewart's delegation (BL-4216)

## Context

BL-4216 carries the MCP client's messages (ADR-1027) over the stdio transport (Model Context Protocol 2025-06-18,
"Base Protocol > Transports > stdio" and "Lifecycle > Shutdown > stdio"): the client starts the server as a
subprocess, writes newline-delimited JSON-RPC to its stdin, reads its stdout, leaves stderr as the server's log, and
ends the session by closing stdin and then ending the process. Upstream curl has no MCP client, so the extension is
tested against the specification (ADR-0999).

## Decision

- `Curl.Extension.McpClient.UnitLibrary/Stdio` sees the server only through `IMcpServerProcess` (stdin and stdout
  streams, wait for exit, kill) and starts it through `IMcpServerProcessStarter`. The one implementation,
  `McpServerProcessStarter`, wraps `System.Diagnostics.Process` with stdin and stdout redirected, stderr inherited
  (the server's log reaches the user's terminal) and stdin written as UTF-8 without a byte order mark. Every line of
  it drives a real process, so it carries `[ExcludeFromCodeCoverage]` under ADR-0083; BL-4220 adds its Integration
  tests.
- `McpStdioSession.SendMessageAsync` writes a message followed by one `\n` and refuses a message holding `\n` or `\r`.
  `SendRequestAsync` reads stdout line by line (`McpLineReader`, which drops a `\r` before the `\n`) and returns the
  first response to the request's id, skipping empty lines, server notifications, server requests and responses to
  other ids. A line that is not JSON-RPC 2.0 is not skipped: the specification forbids the server from writing
  anything else to stdout, so it throws `McpWireFormatException` rather than waiting forever behind it.
- End of stdout before the response throws `McpServerExitedException`.
- `EndAsync` closes stdin, waits `ExitGracePeriod` (5 seconds, on the injected `TimeProvider`) for the process to
  exit, kills it and its child processes if it has not, and disposes it. The specification's intermediate SIGTERM
  step is folded into the kill: .NET has no portable SIGTERM, and a server that ignored end of input for 5 seconds
  is not shutting down.

## Consequences

Everything above the seam is unit tested with in-memory streams and a hand-written time provider; only the
`Process` adapter needs a real child process.
