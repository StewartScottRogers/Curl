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
completed: 2026-09-27
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

- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1` lists no failing member for `Curl.Console`.
- [x] `dotnet build -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Filed by BL-349's run (2026-09-27).
- Delivered in the session rather than through the full `/feature` agent chain: a test-category change and one method restructured, no new behaviour.
- Choice: fast tests against a temporary directory. `DiskWriteOutFileOpenerTests` drops `[TestCategory("Integration")]` from its six disk tests, following `PhysicalOutputPathsTests`, which already runs disk tests in the fast run for the same coverage gate. BL-280 gave no reason for the category beyond "opens real files", so this does not depart from a recorded decision and no ADR was written. The tests are platform-neutral: empty name (`ArgumentException`), missing directory (`DirectoryNotFoundException`) and a directory (`UnauthorizedAccessException` on Windows, Linux and macOS alike, as .NET's `FileStream` refuses a directory there too); text mode is chosen by the constructor argument, not the OS.
- Once `TryOpen` was covered the audit showed one other failing Curl.Console member, `DumpHeaderOutputStream.WriteAsync` (from 61ae865b): an `await` inside a `catch` that ends in `throw;` makes the compiler emit a "caught object is not an Exception" rethrow branch nothing can take, leaving the catch's closing brace and a branch uncovered. `WriteAsync` now captures the `IOException` with `ExceptionDispatchInfo`, awaits the failure report after the catch and rethrows with `failure.Throw()`, keeping the original stack trace. Same behaviour; its existing tests pass unchanged.
- `Measure-CodeQuality.ps1 -Library Curl.Console`: 100% line, 100% branch, 365 members, 0 failing, worst CRAP 10. `dotnet build -warnaserror`: 0 warnings. Fast run: all green, Curl.Console.UnitTests 945 passed and 3 skipped, the six disk tests included.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. DiskWriteOutFileOpener.TryOpen is covered by the fast run and Curl.Console measures 100% line and branch with no failing member
