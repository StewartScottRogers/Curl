---
id: AF-0876
title: Surviving mutant: an AMQP timestamp of exactly 9999-12-31T23:59:59Z can be refused
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-11-19_0602.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Extended.Protocol.Amqp0.UnitLibrary/Wire/Amqp0Reader.cs:ReadTimestamp-gt:surviving-mutant
reproduction: mutation Curl.Extended.Protocol.Amqp0.UnitLibrary/Wire/Amqp0Reader.cs:39:>
task: BL-5540
tasks: BL-5540
found: 2026-11-19
found-at: 20e558729f39eda4a4d373ccd10b22dd4ea46b2c
scorecard: 2026-11-19_0134.md
duplicate-of:
closed: 2026-11-19
closed-how: mechanical
closed-by: 2026-11-19_0602.md
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

- 2026-11-19 | 2026-11-19_0602.md | reproduces: no | Ran the -Site command: Amqp0Reader.cs:39 (resolvedLine 39), > 253402300799 -> >=, outcome killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-19: filed proposed.
- 2026-11-19: accepted -> closed. Re-audit 2026-11-19_0602.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
