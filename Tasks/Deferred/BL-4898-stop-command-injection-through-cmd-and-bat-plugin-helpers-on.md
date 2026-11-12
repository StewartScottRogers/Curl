---
id: BL-4898
title: Stop command injection through .cmd and .bat plugin helpers on Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.Plugin.UnitLibrary/SystemHelperProcessStarter.cs, Curl.Extended.Protocol.Plugin.UnitTests/BatchHelperArgumentTests.cs, Documentation/Conformance/Extended/Plugin.md]
requirement: none
created: 2026-11-11
completed:
---
# BL-4898 — Stop command injection through .cmd and .bat plugin helpers on Windows

## Goal

A URL handed to a `curl-proto-<scheme>.cmd` or `.bat` helper can never run another command: the helper gets exactly one argument byte-equal to the URL, or the transfer is refused with a clear error.

## Context

From the outside fitness review of 2026-11-11 (commit 6ce7cfcf, the `preview` `curl-win-x64.zip`, Windows 11 x64; REF = curl 8.22.0 Schannel), finding EXT-001 (Critical, confirmed: reproduced at least twice).

- Repro: Put `curl-proto-demo.cmd` first on PATH (it reads a line, echoes `get`, a blank line, reads a line, echoes `ok` and `body`). In an empty directory run `curl.exe -sS "demo://host/a&echo.>pwn1.txt"`, `curl.exe -sS "demo://host/a\"&echo.>pwn2.txt&\""` and `curl.exe -sS "demo://host/%PATH%"`.
- Expected: Plugin.md Invocation-1: "The program runs with the URL as typed as its one argument"; Discovery-3 supports `.BAT;.CMD` through PATHEXT. No other command runs.
- Actual: `pwn1.txt` and `pwn2.txt` are created: cmd.exe runs `echo.>...` as a separate command. Case 3 passes the expanded PATH to the helper split into about 60 arguments. This is the "BatBadBut" class (CVE-2024-24576): anyone who can supply a URL can run commands on a machine with a batch-file helper.
- Where to start: `SystemHelperProcessStarter.Start()`: `new ProcessStartInfo(program)` plus `ArgumentList.Add(url)`; .NET launches `.cmd`/`.bat` through `cmd.exe /c` without escaping `& | < > ^ " %`. Options: refuse batch helpers, launch them through an explicit `cmd.exe /d /s /c` with a fully cmd-escaped argument (and `%` neutralised), or refuse URLs holding cmd metacharacters for batch helpers. Record the choice in an ADR and update Plugin.md Discovery-3.

## Acceptance criteria

- [ ] None of the three repro commands creates a `pwn*.txt` file; the helper either receives exactly one argument byte-equal to the URL (with `%PATH%` literal) or the transfer is refused with a clear error and a non-zero exit.
- [ ] A regression test in `Curl.Extended.Protocol.Plugin.UnitTests` covers `&`, `"` and `%` in a URL bound for a batch helper.
- [ ] `Documentation/Conformance/Extended/Plugin.md` Discovery-3 states how batch helpers are handled.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). Measure real curl (the platform build) before pinning any output text, and add a focused unit test in the fixed library's own `.UnitTests` project.

## Log

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing. Criteria at claim: 8f505c9f ff2b8363 fab8595d a0b49a29.
- 2026-11-11: Doing -> Backlog. Lane 4 could not integrate: build failed, and still build failed after a repair run after rebasing onto the other lanes' work. The work is on branch factory/BL-4898-lane-4-20261111-082724; start with git cherry-pick --no-commit factory/BL-4898-lane-4-20261111-082724 and fix it.
- 2026-11-11: Backlog -> Deferred. Split into BL-4977 (refuse cmd metacharacters for batch helpers, with tests) and BL-4971 (Plugin.md and ADR)
