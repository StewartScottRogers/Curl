---
id: BL-5674
title: Fix AF-0924: Surviving mutant: an MQTT publish of exactly curl's size limit can be refused with exit 100
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Mqtt.UnitLibrary/MqttPackets.cs]
requirement: none
created: 2026-11-21
completed:
---
# BL-5674 — Fix AF-0924: Surviving mutant: an MQTT publish of exactly curl's size limit can be refused with exit 100

## Goal

The defect the audit office reported as AF-0924 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0924 (High, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Original.Protocol.Mqtt.UnitLibrary/MqttPackets.cs:93`

Location: `Curl.Original.Protocol.Mqtt.UnitLibrary/MqttPackets.cs:93`

Mutant 'if (remainingLength > MaximumPublishRemainingLength)' -> 'if (remainingLength >= MaximumPublishRemainingLength)' survived (seed 276683 sample, -MaxMutants 25). The mutant refuses a publish whose remaining length equals the limit with CurlExitCode.TooLarge (exit 100) where the code accepts it. ExecuteAsync_PublishOverCurlsSizeLimit_IsTooLargeAndPublishesNothing tests only a payload over the limit; no test sends one exactly at it, so an off-by-one in the exit-code decision goes unnoticed.

Reproduction: the audit office's mutation test changed `Curl.Original.Protocol.Mqtt.UnitLibrary/MqttPackets.cs line 93` as the evidence above describes (the mutant operator is `>`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] With the mutant the evidence describes applied by hand at `Curl.Original.Protocol.Mqtt.UnitLibrary/MqttPackets.cs line 93`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-21: Created.
