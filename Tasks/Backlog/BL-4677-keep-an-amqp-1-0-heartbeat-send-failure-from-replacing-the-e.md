---
id: BL-4677
title: Keep an AMQP 1.0 heartbeat send failure from replacing the exchange's own result
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Extended.Protocol.Amqp1.UnitLibrary/Client/Amqp1ClientFlow.cs, Curl.Extended.Protocol.Amqp1.UnitTests/Client/Amqp1ClientFlowTests.cs]
requirement: none
created: 2026-11-03
completed:
---
# BL-4677 — Keep an AMQP 1.0 heartbeat send failure from replacing the exchange's own result

## Goal

An AMQP 1.0 transfer ends with the exchange's own result (success, or the broker's error and exit code); a failed heartbeat send never replaces it, and a heartbeat failure on a dead peer ends the exchange instead of waiting for `-m`.

## Context

Found by the shift-end review (ADR-0710) of `f37ba71c...dfa29449`: silent-failure-hunter and code-reviewer.

ADR-1205 added heartbeats in `Curl.Extended.Protocol.Amqp1.UnitLibrary/Client/Amqp1ClientFlow.cs`. `SendHeartbeatsAsync` (~lines 103-117) catches only `OperationCanceledException`. A write failure becomes `Amqp1ClientException(SendError)` from `Amqp1FrameChannel.WriteAsync` and faults the heartbeat task. `RunAsync` (~lines 53-60) awaits `heartbeats` before it rethrows `exchange`, so the heartbeat's exit 55 wins over the exchange's success or the broker's error. Nothing cancels the exchange's pending read when a heartbeat fails, so a half-dead peer hangs until `-m`.

Scenario: the broker sends `close` with an error and drops TCP while a heartbeat is being written. The user sees exit 55 "send failed" instead of the broker's error. It depends on timing, so it would also show up as a flaky test.

## Acceptance criteria

- [ ] `RunAsync` awaits the exchange's outcome first and reports a heartbeat failure only when the exchange did not end on its own (it was cancelled because of the heartbeat failure).
- [ ] A heartbeat send failure cancels the exchange (through a linked token source).
- [ ] `Amqp1ClientFlowTests` has a test where the exchange succeeds while a heartbeat write fails, and the transfer succeeds.
- [ ] `Amqp1ClientFlowTests` has a test where the broker closes with an error while a heartbeat write fails, and the broker's exit code is reported.
- [ ] The tests drive the heartbeat through `ManualAmqp1TimeProvider`, with no real delay; `dotnet build` is clean and the fast tests of `Curl.Extended.Protocol.Amqp1.UnitTests` pass.

## Notes

## Log

- 2026-11-03: Created.
