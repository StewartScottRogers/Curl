---
id: AF-0340
title: ManagedCurlEngine.CreateTransports' ParallelImmediate: false can become true with no test failing
auditor: quality
severity: Medium
status: accepted
reason: 
key: quality:Curl.Engine.UnitLibrary/ManagedCurlEngine.cs:CreateTransports-false:surviving-mutant
reproduction: mutation Curl.Engine.UnitLibrary/ManagedCurlEngine.cs:318:false
task: none
tasks:
found: 2026-10-22
found-at: 608a755586679cf34031a0e5b137f01b9d7fb329
scorecard: 2026-10-22_1120.md
duplicate-of:
closed:
closed-how:
closed-by:
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

## Log

- 2026-10-22: filed proposed.
- 2026-10-22: proposed -> accepted.
