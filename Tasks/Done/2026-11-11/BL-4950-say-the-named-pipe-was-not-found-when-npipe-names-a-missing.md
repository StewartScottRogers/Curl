---
id: BL-4950
title: Say the named pipe was not found when --npipe names a missing pipe
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.NamedPipe.UnitLibrary]
requirement: none
created: 2026-11-11
completed: 2026-11-11
---
# BL-4950 — Say the named pipe was not found when --npipe names a missing pipe

## Goal

`--npipe` to a missing pipe exits 7 with a message that the pipe does not exist, not a timeout.

## Context

From the outside fitness review of 2026-11-11 (commit 6ce7cfcf, the `preview` `curl-win-x64.zip`, Windows 11 x64; REF = curl 8.22.0 Schannel), finding EXT-028 (Low, confirmed: reproduced at least twice).

- Repro: `curl -sS --npipe "\\.\pipe\nonexistent_extopt" http://localhost/x`
- Expected: Exit 7 naming the pipe as not found.
- Actual: `curl: (7) Failed to connect to the named pipe \\.\pipe\nonexistent_extopt: The operation has timed out.` after about 100 ms.
- Where to start: The connector in `Curl.Extension.NamedPipe.UnitLibrary` (NamedPipeClientStream connect timeout). Windows-only test, marked with `OSCondition`.

## Acceptance criteria

- [x] For a missing pipe the message names it as not found and does not say "timed out"; exit 7.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). Measure real curl (the platform build) before pinning any output text, and add a focused unit test in the fixed library's own `.UnitTests` project.

## Log

- Notes: SystemNamedPipeOpener turns the no-wait timeout for a missing local pipe into FileNotFoundException "The named pipe was not found" (exit 7). Measured with Curl.Console; the opener is ADR-0083 excluded, so the connector test gains a FileNotFoundException row.

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing. Criteria at claim: 18e40d88 a0b49a29.
- 2026-11-11: Doing -> Done. Missing --npipe pipe now reports the named pipe was not found, exit 7
