---
id: AF-0802
title: Surviving mutant: lib573 can write its debug trace with hex dumps, unlike upstream's nohex trace
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-11-16_1333.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Conformance.LibTest.UnitLibrary/Lib573.cs:RunAsync-true:surviving-mutant
reproduction: mutation Curl.Conformance.LibTest.UnitLibrary/Lib573.cs:31:true
task: BL-5264
tasks: BL-5253, BL-5264
found: 2026-11-15
found-at: 2a5b0f025b7c8fef02cb85fb555414235eb4e132
scorecard: 2026-11-15_1610.md
duplicate-of:
closed: 2026-11-16
closed-how: mechanical
closed-by: 2026-11-16_1333.md
---
# AF-0802 - Surviving mutant: lib573 can write its debug trace with hex dumps, unlike upstream's nohex trace

## Summary

Medium finding from the quality auditor at `Curl.Conformance.LibTest.UnitLibrary/Lib573.cs:31`: Surviving mutant: lib573 can write its debug trace with hex dumps, unlike upstream's nohex trace.

## Evidence

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib573.cs:31`

The seed 770877 sample of Curl.Conformance.LibTest.UnitLibrary: 'new LibTestDebugTrace(context.StandardError, timeProvider, traceTime: true, noHex: true)' -> 'noHex: false' in RunAsync survived. The program's standard-error trace then carries hex dumps that upstream's lib573 configuration leaves out. No test of Lib573 checks the trace text, so the configured trace format is unpinned.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib573.cs:31:true -Member RunAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

- 2026-11-16 | 2026-11-16_0514.md | reproduces: yes (runner rerun on clean commit) | Ran the -Site command: Lib573.cs:31 resolved to the line's first `true` (traceTime: true -> false), and it survived. The same mutant also survived in the sampled LibTest run with seed 213433. Runner's targeted mutation rerun on the clean audited commit: survived.
- 2026-11-16 | 2026-11-16_1333.md | reproduces: no | Ran the -Site command: resolvedLine 31, outcome killed. The site now mutates the first 'true' on the line (traceTime: true to false), not noHex, so I also flipped noHex: true to false by hand in a scratch worktree at the audited commit: Curl.Conformance.LibTest.UnitTests then failed 1 of 1220 (Lib573_DebugTrace_DumpsDataWithoutHexColumn). Both mutants are killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-15: filed proposed.
- 2026-11-16: accepted -> closed. Re-audit 2026-11-16_1333.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
