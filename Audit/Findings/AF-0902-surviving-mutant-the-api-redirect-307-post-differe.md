---
id: AF-0902
title: Surviving mutant: the api-redirect-307-post differential case can stop following redirects
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-11-20_0456.md: the reproduction no longer reproduces.
key: quality:Curl.Conformance.Differential.UnitLibrary/Api/ApiDifferentialCases.cs:All-true:surviving-mutant
reproduction: none
task: BL-5590
tasks: BL-5590
found: 2026-11-19
found-at: 8469990748da6c83b0388ea33dec77d81d9255ae
scorecard: 2026-11-19_1633.md
duplicate-of:
closed: 2026-11-20
closed-how: reliable-reaudit
closed-by: 2026-11-20_0456.md
---
# AF-0902 - Surviving mutant: the api-redirect-307-post differential case can stop following redirects

## Summary

Medium finding from the quality auditor at `Curl.Conformance.Differential.UnitLibrary/Api/ApiDifferentialCases.cs:27`: Surviving mutant: the api-redirect-307-post differential case can stop following redirects.

## Evidence

Location: `Curl.Conformance.Differential.UnitLibrary/Api/ApiDifferentialCases.cs:27`

Mutant FollowsRedirects = true -> false in new("api-redirect-307-post", "-sL -d x=1 {U}/r307", ...) survived Curl.Conformance.Differential.UnitTests. The case pairs curl -L with an API request. Mutated, the API side no longer follows the 307, so the differential compares two different transfers. No unit test pins the cases' request settings against their command lines.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.Differential.UnitLibrary/Api/ApiDifferentialCases.cs:27:true -Member All -ExcludeBaselineFailures -TimeoutSeconds 240
```

- Expected: killed
- Actual: survived

## Re-audits

- 2026-11-20 | 2026-11-20_0456.md | reproduces: no | Ran the -Site command: ApiDifferentialCases.cs:27 'true' (FollowsRedirects = true -> false) in All, resolvedLine 27, outcome killed.

## Log

- 2026-11-19: filed proposed.
- 2026-11-20: accepted -> closed. Re-audit 2026-11-20_0456.md: the reproduction no longer reproduces.
