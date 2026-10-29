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
completed:
---
# BL-4216 — McpClient extension: stdio transport to a local MCP server process

## Goal

`Curl.Extension.McpClient.UnitLibrary` carries MCP messages over the stdio transport to a local server process it starts.

## Context

Third of BL-4159's split. Specification: Model Context Protocol revision 2025-06-18, "Base Protocol > Transports > stdio": the client launches the server as a subprocess, writes newline-delimited JSON-RPC messages (no embedded newlines) to its stdin and reads them from its stdout; stderr is the server's log; the client ends the session by closing stdin and then ending the process. Starting the process goes behind an interface of this library (one `System.Diagnostics.Process` implementation behind a thin seam, so everything else is unit tested with in-memory streams). Builds on BL-4214's `Wire/` messages.

## Acceptance criteria

- [ ] `Stdio/` writes each message as one line on the process's stdin and returns the awaited response read line by line from stdout, skipping notifications and lines that are not the awaited response.
- [ ] Ending the session closes stdin and ends the process; a process that exits before answering fails with a typed failure.
- [ ] Unit tests use in-memory streams (no real process) and name the specification section each checks; any test that starts a real process is an Integration test in `Curl.Extension.McpClient.IntegrationTests`, platform-neutral.
- [ ] The library meets the four quality gates by `Measure-CodeQuality.ps1`; `dotnet build` is clean and the fast tests pass.

## Log

- 2026-10-29: Created.
