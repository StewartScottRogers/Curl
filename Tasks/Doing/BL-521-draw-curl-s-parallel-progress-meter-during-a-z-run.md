---
id: BL-521
title: Draw curl's parallel progress meter during a -Z run
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-519]
touches: [Curl.Output.UnitLibrary, Curl.Output.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-521 — Draw curl's parallel progress meter during a -Z run

## Goal

A `-Z` run without `-s` writes curl 8.21.0's combined parallel progress meter to standard error (its own header and columns for transfers, live transfers, totals and speed), not one single-transfer meter per URL.

## Context

- Conformance audit 2026-09-28, row 9 (Blocker).
- The single-transfer meter: `Curl.Console/ProgressMeterLines.cs`, `ProgressMeterFields.cs`, `TransferProgressRecorder.cs`; ADR-0082, ADR-0092 and ADR-0099 describe how it is drawn from the handlers' byte reports.
- The parallel meter's header and column layout must be measured, not recalled: run the reference curl with `-Z` against `Record-CurlExchange.ps1 -Connections 2 -ResponseDelayMilliseconds 1500` and capture standard error.

## Acceptance criteria

- [ ] Measured first as above (and with `-#`, which curl may ignore in parallel mode); stderr copied into Notes byte for byte.
- [ ] Tests on a fake `TimeProvider` pin the header, a mid-run line and the final line for two transfers as measured.
- [ ] `-s` and `--no-progress-meter` suppress it, with tests.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
