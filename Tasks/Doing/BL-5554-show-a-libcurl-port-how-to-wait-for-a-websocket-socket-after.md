---
id: BL-5554
title: Show a Libcurl port how to wait for a WebSocket socket after curl_ws_recv returns Again
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-5551]
touches: [Libcurl/WebSocket, Libcurl/README.md]
requirement: none
created: 2026-11-19
completed:
---
# BL-5554 — Show a Libcurl port how to wait for a WebSocket socket after curl_ws_recv returns Again

## Goal

A `Libcurl` port that gets `CurlCode.Again` from the `curl_ws_recv` equivalent can wait for data the way C does (on the socket from `CURLINFO_ACTIVESOCKET`) or with an awaitable, instead of polling with a sleep.

## Context

From bug report `2026-11-19T0642-Curl-bug-report.md`, finding 3 (Low), fourth bullet. Build tested: commit `0afbd10e3b`, `Libcurl 0.2.0`.

The `curl_ws_recv` equivalent returns `Again` with no socket to wait on, so a port polls with a sleep. Upstream curl also returns `CURLE_AGAIN`; a C program then waits on the socket from `curl_easy_getinfo(CURLINFO_ACTIVESOCKET)` with `select` or `poll`. `Libcurl` has `CurlInfo.Activesocket` (`Libcurl/Info/Longs/EasyInfoReaders.Longs.cs:48`), and `CurlEasy` has `RecvAsync` for connect-only transfers.

First find out whether a port can already wait: whether `Activesocket` gives something .NET can wait on, and whether an async WebSocket receive exists or can sit beside the `Again` form. Then do the smallest thing that removes the sleep: document the existing way in `Libcurl/README.md` with a sample, or add an awaitable wait if none exists. Record which in the Notes. Keep `Again` as the result when no frame is ready, as upstream does. Waits for BL-5551, which edits the same README.

## Acceptance criteria

- [ ] `Libcurl/README.md` shows a WebSocket receive loop that waits for data after `Again` without a sleep or busy loop, and the sample compiles.
- [ ] If a new wait API was added, focused unit tests in `Libcurl.UnitTests` pin it, including cancellation.
- [ ] The `curl_ws_recv` equivalent still returns `Again` when no frame is ready.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-19: Created from bug report 2026-11-19T0642-Curl-bug-report.md, finding 3 (ws_recv Again).
- 2026-11-19: Backlog -> Doing. Criteria at claim: 0b154ba5 c077b01b 34acfc7c 3d836d5e.
