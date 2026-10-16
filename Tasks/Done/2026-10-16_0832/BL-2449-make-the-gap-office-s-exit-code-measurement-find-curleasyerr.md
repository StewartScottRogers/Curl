---
id: BL-2449
title: Make the gap office's exit-code measurement find CurlEasyErrorText in its new home (interactive, gap branch)
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Gap/Tools/Measure-ExitCodeGap.ps1, Gap/Instructions/ExitCodes.md]
lane: no
requirement: none
created: 2026-10-15
completed: 2026-10-16
---
# BL-2449 — Make the gap office's exit-code measurement find CurlEasyErrorText in its new home (interactive, gap branch)

## Goal

The gap office's exit-code measurement finds the `CURLE_*` text table at its new path under `Libcurl/Misc/ErrorText` (and still at the old one until the move), so moving `CurlEasyErrorText` changes no gap score.

## Context

ADR-0506; phase 3 of `Documentation/Planning/Curl-Api-And-libcurl.md`. Read the seam as it is in `Curl.Api.UnitLibrary` (EngineContracts, Options, Callbacks, Info, Codes) and the managed engine as it is in `Curl.Engine.UnitLibrary` now, not the plan's guesses, and correct the plan where they differ. Libcurl code and tests live in the area sub-folders named in `touches` (`Libcurl/<Area>` with its tests in `Libcurl.UnitTests/<Area>`) so areas run in parallel lanes; Libcurl tests use fakes of the seam interfaces, never the network. `--ai-help` is untouched (no option changes). Interactive only: `lane: no`, and the work is done on the `gap` branch (ADR-0433). The lane task that moves the table (Libcurl: "Move CurlEasyErrorText") depends on this one; an interactive session finishes it.

## Acceptance criteria

- [x] Done on the `gap` branch in an interactive session (`Gap/` is an audit path): a measurement run before and after the move scores the exit-code area identically.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.

## Notes

- `Gap/Tools/Measure-ExitCodeGap.ps1` now tries `Libcurl/Misc/ErrorText/CurlEasyErrorText.cs` first, then `Curl.Console/CurlEasyErrorText.cs`; a `:strerror` item's evidence names the file actually read. `Gap/Instructions/ExitCodes.md` tells `gap-exitcodes` to suggest that path and its project. Gap PR #158.
- Self-test: 18/18 PASS under Windows PowerShell 5.1 and pwsh; new checks cover the old home, the moved table scoring identically, and the evidence path.
- Real measurement (curl 8.22.0, sources copied from `work/dark-factory` 040792819 into both layouts): 259/259 match, 17 excluded, all 276 items identical in key, state and actual text.
- No `.cs`, project or `PassingUpstreamCases.txt` change; build and fast tests run on the `gap` worktree.

## Log

- 2026-10-15: Created.
- 2026-10-16: Backlog -> Doing.
- 2026-10-16: Doing -> Done. Exit-code measurement reads CurlEasyErrorText from Libcurl/Misc/ErrorText, falling back to Curl.Console; both layouts score 259/259 against curl 8.22.0 (gap PR #158)
