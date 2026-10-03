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
completed:
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

- [ ] `Measure-CodeQuality.ps1 -Library Curl.Protocol.Mqtt.UnitLibrary` (on the MQTT tests' coverage as above) reports 0 failing members.
- [ ] `dotnet test Curl.Protocol.Mqtt.UnitTests --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
