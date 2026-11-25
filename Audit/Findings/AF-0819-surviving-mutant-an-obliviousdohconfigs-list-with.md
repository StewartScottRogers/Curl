---
id: AF-0819
title: Surviving mutant: an ObliviousDoHConfigs list with bytes after its vector can be accepted
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-11-16_1333.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Extension.ObliviousDoh.UnitLibrary/Wire/ObliviousDohConfig.cs:SelectFrom-and:surviving-mutant
reproduction: mutation Curl.Extension.ObliviousDoh.UnitLibrary/Wire/ObliviousDohConfig.cs:30:&&
task: BL-5273
tasks: BL-5273
found: 2026-11-16
found-at: 74f3f45c47e86300b2b262dc1dacd43b508b09aa
scorecard: 2026-11-16_0514.md
duplicate-of:
closed: 2026-11-16
closed-how: mechanical
closed-by: 2026-11-16_1333.md
---
# AF-0819 - Surviving mutant: an ObliviousDoHConfigs list with bytes after its vector can be accepted

## Summary

Medium finding from the quality auditor at `Curl.Extension.ObliviousDoh.UnitLibrary/Wire/ObliviousDohConfig.cs:30`: Surviving mutant: an ObliviousDoHConfigs list with bytes after its vector can be accepted.

## Evidence

Location: `Curl.Extension.ObliviousDoh.UnitLibrary/Wire/ObliviousDohConfig.cs:30`

`list.TryReadVector(out entries) && list.IsEmpty` changed to `||` survived. A configs body with trailing bytes after the length-prefixed list (malformed under RFC 9230 section 5) is then used instead of giving null. Config_SelectFrom_MalformedList_GivesNull has no row with trailing bytes after a valid list.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Extension.ObliviousDoh.UnitLibrary/Wire/ObliviousDohConfig.cs:30:&& -Member SelectFrom -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

- 2026-11-16 | 2026-11-16_1333.md | reproduces: no | Ran the -Site command: resolvedLine 30 ('list.TryReadVector(...) && list.IsEmpty' to '||'), outcome killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-16: filed proposed.
- 2026-11-16: accepted -> closed. Re-audit 2026-11-16_1333.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
