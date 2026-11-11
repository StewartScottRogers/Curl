---
id: BL-4925
title: Show the final progress-meter row for a --fail-with-body error response
priority: Low
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs, Curl.Original.Protocol.Http.UnitTests/FailWithBodyProgressTests.cs]
requirement: none
created: 2026-11-11
completed:
---
# BL-4925 — Show the final progress-meter row for a --fail-with-body error response

## Goal

The non-tty progress meter shows the received bytes before `curl: (22)` under `--fail-with-body`.

## Context

From the outside fitness review of 2026-11-11 (commit 6ce7cfcf, the `preview` `curl-win-x64.zip`, Windows 11 x64; REF = curl 8.22.0 Schannel), finding STD-015 (Low, confirmed: reproduced at least twice).

- Repro: `GET /404` returns 404 with the 10-byte body `not found\n`. `curl --fail-with-body -o body.txt http://127.0.0.1:18100/404`.
- Expected: REF 8.22.0: final meter row `100     10 100     10   0      0 ...` then `curl: (22) The requested URL returned error: 404`.
- Actual: Only the initial all-zero row, then the `(22)` line.
- Where to start: `HttpProtocolHandler.cs` ~line 1478: `ThrowIfFailing(fail, HttpFailMode.FailWithBody, ...)` goes to `OutcomeOfFailure`, which skips the final progress update.

## Acceptance criteria

- [ ] `curl --fail-with-body -o b.txt http://127.0.0.1:18100/404 2>&1 | tr "\r" "\n" | grep -c "^100     10 100     10"` prints 1 or more.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). Measure real curl (the platform build) before pinning any output text, and add a focused unit test in the fixed library's own `.UnitTests` project.

## Log

- 2026-11-11: Created.
