---
id: AF-0714
title: Surviving mutant: a handle's cookie jar can be dropped or taken from a non-HTTP transfer at cleanup
auditor: quality
severity: High
status: closed
reason: Re-audit 2026-11-13_2321.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Engine.UnitLibrary/ManagedEasyTransfer.SetUp.cs:CleanUpUnlessToolDoesAsync-and:surviving-mutant
reproduction: mutation Curl.Engine.UnitLibrary/ManagedEasyTransfer.SetUp.cs:78:&&
task: BL-5073
tasks: BL-5073
found: 2026-11-12
found-at: acc2af773fb64825b3b57d9470bb6f96176fb127
scorecard: 2026-11-12_2105.md
duplicate-of:
closed: 2026-11-13
closed-how: mechanical
closed-by: 2026-11-13_2321.md
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

- 2026-11-13 | 2026-11-13_2321.md | reproduces: no | Ran the -Site command: outcome killed (cookies is not null && IsHttpUrl(...) -> ||). Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-12: filed proposed.
- 2026-11-13: proposed -> accepted.
- 2026-11-13: accepted -> closed. Re-audit 2026-11-13_2321.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
