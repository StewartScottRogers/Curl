---
id: BL-1675
title: Fix AF-0048: large-get (50 MiB download) median wall time is 1.81x curl's
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1749]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
lane: no
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

- 2026-10-08, interactive: root cause was not the body reader. `PhysicalFileSystem` opened files with `FileOptions.Asynchronous`, so each of about 3,200 16 KiB `-o` writes was an overlapped write completed through the thread pool (about 60 ms against about 28 ms synchronous). Commit a7c6bb900 opens files synchronously and makes `DeferredOutputFileStream.WriteAsync` write synchronously once the file is open. `Measure-Performance.ps1 -Iterations 20`, large-get: before curl 93.5 ms, Curl 156.5 ms (1.67x); after curl 92.5 ms, Curl 129.5 ms (1.40x); the other five scenarios within about 3 ms of curl. A back-to-back loopback harness gives 1.04x. The rest (about 40 ms) is before `Main` and tracks binary size (on-launch scan of the 17 MB binary), so it moves to BL-1749; this task waits on it and is then re-measured interactively. The stashed 64 KiB-receive change gained under 1 ms and was dropped.

- 2026-10-07, lane 5: the fix is written and the HTTP unit tests pass (1889 passed,
  18 skipped). `HttpResponseBodyReader.CopyFramedAsync` now receives a Content-Length
  or read-to-close body in reads of up to `ReceiveSize` (64 KiB, four `ReadSize`
  pieces). It still reports and writes each 16 KiB piece separately, so the
  `passed 16384` exit-23 message and the `--trace` data events are unchanged. There
  are a quarter as many receives. The chunked path is unchanged. New test:
  `CopyAsync_LargeContentLengthBody_ReceivesInFourPieceReadsAndWritesInPiecesOfTheReadSize`.
  Why 64 KiB: it is the smallest multiple of curl's 16 KiB write size that cuts the
  number of receives by a useful amount, and the pieces written still match curl's
  (decided by Claude under Stewart's delegation).
- The work is not committed. Rule 6 of the shift sends uncommitted work to the
  shift's stash, under this task's ID.
- Still to do: the first acceptance criterion runs `Audit/Tools/Measure-Performance.ps1`.
  That is an audit path, and the guard hook stops every dark factory lane from reading
  or running it. So this task is now `lane: no`. An interactive session needs to:
  1. Apply the stashed diff.
  2. Run the reproduction.
  3. If Curl's median is at most 1.25 times curl's, tick the box, commit and move the
     task to Done.
  4. If it is not: a likely next cause is the unbuffered stdout FileStream's async
     writes, which run on the thread pool. Fixing that is a `Curl.Console` change and
     needs its own task.
- Added `Curl.Protocol.Http.UnitTests` to `touches` for the new test. No task in
  Doing names it.

## Log

- 2026-10-08: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Backlog. Fix written (64 KiB receives, 16 KiB writes), tests green, left in the shift stash; the reproduction is the audit office's performance script, which no lane may run, so it is lane: no for an interactive session to measure and finish
- 2026-10-08: Backlog -> Doing.
- 2026-10-08: Doing -> Backlog. Transfer fixed in a7c6bb900 (1.67x to 1.40x); the rest is launch cost tracking binary size, now BL-1749; re-measure interactively once it is Done
