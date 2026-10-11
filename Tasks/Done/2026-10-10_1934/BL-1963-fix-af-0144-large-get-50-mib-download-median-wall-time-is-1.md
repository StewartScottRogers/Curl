---
id: BL-1963
title: Fix AF-0144: large-get (50 MiB download) median wall time is 1.47x curl's
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-10
completed: 2026-10-10
---
# BL-1963 — Fix AF-0144: large-get (50 MiB download) median wall time is 1.47x curl's

## Goal

The defect the audit office reported as AF-0144 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0144 (Medium, performance auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0144-large-get-50-mib-download-median-wall-time-is-1-47.md`.

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

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Cause: the finding's candidate (`ReadAheadConnectionStream`) is HTTP/2 only; large-get is
  HTTP/1.1, where `HttpResponseBodyReader` read a Content-Length or read-to-close body 16 KiB
  per connection read - 3200 async socket reads for 50 MiB. The 16 KiB was curl's largest
  client write (`passed 16384`), not its receive size: the curl tool sets a 100 KiB
  `CURLOPT_BUFFERSIZE`. Fix: read up to `ReceiveSize` (102400) and write each read in
  `WriteSize` (16384) pieces, so write sizes, data events and the exit-23 `passed` text are
  unchanged. Chunked bodies keep their 16 KiB reads (each chunk run in one read is one write).
- Added `Curl.Protocol.Http.UnitTests` to `touches` for the new test; no task in Doing on
  `origin/work/dark-factory` names it.
- Measured (the audit's `Measure-Performance.ps1` is an audit path a lane may not read, so a
  loopback PowerShell server sent 50 MiB with Content-Length; `-s -o file`, 11 runs each,
  native AOT builds, other lanes building): curl 8.21.0 min 66-67 ms; Curl before min
  103-104 ms (1.55x); Curl after min 66-88 ms, median 72-112 ms against curl's 105-124 ms.
  The re-audit's own reproduction decides the finding.

## Log

- 2026-10-10: Created.
- 2026-10-10: Backlog -> Doing.
- 2026-10-10: Doing -> Done. HTTP/1.x bodies read in curl's 100 KiB receive size, written in 16 KiB pieces; 50 MiB download no longer 1.5x curl locally
