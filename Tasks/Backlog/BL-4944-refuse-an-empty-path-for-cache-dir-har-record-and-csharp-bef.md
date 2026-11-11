---
id: BL-4944
title: Refuse an empty path for --cache-dir, --har, --record and --csharp before any request
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.HttpCache.UnitLibrary/CacheDirectory.cs, Curl.Extension.RecordReplay.UnitLibrary/ExchangeDirectory.cs, Curl.Extension.CodeGeneration.UnitLibrary/CodeFile.cs, Curl.Extension.Har.UnitLibrary]
requirement: none
created: 2026-11-11
completed:
---
# BL-4944 — Refuse an empty path for --cache-dir, --har, --record and --csharp before any request

## Goal

An empty path for these options is refused with exit 2 and a message naming the option, never a leaked .NET exception.

## Context

From the outside fitness review of 2026-11-11 (commit 6ce7cfcf, the `preview` `curl-win-x64.zip`, Windows 11 x64; REF = curl 8.22.0 Schannel), finding EXT-022 (Low, confirmed: reproduced at least twice).

- Repro: `curl -sS --cache-dir "" http://127.0.0.1:18800/fresh`; same with `--har ""`, `--record ""`, `--csharp ""` (and `--powershell ""`).
- Expected: The port's convention (e.g. `--segments expects a whole number from 1 to 64`): exit 2 naming the option, before any request.
- Actual: The transfer runs, then `curl: (2) The value cannot be an empty string. (Parameter 'path')`; with `--csharp ""` nothing is transferred.
- Where to start: `ArgumentException` escapes from the files in touches; check the HAR writer's actual library name.

## Acceptance criteria

- [ ] Each option with `""` exits 2 before any request with a message naming the option, and no .NET exception text appears.
- [ ] `--ai-help` documents the rule if it describes these options' arguments.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). Measure real curl (the platform build) before pinning any output text, and add a focused unit test in the fixed library's own `.UnitTests` project.

## Log

- 2026-11-11: Created.
