---
id: BL-2464
title: Libcurl: fill CURLOPT_ERRORBUFFER with curl_easy_strerror text when the engine gives no detail
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-2411, BL-2450]
touches: [Libcurl/Easy, Libcurl.UnitTests/Easy, Libcurl/CLAUDE.md]
requirement: none
created: 2026-10-15
completed: 2026-10-16
---
# BL-2464 — Libcurl: fill CURLOPT_ERRORBUFFER with curl_easy_strerror text when the engine gives no detail

## Goal

A failed `CurlEasy.Perform` whose engine `ErrorText` is empty leaves `curl_easy_strerror(code)` in the `CURLOPT_ERRORBUFFER` buffer and in `CurlEasy.ErrorText`, if libcurl 8.22.0 does so (check `lib/multi.c` first; if it does not, close this task with a note saying so).

## Context

BL-2411 made a perform clear the buffer and write the engine's text cut to 255 bytes (`Libcurl/Easy/EasyErrorBuffer.cs`). libcurl's `multi.c` is believed to copy `curl_easy_strerror(result)` into the buffer when nothing more specific was written; `curl_easy_strerror` reaches Libcurl with BL-2450 (`CurlEasyErrorText`), hence the dependency.

## Acceptance criteria

- [x] A test in `Libcurl.UnitTests/Easy/CurlEasyTests.cs` pins the buffer's text for a failure with empty engine text, matching libcurl 8.22.0's source.
- [x] `Measure-CodeQuality.ps1 -Library Libcurl` reports 0 failing members.
- [x] `Libcurl/CLAUDE.md` says what the buffer holds when the engine gives no detail.

## Notes

- Checked libcurl 8.22.0's source (tag `curl-8_22_0`, the whole `lib/` grepped for `set.errorbuffer`): it does NOT copy `curl_easy_strerror` into `CURLOPT_ERRORBUFFER`. The buffer is cleared in `multi.c` (line 512) and `easy.c` (line 752) and written only by `failf` in `lib/curl_trc.c` (line 187); no fallback exists. The option's man page says only that libcurl empties the buffer before a transfer.
- So Libcurl already matches; no production change. As the Goal says, the task closes saying so, with the behaviour pinned by `Perform_AFailureWithNoEngineText_LeavesTheErrorBufferEmptyNotTheStrerrorText` in `CurlEasyTests` and a sentence in `Libcurl/CLAUDE.md`.
- Added `Libcurl/CLAUDE.md` to `touches`: the third criterion names it, and no task in Doing touched it (only BL-2440, `Curl.Engine`).
- `Measure-CodeQuality.ps1 -Library Libcurl`: 0 failing members. Libcurl.UnitTests 1059 passed; solution fast tests green.

## Log

- 2026-10-15: Created.
- 2026-10-16: Backlog -> Doing.
- 2026-10-16: Doing -> Done. Pinned that a failure with no engine detail leaves CURLOPT_ERRORBUFFER empty, as libcurl 8.22.0 never falls back to curl_easy_strerror
