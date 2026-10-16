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
completed:
---
# BL-2449 — Make the gap office's exit-code measurement find CurlEasyErrorText in its new home (interactive, gap branch)

## Goal

The gap office's exit-code measurement finds the `CURLE_*` text table at its new path under `Libcurl/Misc/ErrorText` (and still at the old one until the move), so moving `CurlEasyErrorText` changes no gap score.

## Context

ADR-0506; phase 3 of `Documentation/Planning/Curl-Api-And-libcurl.md`. Read the seam as it is in `Curl.Api.UnitLibrary` (EngineContracts, Options, Callbacks, Info, Codes) and the managed engine as it is in `Curl.Engine.UnitLibrary` now, not the plan's guesses, and correct the plan where they differ. Libcurl code and tests live in the area sub-folders named in `touches` (`Libcurl/<Area>` with its tests in `Libcurl.UnitTests/<Area>`) so areas run in parallel lanes; Libcurl tests use fakes of the seam interfaces, never the network. `--ai-help` is untouched (no option changes). Interactive only: `lane: no`, and the work is done on the `gap` branch (ADR-0433). The lane task that moves the table (Libcurl: "Move CurlEasyErrorText") depends on this one; an interactive session finishes it.

## Acceptance criteria

- [ ] Done on the `gap` branch in an interactive session (`Gap/` is an audit path): a measurement run before and after the move scores the exit-code area identically.
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [ ] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.

## Notes

## Log

- 2026-10-15: Created.
