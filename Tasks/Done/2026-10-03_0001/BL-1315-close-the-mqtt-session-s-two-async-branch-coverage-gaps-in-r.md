---
id: BL-1315
title: Close the MQTT session's two async branch-coverage gaps in ReadPacketAsync and ReadFixedHeaderPingingWhenIdleAsync
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Mqtt.UnitLibrary, Curl.Protocol.Mqtt.UnitTests]
requirement: none
created: 2026-10-03
completed: 2026-10-03
---
# BL-1315 — Close the MQTT session's two async branch-coverage gaps in ReadPacketAsync and ReadFixedHeaderPingingWhenIdleAsync

## Goal

`Measure-CodeQuality.ps1 -Library Curl.Protocol.Mqtt.UnitLibrary` reports no failing member: 100% branch coverage for `MqttSession.ReadPacketAsync` and `MqttSession.ReadFixedHeaderPingingWhenIdleAsync`.

## Context

- Found while finishing BL-1310 (2026-10-03). The measure, run on the MQTT tests' own Cobertura report (`dotnet test Curl.Protocol.Mqtt.UnitTests --collect:"Code Coverage;Format=cobertura" --results-directory <dir>`, then `Measure-CodeQuality.ps1 -Library Curl.Protocol.Mqtt.UnitLibrary -SkipTestRun -ResultsDirectory <dir>`), reports two failing members, identically before and after BL-1310's change:
  - `MqttSession.ReadPacketAsync (async)`: branch 85.71% - the `state switch` line has 6 of 7 conditions covered.
  - `MqttSession.ReadFixedHeaderPingingWhenIdleAsync (async)`: branch 75% - the `!pingSent && !firstByte.IsCompleted` line has 3 of 4 conditions covered, and an `await` has only its synchronous or only its asynchronous completion covered.
- Both are async state machine branches. Cover them with tests (for example a fake connection whose next read completes asynchronously, or a state the switch has not yet been driven through); if a branch is compiler-generated and unreachable, simplify the code so it is gone rather than excluding it.

## Acceptance criteria

- [x] `Measure-CodeQuality.ps1 -Library Curl.Protocol.Mqtt.UnitLibrary` (on the MQTT tests' coverage as above) reports 0 failing members.
- [x] `dotnet test Curl.Protocol.Mqtt.UnitTests --filter "TestCategory!=Integration"` passes.

## Notes

- The `!pingSent && !firstByte.IsCompleted` gap was the `pingSent` true side: no test re-entered the read with a PINGREQ outstanding. `ExecuteAsync_PublishWhilePingRequestOutstanding_SendsNoSecondPingRequest` sends a PUBLISH before the PINGRESP and pins that no second PINGREQ follows.
- The `state switch` gap was the CONNACK arm's await completing asynchronously: every test had the CONNACK body ready with its header. `ExecuteAsync_ConnackBodyArrivingAfterItsHeader_SubscribesAndWritesPublish` (GatedConnection) delivers the body 50 ms later.
- No production code changed; measured 0 failing members, MQTT 100% line and branch.

## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
- 2026-10-03: Doing -> Done. MQTT session's ping-outstanding and late-CONNACK branches are covered; the library measures 0 failing members
