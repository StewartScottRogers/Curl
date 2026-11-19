---
id: AF-0876
title: Surviving mutant: an AMQP timestamp of exactly 9999-12-31T23:59:59Z can be refused
auditor: quality
severity: Medium
status: proposed
reason:
key: quality:Curl.Extended.Protocol.Amqp0.UnitLibrary/Wire/Amqp0Reader.cs:ReadTimestamp-gt:surviving-mutant
reproduction: mutation Curl.Extended.Protocol.Amqp0.UnitLibrary/Wire/Amqp0Reader.cs:39:>
task: none
tasks:
found: 2026-11-19
found-at: 20e558729f39eda4a4d373ccd10b22dd4ea46b2c
scorecard: 2026-11-19_0134.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0876 - Surviving mutant: an AMQP timestamp of exactly 9999-12-31T23:59:59Z can be refused

## Summary

Medium finding from the quality auditor at `Curl.Extended.Protocol.Amqp0.UnitLibrary/Wire/Amqp0Reader.cs:39`: Surviving mutant: an AMQP timestamp of exactly 9999-12-31T23:59:59Z can be refused.

## Evidence

Location: `Curl.Extended.Protocol.Amqp0.UnitLibrary/Wire/Amqp0Reader.cs:39`

Mutant 'if (seconds is < -62135596800 or >= 253402300799)' (from '>') survived in the sample and in the targeted -Site run. 253402300799 is the last second of year 9999, a valid DateTimeOffset. Under the mutant it is refused as a syntax error. No test reads the upper boundary.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Extended.Protocol.Amqp0.UnitLibrary/Wire/Amqp0Reader.cs:39:> -Member ReadTimestamp -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived (resolvedLine 39)

## Re-audits

## Log

- 2026-11-19: filed proposed.
