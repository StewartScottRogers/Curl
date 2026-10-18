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
completed: 2026-10-18
---
# BL-2751 — RtspProtocolHandler makes any libcurl RTSP request from per-perform settings and a handle-lived CSeq and session

## Goal

`RtspProtocolHandler` takes, optionally, per-perform libcurl RTSP request settings (the request kind OPTIONS, DESCRIBE, ANNOUNCE, SETUP, PLAY, PAUSE, TEARDOWN, GET_PARAMETER, SET_PARAMETER, RECORD or RECEIVE; the stream URI; `Transport`; a session ID; the client `CSeq`; the upload or post body) and a session state object that outlives one perform, and makes that request byte for byte as libcurl 8.22.0's `lib/rtsp.c` does, while the curl tool's transfer (no settings given: `OPTIONS *`, `CSeq: 1`) stays exactly as it is.

## Context

Split out of BL-2704 (2026-10-18), whose engine wiring in `Curl.Engine.UnitLibrary` waits on this handler surface. Today `Curl.Protocol.Rtsp.UnitLibrary/RtspProtocolHandler.cs` only sends `RtspMethod.Options` to `OptionsTarget` (`*`) with a fresh `RtspSessionState` per transfer, and `RtspMethod` only has `Options`. The engine builds its handlers once per perform (`ManagedCurlEngine.CreateDispatcher`, which already passes the `CURLOPT_INTERLEAVEFUNCTION` sink), so the settings can arrive through the constructor the same way; the `CSeq` counter and kept session ID must live in an object the engine keeps on the easy handle, so a second perform on one handle continues them (libcurl's `data->state.rtsp_next_client_CSeq` and `data->set.str[STRING_RTSP_SESSION_ID]`).

Keep the Rtsp library free of `Curl.Api.UnitLibrary`: define the settings as the library's own type (the engine maps `EasyRtspOptions` to it under BL-2704). What `lib/rtsp.c` does per kind, to match: SETUP requires `Transport` (else `CURLE_BAD_FUNCTION_ARGUMENT`, "Refusing to issue an RTSP SETUP without a Transport: header."); every kind but OPTIONS / SETUP / RECEIVE with no session ID held refuses ("Refusing to issue an RTSP request [%s] without a session ID.", `CURLE_BAD_FUNCTION_ARGUMENT`); DESCRIBE adds `Accept: application/sdp`; ANNOUNCE and SET_PARAMETER with a body add `Content-Length` and `Content-Type: application/sdp` (ANNOUNCE) or `text/parameters` (SET_PARAMETER) unless a custom header names them; SET_PARAMETER / GET_PARAMETER with no body send `Content-Length: 0`? - check `rtsp.c` before pinning; RECEIVE already exists as `ReceiveAsync`. A reply to SETUP sets the held session ID; a reply `CSeq` mismatch is still 85. Read 8.22.0's `lib/rtsp.c` (`rtsp_do`) for header order before writing the formatter changes; the upstream cases 567 to 572, 577 and 689 under `Curl.Conformance.UnitTests` show the expected bytes in their `<protocol>` sections.

## Acceptance criteria

- [x] With no libcurl settings given, every existing `Curl.Protocol.Rtsp.UnitTests` test passes unchanged (the tool's transfer does not change).
- [x] Each RTSP request kind has a focused unit test in `Curl.Protocol.Rtsp.UnitTests` pinning its request line, headers and body bytes as `lib/rtsp.c` writes them.
- [x] A unit test shows two performs sharing one session state object continue the `CSeq` and send the session ID the first reply gave; another shows a set client `CSeq` and a set session ID are sent.
- [x] SETUP without `Transport` fails with exit 43 and `lib/rtsp.c`'s message, pinned by a unit test; a GET_PARAMETER with no session ID is sent with no `Session` header and no refusal, as curl 8.22.0's `lib/rtsp.c` (which has no session-ID refusal) sends it, pinned by a unit test.
- [x] `Curl.Protocol.Rtsp.UnitLibrary` keeps 100% line and branch coverage and no method past the complexity gate (`Measure-CodeQuality.ps1 -Library Curl.Protocol.Rtsp.UnitLibrary`).
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.

## Notes

- 2026-10-18 (lane 3): criterion 4 corrected before any code. curl 8.22.0's `lib/rtsp.c` (fetched from tag `curl-8_22_0`) has no "Refusing to issue an RTSP request [%s] without a session ID." check: the only setup refusal is SETUP without `Transport`, and `Session:` is sent only when an ID is held. The task's text came from an older libcurl. Moved to Backlog, edited, reclaimed, as `task-board.ps1` prescribes for a criteria change.
- Plan and decisions in ADR-0691. New public types `RtspRequestKind`, `RtspRequestSettings`; `RtspSessionState` made public (new state starts at `CSeq` 0, a new connection moves 0 to 1 as `rtsp_connect` does). `RtspRequestPlan` (internal) decides method, target, `Transport`, `Accept`, body headers, range and default user agent; with no settings it is the tool's `OPTIONS *`.
- Defaults taken: libcurl requests send `Range` only on PLAY/PAUSE/RECORD (8.22.0's `rtsp_setup_request`), while the tool's OPTIONS keeps the measured BL-1293 `Range`; no default `User-Agent` for libcurl; `CSeq` moves on only after a successful send; an empty GET_PARAMETER leaves the reply body unread; a RECEIVE's connection is not kept; a custom `Transport` header stands in for the setting on SETUP.
- Left out and filed as BL-2752: DESCRIBE's `Accept-Encoding` and the SETUP/DESCRIBE time condition headers.
- Measured: `Measure-CodeQuality.ps1 -Library Curl.Protocol.Rtsp.UnitLibrary` - 100% line, 100% branch, max complexity 10, 0 failing members (first run found `Format` at 14 and `StartTransfer` at 12; both split). Fast tests: all green, Rtsp 324 passed / 6 skipped (the existing long-running skips).

## Log

- 2026-10-18: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: 30c46d50 5c61c2c9 2a21c1e2 8cecf683 65359435 4272a6b9.
- 2026-10-18: Doing -> Backlog. Correcting criterion 4: curl 8.22.0's lib/rtsp.c has no 'without a session ID' refusal (fetched from tag curl-8_22_0); reclaimed at once
- 2026-10-18: Backlog -> Doing. Criteria at claim: 30c46d50 5c61c2c9 2a21c1e2 b7e63428 65359435 4272a6b9.
- 2026-10-18: Doing -> Done. RtspProtocolHandler makes every libcurl RTSP request kind from RtspRequestSettings with a handle-lived RtspSessionState, as curl 8.22.0's lib/rtsp.c does; the tool's OPTIONS is unchanged
