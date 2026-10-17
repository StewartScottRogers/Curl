---
id: BL-2469
title: Engine: HTTP/2 PUSH_PROMISE reaches the multi's push callback
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2428, BL-2515, BL-2516]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-16
completed:
---
# BL-2469 — Engine: HTTP/2 PUSH_PROMISE reaches the multi's push callback

## Goal

An HTTP/2 server's `PUSH_PROMISE` on a transfer run by an engine multi handle with `MultiOptions.Push` set reaches `ManagedMultiTransfer.OfferPush` with the promise's headers as `name: value` lines, a pushed transfer duplicated from the parent receives the pushed stream's response, and a refused push is reset with `RST_STREAM` (`CANCEL`); with no push callback the client keeps advertising `SETTINGS_ENABLE_PUSH` 0 as now.

## Context

BL-2428 (ADR-0605) built the engine side: `ManagedMultiTransfer.OfferPush` answers `CurlPushResult.Ok` (the pushed transfer is added), `Deny`, or `ErrorOut` (the parent ends with `CurlCode.Http2`). Nothing in `Curl.Protocol.Http.UnitLibrary`'s HTTP/2 client calls it yet. Follow curl 8.22.0's `lib/http2.c` (`push_promise`, `on_frame_recv`) for when push is enabled and how the pushed stream is bound to its new easy handle. Tests drive the HTTP/2 client through a scripted connection, never the network.

## Acceptance criteria

- [ ] `Curl.Engine.UnitTests` pins a server's `PUSH_PROMISE` on a multi transfer with `MultiOptions.Push` reaching the callback with its headers, through the push sink BL-2516 gives the HTTP handler (pinning the frame layer and session themselves is BL-2515's and BL-2516's).
- [ ] `Curl.Engine.UnitTests` pins an accepted push's transfer receiving the pushed body, and `ErrorOut` ending the parent with `CurlCode.Http2`.
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary,Curl.Engine.UnitLibrary` reports 0 failing members.
- [ ] ADR-0605 and `Curl.Engine.UnitLibrary/CLAUDE.md` say a server push reaches the callback.

## Notes

- 2026-10-16 (lane 2, first claim): split before any code. The work spans three libraries: the frame layer (`Curl.Http2.UnitLibrary`'s `Http2Connection` rejects every PUSH_PROMISE as a PROTOCOL_ERROR and always sends ENABLE_PUSH 0, and `Curl.Http2.UnitLibrary` was not in `touches`), the HTTP session and handler (a push sink and the pushed response), and the engine wiring here. Each needs its own Measure-CodeQuality run (30-45 minutes each while lanes build), which does not fit one 120-minute run. Filed BL-2515 (frame layer) and BL-2516 (`Http2Session` and the handler's push sink, which now carries this task's first criterion). This task keeps the engine side: `EngineComposition.CreateProtocolHandlers` gives the HTTP handler a push sink that, for a transfer in a multi handle with `MultiOptions.Push`, duplicates the parent, binds the pushed response to the duplicate and calls `ManagedMultiTransfer.OfferPush` (refusing on `Deny` and `ErrorOut`), and the pushed `ManagedEasyTransfer`'s perform reads that response to its header and write callbacks instead of sending a request. The HTTP projects stay in `touches` only for the handler-constructor parameter BL-2516 adds; drop them if that lands complete.

## Log

- 2026-10-16: Created.
- 2026-10-16: Backlog -> Doing.
- 2026-10-16: Doing -> Backlog. Split: waits on BL-2515 (Http2Connection accepts PUSH_PROMISE) and BL-2516 (Http2Session push sink); engine wiring stays here
- 2026-10-17: Backlog -> Doing. Criteria at claim: 97709c31 3c10b8a9 4272a6b9 ab273712 53ae3210.
