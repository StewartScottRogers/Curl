---
id: BL-4920
title: Report the joined length in %{size_download} for --segments
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.SegmentedDownload.UnitLibrary/SegmentedDownloadTransferHook.cs, Curl.Extension.SegmentedDownload.UnitTests/SegmentedSizeDownloadTests.cs]
requirement: none
created: 2026-11-11
completed:
---
# BL-4920 — Report the joined length in %{size_download} for --segments

## Goal

`--segments N` reports the complete length in `%{size_download}` (ADR-1087).

## Context

From the outside fitness review of 2026-11-11 (commit 6ce7cfcf, the `preview` `curl-win-x64.zip`, Windows 11 x64; REF = curl 8.22.0 Schannel), finding EXT-011 (Medium, confirmed: reproduced at least twice).

- Repro: Route `/big` serving 100000 bytes with Range. `curl -sS --segments 4 http://127.0.0.1:18800/big -o w.bin -w "%{size_download} %{http_code}\n"`
- Expected: `100000 206`.
- Actual: `25000 206`; `--segments 3` gives `33334`. The joined file is correct.
- Where to start: `SegmentedDownloadTransferHook.JoinAsync` sets `BytesTransferred` but size_download keeps the segment value.

## Acceptance criteria

- [ ] `curl -s --segments 4 http://127.0.0.1:18800/big -o NUL -w "%{size_download}"` prints `100000`.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). Measure real curl (the platform build) before pinning any output text, and add a focused unit test in the fixed library's own `.UnitTests` project.

## Log

- 2026-11-11: Created.
