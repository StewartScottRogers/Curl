---
id: AF-0875
title: Surviving mutant: an AMQP timestamp of exactly 0001-01-01T00:00:00Z can be refused
auditor: quality
severity: Medium
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Extended.Protocol.Amqp0.UnitLibrary/Wire/Amqp0Reader.cs:ReadTimestamp-lt:surviving-mutant
reproduction: mutation Curl.Extended.Protocol.Amqp0.UnitLibrary/Wire/Amqp0Reader.cs:39:<
task: BL-5539
tasks: BL-5539
found: 2026-11-19
found-at: 20e558729f39eda4a4d373ccd10b22dd4ea46b2c
scorecard: 2026-11-19_0134.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0875 - Surviving mutant: an AMQP timestamp of exactly 0001-01-01T00:00:00Z can be refused

## Summary

Medium finding from the quality auditor at `Curl.Extended.Protocol.Amqp0.UnitLibrary/Wire/Amqp0Reader.cs:39`: Surviving mutant: an AMQP timestamp of exactly 0001-01-01T00:00:00Z can be refused.

## Evidence

Location: `Curl.Extended.Protocol.Amqp0.UnitLibrary/Wire/Amqp0Reader.cs:39`

Mutant 'if (seconds is <= -62135596800 or > 253402300799)' (from '<') survived in the sample and in the targeted -Site run. -62135596800 is the first second of year 1, which DateTimeOffset represents. Under the mutant a message whose timestamp is that value is refused with a syntax error, failing the transfer. Timestamp_OutsideDateTimeOffset_IsSyntaxError tests values outside the range, but no test reads the lower boundary itself.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Extended.Protocol.Amqp0.UnitLibrary/Wire/Amqp0Reader.cs:39:< -Member ReadTimestamp -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived (resolvedLine 39)

## Re-audits

## Log

- 2026-11-19: filed proposed.
