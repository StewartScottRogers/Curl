---
id: BL-1376
title: Bring CurlUrlAuthority.Parse and CurlUrlHost.TryNormalize under cyclomatic complexity 10
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests]
requirement: none
created: 2026-10-03
completed:
---
# BL-1376 — Bring CurlUrlAuthority.Parse and CurlUrlHost.TryNormalize under cyclomatic complexity 10

## Goal

`Measure-CodeQuality.ps1 -Library Curl.Protocol.Abstractions.UnitLibrary` reports no failing member.

## Context

- Found 2026-10-03 while checking BL-1325: the library is at 100% line and branch coverage, but two members are over the complexity limit of 10 in `CodeMetricsConfig.txt` as the measurement counts it: `CurlUrlAuthority.Parse(string, string, bool, out CurlUrlRejection)` (`CurlUrlAuthority.cs:46`, complexity 14) and `CurlUrlHost.TryNormalize(string, out string, out string, out string, out CurlUrlRejection)` (`CurlUrlHost.cs:39`, complexity 12). Neither was touched by BL-1325.
- Split each into smaller private methods without changing behaviour; the existing tests are the safety net. Never raise the threshold.

## Acceptance criteria

- [ ] `Measure-CodeQuality.ps1 -Library Curl.Protocol.Abstractions.UnitLibrary` reports 0 failing members, still at 100% line and branch.
- [ ] No test changes its expected value; `dotnet test Curl.Protocol.Abstractions.UnitTests` passes.
- [ ] `dotnet build` is clean and the fast tests are green.

## Notes

## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
