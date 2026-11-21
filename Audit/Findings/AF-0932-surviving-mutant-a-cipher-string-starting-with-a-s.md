---
id: AF-0932
title: Surviving mutant: a cipher string starting with a separator can be read as one element
auditor: quality
severity: Medium
status: proposed
reason:
key: quality:Curl.Networking.UnitLibrary/OpenSslCipherString.cs:ApplyNextRule-lt:surviving-mutant
reproduction: mutation Curl.Networking.UnitLibrary/OpenSslCipherString.cs:195:<
task: none
tasks:
found: 2026-11-21
found-at: 57bece6c118995fac41401b81793a8395d3fb16d
scorecard: 2026-11-21_0835.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0932 - Surviving mutant: a cipher string starting with a separator can be read as one element

## Summary

Medium finding from the quality auditor at `Curl.Networking.UnitLibrary/OpenSslCipherString.cs:195`: Surviving mutant: a cipher string starting with a separator can be read as one element. Reported by an auditor flagged unreliable in 2026-11-21_0835.md.

## Evidence

Location: `Curl.Networking.UnitLibrary/OpenSslCipherString.cs:195`

Seed 812510 sample: var element = end < 0 ? _value[_position..] : _value[_position..end]; mutated to end <= 0 survived. With a separator at index 0 (a --ciphers value starting with ':', ' ', ';' or ','), the mutant takes the whole rest as one selection, changing the parsed list and whether curl refuses it. No test starts a cipher string with a separator.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/OpenSslCipherString.cs:195:< -Member ApplyNextRule -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-11-21: filed proposed.
