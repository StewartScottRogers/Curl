---
id: AF-0657
title: Surviving mutant: a pinned-public-key file of exactly 1 MiB can be refused
auditor: quality
severity: Medium
status: accepted
reason: 
key: quality:Curl.Networking.UnitLibrary/PinnedPublicKey.cs:ReadKeyFile-gt:surviving-mutant
reproduction: mutation Curl.Networking.UnitLibrary/PinnedPublicKey.cs:165:>
task: none
tasks:
found: 2026-11-04
found-at: 650d20c75108547e7bc74da2e86fbee9c49f7931
scorecard: 2026-11-04_0643.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0657 - Surviving mutant: a pinned-public-key file of exactly 1 MiB can be refused

## Summary

Medium finding from the quality auditor at `Curl.Networking.UnitLibrary/PinnedPublicKey.cs:165`: Surviving mutant: a pinned-public-key file of exactly 1 MiB can be refused.

## Evidence

Location: `Curl.Networking.UnitLibrary/PinnedPublicKey.cs:165`

Mutant 'file.Length > MaximumFileSize' -> '>=' survived (MaximumFileSize = 1_048_576). A key file at the limit then reads as missing and pinning fails. No test covers the boundary.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/PinnedPublicKey.cs:165:> -Member ReadKeyFile -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

## Log

- 2026-11-04: filed proposed.
- 2026-11-04: proposed -> accepted.
