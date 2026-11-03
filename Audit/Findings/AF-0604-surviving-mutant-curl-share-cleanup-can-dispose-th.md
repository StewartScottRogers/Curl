---
id: AF-0604
title: Surviving mutant: curl_share_cleanup can dispose the shared connections when it refuses and keep them when it succeeds without a test failing
auditor: quality
severity: Medium
status: proposed
reason:
key: quality:Curl.Engine.UnitLibrary/ManagedShareHandle.cs:Cleanup-eq:surviving-mutant
reproduction: mutation Curl.Engine.UnitLibrary/ManagedShareHandle.cs:102:==
task: none
tasks:
found: 2026-11-03
found-at: f4a738a932020f9832b23c0977dbaf0f27206871
scorecard: 2026-11-03_1017.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0604 - Surviving mutant: curl_share_cleanup can dispose the shared connections when it refuses and keep them when it succeeds without a test failing

## Summary

Medium finding from the quality auditor at `Curl.Engine.UnitLibrary/ManagedShareHandle.cs:102`: Surviving mutant: curl_share_cleanup can dispose the shared connections when it refuses and keep them when it succeeds without a test failing. Reported by an auditor flagged unreliable in 2026-11-03_1017.md.

## Evidence

Location: `Curl.Engine.UnitLibrary/ManagedShareHandle.cs:102`

if (code == CurlShareCode.Ok) -> != survived (sample and targeted rerun): a cleanup refused as InUse then disposes the pool the attached transfers still use, and a successful one leaks it.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/ManagedShareHandle.cs:102:== -Member Cleanup -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-11-03: filed proposed.
