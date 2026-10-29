---
id: BL-4085
title: Meet the quality gates for Curl.Engine.UnitLibrary after the CGR-034 probe
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-3846, BL-3847, BL-3859, BL-3890, BL-4015, BL-4016, BL-4007, BL-4072, BL-4073, BL-4069, BL-3977, BL-4075, BL-4084]
touches: [Curl.Engine.UnitTests/Cgr034Tests.cs]
requirement: none
created: 2026-10-29
completed:
---
# BL-4085 — Meet the quality gates for Curl.Engine.UnitLibrary after the CGR-034 probe

## Goal

`Curl.Engine.UnitLibrary` meets the quality gates for the files BL-3976 and BL-3977 changed, and the build and fast tests are green.

## Context

Split from BL-3978 (cost cap), the quality-gate part. Run after BL-4084 has recorded the probe results. Add tests in `Curl.Engine.UnitTests/Cgr034Tests.cs` for any uncovered line or branch.

## Acceptance criteria

- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary` reports no member outside a gate for the files BL-3976 and BL-3977 changed.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Split from BL-3978 (cost cap).

## Log

- 2026-10-29: Created.
