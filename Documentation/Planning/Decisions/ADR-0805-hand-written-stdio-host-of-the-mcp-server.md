# ADR-0805 — The hand-written stdio host of the MCP server

- Status: Accepted
- Date: 2026-10-23
- Task: BL-3382
- Decided by Claude under Stewart's delegation.

## Context

BL-3382 replaces `Host.CreateApplicationBuilder(...).Build().RunAsync()` with `McpStdioHost` and
`McpStandardErrorLog` in `Curl.Mcp`, so the cut-over task can drop `Microsoft.Extensions.Hosting`.
The generic host stopped on the signals its console lifetime registers (SIGINT, SIGQUIT, SIGTERM,
through `PosixSignalRegistration`) and when standard input closed, waited its shutdown timeout
(30 seconds by default since .NET 8) for work in flight, and logged through
`Microsoft.Extensions.Logging` to standard error.

## Decision

- `McpStdioHost` takes every collaborator through its constructor: the server info, a factory that
  builds the method handlers from the `McpConfinementRoot` and `McpResponseCap` it reads, the input
  and output streams, the log, the environment reader, a `TimeProvider` and the stop-signal
  registration. No container and no reflection.
- `RunAsync` reads `CURL_MCP_ROOT` and `CURL_MCP_MAX_RESPONSE_BYTES` before any input, so a bad
  value throws (`ArgumentException`, `McpConfigurationException`) before the server starts, then
  makes the root the current directory (ADR-0575).
- Stop signals are SIGINT, SIGQUIT, SIGTERM and SIGHUP, all through `PosixSignalRegistration`, as the
  generic host's console lifetime did, rather than `Console.CancelKeyPress`: on .NET SIGINT through
  `PosixSignalRegistration` is Ctrl+C on every platform, so one mechanism covers it. The handler sets
  `Cancel`, so the runtime does not end the process at once and requests in flight can finish.
- Once a stop is requested, requests in flight have 30 seconds (`ShutdownTimeout`, the generic host's
  default), measured on the injected `TimeProvider`.
- Exit code 0 on a clean stop (end of input, cancellation or a signal); 1, with a log line, when
  reading the input failed or requests outlived the shutdown timeout.
- `McpStandardErrorLog` writes `yyyy-MM-ddTHH:mm:ss.fffZ message` lines (UTC) to the error writer
  under a lock, flushing each, so concurrent lines never interleave and nothing reaches standard
  output.

## Consequences

The host is fully covered by unit tests over in-memory streams, a manual `TimeProvider` and a
recording signal registration. The cut-over task wires `Program.cs` to
`new McpStdioHost(..., Console.OpenStandardInput(), Console.OpenStandardOutput(), new McpStandardErrorLog(Console.Error, TimeProvider.System), Environment.GetEnvironmentVariable, TimeProvider.System, McpStdioHost.RegisterProcessStopSignals)`.
