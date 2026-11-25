---
id: AF-0981
title: Surviving mutant: a 300 Multiple Choices with a Location can be treated as not a redirect (>= 300 -> > 300)
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-11-22_1817.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Extension.OpenTelemetry.UnitLibrary/OpenTelemetryTransferHook.cs:IsRedirect-ge:surviving-mutant
reproduction: mutation Curl.Extension.OpenTelemetry.UnitLibrary/OpenTelemetryTransferHook.cs:111:>=
task: BL-5778
tasks: BL-5778
found: 2026-11-22
found-at: fdfc7d13ff07752cdeb7bf999a56dd653660b7a2
scorecard: 2026-11-22_1351.md
duplicate-of:
closed: 2026-11-22
closed-how: mechanical
closed-by: 2026-11-22_1817.md
---
# AF-0981 - Surviving mutant: a 300 Multiple Choices with a Location can be treated as not a redirect (>= 300 -> > 300)

## Summary

Medium finding from the quality auditor at `Curl.Extension.OpenTelemetry.UnitLibrary/OpenTelemetryTransferHook.cs:111`: Surviving mutant: a 300 Multiple Choices with a Location can be treated as not a redirect (>= 300 -> > 300).

## Evidence

Location: `Curl.Extension.OpenTelemetry.UnitLibrary/OpenTelemetryTransferHook.cs:111`

Invoke-MutationTest -Library Curl.Extension.OpenTelemetry.UnitLibrary -MaxMutants 25 -Seed 891023: survived. Mutated 'result.Report is { ResponseCode: >= 300 and < 400, RedirectUrl: not null }' to '> 300 and < 400'. A followed 300 hop would export its own span and start a new trace instead of carrying the chain's trace id. The redirect tests use only 302 and 307.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Extension.OpenTelemetry.UnitLibrary/OpenTelemetryTransferHook.cs:111:>= -Member IsRedirect -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

- 2026-11-22 | 2026-11-22_1817.md | reproduces: no | Ran the -Site command: killed at line 111 ('ResponseCode: >= 300' -> '> 300' in IsRedirect). Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-22: filed proposed.
- 2026-11-22: accepted -> closed. Re-audit 2026-11-22_1817.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
