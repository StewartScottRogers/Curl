---
id: BL-2015
title: Fix flaky MQTT test ExecuteAsync_PublishPayloadReadPending_ReportsAgainOnceBeforeThePayload (RecvError 56 in the full fast run)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Mqtt.UnitTests]
requirement: none
created: 2026-10-10
completed: 2026-10-10
---
# BL-2015 — Fix flaky MQTT test ExecuteAsync_PublishPayloadReadPending_ReportsAgainOnceBeforeThePayload (RecvError 56 in the full fast run)

## Goal

`ExecuteAsync_PublishPayloadReadPending_ReportsAgainOnceBeforeThePayload` passes in every
full fast run, however loaded the machine.

## Context

The test failed once in the full fast run (exit 56 is its expected result; the transcript
differed). `MqttSession.RunPublishReadAsync` writes "EEEE AAAAGAIN" when
`MqttPacketReader.IsNextByteReady` finds the read it just started not yet completed. The
MQTT `ScriptedConnection` fake made a held read complete with `Task.Yield`, which posts the
rest to the thread pool; under load another thread can finish it before the handler asks
`IsCompleted`, so no "EEEE AAAAGAIN" line was written.

## Acceptance criteria

- [x] A held read in `Curl.Protocol.Mqtt.UnitTests/Fakes/ScriptedConnection.cs` completes only when `ReleaseHeldRead` is called, never on its own.
- [x] `MqttProtocolHandlerTransferEventsTests.RunHeldAsync` releases a held read at the next event the handler reports, so the handler always finds it pending when it asks.
- [x] `dotnet test Curl.Protocol.Mqtt.UnitTests` passes (151 passed, 5 skipped) and the full fast run is green.

## Notes

- Fix is test-side only: the handler's behaviour is right; the fake's timing was not
  deterministic. Held reads now complete on a `TaskCompletionSource` released through the
  new `TranscriptTransferEvents.Recorded` hook. In the two AGAIN tests the releasing event
  is the "EEEE AAAAGAIN" line itself, or the data block written before the next readiness
  check (which `publishReadPending` already turns into an AGAIN line regardless of timing).
- `ExecuteAsync_PublishPayloadBufferedWithItsHeader_ReportsNoAgain` no longer holds read 3:
  the handler reports nothing while waiting for the DISCONNECT, so a held read would never
  be released, and the hold never changed its path (a buffered PUBLISH fills its run, so
  readiness is never asked).

## Log

- 2026-10-10: Created.
- 2026-10-10: Backlog -> Doing.
- 2026-10-10: Doing -> Done. MQTT held reads complete only when released by the next reported event, so the AGAIN tests no longer race the thread pool
