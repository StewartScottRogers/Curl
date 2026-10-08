---
id: BL-1682
title: Fix AF-0063: small-get peak working set is 2.017x curl's (just over the 2x threshold)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console]
requirement: none
created: 2026-10-08
completed:
---
# BL-1682 — Fix AF-0063: small-get peak working set is 2.017x curl's (just over the 2x threshold)

## Goal

The defect the audit office reported as AF-0063 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0063 (Medium, performance auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0063-small-get-peak-working-set-is-2-017x-curl-s-just-o.md`.

Location: `Curl.Console`

Location: `Curl.Console`

Median peak working set: curl 6,094,848 bytes, Curl 12,296,192 bytes. Ratio 2.017x, over the 2x threshold by a thin margin. Wall time is fine (64.5 ms against curl's 67.5 ms). I did not find a cause in the code; the gap may be the native AOT runtime baseline.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-Performance.ps1 -Iterations 20 -OutDirectory $env:TEMP\perf; (Get-Content $env:TEMP\perf\performance.json -Raw | ConvertFrom-Json).scenarios | Where-Object name -eq 'small-get' | ForEach-Object { $_.curl.medianPeakWorkingSetBytes; $_.candidate.medianPeakWorkingSetBytes }
```

- Expected: Curl peak working set at most 2x curl's.
- Actual: curl 6,094,848 bytes, Curl 12,296,192 bytes (2.017x).

The finding closes only when a later re-audit by the performance auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-08: Created.
- 2026-10-07: Backlog -> Doing.
