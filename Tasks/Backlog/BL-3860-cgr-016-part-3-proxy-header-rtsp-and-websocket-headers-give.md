---
id: BL-3860
title: CGR-016 part 3: --proxy-header, RTSP and WebSocket headers give invalid UTF-8 argument bytes unchanged
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Networking.UnitLibrary/HttpProxyTunnel.cs, Curl.Protocol.Rtsp.UnitLibrary/RtspRequestFormatter.cs, Curl.Protocol.Ws.UnitLibrary/WsUpgradeRequestFormatter.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3860 — CGR-016 part 3: --proxy-header, RTSP and WebSocket headers give invalid UTF-8 argument bytes unchanged

## Goal

On Linux and macOS, `--proxy-header $'X-A: \xff'` on a CONNECT tunnel, and `-H $'X-A: \xff'` on an `rtsp://` or `ws://` request, send the byte 0xFF where today they send EF BF BD, as curl 8.18.0 sends its argument bytes unchanged.

## Context

Found by BL-3850 (CGR-016 part 2). The console hands an argument byte that is not valid UTF-8 over as a lone surrogate U+DC80 + byte (`InvalidUtf8ByteEscape`, BL-3849, ADR-0909). `HttpRequestHeadFormatter.WireText` in Curl.Protocol.Http.UnitLibrary already gives each such surrogate back as its byte, so HTTP `-H` and forward-proxy `--proxy-header` are right. Three other places encode command-line header text with `Encoding.Latin1.GetString(options.CommandLineTextEncoding.GetBytes(text))`, which on Linux (UTF-8) turns the surrogate into EF BF BD: `Curl.Networking.UnitLibrary/HttpProxyTunnel.cs` (near line 112, the CONNECT request's `--proxy-header` lines), `Curl.Protocol.Rtsp.UnitLibrary/RtspRequestFormatter.cs` (`HeadText`, near line 197) and `Curl.Protocol.Ws.UnitLibrary/WsUpgradeRequestFormatter.cs` (`HeadText`, near line 95). Each library may not reference another protocol library, so each gets its own copy of the escape rule (a lone U+DC80..U+DCFF not after a high surrogate stands for the byte below U+DC00), or a shared one in Curl.Protocol.Abstractions.UnitLibrary if a task holds that file. Real curl 8.18.0 in WSL (BL-3850's measurement) sends `-H $'X-A: \xff'` as `58 2D 41 3A 20 FF`.

## Acceptance criteria

- [ ] A CONNECT request's `--proxy-header`, an RTSP request's `-H` and a WebSocket upgrade's `-H` holding the surrogate U+DCFF send the byte FF, pinned by a focused unit test in each library's own `.UnitTests` project that runs on every platform.
- [ ] Valid UTF-8 values and every value on Windows go out as before.
- [ ] Each changed library stays within the coverage, complexity and CRAP gates (`Measure-CodeQuality.ps1 -Library <all changed libraries>`).
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-10-26: Created.
