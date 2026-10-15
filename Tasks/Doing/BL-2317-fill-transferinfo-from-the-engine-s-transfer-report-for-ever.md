---
id: BL-2317
title: Fill TransferInfo from the engine's transfer report for every CURLINFO_* the engine measures
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2316]
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-15
completed:
---
# BL-2317 — Fill TransferInfo from the engine's transfer report for every CURLINFO_* the engine measures

## Goal

After `ManagedEasyTransfer.PerformAsync`, `Info` holds every `CURLINFO_*` value the engine measures (response code, effective URL and method, times, sizes, speeds, IPs and ports, redirect count and URL, auth used and available, connection and transfer IDs, TLS verify result, certificates and the rest), taken from the transfer's `TransferReport`.

## Context

ADR-0506, plan phase 2 step 4; needs BL-2316. `Curl.Output.UnitLibrary/TransferWriteOutVariables.cs` shows what Curl measures today for `-w`; every value it can print has a `TransferInfo` property to fill. A `CURLINFO_*` the engine does not measure yet keeps its fresh-handle default; list each such one in Notes and file one follow-up task for them.

## Acceptance criteria

- [ ] A mapping class in `Curl.Engine.UnitLibrary` (for example `TransferInfoFromReport`) fills `TransferInfo` from `TransferReport`, and `ManagedEasyTransfer.Info` returns it after each perform.
- [ ] `TransferInfoFromReportTests` cover every property the mapping sets, one assertion each.
- [ ] This task's Notes list every `CURLINFO_*` left at its default, and a follow-up task is filed for them (its ID in Notes).
- [ ] `dotnet build` is clean (warnings are errors).
- [ ] `dotnet test --filter "TestCategory!=Integration"` passes and no existing test was deleted, skipped or weakened.
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1` passes (100% line and branch coverage, complexity <= 10, CRAP <= 30).
- [ ] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged and every case it lists still passes.

## Notes

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
