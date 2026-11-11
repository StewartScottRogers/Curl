---
id: BL-4923
title: Pass non-event-stream responses through under --sse
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.ServerSentEvents.UnitLibrary, Curl.Extension.ServerSentEvents.UnitTests/SsePassthroughTests.cs]
requirement: none
created: 2026-11-11
completed: 2026-11-11
---
# BL-4923 — Pass non-event-stream responses through under --sse

## Goal

Under `--sse`, a response that is not `text/event-stream` (or not 2xx) ends with curl's own result (ADR-1067): its body is written.

## Context

From the outside fitness review of 2026-11-11 (commit 6ce7cfcf, the `preview` `curl-win-x64.zip`, Windows 11 x64; REF = curl 8.22.0 Schannel), finding EXT-014 (Medium, confirmed: reproduced at least twice).

- Repro: `/status500` returns 500 `text/plain` `err`; `/plain` 200 `text/plain` `hello`. `curl -sS --sse http://127.0.0.1:18800/status500` and `.../plain`.
- Expected: `err` and `hello` printed.
- Actual: Nothing printed, exit 0; `-w "%{http_code} %{size_download}"` shows `200 6`.
- Where to start: The transfer hook applies the event-stream normaliser before checking status and `Content-Type: text/event-stream`.

## Acceptance criteria

- [x] `curl -s --sse http://127.0.0.1:18800/status500` prints `err`.
- [x] The `/plain` command prints `hello`.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Done: ResponseHeadWatchingStream is the hook's header output; it forwards headers and records status/Content-Type, so EventStreamWritingStream passes bytes through unless the head is 200 text/event-stream (no head seen = event stream). Verified by SsePassthroughTests; quality run for the library 100/100. Linux/macOS: tests are platform-neutral. Not run against a live server.

Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). Measure real curl (the platform build) before pinning any output text, and add a focused unit test in the fixed library's own `.UnitTests` project.

## Log

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing. Criteria at claim: 5ef846dd 549d4d25 a0b49a29.
- 2026-11-11: Doing -> Done. Non-event-stream responses pass through under --sse
