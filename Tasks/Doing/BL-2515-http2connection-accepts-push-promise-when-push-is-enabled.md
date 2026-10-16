---
id: BL-2515
title: Http2Connection accepts PUSH_PROMISE when push is enabled
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Http2.UnitLibrary, Curl.Http2.UnitTests]
requirement: none
created: 2026-10-16
completed:
---
# BL-2515 — Http2Connection accepts PUSH_PROMISE when push is enabled

## Goal

An `Http2Connection` built with push enabled advertises `SETTINGS_ENABLE_PUSH` 1, accepts a server's `PUSH_PROMISE` on a stream it opened, reserves the promised (even) stream so its HEADERS and DATA are received like any stream's, and can reset it with `RST_STREAM`; one built without push behaves exactly as now (ENABLE_PUSH 0, PUSH_PROMISE a PROTOCOL_ERROR).

## Context

Split out of BL-2469 (server push to the engine multi's push callback). curl 8.22.0's `lib/http2.c` sends `SETTINGS_ENABLE_PUSH` as `data->multi->push_cb != NULL` (`populate_settings`), so the frame layer needs a per-connection switch. Today `Curl.Http2.UnitLibrary/Http2Connection.cs` has a static `ClientSettings` with ENABLE_PUSH 0, `ReceiveFrameAsync` throws PROTOCOL_ERROR on any PUSH_PROMISE, and `IsIdle` treats every even stream as idle (so RST_STREAM on a promised stream throws). `Http2PushPromisePayload` and `Http2FramePayloadParser` already parse the payload. Keep the existing public surface working: `Curl.Protocol.Http`'s `Http2Session.UpgradeSettings` reads `ClientSettings`.

Suggested shape (decide and record in an ADR if it differs): a constructor overload or init property `IsPushEnabled`; an instance `Settings` list sent by `SendPrefaceAsync` (ENABLE_PUSH 1 when enabled, `ClientSettings` unchanged as the push-off list); a PUSH_PROMISE whose header block may span CONTINUATION frames is returned from `ReadFrameAsync` as an `Http2StreamFrame` of type `PushPromise` on the parent stream with the whole header block as `Content` and the promised stream identifier (a new optional `PromisedStreamId` on the record, or a derived record); the promised stream is registered half closed (local), counted apart from `OpenStreamCount`'s client streams for SETTINGS_MAX_CONCURRENT_STREAMS; a PUSH_PROMISE on a stream the client did not open, an odd or non-increasing promised id, or one while push is off is a PROTOCOL_ERROR (RFC 9113 section 6.6, 8.4).

## Acceptance criteria

- [ ] `Curl.Http2.UnitTests` pins the preface's SETTINGS with ENABLE_PUSH 1 when push is enabled, and 0 (unchanged bytes) when not.
- [ ] `Curl.Http2.UnitTests` pins a PUSH_PROMISE (one frame, and one continued by CONTINUATION) returned with its parent stream, promised stream and header block, then the promised stream's HEADERS and DATA returned for that stream.
- [ ] `Curl.Http2.UnitTests` pins `ResetStreamAsync` on a promised stream writing RST_STREAM, and later frames on it dropped.
- [ ] `Curl.Http2.UnitTests` pins PROTOCOL_ERROR for PUSH_PROMISE with push off, on an unopened parent stream, and with an odd promised stream id.
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Http2.UnitLibrary` reports 0 failing members.

## Notes

## Log

- 2026-10-16: Created.
- 2026-10-16: Backlog -> Doing. Criteria at claim: 32289025 4d9e7b43 65020420 9e5bf8e0 4272a6b9 5f66a896.
