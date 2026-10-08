---
id: AF-0048
title: large-get (50 MiB download) median wall time is 1.81x curl's
auditor: performance
severity: Medium
status: accepted
reason: 
key: performance:Curl.Protocol.Http.UnitLibrary/HttpResponseBodyReader.cs:large-get:slower-than-curl
reproduction: none
task: none
tasks:
found: 2026-10-07
found-at: 5a627a2fb4baf7b4b2662dc309939ec576dcad20
scorecard: 2026-10-07_0844.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0048 - large-get (50 MiB download) median wall time is 1.81x curl's

## Summary

Medium finding from the performance auditor at `Curl.Protocol.Http.UnitLibrary/HttpResponseBodyReader.cs:27`: large-get (50 MiB download) median wall time is 1.81x curl's.

## Evidence

Location: `Curl.Protocol.Http.UnitLibrary/HttpResponseBodyReader.cs:27`

large-get median 167.5 ms (p90 202) for Curl against 92.5 ms (p90 103) for curl: a ratio of 1.81, over the 1.25 Medium threshold and under the 2.0 High one. Cause not confirmed. The body copy (CopyFramedAsync, line 294) reads in 16 KiB pieces (ReadSize = 16384, line 27) and goes through the ReportReceived and WriteAsync layers for every piece. The standard-output stream is an unbuffered FileStream, so small writes may be the cost, but I did not measure that.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-Performance.ps1 -Iterations 20 -OutDirectory $env:TEMP\perf; (Get-Content $env:TEMP\perf\performance.json -Raw | ConvertFrom-Json).scenarios | Where-Object name -eq 'large-get' | ForEach-Object { $_.curl.medianMs; $_.candidate.medianMs }
```

- Expected: Curl's median is at most 1.25 times curl's (about 116 ms or less).
- Actual: curl 92.5 ms, Curl 167.5 ms (1.81x).

## Re-audits

- 2026-10-07 | 2026-10-07_1336.md | not re-audited | overlaps planted defect PD-203 in Curl.Protocol.Http.UnitLibrary/HttpResponseBodyReader.cs, so the auditor's verdict (reproduces yes) is set aside: Ran the reproduction with 20 iterations: large-get curl median 195.5 ms, Curl median 263.0 ms, a ratio of 1.35x. That is over the 1.25x Medium threshold, so the slowdown still reproduces, though the 1.81x in the finding was not matched on this run. Cause is the whole-body MemoryStream in HttpResponseBodyReader.CopyFramedAsync (lines 327-347).

## Log

- 2026-10-07: filed proposed.
- 2026-10-07: proposed -> accepted.
