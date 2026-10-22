---
id: AF-0315
title: ManagedMultiTransfer.RemainingMilliseconds' || can become && with no test failing, so curl_multi_timeout answers 0 instead of -1 for running transfers with no timeout
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-10-22_1120.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Engine.UnitLibrary/ManagedMultiTransfer.cs:RemainingMilliseconds-or:surviving-mutant
reproduction: mutation Curl.Engine.UnitLibrary/ManagedMultiTransfer.cs:831:||
task: BL-3068
tasks: BL-3068
found: 2026-10-22
found-at: 167e765b2a009a4887fc971075283ef9d24a73a1
scorecard: 2026-10-22_0130.md
duplicate-of:
closed: 2026-10-22
closed-how: mechanical
closed-by: 2026-10-22_1120.md
---
# AF-0315 - ManagedMultiTransfer.RemainingMilliseconds' || can become && with no test failing, so curl_multi_timeout answers 0 instead of -1 for running transfers with no timeout

## Summary

Medium finding from the quality auditor at `Curl.Engine.UnitLibrary/ManagedMultiTransfer.cs:831`: ManagedMultiTransfer.RemainingMilliseconds' || can become && with no test failing, so curl_multi_timeout answers 0 instead of -1 for running transfers with no timeout.

## Evidence

Location: `Curl.Engine.UnitLibrary/ManagedMultiTransfer.cs:831`

Sampled mutant (seed 0) survived: 'if (entry.Done || entry.Perform is null || limit <= TimeSpan.Zero) return -1;' with the first || turned into &&. For a running transfer with no CURLOPT_TIMEOUT (limit 0), the mutant skips the early return and computes Math.Max(0, ceil(0 - elapsed)) = 0. GetTimeout (line 380) then takes that 0 as its minimum, so it answers 0 instead of -1. WaitAsync caps its wait at GetTimeout, so a multi loop would spin. curl_multi_timeout's answer is visible to every Libcurl and Curl.Api multi user.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/ManagedMultiTransfer.cs:831:|| -Member RemainingMilliseconds -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

- 2026-10-22 | 2026-10-22_1120.md | reproduces: no | Ran the -Site reproduction: resolvedLine 831, RemainingMilliseconds mutant killed (score 1). Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-10-22: filed proposed.
- 2026-10-22: proposed -> accepted.
- 2026-10-22: accepted -> closed. Re-audit 2026-10-22_1120.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
