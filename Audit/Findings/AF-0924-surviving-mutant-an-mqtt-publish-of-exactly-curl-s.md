---
id: AF-0924
title: Surviving mutant: an MQTT publish of exactly curl's size limit can be refused with exit 100
auditor: quality
severity: High
status: proposed
reason:
key: quality:Curl.Original.Protocol.Mqtt.UnitLibrary/MqttPackets.cs:BuildPublish-gt:surviving-mutant
reproduction: mutation Curl.Original.Protocol.Mqtt.UnitLibrary/MqttPackets.cs:93:>
task: none
tasks:
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

## Log

- 2026-11-21: filed proposed.
