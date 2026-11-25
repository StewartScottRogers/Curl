---
id: BL-5631
title: Kafka SCRAM refuses a server-first iteration count above a fixed limit
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Extended.Protocol.Kafka.UnitLibrary/Wire/KafkaSasl/KafkaScramClient.cs, Curl.Extended.Protocol.Kafka.UnitTests/Wire/KafkaSasl/KafkaScramClientTests.cs]
requirement: none
created: 2026-11-20
completed: 2026-11-20
---
# BL-5631 — Kafka SCRAM refuses a server-first iteration count above a fixed limit

## Goal

A broker's SCRAM server-first message with an absurd `i=` iteration count ends the `kafka://` transfer at once with exit 8, instead of hanging in key derivation.

## Context

Found by the shift-end review of 2026-11-20 (code-reviewer and silent-failure-hunter) in the diff b73256235592..071c3980c0.

In `Curl.Extended.Protocol.Kafka.UnitLibrary/Wire/KafkaSasl/KafkaScramClient.cs`:
- `ParseServerFirst` (line 67) checks only `iterations < 1`.
- `ClientFinal` (line 37) then runs `Rfc2898DeriveBytes.Pbkdf2(..., iterations, ...)` synchronously, so the transfer's cancellation token, and with it `-m`, cannot interrupt it.

Failure: a hostile or misconfigured broker sends `i=2147483647`. The transfer then spends hours in PBKDF2 with SHA-512, and `-m` does not stop it. Kafka's brokers cap the count at 16384 (`ScramMechanism`).

## Acceptance criteria

- [x] `ParseServerFirst` refuses an iteration count above a named constant limit (for example 100000) with the same `FormatException` path as other malformed server-first messages, so the transfer ends with exit 8.
- [x] `KafkaScramClientTests` pins the refusal just above the limit, and acceptance at the limit.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- MaximumIterations = 100000 (internal const); check lives in TryParseIterations to keep ParseServerFirst under the complexity gate. Quality run: 0 failing, 100%/100%. Kafka fast tests 275 pass.

## Log

- 2026-11-20: Created.
- 2026-11-20: Backlog -> Doing. Criteria at claim: 979e392e 09e22103 3d836d5e.
- 2026-11-20: Doing -> Done. Kafka SCRAM refuses server-first iteration counts above 100000 (exit 8)
