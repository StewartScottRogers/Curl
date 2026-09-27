---
id: BL-432
title: Cover DiskWriteOutFileOpener.TryOpen in the fast test run
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-27
completed:
---
# BL-432 — Cover DiskWriteOutFileOpener.TryOpen in the fast test run

## Goal

`Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Console`, with `DiskWriteOutFileOpener.TryOpen` covered by the fast (non-Integration) run.

## Context

- `Curl.Console/DiskWriteOutFileOpener.cs` `TryOpen` shows 0% line and branch coverage in
  `Measure-CodeQuality.ps1`, because every test that drives it
  (`Curl.Console.UnitTests/DiskWriteOutFileOpenerTests.cs`) is `[TestCategory("Integration")]`
  (BL-280), and the audit measures the fast run. It is the one failing Curl.Console member,
  noted as pre-existing by BL-351, BL-375, BL-416 and BL-349.
- Choose between: a seam (an injected file opener delegate) so the logic is unit tested
  without the disk, fast tests against a temporary directory that pass on Windows, Linux and
  macOS, or an `[ExcludeFromCodeCoverage]` thin-adapter justification in the style of
  ADR-0083. Record the choice in an ADR if it departs from BL-280's reasoning.
- Base class library only; no package.

## Acceptance criteria

- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1` lists no failing member for `Curl.Console`.
- [ ] `dotnet build -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Filed by BL-349's run (2026-09-27).

## Log

- 2026-09-27: Created.
