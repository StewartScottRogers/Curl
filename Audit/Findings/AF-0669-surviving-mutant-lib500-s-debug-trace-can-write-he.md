---
id: AF-0669
title: Surviving mutant: lib500's debug trace can write hex dumps (noHex false)
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-11-05_2025.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Conformance.LibTest.UnitLibrary/Lib500.cs:TestAsync-true:surviving-mutant
reproduction: mutation Curl.Conformance.LibTest.UnitLibrary/Lib500.cs:50:true
task: BL-4857
tasks: BL-4857
found: 2026-11-04
found-at: 4749d66adb6a165fcd90aec741b5f49017616c28
scorecard: 2026-11-04_1442.md
duplicate-of:
closed: 2026-11-05
closed-how: mechanical
closed-by: 2026-11-05_2025.md
---
# AF-0669 - Surviving mutant: lib500's debug trace can write hex dumps (noHex false)

## Summary

Medium finding from the quality auditor at `Curl.Conformance.LibTest.UnitLibrary/Lib500.cs:50`: Surviving mutant: lib500's debug trace can write hex dumps (noHex false). Reported by an auditor flagged unreliable in 2026-11-04_1442.md.

## Evidence

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib500.cs:50`

Sampled mutant 'noHex: true' -> 'noHex: false' in 'new LibTestDebugTrace(context.StandardError, timeProvider, traceTime: true, noHex: true)' survived (sample -Seed 413592). Upstream lib500 sets nohex; the trace lines it writes change and no test pins them. Same caveat as Lib1540: -Site ...:50:true mutates the first 'true' (traceTime) on the line, not this noHex site.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib500.cs:50:true -Member TestAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived (sampled noHex mutant)

## Re-audits

- 2026-11-05 | 2026-11-05_0819.md | not re-audited | Ran the -Site command: outcome killed, but the site resolved line 50's first 'true', now traceTime, not noHex. The noHex mutant was not run, so the result does not tell.
- 2026-11-05 | 2026-11-05_2025.md | reproduces: no | Ran the -Site reproduction: outcome killed (resolvedLine 50; it mutated traceTime). The noHex: true -> false mutant at the same line, applied by hand in a scratch worktree, is also killed: 1 failed (RunAsync_SentHeader_TracesItWithoutAHexColumn). Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-04: filed proposed.
- 2026-11-05: accepted -> closed. Re-audit 2026-11-05_2025.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
