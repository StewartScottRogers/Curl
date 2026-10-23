---
id: BL-3351
title: Re-fix AF-0340: ManagedCurlEngine.CreateTransports' ParallelImmediate: false can become true with no test failing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary]
requirement: none
created: 2026-10-23
completed:
---
# BL-3351 — Re-fix AF-0340: ManagedCurlEngine.CreateTransports' ParallelImmediate: false can become true with no test failing

## Goal

The defect the audit office reported as AF-0340 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0340 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0340-managedcurlengine-createtransports-parallelimmedia.md`.

Re-fix: the earlier task(s) BL-3120 reached Done, and a later re-audit by the quality auditor found the reproduction still reproduces:

- 2026-10-23 | 2026-10-23_0618.md | reproduces: yes (runner rerun on clean commit) | Ran the -Site command: site ManagedCurlEngine.cs:318:false resolved to line 337 in CreateTransports. The code now reads 'const bool RunsParallel = false;', which feeds both Parallel: and ParallelImmediate:. Mutating it to true survived. Runner's targeted mutation rerun on the clean audited commit: survived.

Location: `Curl.Engine.UnitLibrary/ManagedCurlEngine.cs:318`

Location: `Curl.Engine.UnitLibrary/ManagedCurlEngine.cs:318`

Sampled mutant (seed 0) survived: the library engine's EngineToolOptions became 'ParallelImmediate: true'. That option decides whether a transfer waits for an existing connection to show it multiplexes or opens a new one at once (curl's --parallel-immediate), so a libcurl or Curl.Api handle would change its connection reuse. No test pins the engine's tool options.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/ManagedCurlEngine.cs:318:false -Member CreateTransports -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
