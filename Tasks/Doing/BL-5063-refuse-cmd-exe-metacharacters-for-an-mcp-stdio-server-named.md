---
id: BL-5063
title: Refuse cmd.exe metacharacters for an MCP stdio server named with a trailing dot or space after .cmd or .bat
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Extended.Protocol.McpClient.UnitLibrary/Stdio/McpBatchArgumentRefusal.cs, Curl.Extended.Protocol.McpClient.UnitTests/Stdio/McpBatchArgumentRefusalTests.cs]
requirement: none
created: 2026-11-12
completed:
---
# BL-5063 — Refuse cmd.exe metacharacters for an MCP stdio server named with a trailing dot or space after .cmd or .bat

## Goal

When an `mcp+stdio` server's decoded file name is a `.cmd` or `.bat` file followed by trailing dots or spaces, Curl still refuses arguments that are unsafe for cmd.exe.

## Context

The shift-end review of 2026-11-12 found this in the ADR-1248 change (type-design-analyzer, ADR-0710). It is the command injection ADR-1248 exists to prevent.

- `Curl.Extended.Protocol.McpClient.UnitLibrary/Stdio/McpBatchArgumentRefusal.cs:14-15` decides that the server is a batch file with `program.EndsWith(".cmd"/".bat")`.
- The program name is the percent-decoded URL path (`Client/McpStdioCommand.cs`, `FileNameOf`). So `mcp+stdio://localhost/srv.cmd%20?a%26calc` gives the name `srv.cmd ` (with a trailing space).
- Windows path normalisation strips trailing dots and spaces, so `CreateProcess` still runs `srv.cmd` through cmd.exe.
- The check sees no batch extension, so it lets the argument `&calc` through, and cmd.exe runs `calc`.

## Acceptance criteria

- [x] `RefuseUnsafeArguments` treats a program as a batch file when its name ends in `.cmd` or `.bat` (case-insensitive) after trailing `.` and space characters are removed.
- [x] Unit tests in `McpBatchArgumentRefusalTests` show that `srv.cmd `, `srv.bat.` and `srv.CMD. .` are refused when an argument holds `&`.
- [x] `srv.exe` with the same argument is still allowed.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

TrimEnd of dots and spaces before the extension check; quality run: 0 failing members.

## Log

- 2026-11-12: Created.
- 2026-11-12: Backlog -> Doing. Criteria at claim: 3fb92545 58fcdbec 58acf080 3d836d5e.
