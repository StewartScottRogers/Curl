---
id: AF-0828
title: Surviving mutant: lib570 can accept any failed PLAY as the expected session-ID mismatch
auditor: quality
severity: Medium
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Conformance.LibTest.UnitLibrary/Lib570.cs:PlayAsync-and:surviving-mutant
reproduction: mutation Curl.Conformance.LibTest.UnitLibrary/Lib570.cs:94:&&
task: none
tasks:
found: 2026-11-16
found-at: f28eba04920fb5d8dd2a1c70283f268a1891b7e5
scorecard: 2026-11-16_1333.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0828 - Surviving mutant: lib570 can accept any failed PLAY as the expected session-ID mismatch

## Summary

Medium finding from the quality auditor at `Curl.Conformance.LibTest.UnitLibrary/Lib570.cs:94`: Surviving mutant: lib570 can accept any failed PLAY as the expected session-ID mismatch. Reported by an auditor flagged unreliable in 2026-11-16_1333.md.

## Evidence

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib570.cs:94`

Mutation sample (seed 413266): '&& steps.Result == CurlCode.RtspSessionError)' mutated to '|| steps.Result == CurlCode.RtspSessionError)' survived. With the mutant, a PLAY that fails with any code (couldn't connect, recv error) resets the result to Ok and returns, so lib570 reports success where upstream's lib570 fails with 'Failed to detect a Session ID mismatch'. That changes the port's exit code and the upstream case's verdict.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib570.cs:94:&& -Member PlayAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-11-16: filed proposed.
