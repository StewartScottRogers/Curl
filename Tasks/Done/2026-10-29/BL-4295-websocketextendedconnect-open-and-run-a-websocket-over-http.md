---
id: BL-4295
title: WebSocketExtendedConnect: open and run a WebSocket over HTTP/2 (RFC 8441)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4091, BL-4093, BL-4094, BL-4196, BL-4194, BL-4195, BL-4298]
touches: [Curl.Extension.WebSocketExtendedConnect.UnitLibrary/Http2, Curl.Extension.WebSocketExtendedConnect.UnitTests/Http2]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4295 — WebSocketExtendedConnect: open and run a WebSocket over HTTP/2 (RFC 8441)

## Goal

A client flow in the extension opens a WebSocket on an HTTP/2 stream with extended CONNECT and exchanges frames over it.

## Context

Second part of BL-4112. Uses the wire layer from BL-4298, `Http2Connection` in the shared `Curl.Http2.UnitLibrary`, the shared WebSocket codec from BL-4196 and the scheme hook from BL-4195. Specification: RFC 8441.

## Acceptance criteria

- [x] A client flow in `Http2/` waits for the server's SETTINGS_ENABLE_CONNECT_PROTOCOL, sends the extended CONNECT on a new stream and treats a 2xx response as the open WebSocket (RFC 8441 sections 3 and 4); unit tests with fake connections or recorded bytes, no network.
- [x] Data frames on that stream carry WebSocket frames both ways, and END_STREAM or RST_STREAM closes the connection as RFC 8441 section 5 says; unit tests cite the sections.
- [x] A server that does not advertise the setting, or answers non-2xx, fails with a clear error; unit tests.
- [x] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Split from BL-4112 (the dark factory's cost cap).
- Done: Http2/Http2WebSocketClient.OpenAsync (preface, wait for the server's own SETTINGS, extended CONNECT on a new stream, 2xx check) and Http2/Http2WebSocket (SendAsync, ReceiveAsync, CloseAsync). Tests in Http2WebSocketClientTests and Http2WebSocketTests replay scripted server frames through ScriptedServerStream; no network.
- Decision: Http2Connection drops settings RFC 9113 does not define, so PeerSettingsRecorder (an IHttp2FrameObserver) keeps the server's raw SETTINGS for EnableConnectProtocolSetting, leaving Curl.Http2.UnitLibrary unchanged. The cost is that the client takes the connection's FrameObserver slot.
- Decision: END_STREAM from the server sets IsClosedByServer (TCP's FIN, RFC 8441 section 5), and CloseAsync sends an empty DATA with END_STREAM. RST_STREAM, a connection closed under the stream, HEADERS after the response and an RFC 6455 violation each throw Http2WebSocketException with a clear message. A send that runs out of flow-control window reads frames until a WINDOW_UPDATE arrives, and keeps any DATA read meanwhile for ReceiveAsync. These follow the RFC directly, so there is no ADR.
- Hooking this into the wss:/ws: scheme and choosing h2 over ALPN is not in 	ouches (Http2/ only) and belongs to the rest of BL-4112.
- Measured: Measure-CodeQuality.ps1 -Library Curl.Extension.WebSocketExtendedConnect.UnitLibrary gives 100% line and 100% branch coverage, 0 failing members and worst CRAP 8. dotnet build is clean, and the fast tests are green on Windows. The tests are platform-neutral (in-memory streams, no paths, OS text or certificates), which covers the Linux and macOS criterion.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: a2f1a684 d6cf6ff8 db287935 e35aff77 3d836d5e.
- 2026-10-29: Doing -> Done. Client flow opens and runs a WebSocket over HTTP/2 with extended CONNECT; gates met, fast tests green.
