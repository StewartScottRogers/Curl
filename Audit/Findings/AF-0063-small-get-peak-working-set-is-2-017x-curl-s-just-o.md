---
id: AF-0063
title: small-get peak working set is 2.017x curl's (just over the 2x threshold)
auditor: performance
severity: Medium
status: accepted
reason: 
key: performance:Curl.Console:small-get:memory-heavier-than-curl
reproduction: none
task: BL-1682
tasks: BL-1682
found: 2026-10-07
found-at: 0fcb5afc262ef32bb48ad058cf1f4a2b2c68d511
scorecard: 2026-10-07_1336.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0063 - small-get peak working set is 2.017x curl's (just over the 2x threshold)

## Summary

Medium finding from the performance auditor at `Curl.Console`: small-get peak working set is 2.017x curl's (just over the 2x threshold).

## Evidence

Location: `Curl.Console`

Median peak working set: curl 6,094,848 bytes, Curl 12,296,192 bytes. Ratio 2.017x, over the 2x threshold by a thin margin. Wall time is fine (64.5 ms against curl's 67.5 ms). I did not find a cause in the code; the gap may be the native AOT runtime baseline.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-Performance.ps1 -Iterations 20 -OutDirectory $env:TEMP\perf; (Get-Content $env:TEMP\perf\performance.json -Raw | ConvertFrom-Json).scenarios | Where-Object name -eq 'small-get' | ForEach-Object { $_.curl.medianPeakWorkingSetBytes; $_.candidate.medianPeakWorkingSetBytes }
```

- Expected: Curl peak working set at most 2x curl's.
- Actual: curl 6,094,848 bytes, Curl 12,296,192 bytes (2.017x).

## Re-audits

## Log

- 2026-10-07: filed proposed.
- 2026-10-07: proposed -> accepted.
