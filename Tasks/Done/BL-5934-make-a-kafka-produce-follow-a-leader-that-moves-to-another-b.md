---
id: BL-5934
title: Make a Kafka produce follow a leader that moves to another broker during its retries
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-5902]
touches: [Curl.Extended.Protocol.Kafka.UnitLibrary/Client/KafkaClientFlow.cs, Curl.Extended.Protocol.Kafka.UnitLibrary/Client/KafkaProtocolHandler.cs, Curl.Extended.Protocol.Kafka.UnitTests/Client/KafkaLeaderRetryTests.cs, Curl.Extended.Protocol.Kafka.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-11-24
completed: 2026-11-25
---
# BL-5934 — Make a Kafka produce follow a leader that moves to another broker during its retries

## Goal

`curl -d x kafka://host/topic` produces its record when the partition's leader moves to another broker between the Metadata answer and a retried Produce.

## Context

Follow-up of BL-5902 (ADR-1370). `KafkaClientFlow.ProduceAsync` asks Metadata again after a Produce answered with error 3, 5 or 6, but sends the record again over the same connection and ignores the leader that Metadata names. In a cluster where leadership moved to another broker, every retry fails and the transfer ends with exit 8. `KafkaProtocolHandler.TransferAsync` and `RedirectOf` already reconnect to the leader on the first Metadata answer (ADR-1080); do the same for a later one, within the same retry budget.

## Acceptance criteria

- [x] A unit test in `Curl.Extended.Protocol.Kafka.UnitTests` scripts a Produce answered with error 6, then Metadata naming another broker, and shows the record produced over a connection to that broker with exit 0.
- [x] A unit test shows the retries stay bounded when the leader keeps moving.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Added the Kafka library's `CLAUDE.md` to `touches`: it describes when the handler reconnects. No task in Doing on `origin/work/dark-factory` names a Kafka file.
- ADR-1375: the nine retries now belong to the transfer, not a connection; the new leader gets the same record batch without a Metadata request first.
- `ProduceAsync` gives the moved leader back to `KafkaProtocolHandler`, which reconnects. Tests: `ExecuteAsync_LeaderMovesDuringRetries_ProducesOverAConnectionToTheNewLeader` and `ExecuteAsync_LeaderKeepsMoving_StopsAfterNineRetriesWithTheBrokerError`.
- Verified: `dotnet build` clean, fast tests green (Kafka 282 passed), format clean on both Kafka projects, `Measure-CodeQuality.ps1 -Library Curl.Extended.Protocol.Kafka.UnitLibrary` 0 failing members. No command-line option changed, so `--ai-help` is unaffected. Tests use scripted connections only, so they are platform-neutral.

## Log

- 2026-11-24: Created.
- 2026-11-24: Backlog -> Doing. Criteria at claim: df78a778 12fff96b 3d836d5e.
- 2026-11-25: Doing -> Done. A Kafka produce reconnects to a leader that moves during its retries, within the transfer's nine retries
