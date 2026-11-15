---
id: AF-0750
title: Surviving mutant: a gs:// response with status 300 can be taken as success
auditor: quality
severity: High
status: closed
reason: Re-audit 2026-11-14_1801.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Extended.Protocol.GoogleCloudStorage.UnitLibrary/Client/GcsClient.cs:ExchangeAsync-ge:surviving-mutant
reproduction: mutation Curl.Extended.Protocol.GoogleCloudStorage.UnitLibrary/Client/GcsClient.cs:51:>=
task: BL-5199
tasks: BL-5199
found: 2026-11-14
found-at: fc4d1268bd4c1bce123dba90334b07e141be698a
scorecard: 2026-11-14_1335.md
duplicate-of:
closed: 2026-11-14
closed-how: mechanical
closed-by: 2026-11-14_1801.md
---
# AF-0750 - Surviving mutant: a gs:// response with status 300 can be taken as success

## Summary

High finding from the quality auditor at `Curl.Extended.Protocol.GoogleCloudStorage.UnitLibrary/Client/GcsClient.cs:51`: Surviving mutant: a gs:// response with status 300 can be taken as success.

## Evidence

Location: `Curl.Extended.Protocol.GoogleCloudStorage.UnitLibrary/Client/GcsClient.cs:51`

Mutant 'if (response.StatusCode >= 300)' -> 'if (response.StatusCode > 300)' survived the twin's tests (seed 465187 sample). A 300 Multiple Choices reply would then be read as the object (exit 0 with the error body as output) instead of failing with exit 22. The tests cover 403 and 404 but no 300 boundary.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Extended.Protocol.GoogleCloudStorage.UnitLibrary/Client/GcsClient.cs:51:>= -Member ExchangeAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

- 2026-11-14 | 2026-11-14_1801.md | reproduces: no | Ran the -Site command: GcsClient.cs:51 'if (response.StatusCode >= 300)' -> '> 300', outcome killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-14: filed proposed.
- 2026-11-14: accepted -> closed. Re-audit 2026-11-14_1801.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
