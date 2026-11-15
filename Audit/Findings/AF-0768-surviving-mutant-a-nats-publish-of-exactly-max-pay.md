---
id: AF-0768
title: Surviving mutant: a NATS publish of exactly max_payload bytes can be refused with exit 63
auditor: quality
severity: High
status: closed
reason: Re-audit 2026-11-15_0222.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Extended.Protocol.Nats.UnitLibrary/NatsSession.cs:PublishLines-le:surviving-mutant
reproduction: mutation Curl.Extended.Protocol.Nats.UnitLibrary/NatsSession.cs:83:<=
task: BL-5217
tasks: BL-5217
found: 2026-11-14
found-at: 92f6fc980babe08d1ebff5f02d7e076e42c67fd7
scorecard: 2026-11-14_1801.md
duplicate-of:
closed: 2026-11-15
closed-how: mechanical
closed-by: 2026-11-15_0222.md
---
# AF-0768 - Surviving mutant: a NATS publish of exactly max_payload bytes can be refused with exit 63

## Summary

High finding from the quality auditor at `Curl.Extended.Protocol.Nats.UnitLibrary/NatsSession.cs:83`: Surviving mutant: a NATS publish of exactly max_payload bytes can be refused with exit 63. Reported by an auditor flagged unreliable in 2026-11-14_1801.md.

## Evidence

Location: `Curl.Extended.Protocol.Nats.UnitLibrary/NatsSession.cs:83`

Mutant 'payload.Length <= maxPayload' -> '<' survived. The only test, ExecuteAsync_DataOverMaxPayload_FailsWithExit63BeforePublishing, sends 9 bytes to a server whose max_payload is 8; no test publishes exactly max_payload bytes. The mutant refuses the largest allowed payload with exit 63 and sends nothing.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Extended.Protocol.Nats.UnitLibrary/NatsSession.cs:83:<= -Member PublishLines -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

- 2026-11-15 | 2026-11-15_0222.md | reproduces: no | Ran the -Site command: resolvedLine 83, outcome killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-14: filed proposed.
- 2026-11-15: accepted -> closed. Re-audit 2026-11-15_0222.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
