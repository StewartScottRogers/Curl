---
id: BL-4934
title: Fix RTSP %{size_download} and %{content_type} for a discarded reply body
priority: Low
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Original.Protocol.Rtsp.UnitLibrary/RtspProtocolHandler.cs, Curl.Original.Protocol.Rtsp.UnitTests/RtspWriteOutTests.cs]
requirement: none
created: 2026-11-11
completed: 2026-11-11
---
# BL-4934 — Fix RTSP %{size_download} and %{content_type} for a discarded reply body

## Goal

RTSP `-w` gives `%{size_download}` 0 for a discarded body and the reply's `%{content_type}`.

## Context

From the outside fitness review of 2026-11-11 (commit 6ce7cfcf, the `preview` `curl-win-x64.zip`, Windows 11 x64; REF = curl 8.22.0 Schannel), finding STD-024 (Low, confirmed: reproduced at least twice).

- Repro: RTSP mock on 18500 replying to OPTIONS with `RTSP/1.0 200 OK`, matching CSeq, `Content-Length: 10`, `Content-Type: text/plain`, body `0123456789`. `curl -sS rtsp://127.0.0.1:18500/media -w "%{response_code} %{size_download} [%{content_type}]\n"`
- Expected: REF 8.22.0: `200 0 [text/plain]`.
- Actual: `200 10 []`.
- Where to start: `RtspProtocolHandler.ReadReplyAsync`: `report with { DownloadSize = bodyRead }` counts a discarded body; `Content-Type` never reaches the `TransferReport`.

## Acceptance criteria

- [x] The repro prints exactly `200 0 [text/plain]`.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). Measure real curl (the platform build) before pinning any output text, and add a focused unit test in the fixed library's own `.UnitTests` project.

## Notes

Fixed in RtspProtocolHandler.ReadReplyAsync: DownloadSize is the body bytes only when the plan writes the reply body (else 0); ContentType comes from the reply head. Three existing tests that pinned the old size for a discarded body now expect 0. Cross-platform: no OS-specific paths. Quality run for the library: 0 failing members.

## Log

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing. Criteria at claim: b4585b61 a0b49a29.
- 2026-11-11: Doing -> Done. RTSP size_download is 0 for a discarded body and content_type reports the reply's; tests and quality run green
