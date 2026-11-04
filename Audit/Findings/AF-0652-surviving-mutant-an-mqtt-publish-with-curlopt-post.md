---
id: AF-0652
title: Surviving mutant: an MQTT publish with CURLOPT_POSTFIELDSIZE 0 can send every post field instead of none
auditor: quality
severity: High
status: proposed
reason:
key: quality:Curl.Engine.UnitLibrary/ManagedEasyTransfer.Context.cs:MqttPostDataOf-lt:surviving-mutant
reproduction: mutation Curl.Engine.UnitLibrary/ManagedEasyTransfer.Context.cs:241:<
task: none
tasks:
found: 2026-11-04
found-at: 650d20c75108547e7bc74da2e86fbee9c49f7931
scorecard: 2026-11-04_0643.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0652 - Surviving mutant: an MQTT publish with CURLOPT_POSTFIELDSIZE 0 can send every post field instead of none

## Summary

High finding from the quality auditor at `Curl.Engine.UnitLibrary/ManagedEasyTransfer.Context.cs:241`: Surviving mutant: an MQTT publish with CURLOPT_POSTFIELDSIZE 0 can send every post field instead of none.

## Evidence

Location: `Curl.Engine.UnitLibrary/ManagedEasyTransfer.Context.cs:241`

Mutant 'size < 0' -> 'size <= 0' in 'return size < 0 || size >= fields.Length ? fields : fields[..(int)size];' survived. PostFieldSize defaults to -1. With size 0 the original sends an empty payload; the mutant sends the whole post-field bytes. That changes request bytes, and no Engine test notices.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/ManagedEasyTransfer.Context.cs:241:< -Member MqttPostDataOf -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

## Log

- 2026-11-04: filed proposed.
