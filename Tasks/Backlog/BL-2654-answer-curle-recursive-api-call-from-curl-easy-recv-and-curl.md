---
id: BL-2654
title: Answer CURLE_RECURSIVE_API_CALL from curl_easy_recv and curl_easy_send called inside a callback so upstream case 1555 passes
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-17
completed:
---
# BL-2654 — Answer CURLE_RECURSIVE_API_CALL from curl_easy_recv and curl_easy_send called inside a callback so upstream case 1555 passes

## Goal

`curl_easy_recv` and `curl_easy_send` called from inside a transfer's callback answer `CURLE_RECURSIVE_API_CALL` (93), and upstream case 1555 passes and is on the ratchet.

## Context

Found by BL-2541 (lib1555 port). libcurl 8.22.0 refuses `curl_easy_recv`/`curl_easy_send` on a handle whose callback is running (`Curl_is_in_callback`) with `CURLE_RECURSIVE_API_CALL`. Curl answers 1 (`CURLE_UNSUPPORTED_PROTOCOL`, no connect-only connection), so case 1555's stdout differs at byte 24 (line 1): expected `curl_easy_recv returned 93`, got `curl_easy_recv returned 1`. The check belongs where the engine tracks that a callback is running (`ManagedEasyTransfer`).

## Acceptance criteria

- [ ] `curl_easy_recv` and `curl_easy_send` on a handle inside its own callback answer `CURLE_RECURSIVE_API_CALL`, with a focused unit test in `Curl.Engine.UnitTests`.
- [ ] Case 1555 passes through the runner on Windows and is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.

## Notes

## Log

- 2026-10-17: Created.
