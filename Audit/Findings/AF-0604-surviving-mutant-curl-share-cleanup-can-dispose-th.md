---
id: AF-0604
title: Surviving mutant: curl_share_cleanup can dispose the shared connections when it refuses and keep them when it succeeds without a test failing
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-11-03_1615.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Engine.UnitLibrary/ManagedShareHandle.cs:Cleanup-eq:surviving-mutant
reproduction: mutation Curl.Engine.UnitLibrary/ManagedShareHandle.cs:102:==
task: BL-4768
tasks: BL-4768
found: 2026-11-03
found-at: f4a738a932020f9832b23c0977dbaf0f27206871
scorecard: 2026-11-03_1017.md
duplicate-of:
closed: 2026-11-03
closed-how: mechanical
closed-by: 2026-11-03_1615.md
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

- 2026-11-03 | 2026-11-03_1615.md | reproduces: no | Ran the -Site command: ManagedShareHandle.cs:102 == resolved to line 102, outcome killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-03: filed proposed.
- 2026-11-03: proposed -> accepted. Accepted under Stewart's standing accept of 2026-11-02.
- 2026-11-03: accepted -> closed. Re-audit 2026-11-03_1615.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
