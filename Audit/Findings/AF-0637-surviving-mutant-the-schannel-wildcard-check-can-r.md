---
id: AF-0637
title: Surviving mutant: the Schannel wildcard check can refuse a host whose first label is one character, and no test fails
auditor: quality
severity: High
status: proposed
reason:
key: quality:Curl.Networking.UnitLibrary/SchannelCommonNameCheck.cs:WildcardMatchesHost-lt:surviving-mutant
reproduction: mutation Curl.Networking.UnitLibrary/SchannelCommonNameCheck.cs:67:<
task: none
tasks:
found: 2026-11-03
found-at: 0ae5ce3f5e65976edeb9facb28b93fd3f4dcdbc8
scorecard: 2026-11-03_2203.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0637 - Surviving mutant: the Schannel wildcard check can refuse a host whose first label is one character, and no test fails

## Summary

High finding from the quality auditor at `Curl.Networking.UnitLibrary/SchannelCommonNameCheck.cs:67`: Surviving mutant: the Schannel wildcard check can refuse a host whose first label is one character, and no test fails. Reported by an auditor flagged unreliable in 2026-11-03_2203.md.

## Evidence

Location: `Curl.Networking.UnitLibrary/SchannelCommonNameCheck.cs:67`

Invoke-MutationTest -Library Curl.Networking.UnitLibrary -MaxMutants 40 -Seed 221035: survived, 'hostLabelEnd < 1' -> 'hostLabelEnd <= 1' in WildcardMatchesHost. A host such as a.hello.com (first label one character) no longer matches a *.hello.com certificate, so the Schannel build fails a valid certificate with exit 60. The tests cover '.hello.com' (index 0) and longer labels, never a one-character label.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/SchannelCommonNameCheck.cs:67:< -Member WildcardMatchesHost -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-11-03: filed proposed.
