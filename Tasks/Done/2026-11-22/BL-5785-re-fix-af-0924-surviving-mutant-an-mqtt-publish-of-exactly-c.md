---
id: BL-5785
title: Re-fix AF-0924: Surviving mutant: an MQTT publish of exactly curl's size limit can be refused with exit 100
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Mqtt.UnitLibrary/MqttPackets.cs]
requirement: none
created: 2026-11-22
completed: 2026-11-22
---
# BL-5785 — Re-fix AF-0924: Surviving mutant: an MQTT publish of exactly curl's size limit can be refused with exit 100

## Goal

The defect the audit office reported as AF-0924 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0924 (High, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Re-fix: the earlier task(s) BL-5674 reached Done, and a later re-audit by the quality auditor found the reproduction still reproduces:

- 2026-11-22 | 2026-11-22_1817.md | reproduces: yes | Ran the -Site command: site-missing at line 93, because BuildPublish now calls IsOverPublishLimit(remainingLength). Re-ran at the moved site (MqttPackets.cs:102:> -Member IsOverPublishLimit): 'remainingLength > MaximumRemainingLength' -> '>= MaximumRemainingLength' survived, so a publish of exactly the limit can still be refused with exit 100.

Location: `Curl.Original.Protocol.Mqtt.UnitLibrary/MqttPackets.cs:93`

Location: `Curl.Original.Protocol.Mqtt.UnitLibrary/MqttPackets.cs:93`

Mutant 'if (remainingLength > MaximumPublishRemainingLength)' -> 'if (remainingLength >= MaximumPublishRemainingLength)' survived (seed 276683 sample, -MaxMutants 25). The mutant refuses a publish whose remaining length equals the limit with CurlExitCode.TooLarge (exit 100) where the code accepts it. ExecuteAsync_PublishOverCurlsSizeLimit_IsTooLargeAndPublishesNothing tests only a payload over the limit; no test sends one exactly at it, so an off-by-one in the exit-code decision goes unnoticed.

Reproduction: the audit office's mutation test changed `Curl.Original.Protocol.Mqtt.UnitLibrary/MqttPackets.cs line 93` as the evidence above describes (the mutant operator is `>`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] With the mutant the evidence describes applied by hand at `Curl.Original.Protocol.Mqtt.UnitLibrary/MqttPackets.cs line 93`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

The boundary tests already exist (commit ed6cfab26d, BL-5674): the mutant `>` -> `>=` now sits in IsOverPublishLimit (MqttPackets.cs:107). Applied by hand, MqttPublishLimitTests.IsOverPublishLimit_ExactlyAtLimit_IsAccepted fails; reverted, all Mqtt fast tests pass. Library code unchanged.

## Log

- 2026-11-22: Created.
- 2026-11-22: Backlog -> Doing. Criteria at claim: be21a3c5 9742157d.
- 2026-11-22: Doing -> Done. Mutant killed by IsOverPublishLimit_ExactlyAtLimit_IsAccepted
