---
id: BL-5320
title: Publish and get on existing and new RabbitMQ 4 queues over amqp:// with a passive declare first
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.Amqp0.UnitLibrary/Client/Amqp0ClientFlow.cs, Curl.Extended.Protocol.Amqp0.UnitLibrary/Methods/Amqp0QueueDeclare.cs, Curl.Extended.Protocol.Amqp0.UnitTests]
requirement: none
created: 2026-11-16
completed:
---
# BL-5320 — Publish and get on existing and new RabbitMQ 4 queues over amqp:// with a passive declare first

## Goal

`amqp://` publishes to and gets from a queue on a default RabbitMQ 4 broker: an existing durable queue is used as it is, and a missing queue is created as a durable queue, instead of every run failing with exit 8 on the queue declare.

## Context

From the outside fitness review of 2026-11-16 (commit 64e9144, `preview` build), finding EXT-065 (Medium, Confirmed, 5 runs). Area: amqp, amqps; Windows. This is an extension, so it is judged against the port's own docs and ADRs, not curl.

**Repro.**
1. `docker run -d --name rmq -p 127.0.0.1:36672:5672 -e RABBITMQ_DEFAULT_USER=u -e RABBITMQ_DEFAULT_PASS=p rabbitmq:4-alpine`
2. `curl -sS -m 5 -u u:p -d hello amqp://127.0.0.1:36672/%2F/q1`
3. Declare a durable queue `dq` (e.g. pika `channel.queue_declare('dq', durable=True)`) and publish a message to it, then `curl -sS -m 5 -u u:p amqp://127.0.0.1:36672/%2F/dq`

**Expected.** `--extended-help`: "Publishes or gets one message over AMQP 0-9-1 (RabbitMQ): … the queue is declared, -d publishes the data … basic.get writes one message's body". Protocols.md: "Publishes or gets one message on a RabbitMQ queue." Step 2 publishes and exits 0; step 3 writes the message.

**Actual.** Step 2: `curl: (8) AMQP server sent connection.close: reply code 541, INTERNAL_ERROR - Feature 'transient_nonexcl_queues' is deprecated. By default, this feature is not permitted anymore…`, exit 8 (amqps the same). Step 3: `curl: (8) AMQP server sent channel.close: reply code 406, PRECONDITION_FAILED - inequivalent arg 'durable' for queue 'dq' in vhost '/': received 'false' but current is 'true'`, exit 8. Login, vhost-not-found (530), TLS (60) and bad credentials (67) behave correctly.

**Where to start.** `Curl.Extended.Protocol.Amqp0.UnitLibrary/Client/Amqp0ClientFlow.cs:63` always sends `new Amqp0QueueDeclare(queue.Queue)` with passive=false, durable=false (`Methods/Amqp0QueueDeclare.cs`). Declare passively first and use the queue as it is; only on 404 NOT_FOUND (which closes the channel) reopen the channel and declare it durable (or skip the declare for basic.get). Update the `--extended-help` text and the AMQP doc under `Documentation/` if they state how the queue is declared.

## Acceptance criteria

- [x] New tests in `Curl.Extended.Protocol.Amqp0.UnitTests` (a scripted in-memory frame channel, no real broker) prove: the first `queue.declare` sent is passive; when the passive declare succeeds the client does not send a non-passive declare and goes on to publish or get.
- [x] A test proves that on a passive-declare `channel.close` with reply code 404 (NOT_FOUND) the client reopens a channel, sends a durable `queue.declare` (passive=false, durable=true) and then publishes.
- [x] A test proves a `channel.close` with any other reply code (for example 406 or 403) on the passive declare still ends in the existing exit 8 message, and that `basic.get` of an existing durable queue never sends durable=false.
- [x] Tests are platform-neutral.
- [x] Review acceptance check recorded in the Log as a hand check: against `rabbitmq:4-alpine`, step 2 exits 0 and a following `curl -u u:p amqp://127.0.0.1:36672/%2F/q1` prints `hello`; step 3 prints the queued message and exits 0. A real RabbitMQ broker cannot run in a lane; the Log says so when not run.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers must be PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md).
- Review acceptance check: against `rabbitmq:4-alpine`, step 2 exits 0 and a following `curl -u u:p amqp://127.0.0.1:36672/%2F/q1` prints `hello`; step 3 prints the queued message and exits 0.

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing. Criteria at claim: 645cd3ad e1978299 82c723f0 c1cf21f9 175554a5 a0b49a29.
- 2026-11-16: Done. Real RabbitMQ hand check NOT run (no broker in a lane); behaviour covered by scripted-frame tests. ADR-1284. Quality run: Amqp0 library one branch gap fixed by a test. Fast Amqp0 tests 119 pass.
