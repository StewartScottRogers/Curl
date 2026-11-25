---
id: BL-5902
title: Fix fitness finding EXT-127: Kafka produce to a topic auto-created just after broker start fails with NOT_LEADER_OR_FOLLOWER (exit 8); the retriable error is not retried
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.Kafka.UnitLibrary/Client/KafkaClientFlow.cs]
requirement: none
created: 2026-11-24
completed:
---
# BL-5902 — Fix fitness finding EXT-127: Kafka produce to a topic auto-created just after broker start fails with NOT_LEADER_OR_FOLLOWER (exit 8); the retriable error is not retried

## Goal

EXT-127 from the outside fitness review of 2026-11-24 13:58 no longer reproduces: Kafka produce to a topic auto-created just after broker start fails with NOT_LEADER_OR_FOLLOWER (exit 8); the retriable error is not retried. Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-24T1358` (commit `1e5d591`, the `preview` release binaries). That review was a deeper second pass of the same commit as `Curl-Fitness-Review-2026-11-24T0708`, covering areas the earlier pass did not test.

This is an extension (Part B): it is judged against Curl's own documentation (`curl --extended-help`, `Documentation/Protocols.md`, `Documentation/Conformance/Extended/*.md`, the ADRs), never against C curl.

The reviewer's mock servers were Python, and some findings used real servers in Docker (Dovecot, GreenMail, Samba, Mosquitto, Kafka, Prosody, SeaweedFS, Azurite); rebuild any mock you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Check that it still reproduces on the current branch first.

The finding, verbatim:

- **Severity:** Low · **Confidence:** Probable (timing-dependent: 3 of 3 for the first topics created within ~20 s of a fresh broker start, on 2 starts; 0 of 9 once settled) · **Area:** kafka, Windows build.
- **Repro:** start `apache/kafka:3.9.0` (KRaft single node, topic auto-create on, advertised `PLAINTEXT://localhost:38092`); within about 20 s of "Kafka Server started", run `curl -sS -d hello1 kafka://localhost:38092/t1/0 -w "[%{exitcode}]\n"`.
- **Expected:** the `--extended-help` / Kafka docs say an auto-created topic whose leader is not ready is asked about again (up to 10 times, 250 ms apart) and the record is produced, exit 0.
- **Actual (.NET):** `curl: (8) Kafka broker answered error 6 (NOT_LEADER_OR_FOLLOWER) for topic t1 partition 0`, exit 8; the record is not stored. Running the command again succeeds.
- **Likely location:** `Curl.Extended.Protocol.Kafka.UnitLibrary/Client/KafkaClientFlow.cs`: `IsLeaderPending` covers only Metadata errors 3 and 5; `ProduceAsync` turns a Produce-level error 6 (or 5) straight into exit 8. Refresh metadata and retry the produce for these retriable codes within the same attempt budget.
- **Acceptance check:** five runs of `curl -sS -d x kafka://localhost:38092/fresh$i/0` against a freshly started broker all exit 0, and consuming each topic prints `x`.

## Acceptance criteria

- [ ] five runs of `curl -sS -d x kafka://localhost:38092/fresh$i/0` against a freshly started broker all exit 0, and consuming each topic prints `x`.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-24: Created from fitness review 2026-11-24T1358.
