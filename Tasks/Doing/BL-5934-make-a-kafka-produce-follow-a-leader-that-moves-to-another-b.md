---
id: BL-5934
title: Make a Kafka produce follow a leader that moves to another broker during its retries
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-5902]
touches: [Curl.Extended.Protocol.Kafka.UnitLibrary/Client/KafkaClientFlow.cs, Curl.Extended.Protocol.Kafka.UnitLibrary/Client/KafkaProtocolHandler.cs, Curl.Extended.Protocol.Kafka.UnitTests/Client/KafkaLeaderRetryTests.cs]
requirement: none
created: 2026-11-24
completed:
---
# BL-5934 — Make a Kafka produce follow a leader that moves to another broker during its retries

## Goal

`curl -d x kafka://host/topic` produces its record when the partition's leader moves to another broker between the Metadata answer and a retried Produce.

## Context

Follow-up of BL-5902 (ADR-1370). `KafkaClientFlow.ProduceAsync` asks Metadata again after a Produce answered with error 3, 5 or 6, but sends the record again over the same connection and ignores the leader that Metadata names. In a cluster where leadership moved to another broker, every retry fails and the transfer ends with exit 8. `KafkaProtocolHandler.TransferAsync` and `RedirectOf` already reconnect to the leader on the first Metadata answer (ADR-1080); do the same for a later one, within the same retry budget.

## Acceptance criteria

- [ ] A unit test in `Curl.Extended.Protocol.Kafka.UnitTests` scripts a Produce answered with error 6, then Metadata naming another broker, and shows the record produced over a connection to that broker with exit 0.
- [ ] A unit test shows the retries stay bounded when the leader keeps moving.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-24: Created.
- 2026-11-24: Backlog -> Doing. Criteria at claim: df78a778 12fff96b 3d836d5e.
