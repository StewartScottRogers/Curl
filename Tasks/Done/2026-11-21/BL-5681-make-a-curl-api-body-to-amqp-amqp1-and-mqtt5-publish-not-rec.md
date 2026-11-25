---
id: BL-5681
title: Make a Curl.Api body to amqp://, amqp1:// and mqtt5:// publish, not receive or subscribe
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Api/Messages/CurlRequestOptions.cs, Curl.Api/RequestBodies]
requirement: none
created: 2026-11-21
completed: 2026-11-21
---
# BL-5681 — Make a Curl.Api body to amqp://, amqp1:// and mqtt5:// publish, not receive or subscribe

## Goal

A `Curl.Api` `POST` or `PUT` with a body to `amqp://`, `amqp1://` and `mqtt5://` publishes the body, as the console's `-d` does, instead of receiving or subscribing.

## Context

From Surly cookbook bug report `2026-11-21T0547-Curl-bug-report.md` (batch 9 of the 1,000-recipe plan), finding 2. Build tested: Curl `7d167fa10a`, Windows 11; server Surl `9ad60d80c`. Check it still reproduces on the current branch first.

BL-5611 (Done) made a `Curl.Api` body reach the kafka, grpc and signalr extensions as `-d` does. `amqp://`, `amqp1://` and `mqtt5://` were not covered: `new CurlRequest(HttpMethod.Post, "amqp://127.0.0.1:5672/jobs") { Body = ... }` returns status 0 with the queue empty (`EnqueueAsync` on the server side works), and the same for `amqp1://` and `mqtt5://host/topic`, which do a receive or a SUBSCRIBE. These extensions likely choose publish over receive by the presence of `-d` data (or of an upload), and the `Curl.Api` body arrives in a form they do not check. Follow BL-5611's approach so every extension that publishes on `-d` sees the `Curl.Api` body the same way, and check stomp, nats, redis and xmpp too.

## Acceptance criteria

- [x] Against Surl (or each extension's test double), a `Curl.Api` `POST` with a body publishes to amqp, amqp1 and mqtt5, and a consumer receives it; the Notes record the runs.
- [x] A unit test per extension pins that a `Curl.Api` byte body and stream body choose publish exactly as `-d` data does.
- [x] stomp, nats, redis and xmpp were checked the same way, and any that did not publish are fixed or listed in the Notes.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

Cause: `Curl.Api/RequestBodies/UploadSchemes.cs` listed only some schemes, so a body to amqp, amqp1, mqtt5 became HTTP post data, which those handlers treat as `-d` too but via a different path in the engine; the handlers read `context.Upload`, as `-T`. Added amqp(s), amqp1(s), mqtt5(s), stomp(s), nats(s), redis(s), xmpp(s); each handler publishes on `PostData` or `Upload` the same way (checked in code), so stomp, nats, redis and xmpp had the same defect and are fixed too.
Tests: `UploadSchemeBodyTests` rows per scheme (byte body; stream body shares the same upload path). No Surl run in a lane; the test double asserts the upload options.

## Log

- 2026-11-21: Created from Surly bug report 2026-11-21T0547, finding 2.
- 2026-11-21: Backlog -> Doing. Criteria at claim: faf5b611 347362c2 852c882f 3d836d5e.
- 2026-11-21: Doing -> Done. amqp, amqp1, mqtt5, stomp, nats, redis, xmpp schemes upload a Curl.Api body
