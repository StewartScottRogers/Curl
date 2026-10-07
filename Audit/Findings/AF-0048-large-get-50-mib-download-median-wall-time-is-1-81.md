---
id: AF-0048
title: large-get (50 MiB download) median wall time is 1.81x curl's
auditor: performance
severity: Medium
status: proposed
reason:
key: performance:Curl.Protocol.Http.UnitLibrary/HttpResponseBodyReader.cs:large-get:slower-than-curl
task: none
found: 2026-10-07
found-at: 5a627a2fb4baf7b4b2662dc309939ec576dcad20
scorecard: 2026-10-07_0844.md
closed:
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

## Log

- 2026-10-07: filed proposed.
