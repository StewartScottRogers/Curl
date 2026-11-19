---
id: BL-5550
title: Accept the ConnectOnly enum for CurlOption.ConnectOnly in Libcurl SetOption
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Libcurl/Options/CurlOptionValues.cs, Libcurl/Options/CurlOptionType.cs, Libcurl.UnitTests/Options/Network/EasyNetworkOptionSettersTests.cs]
requirement: none
created: 2026-11-19
completed: 2026-11-19
---
# BL-5550 — Accept the ConnectOnly enum for CurlOption.ConnectOnly in Libcurl SetOption

## Goal

`SetOption(CurlOption.ConnectOnly, ConnectOnly.WebSocket)` returns `CurlCode.Ok` and sets WebSocket connect-only mode, the same as `SetOption(CurlOption.ConnectOnly, 2L)`, so a call-for-call port of C code upgrades instead of silently downloading.

## Context

From bug report `2026-11-19T0642-Curl-bug-report.md`, finding 2 (Medium). Build tested: commit `0afbd10e3b`, `Libcurl 0.2.0`, Windows 11.

`SetOption(CurlOption.ConnectOnly, ConnectOnly.WebSocket)` returns `BadFunctionArgument`, while `SetOption(CurlOption.ConnectOnly, 2L)` works. `Libcurl` ships the `ConnectOnly` enum, but the option is a `CURLOT_LONG` and its setter accepts only `int` or `long`; `CURLOT_VALUES` options such as `RtspRequest` do take their enum. C code rarely checks `curl_easy_setopt`'s result, so a port ignores the error; the transfer then does a full download instead of the upgrade, and the program hangs waiting for frames.

**Where to start:** `Libcurl/Options/Network/EasyNetworkOptionSetters.cs:230`, `SetConnectOnly`, which calls `SetInRange` and `NumberOf`. Make `NumberOf` (or this setter) accept a `ConnectOnly` value. Then check every other `CurlOptionType.Long` option whose values `Libcurl` also ships as an enum, and accept those enums too.

## Acceptance criteria

- [x] `SetOption(CurlOption.ConnectOnly, ConnectOnly.WebSocket)` returns `CurlCode.Ok`, and a following perform does the WebSocket upgrade and no download.
- [x] `ConnectOnly.Disabled` and `ConnectOnly.Enabled` are accepted the same way, and an out-of-range value still returns `BadFunctionArgument`.
- [x] Every other `CurlOptionType.Long` option whose values `Libcurl` ships as an enum accepts that enum, or the Notes list each such option checked.
- [x] Focused unit tests in `Libcurl.UnitTests` pin each case above.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

The cause was the type check in `CurlOptionValues`, not the setter: a `Long` option took only `int` or `long`. It now also takes any `Enum` (as `Values` does), so every `CurlOptionType.Long` option accepts its shipped enum (ConnectOnly, IpResolve, UseSsl, and the rest); setters range-check the number, so out-of-range stays BadFunctionArgument. `touches` widened to those files. The perform-does-upgrade criterion holds because the stored mode is the same as for 2L. Measure-CodeQuality for Libcurl: 0 failing members.

## Log

- 2026-11-19: Created from bug report 2026-11-19T0642-Curl-bug-report.md, finding 2.
- 2026-11-19: Backlog -> Doing. Criteria at claim: b2789787 d96edf94 7fe8efb6 1fbe73a0 3d836d5e.
- 2026-11-19: Doing -> Done. ConnectOnly enum accepted; build clean, fast tests green
