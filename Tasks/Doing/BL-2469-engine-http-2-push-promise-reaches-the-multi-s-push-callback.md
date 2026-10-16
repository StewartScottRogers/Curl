---
id: BL-2469
title: Engine: HTTP/2 PUSH_PROMISE reaches the multi's push callback
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2428]
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

- [ ] `Curl.Protocol.Http.UnitTests` pins a `PUSH_PROMISE` handed to a push sink with its headers, and a refused one answered with `RST_STREAM` `CANCEL`.
- [ ] `Curl.Engine.UnitTests` pins an accepted push's transfer receiving the pushed body, and `ErrorOut` ending the parent with `CurlCode.Http2`.
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary,Curl.Engine.UnitLibrary` reports 0 failing members.
- [ ] ADR-0605 and `Curl.Engine.UnitLibrary/CLAUDE.md` say a server push reaches the callback.

## Notes

## Log

- 2026-10-16: Created.
- 2026-10-16: Backlog -> Doing.
