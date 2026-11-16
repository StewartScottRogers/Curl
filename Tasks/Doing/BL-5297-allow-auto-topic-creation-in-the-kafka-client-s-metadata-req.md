---
id: BL-5297
title: Allow auto topic creation in the kafka client's Metadata request
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.Kafka.UnitLibrary/Wire/KafkaRequests.cs, Curl.Extended.Protocol.Kafka.UnitTests/Wire/KafkaMetadataAutoTopicCreationTests.cs]
requirement: none
created: 2026-11-16
completed:
---
# BL-5297 — Allow auto topic creation in the kafka client's Metadata request

## Goal

A produce to a topic nobody declared sends Metadata (v4 or later) with `allow_auto_topic_creation` set, so a broker that allows creation makes the topic instead of answering `UNKNOWN_TOPIC_OR_PARTITION`.

## Context

Filed from Surl's Phase 7 measurement of the port's extended clients (Stewart approved filing on the port's board, 2026-11-16): Surl `Documentation/Conformance/CurlPort/Port-Board-Handoff.md`, entry 23 (github.com/StewartScottRogers/Surl). Port measured at commit `b42c319ba940f66452b2b6f3e9910d46825b08fe` (port `curl.exe` SHA-256 `F28D1417807AA862BA2A6D5B0BF0CF1200673BE45B959B1A78E1E4B476EF8FA7`), Windows x64. Upstream curl has no `kafka` scheme, so this is judged by the Kafka protocol specification.

- **Finding:** Surl `Documentation/Conformance/CurlPort/Extended.md`, EXT-6 (Surl task BL-523; Surl ADR-0105 decision 2).
- **Port (defect):** the flag is not set; the produce ends with exit 8 (`UNKNOWN_TOPIC_OR_PARTITION`).
- **What decides:** the Kafka Metadata request definition (`allow_auto_topic_creation`, added in Metadata v4; https://kafka.apache.org/protocol#The_Messages_Metadata).
- **Where to start:** `Curl.Extended.Protocol.Kafka.UnitLibrary/Wire/KafkaRequests.cs` (Metadata before produce came with BL-4275, which pinned exit 8 for an unknown topic; that stays right when the broker refuses creation).

## Acceptance criteria

- [ ] A port unit test shows the Metadata request sent before a produce carries `allow_auto_topic_creation = true`.
- [ ] A port test producing to an undeclared topic on a broker that auto-creates succeeds (exit 0).

## Notes

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing. Criteria at claim: cd8c16ec e1cb1987.
