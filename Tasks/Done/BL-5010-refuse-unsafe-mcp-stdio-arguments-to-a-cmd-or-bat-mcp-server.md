---
id: BL-5010
title: Refuse unsafe mcp+stdio arguments to a .cmd or .bat MCP server with exit 7
priority: High
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Extended.Protocol.McpClient.UnitLibrary/Stdio, Curl.Extended.Protocol.McpClient.UnitLibrary/Client/McpStdioClientSession.cs, Curl.Extended.Protocol.McpClient.UnitTests/Stdio, Documentation/Conformance/Extended/McpClient-Stdio.md]
requirement: none
created: 2026-11-12
completed: 2026-11-12
---
# BL-5010 — Refuse unsafe mcp+stdio arguments to a .cmd or .bat MCP server with exit 7

## Goal

`mcp+stdio://` refuses, with exit 7 and before the server starts, any argument that cmd.exe would treat as a command when the MCP server program is a `.cmd` or `.bat` file.

## Context

From the outside fitness review of 2026-11-12 (commit 2d2ad7f8b, `preview` curl-win-x64.zip), finding EXT-029 (High, Confirmed, re-reproduced by the orchestrator). Leaks credentials / weakens security: yes. Windows only.

**Repro.**
1. Create `C:\t\mcpsrv\srv.cmd` with three lines: `@echo off`, `>>"%~dp0mcpargs.log" echo [%*]`, `exit /b 0`.
2. `$env:PATH="C:\t\mcpsrv;$env:PATH"; mkdir C:\t\run; cd C:\t\run`
3. Run each of:
   - `curl.exe -sS 'mcp+stdio://localhost/srv.cmd?a%26echo.%3Epwn_mcp1.txt'`
   - `curl.exe -sS 'mcp+stdio://localhost/srv.cmd?a%22%26echo.%3Epwn_mcp2.txt%26%22'`
   - `curl.exe -sS 'mcp+stdio://localhost/srv.cmd?%25PATH%25'`
   - `curl.exe -sS 'mcp+stdio://localhost/C:/t/mcpsrv/srv.cmd?a%26echo.%3Epwn_mcp3.txt'`

**Expected.** `Documentation/Conformance/Extended/McpClient-Stdio.md` and the mcp text of `--extended-help` say each `&`-separated, percent-decoded query part is one argument to the program. The port's own rule for batch files is ADR-1248 (`Documentation/Conformance/Extended/Plugin.md`, Plugin:ADR1142-Discovery-3): any argument holding `& | < > ^ " %` or a control character is refused with exit 7 before the program starts. So each argument must arrive verbatim, or the start must be refused with exit 7.

**Actual.** `pwn_mcp1.txt`, `pwn_mcp2.txt` and `pwn_mcp3.txt` are created in the working directory, so cmd.exe ran the injected command. curl then prints `curl: (8) The MCP server exited before it answered request 1.` and exits 8. The `%PATH%` case passes the expanded PATH value instead of the literal `%PATH%`. `npx.cmd`, commonly used to launch MCP servers, is such a batch file, so any script that puts untrusted text into an `mcp+stdio` query can be made to run commands. Not reachable through `-L`, because redirects to `mcp+stdio` are refused.

**Where to start.**
- `Curl.Extended.Protocol.McpClient.UnitLibrary/Stdio/McpServerProcessStarter.cs`, `Start()`, uses `new ProcessStartInfo(command)` with `ArgumentList.Add(argument)` and no batch-file check. In production `services.ProcessStarter` is null, so this starter is used (`Client/McpClientExtension.cs:24`).
- `Client/McpStdioCommand.cs` percent-decodes the query parts, so `%26` becomes `&`.
- `Client/McpStdioClientSession.cs` line 23 already turns a start failure into `CurlExitCode.CouldntConnect` (7) with `Failed to start the MCP server {command.FileName}: {failure.Message}`; the refusal should surface through that path.
- The existing check is `SystemHelperProcessStarter.RefuseUnsafeBatchArgument(string program, string url)` in `Curl.Extended.Protocol.Plugin.UnitLibrary/SystemHelperProcessStarter.cs` (internal, throws `IOException`, refuses `& | < > ^ "` and `%` and control characters for `.cmd`/`.bat`).

**Isolation constraint.** Extension isolation (`VerifyExtensionIsolation` in `Directory.Build.props`; CLAUDE.md "Extensions") forbids `Curl.Extended.Protocol.McpClient` referencing another extension such as `Curl.Extended.Protocol.Plugin`. So the check cannot be called across; the run decides whether to reproduce it inside the McpClient library (for example a small internal `Stdio` type) or to move the shared rule to an allowed shared layer (`Curl.Extension.Abstractions.UnitLibrary` or `Curl.Protocol.Abstractions.UnitLibrary`) and have both Plugin and McpClient use it. If it moves, add the touched files to this task's `touches`. Either way ADR-1248's character set and the Plugin tests in `Curl.Extended.Protocol.Plugin.UnitTests/BatchHelperArgumentTests.cs` must stay green.

## Acceptance criteria

- [x] Unit tests in `Curl.Extended.Protocol.McpClient.UnitTests/Stdio/McpBatchArgumentRefusalTests.cs` (new) prove that for a program ending in `.cmd` or `.bat` (any letter case), an argument holding each of `&`, `|`, `<`, `>`, `^`, `"`, `%` and a control character is refused before any process starts, and that the refusal surfaces as `CurlExitCode.CouldntConnect` (exit 7) with message `Failed to start the MCP server srv.cmd: ...`.
- [x] A test covers the repro arguments: `a&echo.>pwn_mcp1.txt`, `a"&echo.>pwn_mcp2.txt&"` and `%PATH%` are all refused for `srv.cmd`, including when the program is given as a path (`C:/t/mcpsrv/srv.cmd`).
- [x] Regression: arguments `plain` and `args` to `srv.cmd` are passed unchanged (the stand-in starter receives `["plain", "args"]`), and an unsafe argument to a non-batch program (for example `srv.exe` or `srv`) is passed unchanged, as before.
- [x] The isolation rule holds: `McpClient` has no reference to `Curl.Extended.Protocol.Plugin.*`, and `dotnet build Curl.Extended.Protocol.McpClient.UnitLibrary -warnaserror` is clean (the `VerifyExtensionIsolation` target passes).
- [x] `Documentation/Conformance/Extended/McpClient-Stdio.md` gains a MUST row (ID following the existing `McpClient:` scheme, naming ADR-1248) stating the refusal, exit 7 and message, each row's "Covered by" naming a test from this task; the mcp text of `--extended-help` states it if it describes stdio arguments.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). The unit tests need no real process: use the in-memory starter stand-ins already in `Curl.Extended.Protocol.McpClient.UnitTests/Stdio`.
- McpServerProcessStarter is excluded from coverage (ADR-1031, a thin Process adapter), so put the refusal logic in a separate, fully covered type that the starter calls, not inline in `Start()`.
- Review acceptance check, for a Windows hand check: none of the three injection commands creates a `pwn_mcp*.txt`; each exits 7 with `curl: (7) Failed to start the MCP server srv.cmd: ...` and `mcpargs.log` is not written; `mcp+stdio://localhost/srv.cmd?plain&args` still starts the server and logs `[plain args]`.

## Log

- 2026-11-12: Created.
- 2026-11-12: Backlog -> Doing. Criteria at claim: ff5d2c89 1a5fe848 a7c84aa8 8482c63f 883bc44a a0b49a29.
- 2026-11-12: Done. Refusal lives in new internal Stdio/McpBatchArgumentRefusal.cs (ADR-1248 character set, duplicated not shared because of extension isolation), called from McpStdioClientSession.Start before the starter so every starter is covered and the existing exit 7 path reports it. Quality run: 0 failing members. Build and fast tests green; platform-neutral tests.
- 2026-11-12: Doing -> Done. Unsafe mcp+stdio arguments to .cmd/.bat refused with exit 7
