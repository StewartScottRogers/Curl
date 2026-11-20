---
id: AF-0910
title: Surviving mutant: an AMQP 1.0 upload whose message fills whole frames can send an extra empty transfer frame
auditor: quality
severity: High
status: proposed
reason:
key: quality:Curl.Extended.Protocol.Amqp1.UnitLibrary/Client/Amqp1ClientFlow.cs:SendMessageAsync-lt:surviving-mutant
reproduction: mutation Curl.Extended.Protocol.Amqp1.UnitLibrary/Client/Amqp1ClientFlow.cs:166:<
task: none
tasks:
found: 2026-11-20
found-at: 1ce730765b795e99d4d034faf03432b41b5ec6a4
scorecard: 2026-11-20_0456.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0910 - Surviving mutant: an AMQP 1.0 upload whose message fills whole frames can send an extra empty transfer frame

## Summary

High finding from the quality auditor at `Curl.Extended.Protocol.Amqp1.UnitLibrary/Client/Amqp1ClientFlow.cs:166`: Surviving mutant: an AMQP 1.0 upload whose message fills whole frames can send an extra empty transfer frame.

## Evidence

Location: `Curl.Extended.Protocol.Amqp1.UnitLibrary/Client/Amqp1ClientFlow.cs:166`

Invoke-MutationTest -Library Curl.Extended.Protocol.Amqp1.UnitLibrary -MaxMutants 25 -Seed 912464: 'for (var offset = 0; offset < message.Length; offset += perFrame)' mutated to 'offset <= message.Length' survived. When the encoded message is an exact multiple of perFrame, the mutant goes round once more and sends a further transfer frame with an empty payload and more=false after the delivery is complete. That changes the bytes Curl sends to the broker. The only split test (RunAsync_BodyLargerThanPeerFrame_SplitsTransferWithMore) uses 1200 bytes in 512-byte frames, never an exact multiple.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Extended.Protocol.Amqp1.UnitLibrary/Client/Amqp1ClientFlow.cs:166:< -Member SendMessageAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-11-20: filed proposed.
