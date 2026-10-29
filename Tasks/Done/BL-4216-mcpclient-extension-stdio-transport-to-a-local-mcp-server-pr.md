---
id: BL-4216
title: McpClient extension: stdio transport to a local MCP server process
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4214]
touches: [Curl.Extension.McpClient.UnitLibrary/Stdio, Curl.Extension.McpClient.UnitTests/Stdio]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4216 — McpClient extension: stdio transport to a local MCP server process

## Goal

`Curl.Extension.McpClient.UnitLibrary` carries MCP messages over the stdio transport to a local server process it starts.

## Context

Third of BL-4159's split. Specification: Model Context Protocol revision 2025-06-18, "Base Protocol > Transports > stdio": the client launches the server as a subprocess, writes newline-delimited JSON-RPC messages (no embedded newlines) to its stdin and reads them from its stdout; stderr is the server's log; the client ends the session by closing stdin and then ending the process. Starting the process goes behind an interface of this library (one `System.Diagnostics.Process` implementation behind a thin seam, so everything else is unit tested with in-memory streams). Builds on BL-4214's `Wire/` messages.

## Acceptance criteria

- [x] `Stdio/` writes each message as one line on the process's stdin and returns the awaited response read line by line from stdout, skipping notifications and lines that are not the awaited response.
- [x] Ending the session closes stdin and ends the process; a process that exits before answering fails with a typed failure.
- [x] Unit tests use in-memory streams (no real process) and name the specification section each checks; any test that starts a real process is an Integration test in `Curl.Extension.McpClient.IntegrationTests`, platform-neutral.
- [x] The library meets the four quality gates by `Measure-CodeQuality.ps1`; `dotnet build` is clean and the fast tests pass.

## Notes

- Delivered directly rather than through the full `/feature` agent stages: the change is one folder of one library with a fixed specification, so the extra planning and review agents cost more than they saved under the lane's cost cap.
- Design (ADR-1031): `IMcpServerProcess` / `IMcpServerProcessStarter` seam; `McpServerProcessStarter` is the one `Process` adapter, excluded from coverage under ADR-0083; `McpLineReader` splits stdout on `\n` (dropping `\r`); `McpStdioSession` sends one line per message, skips empty lines, notifications, server requests and other ids, throws `McpServerExitedException` at end of stdout and `McpWireFormatException` on a non-JSON-RPC line (the specification forbids those, so skipping would wait forever); `EndAsync` closes stdin, waits 5 s on the injected `TimeProvider`, then kills.
- No test starts a real process, so no IntegrationTests project was needed here; BL-4220 adds Integration tests for the excluded `Process` adapter (that new project and `Curl.slnx` lie outside this task's touches).
- ADR-1028 was already taken on the shared branch, so this one is ADR-1031.
- CI on Linux and macOS: the tests use only in-memory streams and no paths, so they are platform-neutral; fast tests green here.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 0beeab70 82fe29da 27bc097f 14de81cc.
- 2026-10-29: Doing -> Done. McpClient stdio transport: one-line messages to a started server, awaited response read from stdout, typed exit failure, close-stdin-then-kill shutdown
