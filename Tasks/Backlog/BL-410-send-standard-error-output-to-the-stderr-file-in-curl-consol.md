---
id: BL-410
title: Send standard error output to the --stderr file in Curl.Console
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-27
completed:
---
# BL-410 — Send standard error output to the --stderr file in Curl.Console

## Goal

`--stderr <file>` sends everything curl writes to standard error after the option, `-v` lines and warnings included, to that file; `--stderr -` sends it to standard output.

## Context

- BL-195 parses it into `CommandLineOptions.StandardErrorFile` and leaves opening it to `Curl.Console`. Its Notes, measured on curl 8.21.0: `--stderr se` sends everything written to standard error afterwards, warnings included, to `se`; `--stderr -` sends it to standard output; `--stderr ''` prints `Warning: Warning: Failed to open ` and carries on to standard error.
- BL-242 left it out. Every standard error write in `CurlCommandRunner` goes through its `standardError` stream, and `TransferEventOutput.OpenAsync` takes that stream for `-v`, so one replaced stream carries both.
- Measure first which parser warnings land in the file when `--stderr` comes after the option that raised them.

## Acceptance criteria

- [ ] `--stderr se -v` over a scripted handler writes the `-v` lines and a failure's `curl: (N)` line to `se` and nothing to standard error, as measured.
- [ ] `--stderr ''` writes the measured doubled warning to standard error and carries on.
- [ ] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Console`.

## Notes

- Filed from BL-242 (2026-09-27), which wired `-v` and `--trace` in `Curl.Console`.

## Log

- 2026-09-27: Created.
