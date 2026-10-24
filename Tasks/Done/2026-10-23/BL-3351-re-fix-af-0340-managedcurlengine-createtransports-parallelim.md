---
id: BL-3351
title: Re-fix AF-0340: ManagedCurlEngine.CreateTransports' ParallelImmediate: false can become true with no test failing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests/ManagedCurlEngineTests.cs, Curl.Engine.UnitTests/ManagedCurlEngineTests.Transports.cs]
requirement: none
created: 2026-10-23
completed: 2026-10-23
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

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Cause: BL-3120's shared `RunsParallel` constant left an equivalent mutant. The pool reads the two flags only as `Parallel && !ParallelImmediate`, so with both `false` (or both `true`) nothing changed and no test could kill it.
- Fix (ADR-0806): a library handle's transports now take `Parallel` from `CURLOPT_PIPEWAIT` (`EasyHttpOptions.PipeWait`) and `ParallelImmediate` `false`. The pool therefore waits for multiplexing exactly under PIPEWAIT, as the curl tool's `-Z` without `--parallel-immediate` does. `ManagedCurlEngine.CreateTransports` is now internal so `ManagedCurlEngineTests.Transports.cs` can pin both answers. `ManagedCurlEngineTests` became partial to hold that file.
- Touches: added the two `Curl.Engine.UnitTests` files the test lives in. No task in Doing on origin/work/dark-factory names them. Left the library entry whole because the library's CLAUDE.md also changed.
- Criterion 1: a lane may not run the audit office's mutation tool, so I applied the finding's mutant by hand. With `ParallelImmediate: true` in CreateTransports, `CreateTransports_PoolingConnector_WaitsForMultiplexingOnlyUnderPipeWait` failed (1 of 2 cases), so the mutant is killed. I then reverted it. CreateTransports now holds one `false` literal, and it is that one.
- Gates: dotnet build is clean, and the fast tests pass in all 46 projects with 0 failures. Measure-CodeQuality -Library Curl.Engine.UnitLibrary reports 0 failing members. The test is platform-neutral.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-10-23: Doing -> Done. A library handle's CURLOPT_PIPEWAIT now sets the pool's WaitsForMultiplexing, and a test pins it, so the ParallelImmediate mutant is killed
