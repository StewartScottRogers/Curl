---
id: AF-0340
title: ManagedCurlEngine.CreateTransports' ParallelImmediate: false can become true with no test failing
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-10-23_2322.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Engine.UnitLibrary/ManagedCurlEngine.cs:CreateTransports-false:surviving-mutant
reproduction: mutation Curl.Engine.UnitLibrary/ManagedCurlEngine.cs:318:false
task: BL-3351
tasks: BL-3120, BL-3351
found: 2026-10-22
found-at: 608a755586679cf34031a0e5b137f01b9d7fb329
scorecard: 2026-10-22_1120.md
duplicate-of:
closed: 2026-10-23
closed-how: mechanical
closed-by: 2026-10-23_2322.md
---
# AF-0340 - ManagedCurlEngine.CreateTransports' ParallelImmediate: false can become true with no test failing

## Summary

Medium finding from the quality auditor at `Curl.Engine.UnitLibrary/ManagedCurlEngine.cs:318`: ManagedCurlEngine.CreateTransports' ParallelImmediate: false can become true with no test failing.

## Evidence

Location: `Curl.Engine.UnitLibrary/ManagedCurlEngine.cs:318`

Sampled mutant (seed 0) survived: the library engine's EngineToolOptions became 'ParallelImmediate: true'. That option decides whether a transfer waits for an existing connection to show it multiplexes or opens a new one at once (curl's --parallel-immediate), so a libcurl or Curl.Api handle would change its connection reuse. No test pins the engine's tool options.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/ManagedCurlEngine.cs:318:false -Member CreateTransports -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

- 2026-10-22 | 2026-10-22_1902.md | reproduces: yes (runner rerun on clean commit) | Ran the -Site reproduction: ManagedCurlEngine.cs:318:false (CreateTransports) resolved to line 319 in the same member, outcome survived. Runner's targeted mutation rerun on the clean audited commit: survived.
- 2026-10-23 | 2026-10-23_0618.md | reproduces: yes (runner rerun on clean commit) | Ran the -Site command: site ManagedCurlEngine.cs:318:false resolved to line 337 in CreateTransports. The code now reads 'const bool RunsParallel = false;', which feeds both Parallel: and ParallelImmediate:. Mutating it to true survived. Runner's targeted mutation rerun on the clean audited commit: survived.
- 2026-10-23 | 2026-10-23_2322.md | reproduces: no | Ran the -Site reproduction: the site resolved within CreateTransports to line 344 ('ParallelImmediate: false' -> 'ParallelImmediate: true') and the mutant was killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-10-22: filed proposed.
- 2026-10-22: proposed -> accepted.
- 2026-10-23: accepted -> closed. Re-audit 2026-10-23_2322.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
