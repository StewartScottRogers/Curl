---
id: BL-2751
title: RtspProtocolHandler makes any libcurl RTSP request from per-perform settings and a handle-lived CSeq and session
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Rtsp.UnitLibrary, Curl.Protocol.Rtsp.UnitTests]
requirement: none
created: 2026-10-18
completed:
---
# BL-2751 — RtspProtocolHandler makes any libcurl RTSP request from per-perform settings and a handle-lived CSeq and session

## Goal

`RtspProtocolHandler` takes, optionally, per-perform libcurl RTSP request settings (the request kind OPTIONS, DESCRIBE, ANNOUNCE, SETUP, PLAY, PAUSE, TEARDOWN, GET_PARAMETER, SET_PARAMETER, RECORD or RECEIVE; the stream URI; `Transport`; a session ID; the client `CSeq`; the upload or post body) and a session state object that outlives one perform, and makes that request byte for byte as libcurl 8.22.0's `lib/rtsp.c` does, while the curl tool's transfer (no settings given: `OPTIONS *`, `CSeq: 1`) stays exactly as it is.

## Context

Split out of BL-2704 (2026-10-18), whose engine wiring in `Curl.Engine.UnitLibrary` waits on this handler surface. Today `Curl.Protocol.Rtsp.UnitLibrary/RtspProtocolHandler.cs` only sends `RtspMethod.Options` to `OptionsTarget` (`*`) with a fresh `RtspSessionState` per transfer, and `RtspMethod` only has `Options`. The engine builds its handlers once per perform (`ManagedCurlEngine.CreateDispatcher`, which already passes the `CURLOPT_INTERLEAVEFUNCTION` sink), so the settings can arrive through the constructor the same way; the `CSeq` counter and kept session ID must live in an object the engine keeps on the easy handle, so a second perform on one handle continues them (libcurl's `data->state.rtsp_next_client_CSeq` and `data->set.str[STRING_RTSP_SESSION_ID]`).

Keep the Rtsp library free of `Curl.Api.UnitLibrary`: define the settings as the library's own type (the engine maps `EasyRtspOptions` to it under BL-2704). What `lib/rtsp.c` does per kind, to match: SETUP requires `Transport` (else `CURLE_BAD_FUNCTION_ARGUMENT`, "Refusing to issue an RTSP SETUP without a Transport: header."); every kind but OPTIONS / SETUP / RECEIVE with no session ID held refuses ("Refusing to issue an RTSP request [%s] without a session ID.", `CURLE_BAD_FUNCTION_ARGUMENT`); DESCRIBE adds `Accept: application/sdp`; ANNOUNCE and SET_PARAMETER with a body add `Content-Length` and `Content-Type: application/sdp` (ANNOUNCE) or `text/parameters` (SET_PARAMETER) unless a custom header names them; SET_PARAMETER / GET_PARAMETER with no body send `Content-Length: 0`? - check `rtsp.c` before pinning; RECEIVE already exists as `ReceiveAsync`. A reply to SETUP sets the held session ID; a reply `CSeq` mismatch is still 85. Read 8.22.0's `lib/rtsp.c` (`rtsp_do`) for header order before writing the formatter changes; the upstream cases 567 to 572, 577 and 689 under `Curl.Conformance.UnitTests` show the expected bytes in their `<protocol>` sections.

## Acceptance criteria

- [ ] With no libcurl settings given, every existing `Curl.Protocol.Rtsp.UnitTests` test passes unchanged (the tool's transfer does not change).
- [ ] Each RTSP request kind has a focused unit test in `Curl.Protocol.Rtsp.UnitTests` pinning its request line, headers and body bytes as `lib/rtsp.c` writes them.
- [ ] A unit test shows two performs sharing one session state object continue the `CSeq` and send the session ID the first reply gave; another shows a set client `CSeq` and a set session ID are sent.
- [ ] SETUP without `Transport`, and a session-requiring kind without a session ID, fail with exit 43 and `lib/rtsp.c`'s messages, each pinned by a unit test.
- [ ] `Curl.Protocol.Rtsp.UnitLibrary` keeps 100% line and branch coverage and no method past the complexity gate (`Measure-CodeQuality.ps1 -Library Curl.Protocol.Rtsp.UnitLibrary`).
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.

## Notes

## Log

- 2026-10-18: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: 30c46d50 5c61c2c9 2a21c1e2 8cecf683 65359435 4272a6b9.
