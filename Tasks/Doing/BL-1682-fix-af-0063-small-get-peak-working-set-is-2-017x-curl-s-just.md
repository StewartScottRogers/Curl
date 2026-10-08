---
id: BL-1682
title: Fix AF-0063: small-get peak working set is 2.017x curl's (just over the 2x threshold)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1715]
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

- 2026-10-07, lane 9 (measured, not fixed; run returned to Backlog before its time and
  token limits). The lane cannot run `Audit/Tools/Measure-Performance.ps1` (the guard hook
  refuses audit paths), so it measured the same thing itself: a loopback HTTP/1.1 server
  answering 1 KiB, `curl -s -o NUL http://127.0.0.1:<port>/`, median peak working set
  (`GetProcessMemoryInfo`) over 15 runs. Windows curl.exe 6.85 MB, Curl's native AOT
  build 14.19 MB (2.07x on this machine).
- Floor: a minimal native AOT program that opens one socket and sends one GET peaks at
  9.39 MB, so ~1.37x is the runtime's baseline (combase, the GC heap, startup).
- Where Curl's extra ~4.8 MB is (QueryWorkingSet with the transfer paused): curl.exe image
  pages 1288 (5.0 MB; .text 698 pages, .rdata 406, .data 163) against the minimal program's
  222; non-image private pages 345 against 207. The cost is image code and data that startup
  touches, not the GC heap.
- Ruled out by measurement: `DOTNET_gcConcurrent=0`, `DOTNET_GCgen0size=0x40000`,
  `DOTNET_GCHeapHardLimit` (no change); `OptimizationPreference=Size` (worse, 18.0 MB);
  `IlcFoldIdenticalMethodBodies` + `StackTraceSupport=false` (no change); `IlcDehydrate=false`
  (no effect on the binary).
- `--version` peaks at 10.9 MB, a `file://` transfer at 12.0 MB, a refused HTTP connect at
  13.6 MB, a full GET at 14.2 MB.
- Probe: building only the HTTP handler in `CurlComposition.CreateProtocolHandlers` (none of
  the other 16 handlers, the security context factory or the SASL authenticators) saves
  ~370 KB (14.18 -> 13.81 MB, 2.6%). That alone would bring the auditor's 12.30 MB to about
  11.93 MB (1.96x) but not this machine's 2.07x under 2x. Suggested fix: build handlers
  lazily per scheme (a lazy `IProtocolHandler` that knows its schemes and constructs the
  real handler on first `ExecuteAsync`), then find the remaining ~0.5 MB along the HTTP
  path (the runner's startup and the HTTP handler's static tables) with the same
  QueryWorkingSet page count.

- 2026-10-07, lane 2: split. Filed BL-1715 (lazy per-scheme protocol handlers, the first ~370 KB
  BL-1682's Notes measured) and made this task depend on it. After BL-1715 is Done, this task
  re-measures and trims the remaining ~0.5 MB on the HTTP path (runner startup, HTTP handler
  static tables) with the QueryWorkingSet page count, until the median is at most 2x curl's.

## Log

- 2026-10-08: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Backlog. Measured, not fixed: startup touches 5 MB of the AOT image; lazy protocol handlers save only 2.6% (see Notes); needs lazy handlers plus HTTP-path trimming, more than this run's budget
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Backlog. Waits on BL-1715 (lazy per-scheme protocol handlers); then trim the remaining ~0.5 MB on the HTTP path
- 2026-10-07: Backlog -> Doing.
