---
id: BL-519
title: Run transfers concurrently up to --parallel-max when -Z is given
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-509, BL-517, BL-518]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-519 — Run transfers concurrently up to --parallel-max when -Z is given

## Goal

With `-Z`, `CurlCommandRunner` starts up to `--parallel-max` transfers at once across all URLs and `--next` groups, and the run's output, `-w` lines, error lines and exit code follow BL-518's ADR; without `-Z` nothing changes.

## Context

- Conformance audit 2026-09-28, row 9 (Blocker). Options: BL-517; design: BL-518's ADR (read it first); groups: BL-509.
- Code: `Curl.Console/CurlCommandRunner.cs`, `UrlTransfer.cs`, `CurlComposition.cs`. Anything the ADR says must become safe for concurrent use and lives outside `Curl.Console` is a follow-up task, not a widening of this one; file it and depend on it if it blocks.

## Acceptance criteria

- [ ] `Curl.Console.UnitTests` tests with fake handlers that complete out of order (driven by a fake `TimeProvider` or task completion sources) show at most `--parallel-max` running at once, every transfer run once, and output, `-w` and error lines in the ADR's order.
- [ ] The exit code for mixed outcomes and the `--fail-early` behaviour match the ADR's measured cases, pinned by tests.
- [ ] A run without `-Z` behaves exactly as before (existing tests pass unchanged).
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Console` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
