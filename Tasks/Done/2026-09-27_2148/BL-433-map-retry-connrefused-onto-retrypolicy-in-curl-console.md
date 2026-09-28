---
id: BL-433
title: Map --retry-connrefused onto RetryPolicy in Curl.Console
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-390]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-433 — Map --retry-connrefused onto RetryPolicy in Curl.Console

## Goal

`RetryPolicyMapping.FromCommandLine` copies `CommandLineOptions.RetryConnectionRefused` to `RetryPolicy.RetryConnectionRefused`, so `curl --retry 2 --retry-connrefused` retries a refused connect.

## Context

- BL-390 added `RetryPolicy.RetryConnectionRefused` and the retrier rule in `Curl.Core`; `Curl.Cli` already parses `--retry-connrefused` into `CommandLineOptions.RetryConnectionRefused`. Only the mapping in `Curl.Console/RetryPolicyMapping.cs` is missing, and it was outside BL-390's `touches`.

## Acceptance criteria

- [x] A test of `RetryPolicyMapping.FromCommandLine` shows `--retry-connrefused` sets `RetryConnectionRefused` and its absence leaves it `false`.
- [x] `dotnet build` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports no failing member for `Curl.Console`.

## Notes

- `RetryPolicyMapping.FromCommandLine` now copies `RetryConnectionRefused`; pinned by `RetryPolicyMappingTests` (Curl.Console.UnitTests), which parses real command lines. Console: 947 passed (3 skipped); Measure-CodeQuality: Curl.Console 100/100, 0 failing, worst CRAP 10.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. curl --retry N --retry-connrefused now retries a refused connect: RetryPolicyMapping copies RetryConnectionRefused onto RetryPolicy
