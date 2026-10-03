---
id: AF-0018
title: large-get peak working set is 3.7x curl's, from per-byte allocation in HttpResponseBodyReader.CopyFramedAsync
auditor: performance
severity: Medium
status: accepted
reason: 
key: performance:Curl.Protocol.Http.UnitLibrary/HttpResponseBodyReader.cs:large-get:high-memory
task: none
found: 2026-10-02
found-at: 337ed10b42ddd4d09991deaecb10826c2dedba00
scorecard: 2026-10-02_1400.md
closed:
closed-by:
---
# AF-0018 - large-get peak working set is 3.7x curl's, from per-byte allocation in HttpResponseBodyReader.CopyFramedAsync

## Summary

Medium finding from the performance auditor at `Curl.Protocol.Http.UnitLibrary/HttpResponseBodyReader.cs:294`: large-get peak working set is 3.7x curl's, from per-byte allocation in HttpResponseBodyReader.CopyFramedAsync.

## Evidence

Location: `Curl.Protocol.Http.UnitLibrary/HttpResponseBodyReader.cs:294`

large-get median peak working set: curl 9457664 bytes, candidate 34695168 bytes, 3.67 times curl's and over the 2x Medium threshold. The same code allocates a new 1-byte array per loop pass (line 294) over a 50 MiB body, which drives garbage-collector churn.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-Performance.ps1 -Iterations 20 -OutDirectory $env:TEMP\perf; (Get-Content $env:TEMP\perf\performance.json -Raw | ConvertFrom-Json).scenarios | Where-Object name -eq 'large-get' | ForEach-Object { $_.curl.medianPeakWorkingSetBytes; $_.candidate.medianPeakWorkingSetBytes }
```

- Expected: Candidate peak working set at most 2x curl's (about 19 MB).
- Actual: curl 9457664 bytes, candidate 34695168 bytes (3.67x).

## Re-audits

## Log

- 2026-10-02: filed proposed.
- 2026-10-02: proposed -> accepted.
