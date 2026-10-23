---
id: BL-3382
title: Replace Microsoft.Extensions.Hosting with a hand-written stdio host for Curl.Mcp
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3377, BL-3379]
touches: [Curl.Mcp/McpStdioHost.cs, Curl.Mcp/McpStandardErrorLog.cs, Curl.Mcp.UnitTests/McpStdioHostTests.cs, Curl.Mcp.UnitTests/McpStandardErrorLogTests.cs]
requirement: none
created: 2026-10-23
completed:
---
# BL-3382 — Replace Microsoft.Extensions.Hosting with a hand-written stdio host for Curl.Mcp

## Goal

`Curl.Mcp` has a hand-written `McpStdioHost` that does what `Host.CreateApplicationBuilder(...).Build().RunAsync()` did for the server (start, run until the client closes standard input or the process is told to stop, log to standard error only), ready for the cut-over task to wire the tools into it.

## Context

- Today `Curl.Mcp/CurlMcpHost.cs` builds a generic host: reads `CURL_MCP_ROOT` and `CURL_MCP_MAX_RESPONSE_BYTES` (a bad value stops the server before it starts), sets the current directory to the root (ADR-0575), clears the console logger and logs to standard error at every level, creates one `ManagedCurlEngine` for the process, registers the tools. The host also stops on Ctrl+C and SIGTERM (the generic host's lifetime) and when stdin closes.
- The new `McpStdioHost` takes an `McpServer` (from BL-3379) or the objects to build one, the input and output streams, the error writer and the environment reader, as constructor parameters (constructor injection, no container, no reflection scanning), and `RunAsync(CancellationToken)`: wires cancellation to Ctrl+C (`Console.CancelKeyPress`), `PosixSignalRegistration` for SIGTERM and SIGHUP where supported, and end of input; lets in-flight requests finish or cancel within a bounded time; exits 0 on a clean stop. Nothing but protocol messages is ever written to the output stream.
- `McpStandardErrorLog` is a tiny logger writing timestamped lines to standard error (inject `TimeProvider`), replacing `Microsoft.Extensions.Logging`; it is thread-safe.
- Do not edit `Program.cs` or `CurlMcpHost.cs` here (the cut-over task does, and keeps this task parallel with the tool ports). `Program.cs` keeps its `ExcludeFromCodeCoverage` justification (ADR-0083) only for the entry point; the host itself is fully covered by tests over in-memory streams.
- Tests in `Curl.Mcp.UnitTests`: stops on end of input, stops on cancellation, a bad `CURL_MCP_MAX_RESPONSE_BYTES` fails before reading any input with the existing `McpConfigurationException`, logs go only to the error writer, output carries only protocol lines.

## Acceptance criteria

- [ ] `McpStdioHost` and `McpStandardErrorLog` exist in `Curl.Mcp`, with XML doc comments, and neither references `Microsoft.Extensions.*`.
- [ ] Tests cover the cases in Context; no `Thread.Sleep`, time through `TimeProvider`.
- [ ] `dotnet build Curl.Mcp -warnaserror` is clean and `dotnet publish Curl.Mcp -r win-x64 -p:PublishAot=true` (with vswhere on PATH) adds no new IL or trim warning.
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Mcp,Curl.Mcp.UnitLibrary` reports 0 failing members (100% line, 100% branch, complexity at most 10, CRAP at most 30), measured from the fast tests only.
- [ ] `dotnet build` of every project named in touches is clean with `-warnaserror`, and `dotnet test --filter "TestCategory!=Integration"` passes for the test projects named in touches, with no test deleted, skipped or weakened.

## Notes

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: b69e910b 5ce2cc77 15c7a409 96ffe31b 6d2cec38.
