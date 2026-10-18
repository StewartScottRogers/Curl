---
id: BL-2704
title: Carry the CURLOPT_RTSP_* options into the RTSP handler so upstream cases 567 to 572, 577 and 689 pass
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2617, BL-2749]
touches: [Libcurl, Libcurl.UnitTests, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Protocol.Rtsp.UnitLibrary, Curl.Protocol.Rtsp.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-17
completed:
---
# BL-2704 — Carry the CURLOPT_RTSP_* options into the RTSP handler so upstream cases 567 to 572, 577 and 689 pass

## Goal

A `Libcurl` easy perform of an `rtsp://` URL sends the request `CURLOPT_RTSP_REQUEST` names (OPTIONS, DESCRIBE, ANNOUNCE, SETUP, PLAY, PAUSE, TEARDOWN, GET_PARAMETER, SET_PARAMETER, RECORD, RECEIVE) to `CURLOPT_RTSP_STREAM_URI`, with `CURLOPT_RTSP_TRANSPORT`, `CURLOPT_RTSP_SESSION_ID`, `CURLOPT_RTSP_CLIENT_CSEQ`, `CURLOPT_RTSPHEADER` and the upload / post body, keeps the CSeq and session across performs on one handle, and hands interleaved RTP to `CURLOPT_INTERLEAVEFUNCTION`, as libcurl 8.22.0's `lib/rtsp.c` does.

## Context

Found by BL-2617, which built the `rtspd` stand-in (`RtspServerConnector`, ADR-0674), so the RTSP libtest cases now run through the case runner with the ported `Lib567` to `Lib572` (`Curl.Conformance.LibTest.UnitLibrary`). Every one of them differs in the client: `Libcurl/Options/Protocols/EasyProtocolOptionSetters.cs` stores the options in `EasyRtspOptions`, but `Curl.Protocol.Rtsp.UnitLibrary/RtspProtocolHandler.cs` only knows the tool's transfer, `OPTIONS *` (`OptionsTarget`) then the URL. Measured on Windows, 2026-10-17, first differences:

- test567, test577, test689: `<verify><protocol>` line 1, expected `OPTIONS rtsp://127.0.0.1:8996/567 RTSP/1.0` (577: `/577`, 689: `/689`), got `OPTIONS * RTSP/1.0`.
- test568: line 1, expected `ANNOUNCE rtsp://127.0.0.1:8996/5680001 RTSP/1.0`, got `OPTIONS * RTSP/1.0`.
- test569: line 1, expected `SETUP rtsp://127.0.0.1:8996/5690001 RTSP/1.0`, got `OPTIONS * RTSP/1.0`.
- test570: line 1, expected `OPTIONS rtsp://127.0.0.1:8996/5700001 RTSP/1.0`, got `OPTIONS * RTSP/1.0`.
- test571: `<verify><stdout>` line 1, expected `RTP: message size 10, channel 1`, got nothing (no SETUP / PLAY, so no interleaved packet reaches the callback).
- test572: line 1, expected `SETUP rtsp://127.0.0.1:8996/5720001 RTSP/1.0`, got `OPTIONS * RTSP/1.0`.

Debug one with `CURL_UPSTREAM_CASE=<n>` and `dotnet test Curl.Conformance.UnitTests --filter "TestCategory=Manual"`, or all with `--filter "TestCategory=rtsp"`. test3100 is BL-2699's. The tool's own RTSP transfer (`curl rtsp://...`) must not change. If the engine wiring sits in another project (the `Libcurl` engine's transfer request in `Curl.Api.UnitLibrary`, say), add it to `touches` by the lane rules.

## Acceptance criteria

- [ ] Each of cases 567, 568, 569, 570, 571, 572, 577 and 689 that passes through the runner on Windows is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; one that still fails is filed with the board script with its differing lines.
- [ ] Each RTSP request kind and option the work carries has a focused unit test in `Curl.Protocol.Rtsp.UnitTests` or `Libcurl.UnitTests` (ADR-0482).
- [ ] No added case's output depends on the platform.
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.

## Notes

- 2026-10-18 (lane 3, first-pass sizing, no code edited): the Libcurl perform reaches the RTSP handler through `Curl.Engine.UnitLibrary` (`ManagedCurlEngine.CreateDispatcher` -> `EngineComposition.CreateProtocolHandlers`, which already passes the interleave sink), not through `Libcurl`, so `Curl.Engine.UnitLibrary` and `Curl.Engine.UnitTests` are added to `touches`. BL-2735 (in Doing) touches `Curl.Engine.UnitLibrary`, so the task goes back to Backlog until they no longer overlap.
- The handler today knows only `OPTIONS *` with a per-transfer `RtspSessionState`; carrying every request kind, the stream URI, `Transport`, session ID, client `CSeq`, body and a handle-lived session is its own sizeable piece, split out as BL-2749 (Rtsp library only, ready now). What is left here: map `EasyRtspOptions` and the upload / post body to BL-2749's settings in the engine, keep the session state on the easy handle across performs, run the eight cases and add the passing ones to the ratchet.

## Log

- 2026-10-17: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: 67d405dd 74e45cb9 21e5ddeb 4272a6b9.
- 2026-10-18: Doing -> Backlog. Waits on BL-2749 (handler surface for libcurl RTSP requests) and overlaps BL-2735 on Curl.Engine.UnitLibrary
