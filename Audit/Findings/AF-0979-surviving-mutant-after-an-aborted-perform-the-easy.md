---
id: AF-0979
title: Surviving mutant: after an aborted perform the easy handle can keep Digest as still available (?? false -> ?? true)
auditor: quality
severity: Medium
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Engine.UnitLibrary/ManagedEasyTransfer.Parsed.cs:KeepHandleStateOf-false:surviving-mutant
reproduction: mutation Curl.Engine.UnitLibrary/ManagedEasyTransfer.Parsed.cs:134:false
task: BL-5776
tasks: BL-5776
found: 2026-11-22
found-at: fdfc7d13ff07752cdeb7bf999a56dd653660b7a2
scorecard: 2026-11-22_1351.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0979 - Surviving mutant: after an aborted perform the easy handle can keep Digest as still available (?? false -> ?? true)

## Summary

Medium finding from the quality auditor at `Curl.Engine.UnitLibrary/ManagedEasyTransfer.Parsed.cs:134`: Surviving mutant: after an aborted perform the easy handle can keep Digest as still available (?? false -> ?? true).

## Evidence

Location: `Curl.Engine.UnitLibrary/ManagedEasyTransfer.Parsed.cs:134`

Invoke-MutationTest -Library Curl.Engine.UnitLibrary -MaxMutants 40 -Seed 891023 (3 tests that already fail left out): survived. Mutated 'keptDigestStaysAvailable = result?.Report?.DigestStaysAvailable ?? false;' to '?? true'. After a perform that was aborted, or that returned no report, the handle would treat the kept Digest challenge as reusable, so the next perform on the handle could answer it again. That decides whether an Authorization header is sent. No test covers the null-result or null-report case.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/ManagedEasyTransfer.Parsed.cs:134:false -Member KeepHandleStateOf -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

## Log

- 2026-11-22: filed proposed.
