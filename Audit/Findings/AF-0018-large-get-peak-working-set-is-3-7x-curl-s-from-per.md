---
id: AF-0018
title: large-get peak working set is 3.7x curl's, from per-byte allocation in HttpResponseBodyReader.CopyFramedAsync
auditor: performance
severity: Medium
status: closed
reason: Re-audit 2026-10-07_0844.md: the reproduction no longer reproduces.
key: performance:Curl.Protocol.Http.UnitLibrary/HttpResponseBodyReader.cs:large-get:high-memory
task: BL-1274
found: 2026-10-02
found-at: 337ed10b42ddd4d09991deaecb10826c2dedba00
scorecard: 2026-10-02_1400.md
closed: 2026-10-07
closed-by: 2026-10-07_0844.md
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

- 2026-10-03 | 2026-10-03_0623.md | reproduces: yes | Ran the reproduction at 20 iterations. large-get median peak working set: curl 9478144 bytes, candidate 31477760 bytes, 3.32x curl's, over the 2x threshold. The ratio is lower than the 3.7x the finding gave but still over the threshold. The cause is still in place: HttpResponseBodyReader.cs:295 allocates `new byte[1]` on every pass of the CopyFramedAsync loop. The same loop makes large-get 278x slower (see the new finding).
- 2026-10-07 | 2026-10-07_0844.md | reproduces: no | Ran the reproduction with 20 iterations. large-get median peak working set: curl 8957952 bytes, Curl 13633536 bytes, a ratio of 1.52, under the 2x threshold. The finding's 3.7x does not reproduce. CopyFramedAsync allocates one 16 KiB buffer outside its loop (HttpResponseBodyReader.cs:312) and has no per-byte allocation. large-get wall time is a separate finding.

## Log

- 2026-10-02: filed proposed.
- 2026-10-02: proposed -> accepted.
- 2026-10-07: accepted -> closed. Re-audit 2026-10-07_0844.md: the reproduction no longer reproduces.
