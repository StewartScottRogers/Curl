---
id: BL-1110
title: Refuse an HTTP/2 SETTINGS frame of more than 32 entries with ENHANCE_YOUR_CALM, as nghttp2 does
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Http2.UnitLibrary, Curl.Http2.UnitTests]
requirement: none
created: 2026-10-01
completed:
---
# BL-1110 — Refuse an HTTP/2 SETTINGS frame of more than 32 entries with ENHANCE_YOUR_CALM, as nghttp2 does

## Goal

`Http2Connection` ends the connection with a GOAWAY of `ENHANCE_YOUR_CALM` when the peer sends a non-acknowledgement SETTINGS frame carrying more than 32 entries, as the nghttp2 library inside curl does, instead of applying however many entries arrive.

## Context

- curl's HTTP/2 is nghttp2, and curl does not change its `max_settings` option, so nghttp2's default applies: `NGHTTP2_DEFAULT_MAX_SETTINGS` is 32 (`lib/includes/nghttp2/nghttp2.h` line 266, https://github.com/nghttp2/nghttp2). In `lib/nghttp2_session.c` `nghttp2_session_mem_recv2`, on a SETTINGS frame header (before its payload is read) with `max_niv - 1 = length / 6 > session->max_settings`, nghttp2 calls `nghttp2_session_terminate_session_with_reason(session, NGHTTP2_ENHANCE_YOUR_CALM, "SETTINGS: too many setting entries")`. An acknowledgement (which must be empty) is not counted. This is nghttp2's guard against SETTINGS floods, the same family as the CONTINUATION limit `Http2Connection.MaximumContinuationFrames` = 8 already mirrors.
- Curl today: `Curl.Http2.UnitLibrary/Http2FramePayloadParser.cs` `ParseSettings` checks only that the length is a multiple of 6 and returns every entry; `Http2Connection.ReceiveSettingsAsync` applies them all. Nothing limits the count.
- Raise it the way the CONTINUATION flood is raised (`Http2Connection` around line 594: `new Http2ProtocolException(Http2ErrorCode.EnhanceYourCalm, ...)`), so the connection's existing error path sends the GOAWAY; test it as `Http2ConnectionTests.ReadStreamFrameAsync_FloodOfEmptyContinuations_IsEnhanceYourCalm` and `ReadStreamFrameAsync_InvalidSetting_SendsGoAwayWithProtocolError` do. Follow the existing GOAWAYs' convention for debug data (they carry none) and keep the nghttp2 reason text in the exception message.
- Name the limit as a public constant beside `MaximumContinuationFrames`, with a doc comment citing nghttp2.

## Acceptance criteria

- [ ] New tests in `Curl.Http2.UnitTests/Http2ConnectionTests.cs`: a SETTINGS frame of 33 entries (198-byte payload) fails with `Http2ErrorCode.EnhanceYourCalm` and the connection writes a GOAWAY with that code and no SETTINGS acknowledgement; a frame of exactly 32 entries is applied and acknowledged.
- [ ] A test pins that the 33-entry frame's settings are not applied (for example `PeerSettings.InitialWindowSize` keeps its value).
- [ ] `dotnet build Curl.slnx -warnaserror` is clean; the fast tests pass; `Measure-CodeQuality.ps1 -Library Curl.Http2.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-10-01: Created.
