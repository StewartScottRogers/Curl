---
id: AF-0884
title: Surviving mutant: a NATS MSG of exactly max_payload bytes can be refused with exit 8
auditor: quality
severity: High
status: proposed
reason:
key: quality:Curl.Extended.Protocol.Nats.UnitLibrary/NatsMessageLine.cs:TryReadLengths-le:surviving-mutant
reproduction: mutation Curl.Extended.Protocol.Nats.UnitLibrary/NatsMessageLine.cs:43:<=
task: none
tasks:
found: 2026-11-19
found-at: 6e68cb60b626d421113f6d7d7f35c2a46bbc3b46
scorecard: 2026-11-19_0602.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0884 - Surviving mutant: a NATS MSG of exactly max_payload bytes can be refused with exit 8

## Summary

High finding from the quality auditor at `Curl.Extended.Protocol.Nats.UnitLibrary/NatsMessageLine.cs:43`: Surviving mutant: a NATS MSG of exactly max_payload bytes can be refused with exit 8.

## Evidence

Location: `Curl.Extended.Protocol.Nats.UnitLibrary/NatsMessageLine.cs:43`

Mutant && total <= maxPayload -> && total < maxPayload in TryReadLengths survived every Curl.Extended.Protocol.Nats.UnitTests test (seed 446316). With it, a MSG or HMSG line whose total length equals the server's max_payload throws NatsException(WeirdServerReply, "NATS server sent an unexpected line: ..."), so a subscribe that receives a maximum-size message ends with exit 8 instead of writing the payload. The tests cover publishing exactly max_payload (ExecuteAsync_DataOfExactlyMaxPayload_PublishesIt) but never receiving it, and MessageLineParse_FourOrFiveFields_ReadsThem uses lengths well under the limit of 10.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Extended.Protocol.Nats.UnitLibrary/NatsMessageLine.cs:43:<= -Member TryReadLengths -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-11-19: filed proposed.
