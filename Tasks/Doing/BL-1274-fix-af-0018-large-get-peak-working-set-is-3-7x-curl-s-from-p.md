---
id: BL-1274
title: Fix AF-0018: large-get peak working set is 3.7x curl's, from per-byte allocation in HttpResponseBodyReader.CopyFramedAsync
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary]
requirement: none
created: 2026-10-03
completed:
---
# BL-1274 — Fix AF-0018: large-get peak working set is 3.7x curl's, from per-byte allocation in HttpResponseBodyReader.CopyFramedAsync

## Goal

The defect the audit office reported as AF-0018 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0018 (Medium, performance auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0018-large-get-peak-working-set-is-3-7x-curl-s-from-per.md`.

Location: `Curl.Protocol.Http.UnitLibrary/HttpResponseBodyReader.cs:294`

Location: `Curl.Protocol.Http.UnitLibrary/HttpResponseBodyReader.cs:294`

large-get median peak working set: curl 9457664 bytes, candidate 34695168 bytes, 3.67 times curl's and over the 2x Medium threshold. The same code allocates a new 1-byte array per loop pass (line 294) over a 50 MiB body, which drives garbage-collector churn.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-Performance.ps1 -Iterations 20 -OutDirectory $env:TEMP\perf; (Get-Content $env:TEMP\perf\performance.json -Raw | ConvertFrom-Json).scenarios | Where-Object name -eq 'large-get' | ForEach-Object { $_.curl.medianPeakWorkingSetBytes; $_.candidate.medianPeakWorkingSetBytes }
```

- Expected: Candidate peak working set at most 2x curl's (about 19 MB).
- Actual: curl 9457664 bytes, candidate 34695168 bytes (3.67x).

The finding closes only when a later re-audit by the performance auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-03: Created.
- 2026-10-02: Backlog -> Doing.
