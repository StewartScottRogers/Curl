---
id: AF-0980
title: Surviving mutant: --max-redirs 0 can be read as unlimited when deciding whether a redirect is followed (< 0 -> <= 0)
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-11-22_1817.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Extension.OpenTelemetry.UnitLibrary/OpenTelemetryTransferHook.cs:MayFollowAnother-lt:surviving-mutant
reproduction: mutation Curl.Extension.OpenTelemetry.UnitLibrary/OpenTelemetryTransferHook.cs:108:<
task: BL-5777
tasks: BL-5777
found: 2026-11-22
found-at: fdfc7d13ff07752cdeb7bf999a56dd653660b7a2
scorecard: 2026-11-22_1351.md
duplicate-of:
closed: 2026-11-22
closed-how: mechanical
closed-by: 2026-11-22_1817.md
---
# AF-0980 - Surviving mutant: --max-redirs 0 can be read as unlimited when deciding whether a redirect is followed (< 0 -> <= 0)

## Summary

Medium finding from the quality auditor at `Curl.Extension.OpenTelemetry.UnitLibrary/OpenTelemetryTransferHook.cs:108`: Surviving mutant: --max-redirs 0 can be read as unlimited when deciding whether a redirect is followed (< 0 -> <= 0).

## Evidence

Location: `Curl.Extension.OpenTelemetry.UnitLibrary/OpenTelemetryTransferHook.cs:108`

Invoke-MutationTest -Library Curl.Extension.OpenTelemetry.UnitLibrary -MaxMutants 25 -Seed 891023: survived. Mutated 'http.MaxRedirects < 0 || http.RedirectsFollowed < http.MaxRedirects' to 'http.MaxRedirects <= 0 || ...'. With -L --max-redirs 0, a 3xx would count as a followed redirect, so the hook would defer the span export to a hop curl never makes and drop the span. OpenTelemetryRedirectTests covers maxRedirects 1 and -1 but not 0.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Extension.OpenTelemetry.UnitLibrary/OpenTelemetryTransferHook.cs:108:< -Member MayFollowAnother -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

- 2026-11-22 | 2026-11-22_1817.md | reproduces: no | Ran the -Site command: killed at line 108 ('http.MaxRedirects < 0' -> '<= 0' in MayFollowAnother). Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-22: filed proposed.
- 2026-11-22: accepted -> closed. Re-audit 2026-11-22_1817.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
