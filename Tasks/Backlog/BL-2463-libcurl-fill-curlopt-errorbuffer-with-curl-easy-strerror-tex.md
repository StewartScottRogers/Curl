---
id: BL-2463
title: Libcurl: fill CURLOPT_ERRORBUFFER with curl_easy_strerror text when the engine gives no detail
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-2411, BL-2450]
touches: [Libcurl/Easy, Libcurl.UnitTests/Easy]
requirement: none
created: 2026-10-15
completed:
---
# BL-2463 — Libcurl: fill CURLOPT_ERRORBUFFER with curl_easy_strerror text when the engine gives no detail

## Goal

A failed `CurlEasy.Perform` whose engine `ErrorText` is empty leaves `curl_easy_strerror(code)` in the `CURLOPT_ERRORBUFFER` buffer and in `CurlEasy.ErrorText`, if libcurl 8.22.0 does so (check `lib/multi.c` first; if it does not, close this task with a note saying so).

## Context

BL-2411 made a perform clear the buffer and write the engine's text cut to 255 bytes (`Libcurl/Easy/EasyErrorBuffer.cs`). libcurl's `multi.c` is believed to copy `curl_easy_strerror(result)` into the buffer when nothing more specific was written; `curl_easy_strerror` reaches Libcurl with BL-2450 (`CurlEasyErrorText`), hence the dependency.

## Acceptance criteria

- [ ] A test in `Libcurl.UnitTests/Easy/CurlEasyTests.cs` pins the buffer's text for a failure with empty engine text, matching libcurl 8.22.0's source.
- [ ] `Measure-CodeQuality.ps1 -Library Libcurl` reports 0 failing members.
- [ ] `Libcurl/CLAUDE.md` says what the buffer holds when the engine gives no detail.

## Notes

## Log

- 2026-10-15: Created.
