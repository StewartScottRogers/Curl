---
id: AF-0249
title: SplitAtFirstColon's colon < 0 can become <= 0 with no test failing, mis-splitting a proxy user:password that starts with ':'
auditor: quality
severity: High
status: accepted
reason: 
key: quality:Curl.Engine.UnitLibrary/ManagedEasyTransfer.cs:static-lt:surviving-mutant
reproduction: mutation Curl.Engine.UnitLibrary/ManagedEasyTransfer.cs:1755:<
task: BL-2760
tasks: BL-2760
found: 2026-10-18
found-at: 19ea231be8b4d749462d15058a3c6b97c7e70836
scorecard: 2026-10-18_1045.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0249 - SplitAtFirstColon's colon < 0 can become <= 0 with no test failing, mis-splitting a proxy user:password that starts with ':'

## Summary

High finding from the quality auditor at `Curl.Engine.UnitLibrary/ManagedEasyTransfer.cs:1755`: SplitAtFirstColon's colon < 0 can become <= 0 with no test failing, mis-splitting a proxy user:password that starts with ':'. Reported by an auditor flagged unreliable in 2026-10-18_1045.md.

## Evidence

Location: `Curl.Engine.UnitLibrary/ManagedEasyTransfer.cs:1755`

Invoke-MutationTest -Library Curl.Engine.UnitLibrary -MaxMutants 40 -Seed 0: survived. Original `return colon < 0 ? (userPassword, string.Empty) : (userPassword[..colon], userPassword[(colon + 1)..]);`, mutated `colon <= 0`. With --proxy-user ':secret' the original gives user '' and password 'secret'. The mutant gives user ':secret' and an empty password, so the Proxy-Authorization request bytes change. The tool named the member 'static'; the method is SplitAtFirstColon.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/ManagedEasyTransfer.cs:1755:< -Member static -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-10-18: filed proposed.
- 2026-10-18: proposed -> accepted.
