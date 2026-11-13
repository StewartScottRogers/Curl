---
id: AF-0714
title: Surviving mutant: a handle's cookie jar can be dropped or taken from a non-HTTP transfer at cleanup
auditor: quality
severity: High
status: proposed
reason:
key: quality:Curl.Engine.UnitLibrary/ManagedEasyTransfer.SetUp.cs:CleanUpUnlessToolDoesAsync-and:surviving-mutant
reproduction: mutation Curl.Engine.UnitLibrary/ManagedEasyTransfer.SetUp.cs:78:&&
task: none
tasks:
found: 2026-11-12
found-at: acc2af773fb64825b3b57d9470bb6f96176fb127
scorecard: 2026-11-12_2105.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0714 - Surviving mutant: a handle's cookie jar can be dropped or taken from a non-HTTP transfer at cleanup

## Summary

High finding from the quality auditor at `Curl.Engine.UnitLibrary/ManagedEasyTransfer.SetUp.cs:78`: Surviving mutant: a handle's cookie jar can be dropped or taken from a non-HTTP transfer at cleanup. Reported by an auditor flagged unreliable in 2026-11-12_2105.md.

## Evidence

Location: `Curl.Engine.UnitLibrary/ManagedEasyTransfer.SetUp.cs:78`

Mutant 'cookies is not null && IsHttpUrl(...)' -> 'cookies is not null || IsHttpUrl(...)' survived. With it, an HTTP transfer with no cookie engine sets handleCookieJar to null (the jar written at cleanup loses its cookies), and an FTP transfer with cookies replaces the jar. Both change the bytes of the CURLOPT_COOKIEJAR file.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/ManagedEasyTransfer.SetUp.cs:78:&& -Member CleanUpUnlessToolDoesAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

## Log

- 2026-11-12: filed proposed.
