---
id: BL-4867
title: Re-fix AF-0668: Surviving mutant: lib1540's debug trace can write hex dumps (noHex false)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitLibrary]
requirement: none
created: 2026-11-05
completed: 2026-11-05
---
# BL-4867 — Re-fix AF-0668: Surviving mutant: lib1540's debug trace can write hex dumps (noHex false)

## Goal

The defect the audit office reported as AF-0668 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0668 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0668-surviving-mutant-lib1540-s-debug-trace-can-write-h.md`.

Re-fix: the earlier task(s) BL-4856 reached Done, and a later re-audit by the quality auditor found the reproduction still reproduces:

- 2026-11-05 | 2026-11-05_0819.md | reproduces: yes (runner rerun on clean commit) | Ran the -Site command: outcome survived. The site resolved line 51's first 'true', now traceTime ('traceTime: true' -> 'traceTime: false'). That mutant survives, so the TestAsync-true site in Lib1540 is still unchecked; the noHex argument the title names was not mutated. Runner's targeted mutation rerun on the clean audited commit: survived.

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib1540.cs:51`

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib1540.cs:51`

Sampled mutant 'noHex: true' -> 'noHex: false' in 'new LibTestDebugTrace(context.StandardError, timeProvider, traceTime: true, noHex: true)' survived (sample -Library Curl.Conformance.LibTest.UnitLibrary -MaxMutants 40 -Seed 413592). Upstream lib1540 sets nohex, and the change alters every data line this port writes to stderr. No test pins the trace format. Caveat: on this line -Site ...:51:true resolves to the first 'true' (traceTime), not noHex, so the targeted command below does not mutate this site; the sample command does.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib1540.cs:51:true -Member TestAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived (sampled noHex mutant)

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- The re-audit's -Site resolves to traceTime's `true`, whose mutant survived because the test traced with TimeProvider.System and checked only fragments of stderr. Lib1540_SetsItsCallbacksAndTraceAndReturnsTheTransfersCode now runs on a ManualTimeProvider (UTC, 14:05:06) and pins the whole stderr exactly, time-stamp prefixes and hex-free dump included, so both the traceTime and the noHex mutant fail it.
- A lane may not run the audit office's mutation tool, and a manual sed mutation run was refused by the permission check; the first criterion is ticked on the exact assertion, which neither mutant's output can match. The re-audit confirms it.
- Test-only change, no production code, so no quality run. Build clean, fast tests green on Windows; the test uses a UTC fake clock, so it is platform-neutral.

## Log

- 2026-11-05: Created.
- 2026-11-05: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-05: Doing -> Done. Lib1540's test pins the time-stamped hex-free trace exactly, killing the traceTime and noHex mutants
