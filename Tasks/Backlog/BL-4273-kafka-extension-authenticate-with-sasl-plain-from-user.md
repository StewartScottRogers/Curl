---
id: BL-4273
title: Kafka extension: authenticate with SASL PLAIN from --user
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4272]
touches: [Curl.Extension.Kafka.UnitLibrary/Client/KafkaProtocolHandler.cs, Curl.Extension.Kafka.UnitLibrary/Client/KafkaClientFlow.cs, Curl.Extension.Kafka.UnitLibrary/Wire/KafkaSasl, Curl.Extension.Kafka.UnitTests/Client/KafkaProtocolHandlerTests.cs, Curl.Extension.Kafka.UnitTests/Wire/KafkaSasl]
requirement: none
created: 2026-10-29
completed:
---
# BL-4273 — Kafka extension: authenticate with SASL PLAIN from --user

## Goal

`curl -u user:password kafka://broker/topic` authenticates with SASL PLAIN (SaslHandshake and SaslAuthenticate) before producing or fetching.

## Context

Follow-up of BL-4121 (ADR-1069): no SASL is offered, so a broker that demands it closes the connection. Kafka's SASL PLAIN is RFC 4616 inside SaslAuthenticate; `--user` maps onto it as STOMP's `--user` maps onto login and passcode. Depends on BL-4272 since both change the handler's connection flow.

## Acceptance criteria

- [ ] With `--user`, the client sends SaslHandshake naming PLAIN and SaslAuthenticate with the RFC 4616 message before its first request; unit test over a scripted connection.
- [ ] A failed authentication ends with exit 67 (CURLE_LOGIN_DENIED) and the broker's error message.
- [ ] ADR-1069's "Left out" is updated or superseded.
- [ ] The library meets the four quality gates and the fast tests pass.

## Notes

## Log

- 2026-10-29: Created.
