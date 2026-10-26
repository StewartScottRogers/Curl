---
id: BL-3850
title: CGR-016 part 2: Linux and macOS send -H header values and -b cookie values with invalid UTF-8 argument bytes unchanged
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-3849]
touches: [Curl.Cli.UnitLibrary/CommandLineOptionTable.cs, Curl.Cli.UnitLibrary/CommandLineCookie.cs, Curl.Cli.UnitTests/Cgr016HeaderCookieTests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3850 — CGR-016 part 2: Linux and macOS send -H header values and -b cookie values with invalid UTF-8 argument bytes unchanged

## Goal

On Linux and macOS, `-H $'X-A: \xff'` sends the header line `X-A: ` followed by the byte 0xFF, and `-b $'a=\xff'` sends `Cookie: a=` followed by 0xFF, as curl 8.18.0 does (today EF BF BD and `?`).

## Context

Split from BL-3545 (third-party gap report `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`, CGR-016). On Linux, .NET hands `Main` an argv already decoded as UTF-8 with every invalid byte replaced by U+FFFD, so every option then re-encodes EF BF BD where curl 8.18.0 (OpenSSL, Linux) uses the raw byte (curl never re-encodes argv). Measure real curl in WSL with `Record-CurlExchange.ps1` before pinning output; no Python.

Needs BL-3849's lossless argument decoding (`InvalidUtf8ByteEscape`). The request side writes header and cookie strings one character per byte (Latin-1): see `AsRawWireText` in `Curl.Cli.UnitLibrary/CommandLineOptionTable.cs` (near line 1014), which re-spells config-file values that way (CGR-020); extend it so a command-line value holding escaped bytes is re-spelled through the argument wire encoding too, and do the same for `-b` cookie strings (`CommandLineCookie.cs`). Check `--proxy-header` and `-X` go the same way.

## Acceptance criteria

- [x] `-H $'X-A: \xff'` and `-b $'a=\xff'` send the same request bytes as curl 8.18.0 on Linux; the measured commands and both results are recorded in Notes.
- [x] Valid UTF-8 header and cookie values, and every value on Windows, go out as before.
- [x] A focused unit test in `Curl.Cli.UnitTests/Cgr016HeaderCookieTests.cs` fails before the change and passes after it on every platform.
- [x] Curl.Cli.UnitLibrary stays within the coverage, complexity and CRAP gates (`Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary`).
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Measured real curl 8.18.0 (OpenSSL, WSL Ubuntu): `Record-CurlExchange.ps1 -Port 18850 -ListenAddress 172.26.96.1 -Curl wsl.exe -CurlArgs '-d','Ubuntu','--','bash','-c',"curl -s -H `$'X-A: \xff' -b `$'a=\xff' -b `$'c=\xc3\xa9' -X `$'G\xffT' http://172.26.96.1:18850/a"`. Request bytes: `47 FF 54 20 2F 61 20 48 54 54 50 2F 31 2E 31` (request line `G<FF>T /a HTTP/1.1`), `Cookie: a=<FF>; c=<C3 A9>`, `X-A: <FF>`. Every argument byte goes out unchanged.
- Curl after the change: `-H` already matched once BL-3849 landed. The console hands the byte over as the surrogate U+DCFF, `AddHeaderValue` stores it as it is, and `HttpRequestHeadFormatter.WireText` (Curl.Protocol.Http) gives each escaped surrogate back as its byte, so the line is `X-A: FF`. `Cgr016HeaderCookieTests.Parse_HeaderOfInvalidByte_KeepsTheEscapeForTheRequestHead` pins that the parse keeps the surrogate for the request head. `-b` and `-X` were wrong: they go out one character per byte (Cookie via `CookieEngine` to `AppendAlways`, the method into the request line), so the surrogate went out as `?`. `AsRawWireText` now gives each lone U+DC80..U+DCFF as the Latin-1 character of its byte, so they send `a=FF` and `G FF T`. The cookie and method tests fail before the change and pass after it. The tests run on every platform because they build the surrogate with `InvalidUtf8ByteEscape.Decode`; on Windows no argument holds one, so nothing changes there.
- Decision (sensible default, no ADR: this applies ADR-0821's escape scheme to two more options and decides nothing new): valid non-ASCII command-line characters in `-b` and `-X` stay as they were, per the second criterion. The measurement shows Linux curl sends `c=é` as C3 A9, while Curl is believed to send E9; filed as BL-3857.
- `--proxy-header` on a CONNECT tunnel (`HttpProxyTunnel`), and RTSP and WebSocket `-H`, encode with `CommandLineTextEncoding.GetBytes` and so send EF BF BD for the surrogate. They sit outside this task's touches, so they are filed as BL-3856. A forward proxy's `--proxy-header` goes through `WireText` and is already right.
- Quality: `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary` reports 0 failing members. Fast tests: 46 projects, 43417 passed, 0 failed. The CI criterion on Linux and macOS is met by keeping the change platform-neutral.

## Log

- 2026-10-26: Created.
- 2026-10-26: Backlog -> Doing. Criteria at claim: d57f3b92 56067fdf dd7dccc9 c285e6d4 779790c0.
- 2026-10-26: Doing -> Done. -b and -X send an invalid UTF-8 argument byte unchanged as curl 8.18.0 does on Linux; -H already did
