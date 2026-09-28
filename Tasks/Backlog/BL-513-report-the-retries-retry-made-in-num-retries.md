---
id: BL-513
title: Report the retries --retry made in %{num_retries}
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Output.UnitLibrary, Curl.Output.UnitTests, Curl.Core.UnitLibrary, Curl.Core.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-513 — Report the retries --retry made in %{num_retries}

## Goal

`%{num_retries}` (and its `%{json}` entry) prints how many times `--retry` retried the transfer, as curl 8.21.0 does, instead of a hard-wired 0.

## Context

- Conformance audit 2026-09-28, row 40 (Major; sequenced into the opening queue at Stewart's request).
- `Curl.Output.UnitLibrary/TransferWriteOutVariables.cs` maps `num_retries` to a constant `0` (the `VariableFormatters` table). ADR-0043 recorded that as correct only because `--retry` was not parsed then; it now is (`Curl.Core.UnitLibrary/TransferRetrier.cs`, `RetryPolicy.cs`; `Curl.Console/RetryPolicyMapping.cs`).
- The count has to travel from the retrier to the write-out variables; pick the smallest route (a property on whatever `TransferWriteOutVariables` is built from in `Curl.Console`).

## Acceptance criteria

- [ ] Measured first with `Record-CurlExchange.ps1 -Connections 3`: `--retry 2 --retry-delay 1 -w '%{num_retries}'` against 503, 503, 200 and against three 503s, plus `-w '%{json}'`; stdout and exit code copied into Notes.
- [ ] `Curl.Output.UnitTests` pin `num_retries` from the value it is given; `Curl.Console.UnitTests` pin the measured values end to end on a fake `TimeProvider`.
- [ ] ADR-0043's `num_retries` line is not edited here (the stale-ADR task covers it); the XML doc on the variable source says where the count comes from.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-28: Created.
