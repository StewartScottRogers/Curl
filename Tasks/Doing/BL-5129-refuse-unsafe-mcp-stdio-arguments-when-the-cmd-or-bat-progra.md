---
id: BL-5129
title: Refuse unsafe mcp+stdio arguments when the .cmd or .bat program name ends in whitespace or a control character (EXT-044, bypass of BL-5010)
priority: High
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Extended.Protocol.McpClient.UnitLibrary/Stdio/McpBatchArgumentRefusal.cs, Curl.Extended.Protocol.McpClient.UnitTests/Stdio/McpBatchArgumentRefusalTests.cs, Documentation/Conformance/Extended/McpClient-Stdio.md]
requirement: none
created: 2026-11-13
completed:
---
# BL-5129 — Refuse unsafe mcp+stdio arguments when the .cmd or .bat program name ends in whitespace or a control character (EXT-044, bypass of BL-5010)

## Goal

`mcp+stdio://` refuses, with exit 7 and before the server starts, an unsafe argument to a `.cmd` or `.bat` program even when the program name in the URL ends in a tab, vertical tab, form feed, no-break space or any other whitespace or control character.

## Context

From the outside fitness review of 2026-11-13 (commit 56cb97ede, `preview` build), finding EXT-044 (High, Confirmed, re-reproduced by the orchestrator). Arbitrary command execution from a URL on Windows. This is a bypass of the fix for EXT-029 (BL-5010, ADR-1248).

**Repro (PowerShell; the PATH lookup does not reproduce from Git Bash).**
1. Create a folder `srvdir` containing `srv.cmd` (CRLF line endings):
   ```
   @echo off
   >>"%~dp0mcpargs.log" echo [%*]
   exit /b 0
   ```
2. `$env:PATH = "<full path of srvdir>;$env:PATH"`; then `cd` into an empty folder `run`.
3. Control (refused): `curl.exe -sS 'mcp+stdio://localhost/srv.cmd?a%26echo.%3ENOWS.txt'` gives exit 7, no file.
4. Bypass: `curl.exe -sS 'mcp+stdio://localhost/srv.cmd%09?a%26echo.%3EINJECTED.txt'`. Also with `%0B` (vertical tab), `%0C` (form feed) and `%C2%A0` (NBSP) in place of `%09`.

**Expected.** `Documentation/Conformance/Extended/McpClient-Stdio.md` row `McpClient:Stdio-BatchArguments-1` (MUST), ADR-1248: when the server program is a `.cmd`/`.bat` file, an argument holding `& | < > ^ " %` or a control character is refused before the server starts, with exit 7.

**Actual.** `INJECTED.txt` is created in `run` (cmd.exe ran `echo.>INJECTED.txt`); `mcpargs.log` shows only `[a]`; curl prints `curl: (8) The MCP server exited before it answered request 1.` and exits 8.

**Where to start.**
- `Curl.Extended.Protocol.McpClient.UnitLibrary/Stdio/McpBatchArgumentRefusal.cs`, `RefuseUnsafeArguments`: `var name = program.TrimEnd('.', ' ');` (line 14), then `name.EndsWith(".cmd"/".bat")`. A trailing tab, VT, FF or NBSP makes `EndsWith` false, so the whole check is skipped, yet Windows still runs the `.cmd` through cmd.exe.
- The program name comes straight from the URL (`Client/McpStdioCommand.From`, `Uri.UnescapeDataString(url.AbsolutePath)`).
- Fix: strip all trailing whitespace and control characters (`char.IsWhiteSpace`, `char.IsControl`) and dots before classifying, or refuse any program name holding whitespace or control characters when it could resolve to a batch file.

**Not bypasses (checked by the reviewer).** `::$DATA` suffix (exit 7), `.CmD` (caught), `srv.cmd.` and dot/space mixes (caught), extension-less `srv` (not resolved, exit 7). 8.3 short names were not tested; the run should consider them (a program name that is an 8.3 alias of a `.cmd` file) and say in the ADR or the spec row what it does about them.

**Isolation.** Extension isolation forbids referencing `Curl.Extended.Protocol.Plugin`; the rule stays duplicated inside McpClient as BL-5010 left it. Check whether `Curl.Extended.Protocol.Plugin.UnitLibrary/SystemHelperProcessStarter.cs` (`RefuseUnsafeBatchArgument`) has the same trailing-whitespace gap; if so, file it as a follow-up task rather than widening this one.

## Acceptance criteria

- [ ] New tests in `Curl.Extended.Protocol.McpClient.UnitTests/Stdio/McpBatchArgumentRefusalTests.cs` prove that for programs `srv.cmd` followed by each of a tab, vertical tab, form feed, NBSP (U+00A0) and a mix such as `.cmd` + space + tab, and likewise for `.bat`, an argument `a&echo.>INJECTED.txt` is refused before any process starts and surfaces as `CurlExitCode.CouldntConnect` (exit 7) with `Failed to start the MCP server ...`.
- [ ] A regression test proves `srv.cmd` with `plain` and `args` still starts (the stand-in starter receives `["plain", "args"]`) and that an unsafe argument to a non-batch program (`srv.exe`, `srv`) is passed unchanged, as before.
- [ ] The tests are platform-neutral (no process is started, no Windows path), and the existing BL-5010 tests stay green.
- [ ] `Documentation/Conformance/Extended/McpClient-Stdio.md` row `McpClient:Stdio-BatchArguments-1` (or a new MUST row beside it) states that trailing whitespace and control characters on the program name do not hide a batch file, with "Covered by" naming a test from this task.
- [ ] `dotnet build Curl.Extended.Protocol.McpClient.UnitLibrary -warnaserror` is clean, the `VerifyExtensionIsolation` target passes, `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers in the review were Python; build any stand-in in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python. The unit tests need no real process: use the in-memory starter stand-ins in `Curl.Extended.Protocol.McpClient.UnitTests/Stdio`.
- Review acceptance check, for a Windows hand check: each of the four step-4 URLs exits 7 with `Failed to start the MCP server ...`, creates no file and writes nothing to `mcpargs.log`; `mcp+stdio://localhost/srv.cmd?plain&args` still starts and logs `[plain args]`.

## Log

- 2026-11-13: Created.
- 2026-11-13: Backlog -> Doing. Criteria at claim: 600ff565 4137d9c5 c7fafb89 dd4dc622 82003f08.
