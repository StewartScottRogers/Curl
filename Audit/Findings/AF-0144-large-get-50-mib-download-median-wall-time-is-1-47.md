---
id: AF-0144
title: large-get (50 MiB download) median wall time is 1.47x curl's
auditor: performance
severity: Medium
status: accepted
reason: 
key: performance:large-get:slower-than-curl
reproduction: none
task: BL-1963
tasks: BL-1963
found: 2026-10-10
found-at: 1b27494521dec4bdaa3fe60c8dc7a3fc73874253
scorecard: 2026-10-10_0123.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0144 - large-get (50 MiB download) median wall time is 1.47x curl's

## Summary

Medium finding from the performance auditor at `Curl.Protocol.Http.UnitLibrary/ReadAheadConnectionStream.cs:22`: large-get (50 MiB download) median wall time is 1.47x curl's.

## Evidence

Location: `Curl.Protocol.Http.UnitLibrary/ReadAheadConnectionStream.cs:22`

large-get curl median 84 ms (p90 101), candidate 123.5 ms (p90 134), ratio 1.47, over the 1.25 Medium threshold. Peak working set is 16.6 MB against 8.9 MB (1.87x, under the 2x threshold). I did not pin the cause. A candidate is the 16 KiB ReadAheadConnectionStream.BufferSize (line 22), a small fixed buffer on a large transfer.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-Performance.ps1 -Iterations 20 -OutDirectory $env:TEMP\perf; (Get-Content $env:TEMP\perf\performance.json -Raw | ConvertFrom-Json).scenarios | Where-Object name -eq 'large-get' | ForEach-Object { $_.curl.medianMs; $_.candidate.medianMs }
```

- Expected: Candidate median at most 1.25 times curl's.
- Actual: curl 84 ms, candidate 123.5 ms (1.47x).

## Re-audits

## Log

- 2026-10-10: filed proposed.
- 2026-10-10: proposed -> accepted.
