---
id: AF-0048
title: large-get (50 MiB download) median wall time is 1.81x curl's
auditor: performance
severity: Medium
status: accepted
reason: 
key: performance:Curl.Protocol.Http.UnitLibrary/HttpResponseBodyReader.cs:large-get:slower-than-curl
reproduction: none
task: BL-1675
tasks: BL-1675
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
- 2026-10-08 | 2026-10-08_0748.md | not re-audited | overlaps planted defect PD-203 in Curl.Protocol.Http.UnitLibrary/HttpResponseBodyReader.cs, so the auditor's verdict (reproduces yes) is set aside: Ran the reproduction with 20 iterations: large-get curl median 91.5 ms, candidate 165.5 ms, which is 1.81x. Still over the 1.25x threshold. Cause is the MemoryStream buffering in HttpResponseBodyReader.CopyFramedAsync (line 321).
- 2026-10-08 | 2026-10-08_2315.md | not re-audited | overlaps planted defect PD-203 in Curl.Protocol.Http.UnitLibrary/HttpResponseBodyReader.cs, so the auditor's verdict (reproduces yes) is set aside: Ran Measure-Performance.ps1 with 20 iterations: large-get curl median 95.5 ms, candidate 161.5 ms (1.69x). The ratio is down from the 1.81x recorded, but it is still over the 1.25x Medium threshold. Cause: the full-body MemoryStream in HttpResponseBodyReader.cs:326.
- 2026-10-09 | 2026-10-09_0647.md | not re-audited | overlaps planted defect PD-203 in Curl.Protocol.Http.UnitLibrary/HttpResponseBodyReader.cs, so the auditor's verdict (reproduces yes) is set aside: Ran the reproduction with 20 iterations: large-get curl median 106.5 ms, candidate 145.5 ms, a ratio of 1.37x. That is lower than the 1.81x the finding recorded but still over the 1.25x Medium threshold. Cause still present: the unused MemoryStream 'whole' at HttpResponseBodyReader.cs lines 329 and 341.

## Log

- 2026-10-07: filed proposed.
- 2026-10-07: proposed -> accepted.
