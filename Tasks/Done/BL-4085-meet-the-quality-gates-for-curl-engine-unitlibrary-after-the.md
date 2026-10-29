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
completed: 2026-10-29
---
# BL-4085 â€” Meet the quality gates for Curl.Engine.UnitLibrary after the CGR-034 probe

## Goal

`Curl.Engine.UnitLibrary` meets the quality gates for the files BL-3976 and BL-3977 changed, and the build and fast tests are green.

## Context

Split from BL-3978 (cost cap), the quality-gate part. Run after BL-4084 has recorded the probe results. Add tests in `Curl.Engine.UnitTests/Cgr034Tests.cs` for any uncovered line or branch.

## Acceptance criteria

- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary` reports no member outside a gate for the files BL-3976 and BL-3977 changed.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Split from BL-3978 (cost cap).
- Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary: 100% line, 100% branch, 1899 members, 1 failing: EngineComposition.CreateProtocolHandlers (complexity 12). That came from BL-4079's extension handlers, not from BL-3976 or BL-3977, and BL-4083 already covers it. Every member in the files BL-3976's work changed (ManagedMultiTransfer.cs, TransferSocketWait.cs, SocketActionGatedTcpDialer.cs, ConnectOnlyConnection.cs) and BL-3977's tests passes every gate, so no test was added to Cgr034Tests.cs.
- dotnet build: 0 warnings, 0 errors; fast tests passed (exit 0) on Windows. No code changed, so nothing new depends on the platform.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: d1c58933 779790c0.
- 2026-10-29: Doing -> Done. Curl.Engine.UnitLibrary's files from BL-3976 and BL-3977 meet every quality gate; build clean, fast tests green
