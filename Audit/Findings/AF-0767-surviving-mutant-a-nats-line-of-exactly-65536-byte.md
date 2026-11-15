---
id: AF-0767
title: Surviving mutant: a NATS line of exactly 65536 bytes can be refused with exit 8
auditor: quality
severity: High
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Extended.Protocol.Nats.UnitLibrary/NatsWireReader.cs:CheckLength-gt:surviving-mutant
reproduction: mutation Curl.Extended.Protocol.Nats.UnitLibrary/NatsWireReader.cs:82:>
task: none
tasks:
found: 2026-11-14
found-at: 92f6fc980babe08d1ebff5f02d7e076e42c67fd7
scorecard: 2026-11-14_1801.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0767 - Surviving mutant: a NATS line of exactly 65536 bytes can be refused with exit 8

## Summary

High finding from the quality auditor at `Curl.Extended.Protocol.Nats.UnitLibrary/NatsWireReader.cs:82`: Surviving mutant: a NATS line of exactly 65536 bytes can be refused with exit 8. Reported by an auditor flagged unreliable in 2026-11-14_1801.md.

## Evidence

Location: `Curl.Extended.Protocol.Nats.UnitLibrary/NatsWireReader.cs:82`

Mutant 'if (line.Length > MaxLineLength)' -> '>=' survived (MaxLineLength = 65536, line 14). The over-limit tests (NatsWireTests ReadLineAsync_ALineOverTheLimit...) send 4 x 16384 + 'x', which is past the limit; no test sends a line of exactly the limit. The mutant changes a valid boundary line into exit 8, 'line too long'.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Extended.Protocol.Nats.UnitLibrary/NatsWireReader.cs:82:> -Member CheckLength -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-11-14: filed proposed.
