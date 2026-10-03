---
id: BL-1274
title: Fix AF-0018: large-get peak working set is 3.7x curl's, from per-byte allocation in HttpResponseBodyReader.CopyFramedAsync
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1288]
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

- 2026-10-02, lane 7: the finding's named cause is not in the code. `CopyFramedAsync` allocates its
  16 KiB buffer once, before the loop (line 292); line 294 is the `while`, and no per-byte array
  exists. The body reader's private memory over a 50 MiB GET is small: Curl's whole private working
  set during the transfer is 7.2 MB, of which 5.4 MB is already there at `--version`.
- Measured on this machine (Windows 11, `K32GetProcessMemoryInfo` peak after exit, median of 5):
  Windows `curl.exe` 7.47 MB; Curl 20.38 MB with `-o` (2.73x), 21.61 MB to piped stdout; a bare
  NativeAOT hello-world alone is 9.54 MB (1.28x curl). So the excess is the native build's startup
  footprint, in `Curl.Console` and `Curl.Cli.UnitLibrary`, outside this task's `touches`.
- Found one cause: `Environment.GetFolderPath(SpecialFolder.UserProfile)`, called on every run by
  `CurlComposition` and `DefaultConfigFileSearch.ForProcess` for a value only read off Windows,
  loads `shell32.dll` and `windows.storage.dll`. Skipping it on Windows cut the `-o` run to 17.56 MB
  (2.35x) and stdout to 18.78 MB (2.51x) - still over 2x. GC environment settings, size-optimised
  ILC and lazy P/Invokes gave nothing.
- The reproduction script is under `Audit/`, which the audit guard refuses to a lane, so a lane
  cannot tick the first criterion itself; an interactive session (or the re-audit) runs it.
- Filed BL-1288 (touches Curl.Console, Curl.Cli.UnitLibrary and their tests; no task in Doing on
  `origin/work/dark-factory` touches them) for the shell32 fix and the remaining ~2.6-3.8 MB, with the
  measurement method. This task waits on it, then only needs the reproduction run.

## Log

- 2026-10-03: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Backlog. Waits on BL-1288: the excess working set is Curl.Console/Curl.Cli startup footprint, not HttpResponseBodyReader; shell32 fix alone reaches 2.35x, not 2x
- 2026-10-03: Backlog -> Doing.
