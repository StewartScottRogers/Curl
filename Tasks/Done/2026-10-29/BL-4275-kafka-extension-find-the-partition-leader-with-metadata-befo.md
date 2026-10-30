---
id: BL-4275
title: Kafka extension: find the partition leader with Metadata before produce and fetch
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.Kafka.UnitLibrary/Client/KafkaProtocolHandler.cs, Curl.Extension.Kafka.UnitLibrary/Client/KafkaClientFlow.cs, Curl.Extension.Kafka.UnitLibrary/Wire/KafkaMetadata, Curl.Extension.Kafka.UnitTests/Client/KafkaProtocolHandlerTests.cs, Curl.Extension.Kafka.UnitTests/Wire/KafkaMetadata, Curl.Extension.Kafka.UnitLibrary/Wire/KafkaRequests.cs, Curl.Extension.Kafka.UnitTests/Wire/KafkaTestBytes.cs, Curl.Extension.Kafka.UnitTests/Client/ScriptedKafkaConnector.cs, Curl.Extension.Kafka.UnitLibrary/CLAUDE.md, Documentation/Planning/Decisions/ADR-1076-kafka-produces-one-record-with-a-body-and-consumes-to-the-high-watermark-without-one.md]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4275 — Kafka extension: find the partition leader with Metadata before produce and fetch

## Goal

`kafka://` produces to and fetches from the partition's leader even when the URL names another broker of the cluster.

## Context

Follow-up of BL-4121 (ADR-1076): the client sends Produce and Fetch to the URL's broker, which answers NOT_LEADER_OR_FOLLOWER (6) when it does not lead the partition. Send Metadata (the oldest version Kafka 4.0 accepts, non-flexible) first, read the partition's leader and its host and port, and reconnect through the connector to the leader when it is another broker.

## Acceptance criteria

- [x] With the URL's broker not the leader, the transfer connects to the leader Metadata names and succeeds; unit test over scripted connections.
- [x] An unknown topic in Metadata ends with exit 8 naming UNKNOWN_TOPIC_OR_PARTITION.
- [x] ADR-1076's "Left out" is updated or superseded.
- [x] The library meets the four quality gates and the fast tests pass.

## Notes

- Plan: Metadata v4 (oldest Kafka 4.0 accepts, non-flexible) for the URL topic on the URL broker, `allow_auto_topic_creation` false; same host and port continues on that connection, otherwise the handler reconnects through the connector to the advertised leader with the URL TLS choice and the transfer proxy and logs. Decided in ADR-1080; ADR-1076 "Left out" now points to it.
- Touches widened (no Doing task on origin held them): KafkaRequests.cs (the Metadata request beside Produce and Fetch), KafkaTestBytes.cs (Metadata response builder), ScriptedKafkaConnector.cs (several connections in turn), the library CLAUDE.md and ADR-1076 (criterion 3).
- Measured: Measure-CodeQuality for Curl.Extension.Kafka.UnitLibrary 100% line, 100% branch, 0 failing members, worst CRAP 8. Full build clean, fast tests green (72 test assemblies passed). Tests use scripted connections only, no paths or OS text, so they are platform-neutral.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 0d2683f0 ec52556a 615ff7b1 dc514177.
- 2026-10-29: Doing -> Done. kafka:// produces to and fetches from the partition leader Metadata names, reconnecting when the URL's broker does not lead it
