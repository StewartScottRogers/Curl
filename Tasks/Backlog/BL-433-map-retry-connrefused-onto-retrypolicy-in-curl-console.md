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
completed:
---
# BL-433 — Map --retry-connrefused onto RetryPolicy in Curl.Console

## Goal

`RetryPolicyMapping.FromCommandLine` copies `CommandLineOptions.RetryConnectionRefused` to `RetryPolicy.RetryConnectionRefused`, so `curl --retry 2 --retry-connrefused` retries a refused connect.

## Context

- BL-390 added `RetryPolicy.RetryConnectionRefused` and the retrier rule in `Curl.Core`; `Curl.Cli` already parses `--retry-connrefused` into `CommandLineOptions.RetryConnectionRefused`. Only the mapping in `Curl.Console/RetryPolicyMapping.cs` is missing, and it was outside BL-390's `touches`.

## Acceptance criteria

- [ ] A test of `RetryPolicyMapping.FromCommandLine` shows `--retry-connrefused` sets `RetryConnectionRefused` and its absence leaves it `false`.
- [ ] `dotnet build` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports no failing member for `Curl.Console`.

## Notes

## Log

- 2026-09-27: Created.
