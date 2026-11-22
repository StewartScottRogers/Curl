---
id: BL-5745
title: Make a Curl.Api POST with a body to mqtt5:// publish, not subscribe (BL-5681 missed it)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.Mqtt5.UnitLibrary]
requirement: none
created: 2026-11-22
completed:
---
# BL-5745 — Make a Curl.Api POST with a body to mqtt5:// publish, not subscribe (BL-5681 missed it)

## Goal

A `Curl.Api` `POST` with a body to `mqtt5://host/topic` publishes the body to the topic, as `curl -d` does, and sends no SUBSCRIBE.

## Context

From Surly cookbook bug report `2026-11-22T0726-Curl-bug-report.md` (batch 10 of the 1,000-recipe plan), finding 1. Build tested: Curl `c84035f797`, Windows 11; server Surl `5da5b3eaf`. Check it still reproduces on the current branch first.

BL-5681 (Done 2026-11-21) made a `Curl.Api` body publish on amqp, amqp1 and mqtt5, and the build tested contains it, yet `new CurlRequest(HttpMethod.Post, "mqtt5://127.0.0.1:1883/sensors/t") { Body = CurlContent.FromString("21.5") }` against `surl mqtt://127.0.0.1:1883/` still sends a SUBSCRIBE and publishes nothing (surl's log). The cookbook recipe `mqtt5-subscribe-over-websocket-in-csharp` works around it by publishing through `mqtt://`. Read BL-5681's notes and test: find why mqtt5 still decides "subscribe" for a `Curl.Api` body (a different decision path from the one BL-5681 changed, or a test that pinned another form of the body), and fix it. Check `mqtt5s://` and `mqtt5+ws://` too.

## Acceptance criteria

- [ ] Against Surl's MQTT listener (or the MQTT 5 test double), a `Curl.Api` `POST` with a string body to `mqtt5://host/topic` publishes it and sends no SUBSCRIBE; a subscriber receives it. The Notes record the run.
- [ ] A unit test pins that a `Curl.Api` byte, string and stream body each choose publish for `mqtt5://`, `mqtt5s://` and `mqtt5+ws://`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-22: Created from Surly bug report 2026-11-22T0726, finding 1.
- 2026-11-22: Backlog -> Doing. Criteria at claim: ba15cb0c 48d2aff2 3d836d5e.
