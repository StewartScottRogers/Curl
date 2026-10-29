---
id: BL-4276
title: Kafka extension: authenticate with SASL PLAIN from --user
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4275]
touches: [Curl.Extension.Kafka.UnitLibrary/Client/KafkaProtocolHandler.cs, Curl.Extension.Kafka.UnitLibrary/Client/KafkaClientFlow.cs, Curl.Extension.Kafka.UnitLibrary/Wire/KafkaSasl, Curl.Extension.Kafka.UnitTests/Client/KafkaProtocolHandlerTests.cs, Curl.Extension.Kafka.UnitTests/Wire/KafkaSasl, Documentation/Planning/Decisions/ADR-1076-kafka-produces-one-record-with-a-body-and-consumes-to-the-high-watermark-without-one.md, Curl.Extension.Kafka.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4276 — Kafka extension: authenticate with SASL PLAIN from --user

## Goal

`curl -u user:password kafka://broker/topic` authenticates with SASL PLAIN (SaslHandshake and SaslAuthenticate) before producing or fetching.

## Context

Follow-up of BL-4121 (ADR-1076): no SASL is offered, so a broker that demands it closes the connection. Kafka's SASL PLAIN is RFC 4616 inside SaslAuthenticate; `--user` maps onto it as STOMP's `--user` maps onto login and passcode. Depends on BL-4275 since both change the handler's connection flow.

## Acceptance criteria

- [x] With `--user`, the client sends SaslHandshake naming PLAIN and SaslAuthenticate with the RFC 4616 message before its first request; unit test over a scripted connection.
- [x] A failed authentication ends with exit 67 (CURLE_LOGIN_DENIED) and the broker's error message.
- [x] ADR-1076's "Left out" is updated or superseded.
- [x] The library meets the four quality gates and the fast tests pass.

## Notes

- Touches widened by ADR-1076 (criterion 3 updates its Left out) and the library CLAUDE.md (lists Wire/KafkaSasl); no other task in Doing names either.
- Delivered directly rather than through the full /feature agents: a small change in one library with a clear spec. Decided in ADR-1081: SaslHandshake v1 and SaslAuthenticate v0 (last non-flexible versions), on every connection including the leader ADR-1080 reconnects to; a handshake error and an authentication error both end with exit 67, the latter with the broker's error_message.
- Kafka tests 133 passed; Measure-CodeQuality for Curl.Extension.Kafka.UnitLibrary: 100% line, 100% branch, max complexity 10, 0 failing members. Platform-neutral (no paths, OS text or certificates).

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 6dd0b567 1112e3d2 615ff7b1 dc514177.
- 2026-10-29: Doing -> Done. kafka:// and kafkas:// with --user authenticate with SASL PLAIN; refusal ends with exit 67 and the broker's message
