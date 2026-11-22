---
id: BL-5745
title: Make a Curl.Api POST with a body to mqtt5:// publish, not subscribe (BL-5681 missed it)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.Mqtt5.UnitLibrary, Curl.Api/RequestBodies/UploadSchemes.cs, Curl.Api.UnitTests/RequestBodies/UploadSchemeBodyTests.cs, Curl.Engine.UnitLibrary/ManagedEasyTransfer.Context.cs, Curl.Engine.UnitTests/ManagedEasyTransferTests.Mqtt.cs]
requirement: none
created: 2026-11-22
completed: 2026-11-22
---
# BL-5745 — Make a Curl.Api POST with a body to mqtt5:// publish, not subscribe (BL-5681 missed it)

## Goal

A `Curl.Api` `POST` with a body to `mqtt5://host/topic` publishes the body to the topic, as `curl -d` does, and sends no SUBSCRIBE.

## Context

From Surly cookbook bug report `2026-11-22T0726-Curl-bug-report.md` (batch 10 of the 1,000-recipe plan), finding 1. Build tested: Curl `c84035f797`, Windows 11; server Surl `5da5b3eaf`. Check it still reproduces on the current branch first.

BL-5681 (Done 2026-11-21) made a `Curl.Api` body publish on amqp, amqp1 and mqtt5, and the build tested contains it, yet `new CurlRequest(HttpMethod.Post, "mqtt5://127.0.0.1:1883/sensors/t") { Body = CurlContent.FromString("21.5") }` against `surl mqtt://127.0.0.1:1883/` still sends a SUBSCRIBE and publishes nothing (surl's log). The cookbook recipe `mqtt5-subscribe-over-websocket-in-csharp` works around it by publishing through `mqtt://`. Read BL-5681's notes and test: find why mqtt5 still decides "subscribe" for a `Curl.Api` body (a different decision path from the one BL-5681 changed, or a test that pinned another form of the body), and fix it. Check `mqtt5s://` and `mqtt5+ws://` too.

## Acceptance criteria

- [x] Against Surl's MQTT listener (or the MQTT 5 test double), a `Curl.Api` `POST` with a string body to `mqtt5://host/topic` publishes it and sends no SUBSCRIBE; a subscriber receives it. The Notes record the run.
- [x] A unit test pins that a `Curl.Api` byte, string and stream body each choose publish for `mqtt5://`, `mqtt5s://` and `mqtt5+ws://`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

Reproduced on the current branch with a file-based app (loopback fake broker + `CurlClient.Create()`): a `Curl.Api` POST with a bytes body to `mqtt5://` already sends PUBLISH (packet type 3), no SUBSCRIBE, so BL-5681's fix holds for mqtt5:// and mqtt5s:// (there is no `CurlContent.FromString`; a string is sent as bytes). The real gaps:
- `mqtt5+ws://` was missing from `Curl.Api/RequestBodies/UploadSchemes.cs`, so its body became HTTP post fields; the engine passed `PostFields` on only for mqtt/mqtts, so the handler saw no payload and SUBSCRIBEd. Added `mqtt5+ws`.
- `ManagedEasyTransfer.Context.cs` `IsMqttUrl` now also names mqtt5, mqtt5s and mqtt5+ws, so `-d`-style post fields on those publish too (a second route to the same wrong subscribe).
- Surl was not run in a lane; the fake-broker run above is the record (PUBLISH seen, no SUBSCRIBE).
- touches widened to Curl.Api (UploadSchemes.cs, its test) and Curl.Engine.UnitLibrary (ManagedEasyTransfer.Context.cs, its test): the Mqtt5 library needed no change; no other Doing task holds them.
- Tests: `SendAsync_PostedBytesAndStreamBodiesToMqtt5_PublishAsAnUpload` (mqtt5, mqtt5s, mqtt5+ws; bytes and stream; a string is bytes), `TransferSettingsOf_PostFieldsOnAnMqtt5Url_PublishesThem`. Measure-CodeQuality for Curl.Engine.UnitLibrary and Curl.Api.UnitLibrary: 0 failing members. Fast tests pass in both test projects. Linux/macOS: platform-neutral.

## Log

- 2026-11-22: Created from Surly bug report 2026-11-22T0726, finding 1.
- 2026-11-22: Backlog -> Doing. Criteria at claim: ba15cb0c 48d2aff2 3d836d5e.
- 2026-11-22: Doing -> Done. mqtt5+ws body publishes; engine passes post fields for mqtt5 family
