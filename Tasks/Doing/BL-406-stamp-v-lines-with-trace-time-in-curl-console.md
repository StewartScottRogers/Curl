---
id: BL-406
title: Stamp -v lines with --trace-time in Curl.Console
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-358]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-27
completed:
---
# BL-406 — Stamp -v lines with --trace-time in Curl.Console

## Goal

Under `-v --trace-time` (and `-vv`), `Curl.Console` builds `VerboseTransferEventWriter` with the runner's clock and stamping on, so `-v` lines carry curl's `HH:MM:SS.uuuuuu ` prefix.

## Context

- BL-358 teaches `VerboseTransferEventWriter` to stamp; BL-242 builds it in `Curl.Console/TransferEventOutput.cs` without stamps.
- `CommandLineOptions.TraceTime` already carries the switch (BL-195).

## Acceptance criteria

- [ ] Over a scripted handler, `-s -v --trace-time` writes the stamped lines BL-358 measured, from the runner's injected clock.
- [ ] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Console`.

## Notes

- Filed from BL-242 (2026-09-27), which wired `-v` and `--trace` in `Curl.Console`.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
