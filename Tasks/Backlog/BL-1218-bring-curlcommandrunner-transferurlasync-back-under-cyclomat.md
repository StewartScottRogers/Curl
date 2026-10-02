---
id: BL-1218
title: Bring CurlCommandRunner.TransferUrlAsync back under cyclomatic complexity 10
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-02
completed:
---
# BL-1218 — Bring CurlCommandRunner.TransferUrlAsync back under cyclomatic complexity 10

## Goal

`Measure-CodeQuality.ps1 -Library Curl.Console` reports no failing member: `CurlCommandRunner.TransferUrlAsync` is back under cyclomatic complexity 10.

## Context

- Found by BL-1191 on 2026-10-02: `CurlCommandRunner.TransferUrlAsync (async)` at `Curl.Console\CurlCommandRunner.cs:1971` measures cyclomatic complexity 12 (CRAP 12), over the gate of 10, with 100% line and branch coverage. BL-1191 did not touch the file; the last change to it was BL-1188's `[MULTI]` lines (5455dac3).
- The build's CA1502 does not catch it (the async state machine is counted by the coverage tool), so only the measurement shows it. Extract a step into a private method; behaviour must not change.

## Acceptance criteria

- [ ] `Measure-CodeQuality.ps1 -Library Curl.Console` reports 0 failing members.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests pass unchanged.

## Notes

## Log

- 2026-10-02: Created.
