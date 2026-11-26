---
id: BL-6030
title: Fix fitness finding EXT-128: percent-escapes that are not valid UTF-8 in extended-protocol URLs are sent literally, so the transfer acts on a different key, topic, subject, queue or body (exit 0)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.Redis.UnitLibrary/Client/RedisUrlCommand.cs, Curl.Extended.Protocol.Mqtt5.UnitLibrary/Client/Mqtt5UrlTopic.cs, Curl.Extended.Protocol.Nats.UnitLibrary/NatsSubject.cs, Curl.Extended.Protocol.Stomp.UnitLibrary/Client/StompProtocolHandler.cs, Curl.Extended.Protocol.Amqp0.UnitLibrary/Client/Amqp0UrlQueue.cs, Curl.Extended.Protocol.Kafka.UnitLibrary/Client/KafkaAddress.cs, Curl.Extended.Protocol.Xmpp.UnitLibrary/Client/XmppProtocolHandler.cs, Curl.Extended.Protocol.Xmpp.UnitLibrary/Client/XmppRequests.cs]
requirement: none
created: 2026-11-26
completed:
---
# BL-6030 — Fix fitness finding EXT-128: percent-escapes that are not valid UTF-8 in extended-protocol URLs are sent literally, so the transfer acts on a different key, topic, subject, queue or body (exit 0)

## Goal

EXT-128 from the outside fitness review of 2026-11-26 07:04 no longer reproduces: percent-escapes that are not valid UTF-8 in extended-protocol URLs are sent literally, so the transfer acts on a different key, topic, subject, queue or body (exit 0). Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-26T0704` (commit `23b62c2`, the `preview` release binaries).

This is an extension (Part B: extended protocols and extension options): it is judged against Curl's own documentation (`curl --extended-help`, `Documentation/Protocols.md`, `Documentation/Conformance/Extended/*.md`, the ADRs), never against C curl.

The reviewer used Python mocks and real servers in Docker (pinned images listed in the review workspace's TestServers.md); rebuild any mock you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Check that it still reproduces on the current branch first.

The finding, verbatim:

- **Severity:** Medium (silent wrong operation; `DEL` removes a different Redis key) · **Confidence:** Confirmed (Redis 4 runs, MQTT 5 2 runs, NATS/STOMP/AMQP/XMPP 1–2 runs) · **Area:** redis, mqtt5, nats, stomp, amqp, xmpp (kafka by code inspection), Windows build.
- **Servers** (all bound to 127.0.0.1): `redis:7.4.12-alpine` on 28379; `eclipse-mosquitto:2.1.2-alpine` on 28883 (`allow_anonymous true`); `nats:2.15.3-alpine --auth s3cret` on 28422; `rabbitmq:4.4.0-alpine` (user `rguest`/`rpass`, `rabbitmq_stomp`) on 28672 (AMQP) and 28613 (STOMP); `ghcr.io/processone/ejabberd:26.09.1` on 28222 (users `bob`/`bobpw`, `carol`/`carolpw`).
- **Repro:**
  1. `curl -sS "redis://127.0.0.1:28379/SET/m2/%FF%00"`, then `redis-cli --no-raw GET m2` → `"%FF\x00"` (`%00` decoded, `%FF` not).
  2. `redis-cli SET 'k%E9' bystander; curl -sS redis://127.0.0.1:28379/DEL/k%E9` → `1`: the key named `k%E9` is deleted.
  3. With `mosquitto_sub -t 'b%FF' -v` running: `curl -sS -d again mqtt5://127.0.0.1:28883/b%FF` → the subscriber prints `b%FF again`, exit 0.
  4. `curl -sS -u s3cret: -d natsmsg nats://127.0.0.1:28422/a%E9` → delivered on the literal subject `a%E9`.
  5. `curl -u rguest:rpass -d smsg stomp://127.0.0.1:28613/queue/s%E9` and `curl -u rguest:rpass -d amsg amqp://127.0.0.1:28672/%2F/q%E9` → `rabbitmqctl list_queues` shows queues `s%E9` and `q%E9`.
  6. `curl -k -u bob:bobpw "xmpp://localhost:28222/carol@localhost?message;body=caf%E9"` → exit 0; carol receives `<body>caf%E9</body>` (the same bytes via `-T` are refused with exit 43).
- **Expected:** the port percent-decodes every other escape (`%20`, `%00`, `%C3%A9`, `%2F`). For binary-safe Redis arguments ("one argument per segment" in `--extended-help`), `%E9` should become the byte 0xE9. For text-only fields (MQTT topic, NATS subject, XMPP body/JID, STOMP destination, AMQP vhost/queue, Kafka topic), the URL should be refused with a message, as the port already does for a NATS subject containing CR LF (exit 3) and for an invalid-UTF-8 XMPP `-T` body (exit 43).
- **Actual (.NET):** exit 0, with the literal text `%E9` / `%FF` used as the name or value.
- **Likely location:** `Uri.UnescapeDataString`, which leaves invalid-UTF-8 escapes undecoded, in `Curl.Extended.Protocol.Redis.UnitLibrary/Client/RedisUrlCommand.cs:31` (should decode to raw bytes), `Curl.Extended.Protocol.Mqtt5.UnitLibrary/Client/Mqtt5UrlTopic.cs:21`, `Curl.Extended.Protocol.Nats.UnitLibrary/NatsSubject.cs:16`, `Curl.Extended.Protocol.Stomp.UnitLibrary/Client/StompProtocolHandler.cs:45`, `Curl.Extended.Protocol.Amqp0.UnitLibrary/Client/Amqp0UrlQueue.cs:25-26`, `Curl.Extended.Protocol.Kafka.UnitLibrary/Client/KafkaAddress.cs:22`, `Curl.Extended.Protocol.Xmpp.UnitLibrary/Client/XmppProtocolHandler.cs:40` and `XmppRequests.cs:29,44`.
- **Acceptance check:** after `curl -sS redis://127.0.0.1:28379/SET/m2/%FF%00`, `redis-cli --no-raw GET m2` prints `"\xff\x00"`; `curl -sS -d x mqtt5://127.0.0.1:28883/b%FF` exits non-zero naming invalid UTF-8 in the topic; the XMPP command exits 43.

## Acceptance criteria

- [ ] after `curl -sS redis://127.0.0.1:28379/SET/m2/%FF%00`, `redis-cli --no-raw GET m2` prints `"\xff\x00"`; `curl -sS -d x mqtt5://127.0.0.1:28883/b%FF` exits non-zero naming invalid UTF-8 in the topic; the XMPP command exits 43.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-26: Created from fitness review 2026-11-26T0704.
