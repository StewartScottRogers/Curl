---
id: BL-4903
title: Make -T upload or refuse on stomp, nats, redis, mqtt5, kafka, amqp and xmpp instead of receiving
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.Nats.UnitLibrary, Curl.Extended.Protocol.Stomp.UnitLibrary, Curl.Extended.Protocol.Redis.UnitLibrary, Curl.Extended.Protocol.Mqtt5.UnitLibrary, Curl.Extended.Protocol.Kafka.UnitLibrary, Curl.Extended.Protocol.Amqp0.UnitLibrary, Curl.Extended.Protocol.Xmpp.UnitLibrary]
requirement: none
created: 2026-11-11
completed:
---
# BL-4903 — Make -T upload or refuse on stomp, nats, redis, mqtt5, kafka, amqp and xmpp instead of receiving

## Goal

`-T file` on each of these schemes publishes the file's bytes (as `-d` does), or fails at once with exit 2 before connecting; it never subscribes, fetches or consumes.

## Context

From the outside fitness review of 2026-11-11 (commit 6ce7cfcf, the `preview` `curl-win-x64.zip`, Windows 11 x64; REF = curl 8.22.0 Schannel), finding EXT-008 (Medium (wrong operation), confirmed: reproduced at least twice).

- Repro: NATS capture mock on 18602 (sends INFO, logs everything); `up.txt` holds `filebody`. `curl -sS -m 3 -T up.txt nats://127.0.0.1:18602/foo`
- Expected: curl's `-T` means upload; the port's amqp1 already accepts "-d or -T". The server receives `PUB foo 8` and `filebody`.
- Actual: The server receives `SUB foo 1` and curl waits until `-m`. stomp subscribes, mqtt5 subscribes, kafka fetches, amqp does `basic.get` (consuming a message the user meant to send), redis sends `SET kk` with no value, xmpp sends a disco query.
- Where to start: Those handlers read only `context.PostData`; only `Curl.Extended.Protocol.Amqp1.UnitLibrary` also reads `context.Upload`. Too big for one run: split it per scheme (one task per library) at claim time.

## Acceptance criteria

- [ ] For each of nats, stomp, redis, mqtt5, kafka, amqp and xmpp, `-T up.txt` either sends `filebody` as the publish payload or exits 2 before connecting; a unit test per scheme pins it.
- [ ] Each scheme's conformance document states what `-T` does.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). Measure real curl (the platform build) before pinning any output text, and add a focused unit test in the fixed library's own `.UnitTests` project.

## Log

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing. Criteria at claim: b4cfa0fa 6e5cc0f2 a0b49a29.
