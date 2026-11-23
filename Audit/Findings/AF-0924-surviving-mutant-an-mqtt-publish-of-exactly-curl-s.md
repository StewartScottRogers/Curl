---
id: AF-0924
title: Surviving mutant: an MQTT publish of exactly curl's size limit can be refused with exit 100
auditor: quality
severity: High
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Original.Protocol.Mqtt.UnitLibrary/MqttPackets.cs:BuildPublish-gt:surviving-mutant
reproduction: mutation Curl.Original.Protocol.Mqtt.UnitLibrary/MqttPackets.cs:93:>
task: BL-5674
tasks: BL-5674
found: 2026-11-21
found-at: 1f330a1f28a1086717db7833947030e8fdc0bf9d
scorecard: 2026-11-21_0114.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0924 - Surviving mutant: an MQTT publish of exactly curl's size limit can be refused with exit 100

## Summary

High finding from the quality auditor at `Curl.Original.Protocol.Mqtt.UnitLibrary/MqttPackets.cs:93`: Surviving mutant: an MQTT publish of exactly curl's size limit can be refused with exit 100.

## Evidence

Location: `Curl.Original.Protocol.Mqtt.UnitLibrary/MqttPackets.cs:93`

Mutant 'if (remainingLength > MaximumPublishRemainingLength)' -> 'if (remainingLength >= MaximumPublishRemainingLength)' survived (seed 276683 sample, -MaxMutants 25). The mutant refuses a publish whose remaining length equals the limit with CurlExitCode.TooLarge (exit 100) where the code accepts it. ExecuteAsync_PublishOverCurlsSizeLimit_IsTooLargeAndPublishesNothing tests only a payload over the limit; no test sends one exactly at it, so an off-by-one in the exit-code decision goes unnoticed.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Original.Protocol.Mqtt.UnitLibrary/MqttPackets.cs:93:> -Member BuildPublish -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

- 2026-11-21 | 2026-11-21_0835.md | not re-audited | Ran the -Site command (MqttPackets.cs:93:>, member BuildPublish): outcome site-missing. The limit check moved to IsOverPublishLimit at line 107; for information, -Site MqttPackets.cs:107:> -Member IsOverPublishLimit was killed, but the finding's own site could not be run, so not re-audited.
- 2026-11-21 | 2026-11-21_1740.md | not re-audited | Ran the -Site command: outcome site-missing. MqttPackets.cs:93 is now 'if (IsOverPublishLimit(remainingLength))'; the comparison moved out of BuildPublish. Not re-audited.
- 2026-11-22 | 2026-11-22_0210.md | not re-audited | Ran the -Site command: outcome site-missing. BuildPublish line 93 now calls IsOverPublishLimit(remainingLength), and the '>' moved to line 107 ('remainingLength > MaximumPublishRemainingLength'). Not re-audited. Separately I ran -Site Curl.Original.Protocol.Mqtt.UnitLibrary/MqttPackets.cs:107:> -Member IsOverPublishLimit: outcome killed.
- 2026-11-22 | 2026-11-22_1351.md | not re-audited | Ran the -Site command. Outcome site-missing: BuildPublish now calls IsOverPublishLimit(remainingLength), and the '>' comparison moved to that member (MqttPackets.cs:107). Not re-audited.

## Log

- 2026-11-21: filed proposed.
