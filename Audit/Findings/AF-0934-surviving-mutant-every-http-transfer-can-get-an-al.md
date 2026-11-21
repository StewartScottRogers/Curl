---
id: AF-0934
title: Surviving mutant: every HTTP transfer can get an Alt-Svc cache without --alt-svc
auditor: quality
severity: Medium
status: proposed
reason:
key: quality:Curl.Engine.UnitLibrary/TransferPerformer.Setup.cs:OpenAltSvcCacheAsync-and:surviving-mutant
reproduction: mutation Curl.Engine.UnitLibrary/TransferPerformer.Setup.cs:391:&&
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
# AF-0934 - Surviving mutant: every HTTP transfer can get an Alt-Svc cache without --alt-svc

## Summary

Medium finding from the quality auditor at `Curl.Engine.UnitLibrary/TransferPerformer.Setup.cs:391`: Surviving mutant: every HTTP transfer can get an Alt-Svc cache without --alt-svc. Reported by an auditor flagged unreliable in 2026-11-21_0835.md.

## Evidence

Location: `Curl.Engine.UnitLibrary/TransferPerformer.Setup.cs:391`

Seed 812510 sample: if (http.AltSvc is not null && IsHttpUrl(...)) mutated to || survived. AltSvcTransferCache.OpenAsync accepts a null AltSvc (file = http.AltSvc ?? string.Empty), so the mutant gives every HTTP transfer an in-memory Alt-Svc cache, and Alt-Svc headers would move later connections of the run although curl ignores them without --alt-svc. No test checks that no cache opens without the option.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/TransferPerformer.Setup.cs:391:&& -Member OpenAltSvcCacheAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-11-21: filed proposed.
