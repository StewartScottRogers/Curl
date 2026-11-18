---
id: AF-0863
title: Surviving mutant: a SAS token holding '~' can be refused
auditor: quality
severity: Medium
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Extended.Protocol.AzureStorage.UnitLibrary/Requests/AzureStorageRequestAuthorizer.cs:IsForbiddenInQuery-gt:surviving-mutant
reproduction: mutation Curl.Extended.Protocol.AzureStorage.UnitLibrary/Requests/AzureStorageRequestAuthorizer.cs:78:>
task: BL-5505
tasks: BL-5505
found: 2026-11-18
found-at: 4f902456d88c3fda856546dbe0c9a7674b1f04c4
scorecard: 2026-11-18_0810.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0863 - Surviving mutant: a SAS token holding '~' can be refused

## Summary

Medium finding from the quality auditor at `Curl.Extended.Protocol.AzureStorage.UnitLibrary/Requests/AzureStorageRequestAuthorizer.cs:78`: Surviving mutant: a SAS token holding '~' can be refused. Reported by an auditor flagged unreliable in 2026-11-18_0810.md.

## Evidence

Location: `Curl.Extended.Protocol.AzureStorage.UnitLibrary/Requests/AzureStorageRequestAuthorizer.cs:78`

Mutant 'character is <= ' ' or '#' or > '~'' -> '>= '~'' survived. '~' is an unreserved query character. With the mutant, IsSignedQuery rejects any SAS token containing it, so a valid token is refused as an invalid SAS (a changed refusal and exit code). No test passes a SAS with '~', so the upper bound is unchecked.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Extended.Protocol.AzureStorage.UnitLibrary/Requests/AzureStorageRequestAuthorizer.cs:78:> -Member IsForbiddenInQuery -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-11-18: filed proposed.
