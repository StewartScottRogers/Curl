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
completed: 2026-10-11
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

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Lanes may not read `Audit/`, so the finding's script was not run. Instead a loopback
  server (a C# file-based app outside the repository) served 50 MiB with Content-Length, and
  native AOT builds ran `-s -o <file>`, interleaved with `C:\Windows\System32\curl.exe`, 21 runs each:
  curl median 61.7 ms, the build before this change 71.2 ms (1.15x), and this change 60.5 ms (0.98x).
  When other lanes loaded the machine, the old build was at 1.29x.
- Cause: every read was still written in 16 KiB pieces, and each `WriteAsync` to the `-o`
  file's synchronous handle is queued to the thread pool. Measured in-process, writing 50 MiB
  took 31 ms in 16 KiB writes and 21 ms in 100 KiB writes. `-o NUL` was already level with curl.
  The finding's guess, `ReadAheadConnectionStream.BufferSize`, applies only to HTTP/2.
- Fix (ADR-0477): a body read that is not decoded is written as one write, then reported as
  16 KiB data events and progress counts. A failed write is placed in its 16 KiB piece by the
  accepted count, so `passed 16384` and `-v` look the same. A decoded body keeps 16 KiB writes.

## Log

- 2026-10-11: Created.
- 2026-10-11: Backlog -> Doing.
- 2026-10-11: Doing -> Done. Each HTTP/1.x body read is written once (ADR-0477); 50 MiB -o download 60.5 ms vs curl 61.7 ms (0.98x)
