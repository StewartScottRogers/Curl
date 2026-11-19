---
id: BL-5495
title: Fix fitness finding EXT-101: Kafka reports broker error 17 (and other unlisted codes) as UNNAMED, and echoes control characters from the topic
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.Kafka.UnitLibrary/Client/KafkaClientMessages.cs]
requirement: none
created: 2026-11-18
completed:
---
# BL-5495 — Fix fitness finding EXT-101: Kafka reports broker error 17 (and other unlisted codes) as UNNAMED, and echoes control characters from the topic

## Goal

EXT-101 from the outside fitness review of 2026-11-18 06:33 no longer reproduces: Kafka reports broker error 17 (and other unlisted codes) as UNNAMED, and echoes control characters from the topic. Curl matches the reference curl as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-18T0633` (commit `ec53afc`, preview release binaries). In the finding, PORT is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the setup from STD-116"), find the task titled with that finding ID. Measure real curl with `Record-CurlExchange.ps1` before pinning output text. The finding, verbatim:

- **Severity:** Low
- **Confidence:** Confirmed (twice, two topic names)
- **Protocol/area:** kafka, kafkas
- **Repro:** `apache/kafka:3.9.0` single-node KRaft with a plaintext listener advertised as 127.0.0.1:46092. `curl -sS -m 10 -d x "kafka://127.0.0.1:46092/bad%20topic"`; also `kafka://127.0.0.1:46092/a%0D%0Ab`.
- **Expected:** Kafka.md row Kafka:Guide-Errors-1 (SHOULD): "Error codes are reported by their name in the error codes table" — 17 is `INVALID_TOPIC_EXCEPTION`. A message should not carry raw CR/LF.
- **Actual (PORT):** `curl: (8) Kafka broker answered error 17 (UNNAMED) for topic bad topic partition 0`, exit 8. With `a%0D%0Ab` the raw CR/LF is echoed into the message.
- **Likely location:** `Curl.Extended.Protocol.Kafka.UnitLibrary/Client/KafkaClientMessages.cs` — `ErrorNames` holds only -1, 1, 2, 3, 5, 6, 7, 10, 19, 29, 87. Add the rest of the protocol guide's table (at least 4, 9, 13, 17, 18, 20, 21, 22, 36, 58) and escape control characters in the topic.
- **Acceptance check:** the first command prints `… error 17 (INVALID_TOPIC_EXCEPTION) for topic bad topic partition 0`; the second shows no raw CR/LF.

## Acceptance criteria

- [ ] the first command prints `… error 17 (INVALID_TOPIC_EXCEPTION) for topic bad topic partition 0`; the second shows no raw CR/LF.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-18: Created from fitness review Curl-Fitness-Review-2026-11-18T0633, finding EXT-101 (Low).
- 2026-11-18: Backlog -> Doing. Criteria at claim: dab12433 69116afe 3d836d5e.
