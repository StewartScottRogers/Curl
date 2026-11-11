---
id: BL-4977
title: Refuse URLs holding cmd metacharacters for .cmd and .bat plugin helpers
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.Plugin.UnitLibrary/SystemHelperProcessStarter.cs, Curl.Extended.Protocol.Plugin.UnitTests/BatchHelperArgumentTests.cs]
requirement: none
created: 2026-11-11
completed:
---
# BL-4977 — Refuse URLs holding cmd metacharacters for .cmd and .bat plugin helpers

## Goal

A URL handed to a `curl-proto-<scheme>.cmd` or `.bat` helper on Windows can never run another command: a URL holding a cmd metacharacter is refused with a clear error and a non-zero exit, and any other URL reaches the helper as exactly one argument.

## Context

Critical finding EXT-001 of the 2026-11-11 outside fitness review ("BatBadBut" class, CVE-2024-24576). `SystemHelperProcessStarter.Start()` does `new ProcessStartInfo(program)` plus `ArgumentList.Add(url)`; .NET launches `.cmd`/`.bat` through `cmd.exe /c` without escaping `& | < > ^ " %`. Repro: `curl.exe -sS "demo://host/a&echo.>pwn1.txt"`, `"demo://host/a\"&echo.>pwn2.txt&\""` and `"demo://host/%PATH%"` with `curl-proto-demo.cmd` first on PATH. Decision (Claude, under Stewart's delegation): for a `.cmd` or `.bat` helper, refuse a URL holding any of `& | < > ^ " %` or a control character; every other helper kind is unchanged. The ADR and the Plugin.md text are BL-4971.

## Acceptance criteria

- [ ] A URL holding `&`, `"` or `%` bound for a `.cmd` or `.bat` helper is refused before any process starts, with a clear error and a non-zero exit code; none of the three repro commands creates a `pwn*.txt` file.
- [ ] A URL with none of the metacharacters still reaches a batch helper as exactly one argument byte-equal to the URL.
- [ ] A regression test in `Curl.Extended.Protocol.Plugin.UnitTests/BatchHelperArgumentTests.cs` covers `&`, `"` and `%`, and one passing the clean URL; a non-batch helper is unaffected.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass; the library keeps its 100% coverage gates.

## Notes

Split from BL-4898 (over its cost cap). Mock helpers: PowerShell or a C# file-based app, never Python. Tests are platform-neutral: decide on the helper's file extension, not on the OS.

## Log

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing. Criteria at claim: dfb4749c 27237708 07b99a6f 81b8ddc4.
