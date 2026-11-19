---
id: BL-5550
title: Accept the ConnectOnly enum for CurlOption.ConnectOnly in Libcurl SetOption
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Libcurl/Options/Network/EasyNetworkOptionSetters.cs]
requirement: none
created: 2026-11-19
completed:
---
# BL-5550 — Accept the ConnectOnly enum for CurlOption.ConnectOnly in Libcurl SetOption

## Goal

`SetOption(CurlOption.ConnectOnly, ConnectOnly.WebSocket)` returns `CurlCode.Ok` and sets WebSocket connect-only mode, the same as `SetOption(CurlOption.ConnectOnly, 2L)`, so a call-for-call port of C code upgrades instead of silently downloading.

## Context

From bug report `2026-11-19T0642-Curl-bug-report.md`, finding 2 (Medium). Build tested: commit `0afbd10e3b`, `Libcurl 0.2.0`, Windows 11.

`SetOption(CurlOption.ConnectOnly, ConnectOnly.WebSocket)` returns `BadFunctionArgument`, while `SetOption(CurlOption.ConnectOnly, 2L)` works. `Libcurl` ships the `ConnectOnly` enum, but the option is a `CURLOT_LONG` and its setter accepts only `int` or `long`; `CURLOT_VALUES` options such as `RtspRequest` do take their enum. C code rarely checks `curl_easy_setopt`'s result, so a port ignores the error; the transfer then does a full download instead of the upgrade, and the program hangs waiting for frames.

**Where to start:** `Libcurl/Options/Network/EasyNetworkOptionSetters.cs:230`, `SetConnectOnly`, which calls `SetInRange` and `NumberOf`. Make `NumberOf` (or this setter) accept a `ConnectOnly` value. Then check every other `CurlOptionType.Long` option whose values `Libcurl` also ships as an enum, and accept those enums too.

## Acceptance criteria

- [ ] `SetOption(CurlOption.ConnectOnly, ConnectOnly.WebSocket)` returns `CurlCode.Ok`, and a following perform does the WebSocket upgrade and no download.
- [ ] `ConnectOnly.Disabled` and `ConnectOnly.Enabled` are accepted the same way, and an out-of-range value still returns `BadFunctionArgument`.
- [ ] Every other `CurlOptionType.Long` option whose values `Libcurl` ships as an enum accepts that enum, or the Notes list each such option checked.
- [ ] Focused unit tests in `Libcurl.UnitTests` pin each case above.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-19: Created from bug report 2026-11-19T0642-Curl-bug-report.md, finding 2.
