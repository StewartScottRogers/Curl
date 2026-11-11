---
id: BL-4946
title: Report recorded sizes in %{size_download} and %{size_header} under --replay
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.RecordReplay.UnitLibrary/RecordReplayTransferHook.cs, Curl.Extension.RecordReplay.UnitTests/ReplaySizeWriteOutTests.cs]
requirement: none
created: 2026-11-11
completed:
---
# BL-4946 — Report recorded sizes in %{size_download} and %{size_header} under --replay

## Goal

A replay reports the same sizes as the live run (ADR-1135 point 5).

## Context

From the outside fitness review of 2026-11-11 (commit 6ce7cfcf, the `preview` `curl-win-x64.zip`, Windows 11 x64; REF = curl 8.22.0 Schannel), finding EXT-024 (Low, confirmed: reproduced at least twice).

- Repro: `curl -s --record rec2 "http://127.0.0.1:18800/rec?a=1" -o NUL -w "%{http_code} %{size_download}\n"` (prints `200 18`), then the same with `--replay rec2 -w "%{http_code} %{size_download} %{size_header}\n"`.
- Expected: `200 18 <head length>`.
- Actual: `200 0 0`.
- Where to start: `RecordReplayTransferHook.cs`.

## Acceptance criteria

- [ ] The replay prints the same `%{size_download}` and `%{size_header}` as the live run.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). Measure real curl (the platform build) before pinning any output text, and add a focused unit test in the fixed library's own `.UnitTests` project.

## Log

- 2026-11-11: Created.
