---
id: BL-4272
title: Kafka extension: find the partition leader with Metadata before produce and fetch
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.Kafka.UnitLibrary/Client/KafkaProtocolHandler.cs, Curl.Extension.Kafka.UnitLibrary/Client/KafkaClientFlow.cs, Curl.Extension.Kafka.UnitLibrary/Wire/KafkaMetadata, Curl.Extension.Kafka.UnitTests/Client/KafkaProtocolHandlerTests.cs, Curl.Extension.Kafka.UnitTests/Wire/KafkaMetadata]
requirement: none
created: 2026-10-29
completed:
---
# BL-4272 — Kafka extension: find the partition leader with Metadata before produce and fetch

## Goal

`kafka://` produces to and fetches from the partition's leader even when the URL names another broker of the cluster.

## Context

Follow-up of BL-4121 (ADR-1069): the client sends Produce and Fetch to the URL's broker, which answers NOT_LEADER_OR_FOLLOWER (6) when it does not lead the partition. Send Metadata (the oldest version Kafka 4.0 accepts, non-flexible) first, read the partition's leader and its host and port, and reconnect through the connector to the leader when it is another broker.

## Acceptance criteria

- [ ] With the URL's broker not the leader, the transfer connects to the leader Metadata names and succeeds; unit test over scripted connections.
- [ ] An unknown topic in Metadata ends with exit 8 naming UNKNOWN_TOPIC_OR_PARTITION.
- [ ] ADR-1069's "Left out" is updated or superseded.
- [ ] The library meets the four quality gates and the fast tests pass.

## Notes

## Log

- 2026-10-29: Created.
