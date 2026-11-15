---
id: AF-0691
title: Surviving mutant: lib753's debug trace can write hex dumps (noHex false)
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-11-14_1335.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Conformance.LibTest.UnitLibrary/Lib753.cs:TestAsync-true:surviving-mutant
reproduction: mutation Curl.Conformance.LibTest.UnitLibrary/Lib753.cs:41:true
task: BL-4885
tasks: BL-4885
found: 2026-11-05
found-at: 4eba3c63638befcc6a63aaa31ddf5419922fa939
scorecard: 2026-11-05_2025.md
duplicate-of:
closed: 2026-11-14
closed-how: mechanical
closed-by: 2026-11-14_1335.md
---
# AF-0691 - Surviving mutant: lib753's debug trace can write hex dumps (noHex false)

## Summary

Medium finding from the quality auditor at `Curl.Conformance.LibTest.UnitLibrary/Lib753.cs:41`: Surviving mutant: lib753's debug trace can write hex dumps (noHex false).

## Evidence

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib753.cs:41`

Invoke-MutationTest (Curl.Conformance.LibTest.UnitLibrary, -MaxMutants 40, -Seed 982898) changed '... new LibTestDebugTrace(context.StandardError, timeProvider, traceTime: true, noHex: true));' to '... noHex: false));' and every LibTest.UnitTests test passed (survived). I confirmed this by hand in a scratch worktree: 1205/1205 passed. lib753's stderr trace would gain hex dump columns unnoticed, unlike lib500/1515/1522/1540/2700, whose noHex mutants are now killed. Note: the line has two 'true' sites. The -Site form takes the first (traceTime), so a site rerun mutates traceTime, not this noHex mutant.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib753.cs:41:true -Member TestAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

- 2026-11-12 | 2026-11-12_2105.md | not re-audited | Ran the -Site command: outcome killed, but the operator 'true' resolved to the line's first occurrence, 'traceTime: true' -> 'traceTime: false', not the 'noHex: true' the finding names. The targeted run cannot reach the noHex occurrence, so it does not tell whether that mutant still survives. Not re-audited.
- 2026-11-13 | 2026-11-13_2321.md | not re-audited | Ran the -Site command: outcome killed, but line 41 now reads new LibTestDebugTrace(..., traceTime: true, noHex: true), and the tool mutated the first true on the line (traceTime: true -> false), not the noHex value the finding is about. The run says nothing about the noHex mutant: not re-audited.
- 2026-11-14 | 2026-11-14_1335.md | reproduces: no | Ran the -Site command: Lib753.cs:41 'true' mutant (traceTime true->false) outcome killed, resolvedLine 41. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-05: filed proposed.
- 2026-11-14: accepted -> closed. Re-audit 2026-11-14_1335.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
