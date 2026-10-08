---
id: BL-1675
title: Fix AF-0048: large-get (50 MiB download) median wall time is 1.81x curl's
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary]
requirement: none
created: 2026-10-08
completed:
---
# BL-1675 — Fix AF-0048: large-get (50 MiB download) median wall time is 1.81x curl's

## Goal

The defect the audit office reported as AF-0048 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0048 (Medium, performance auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0048-large-get-50-mib-download-median-wall-time-is-1-81.md`.

Location: `Curl.Protocol.Http.UnitLibrary/HttpResponseBodyReader.cs:27`

Location: `Curl.Protocol.Http.UnitLibrary/HttpResponseBodyReader.cs:27`

large-get median 167.5 ms (p90 202) for Curl against 92.5 ms (p90 103) for curl: a ratio of 1.81, over the 1.25 Medium threshold and under the 2.0 High one. Cause not confirmed. The body copy (CopyFramedAsync, line 294) reads in 16 KiB pieces (ReadSize = 16384, line 27) and goes through the ReportReceived and WriteAsync layers for every piece. The standard-output stream is an unbuffered FileStream, so small writes may be the cost, but I did not measure that.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-Performance.ps1 -Iterations 20 -OutDirectory $env:TEMP\perf; (Get-Content $env:TEMP\perf\performance.json -Raw | ConvertFrom-Json).scenarios | Where-Object name -eq 'large-get' | ForEach-Object { $_.curl.medianMs; $_.candidate.medianMs }
```

- Expected: Curl's median is at most 1.25 times curl's (about 116 ms or less).
- Actual: curl 92.5 ms, Curl 167.5 ms (1.81x).

The finding closes only when a later re-audit by the performance auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-08: Created.
