---
id: BL-2043
title: Re-fix AF-0144: large-get (50 MiB download) median wall time is 1.47x curl's
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary]
requirement: none
created: 2026-10-11
completed:
---
# BL-2043 — Re-fix AF-0144: large-get (50 MiB download) median wall time is 1.47x curl's

## Goal

The defect the audit office reported as AF-0144 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0144 (Medium, performance auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0144-large-get-50-mib-download-median-wall-time-is-1-47.md`.

Re-fix: the earlier task(s) BL-1963 reached Done, and a later re-audit by the performance auditor found the reproduction still reproduces:

- 2026-10-11 | 2026-10-11_0051.md | reproduces: yes | Ran the reproduction at 20 iterations: large-get curl median 92 ms, candidate 128 ms, 1.39x, still over the 1.25x threshold (the finding recorded 1.47x). HttpResponseBodyReader.cs now receives 102400 bytes per read, but still writes in 16384-byte pieces.

Location: `Curl.Protocol.Http.UnitLibrary/ReadAheadConnectionStream.cs:22`

Location: `Curl.Protocol.Http.UnitLibrary/ReadAheadConnectionStream.cs:22`

large-get curl median 84 ms (p90 101), candidate 123.5 ms (p90 134), ratio 1.47, over the 1.25 Medium threshold. Peak working set is 16.6 MB against 8.9 MB (1.87x, under the 2x threshold). I did not pin the cause. A candidate is the 16 KiB ReadAheadConnectionStream.BufferSize (line 22), a small fixed buffer on a large transfer.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-Performance.ps1 -Iterations 20 -OutDirectory $env:TEMP\perf; (Get-Content $env:TEMP\perf\performance.json -Raw | ConvertFrom-Json).scenarios | Where-Object name -eq 'large-get' | ForEach-Object { $_.curl.medianMs; $_.candidate.medianMs }
```

- Expected: Candidate median at most 1.25 times curl's.
- Actual: curl 84 ms, candidate 123.5 ms (1.47x).

The finding closes only when a later re-audit by the performance auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-11: Created.
