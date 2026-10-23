---
id: BL-3171
title: A group of small conformance fixes
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3183, BL-3184, BL-3185, BL-3186, BL-3187, BL-3188]
touches: [Curl.Cli.UnitLibrary/CommandLineOptions.cs, Curl.Cli.UnitLibrary/CommandLineOptionTable.cs, Curl.Cli.UnitLibrary/CommandLineRefusal.cs, Curl.Cli.UnitLibrary/ConsolePasswordPrompt.cs, Curl.Cli.UnitTests/CommandLineBlankArgumentTests.cs, Curl.Cli.UnitTests/CommandLineRefusalEmptyCommandLineTests.cs, Curl.Cli.UnitTests/ConsolePasswordPromptTests.cs, Curl.Protocol.Http.UnitLibrary/HttpResponseHeadReader.cs, Curl.Protocol.Http.UnitTests/HttpProtocolHandlerBare100Tests.cs, Curl.Output.UnitLibrary/WriteOutTransferInfo.cs, Curl.Output.UnitTests/WriteOutSpeedDownloadTests.cs, Curl.Mcp.UnitLibrary/McpTransferTool.cs, Curl.Mcp.UnitLibrary/McpTransferRequestCheck.cs, Curl.Mcp.UnitTests/McpTransferToolShapeTests.cs, Documentation/Planning/Decisions/ADR-0762-bare-curl-names-ai-help-in-its-try-help-line.md, Documentation/Planning/Decisions/README.md, Documentation/Wiki/Glossary.md]
requirement: none
created: 2026-10-22
completed:
---
# BL-3171 — A group of small conformance fixes

## Goal

Six small conformance differences from upstream curl are fixed, each as its own sub-criterion with its own test.

## Context

Filed on Stewart's behalf on 2026-10-22 as one task, one sub-criterion per fix. Measure each against curl 8.22.0 (the target) with `Record-CurlExchange.ps1` before pinning, on Windows, and on Linux and macOS through CI or the gap-platforms run where the answer can differ. Work the items in order and note each measurement in Notes.

1. Blank arguments. `--proxy-key ''` and other options given an empty argument must be accepted or rejected as upstream does. Measure which options upstream rejects when given `''` (every option that takes an argument, run once with `''`); the parsing is in `Curl.Cli.UnitLibrary/CommandLineOptions.cs` and `CommandLineOptionTable.cs`.
2. A bare 100 status response. `-i` must show a bare `HTTP/1.1 100 Continue` response (no final response, or a 100 followed by the final one) as upstream shows it. The header reader is `Curl.Protocol.Http.UnitLibrary/HttpResponseHeadReader.cs` (`IsInformational` in `HttpStatusLine.cs`).
3. No arguments. `curl` with no arguments must print upstream's exact message. This REVERSES BL-3145 (Done 2026-10-22), which added `'curl --ai-help'` to that line in `Curl.Cli.UnitLibrary/CommandLineRefusal.cs` (`TryHelpLineNamingAiHelp`, `EmptyCommandLine`) and recorded it in `Documentation/Planning/Decisions/ADR-0762-bare-curl-names-ai-help-in-its-try-help-line.md`. Undo that change; keep the `--ai-help` hint only in `curl --help`'s footer (it is already there: `Use "--ai-help" for Markdown help written for AI agents (Curl only)`); mark ADR-0762 superseded with Stewart's 2026-10-22 decision (and update `Documentation/Planning/Decisions/README.md` and the Glossary entry that mentions it). Stewart called the upstream message "upstream's exact two-line message"; the 2026-10-22 measurement on Windows with curl 8.21.0 Schannel is ONE line, `curl: try 'curl --help' for more information`, exit 2, and the preview Curl prints `curl: try 'curl --help', 'curl --manual' or 'curl --ai-help' for more information`. Measure 8.22.0 on Windows, Linux and macOS and pin each platform's exact bytes in its own `[OSCondition]` test. This task owns the try-help constant: the wrapping task BL-3169 notes that the Windows build has no `--manual`, so settle `TryHelpLine` per platform here, and a separate try-help follow-up task, if that task files one, depends on this one.
4. `%{speed_download}` uses upstream's formula, bytes over total time (`trspeed`), in `Curl.Output.UnitLibrary/WriteOutTransferInfo.cs` / `TransferWriteOutVariables.cs`; measure where Curl differs (for example the divisor or rounding) and fix it.
5. The `-u` password prompt. `-u user` with no password must prompt with upstream's text and newline: `Curl.Cli.UnitLibrary/ConsolePasswordPrompt.cs` and the prompt in `CommandLineOptions.cs` (`Enter host password for user '<user>':`); measure the exact bytes.
6. `Curl.Mcp`'s transfer tool. Wrong argument shapes (for example `headers` as an object, `body` as a string) must be rejected with an error naming the field and the expected shape, not `An error occurred invoking 'transfer'`. See `Curl.Mcp.UnitLibrary/McpTransferTool.cs`, `McpTransferRequest.cs`, `McpTransferRequestCheck.cs`, `McpTransferHeader.cs` and `McpTransferBody.cs`.

## Acceptance criteria

- [ ] Item 1: a test in `Curl.Cli.UnitTests/CommandLineBlankArgumentTests.cs` runs every option that takes an argument with `''` and matches curl 8.22.0's measured accept/reject list (the list is quoted in Notes); `--proxy-key ''` has its own case.
- [ ] Item 2: a test in `Curl.Protocol.Http.UnitTests/HttpProtocolHandlerBare100Tests.cs` shows `-i` output for a bare 100 response, and for a 100 followed by a 200, equal to the bytes curl 8.22.0 prints.
- [ ] Item 3: tests in `Curl.Cli.UnitTests/CommandLineRefusalEmptyCommandLineTests.cs` pin the no-argument standard error and exit 2 per platform (Windows, and non-Windows via `[OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]`) to curl 8.22.0's measured bytes, with no `--ai-help` in that line; `curl --help`'s footer still contains the `--ai-help` hint; `TryHelpLineNamingAiHelp` is gone; ADR-0762 is marked superseded citing Stewart's 2026-10-22 decision, the ADR index and Glossary agree, and `--ai-help` text still describes the options correctly.
- [ ] Item 4: a test in `Curl.Output.UnitTests/WriteOutSpeedDownloadTests.cs` pins `%{speed_download}` for sizes and total times (including a sub-second total, zero total time and a large size) to the values curl 8.22.0 prints.
- [ ] Item 5: a test in `Curl.Cli.UnitTests/ConsolePasswordPromptTests.cs` (extend) pins the prompt text and the newline written after the entry to curl 8.22.0's bytes, for the Windows and non-Windows terminals where they differ.
- [ ] Item 6: tests in `Curl.Mcp.UnitTests/McpTransferToolShapeTests.cs` send `headers` as an object and `body` as a string (and at least one more wrong shape each for `url`, `method` and `options`) and expect an error text naming the field and its expected shape; none yields `An error occurred invoking 'transfer'`, and the right shapes still work.
- [ ] `dotnet build Curl.Cli.UnitLibrary Curl.Output.UnitLibrary Curl.Protocol.Http.UnitLibrary Curl.Mcp.UnitLibrary -warnaserror` is clean and the fast tests of the matching `.UnitTests` projects pass.

## Notes

- 2026-10-22 (lane 6): Split for size before any code: six fixes across four libraries (Curl.Cli, Curl.Protocol.Http, Curl.Output, Curl.Mcp), each needing a curl 8.22.0 measurement and its own Measure-CodeQuality.ps1 run (30 to 45 minutes each), do not fit one run. Filed one subtask per item: BL-3183 (item 1), BL-3184 (item 2), BL-3185 (item 3), BL-3186 (item 4), BL-3187 (item 5), BL-3188 (item 6). Each carries its item's criterion verbatim. Prerequisite pass: every file named in Context exists; item 6 also needs `Curl.Mcp/CurlTransferTool.cs` (the SDK binds arguments there before the UnitLibrary sees them), so BL-3188 names it. curl 8.22.0 Schannel is on PATH here. When all six are Done, the next claim ticks this task's boxes from their results and runs the combined build and fast tests.

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: 1564fed0 4b320894 922672d5 458d0126 4890ae0e 47752f91 992fc681.
- 2026-10-22: Doing -> Backlog. Split for size into BL-3183..BL-3188, one subtask per item; waits on them
