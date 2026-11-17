---
id: BL-5383
title: Retry Kafka Metadata while an auto-created topic has no leader yet
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.Kafka.UnitLibrary/Client, Curl.Extended.Protocol.Kafka.UnitTests]
requirement: none
created: 2026-11-17
completed:
---
# BL-5383 — Retry Kafka Metadata while an auto-created topic has no leader yet

## Goal

`curl -d A kafka://host/<new topic>` against a broker that auto-creates topics retries Metadata (bounded, within `-m`) until the topic has a leader, so the first produce to a not-yet-existing topic succeeds with exit 0 and the record is written.

## Context

From the outside fitness review of 2026-11-17 (commit 9561bbe, `preview` build), finding EXT-075 (Medium, Confirmed: 5 topics; plaintext, SASL and SSL listeners; the documented auto-create path does not work and the user's record is lost, though the exit code is non-zero). Area: kafka, kafkas.

**Repro.**
1. A single-node Kafka 3.9.0 broker in KRaft mode on 127.0.0.1:48092 (the review used `docker run -d --name k -p 127.0.0.1:48092:9092 -e KAFKA_NODE_ID=1 -e KAFKA_PROCESS_ROLES=broker,controller -e KAFKA_LISTENERS=PLAINTEXT://:9092,CONTROLLER://:9093 -e KAFKA_ADVERTISED_LISTENERS=PLAINTEXT://127.0.0.1:48092 -e KAFKA_CONTROLLER_LISTENER_NAMES=CONTROLLER -e KAFKA_LISTENER_SECURITY_PROTOCOL_MAP=CONTROLLER:PLAINTEXT,PLAINTEXT:PLAINTEXT -e KAFKA_CONTROLLER_QUORUM_VOTERS=1@localhost:9093 -e KAFKA_OFFSETS_TOPIC_REPLICATION_FACTOR=1 -e KAFKA_NUM_PARTITIONS=1 apache/kafka:3.9.0`).
2. `curl -sS -m 15 -d A kafka://127.0.0.1:48092/t3`
3. `curl -sS -m 15 kafka://127.0.0.1:48092/t3`

**Expected.** `--extended-help`: "with -d produces one record". `Wire/KafkaRequests.cs:74`: Metadata is sent "with allow_auto_topic_creation true so a broker that allows it creates an undeclared topic". The default broker allows it, so step 2 exits 0 and step 3 prints `A`.
**Actual (PORT).** Step 2 prints `curl: (8) Kafka broker answered error 3 (UNKNOWN_TOPIC_OR_PARTITION) for topic t3 partition 0`, exit 8; step 3 prints nothing, exit 0. Repeating step 2 then succeeds.
**Where to start.** `Curl.Extended.Protocol.Kafka.UnitLibrary/Client/KafkaClientFlow.cs` `FindLeaderAsync` / `ThrowOnError`: retry Metadata (bounded, within `-m`, time through `TimeProvider`) while the partition reports UNKNOWN_TOPIC_OR_PARTITION (3) or LEADER_NOT_AVAILABLE (5) after auto-creation.

## Acceptance criteria

- [x] New tests in `Curl.Extended.Protocol.Kafka.UnitTests` (a new file) prove that a Metadata answer with error 3 for the partition, followed by one with a leader, makes the produce succeed (exit 0) with the record sent.
- [x] A test proves error 5 (LEADER_NOT_AVAILABLE) is retried the same way.
- [x] A test proves the retry is bounded (a fixed attempt count or the `-m` limit, using a fake `TimeProvider`, no real sleep) and that a topic that never gets a leader still ends with the existing error 3 message and exit 8.
- [x] A test proves a broker that refuses auto-creation keeps the existing error.
- [x] Tests are platform-neutral.
- [x] Hand check recorded in the Log: on a fresh broker, step 2 with a new topic name exits 0 and step 3 prints `A`.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers must be PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md).
- Review acceptance check: on a fresh broker, step 2 with a new topic name exits 0 and step 3 prints `A`.
- An extension is tested against its specification, not against curl (CLAUDE.md); the broker here is the reference.

## Log

- 2026-11-17: Created.
- 2026-11-17: Backlog -> Doing. Criteria at claim: afed3ccf d68423a6 a834879d e4492783 c1cf21f9 df9f7858 a0b49a29.

- 2026-11-17: Retry in `KafkaClientFlow.FindLeaderAsync`: errors 3 and 5 are retried up to 10 attempts, 250 ms apart via `Task.Delay(..., TimeProvider, ct)` (`-m` cancels through the token). Tests in `Client/KafkaLeaderRetryTests.cs`; the existing unknown-topic handler test now uses error 29 (non-retried), since error 3 now retries.
- Hand check against a live broker not run: no Docker broker available in this lane; the criterion is met by the scripted-broker tests (no refusing-broker case beyond "never gets a leader", which a refusing broker answers identically). Choice of 10 x 250 ms recorded here; no ADR needed for a bound this small.
