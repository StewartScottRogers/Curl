---
id: BL-097
title: Bring Curl.Core.UnitLibrary PhysicalFileSystem to 100% line and branch coverage
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-097 — Bring Curl.Core.UnitLibrary PhysicalFileSystem to 100% line and branch coverage

## Goal

`powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Core.UnitLibrary`
exits 0 on Windows, with every member of `PhysicalFileSystem` at 100% line and 100%
branch coverage.

## Context

Found during BL-094 (2026-09-26). On Windows the command above exits 1: the library
measures 91.11% line and 87.5% branch across 13 members, and 4 members fail, all in
`Curl.Core.UnitLibrary\FileSystem\PhysicalFileSystem.cs`:

| Member | Line | Coverage | Uncovered |
| --- | --- | --- | --- |
| `OpenForWriteAsync(string, FileWriteMode, UnixFileMode, CancellationToken)` | 90 | line 75%, branch 50% | lines 98, 99 |
| `Open(string, FileStreamOptions)` | 144 | line 66.67% | lines 151-153 |
| `LengthOf(FileStream)` | 164 | branch 50% | one branch |
| `LastWriteTimeUtcOf(FileStream)` | 173 | line 57.14% | lines 178-180 |

(Line numbers are as of BL-094; re-measure before starting.) Some of these lines may be
POSIX-only (for example a `UnixFileMode` branch) or error paths that a real file cannot
easily reach. Where a line is unreachable on Windows, this task decides how to cover it,
for example by introducing a narrow seam (an injected delegate or an `internal`
overload the tests can drive) or by restructuring so the platform-specific decision is a
testable pure function. It must not raise any threshold, exclude the code from coverage,
or add a NuGet package (MSTest only; see the root `CLAUDE.md`). Tests stay off the
network and must not need `TestCategory=Integration`; temporary files under
`Path.GetTempPath()` are fine.

## Acceptance criteria

- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Core.UnitLibrary` exits 0 on Windows.
- [x] `dotnet build Curl.Core.UnitLibrary -warnaserror` and `dotnet build Curl.Core.UnitTests -warnaserror` are clean.
- [x] `dotnet test --filter "TestCategory!=Integration"` is green, and no new test carries `TestCategory=Integration`.
- [x] `CodeMetricsConfig.txt` and the thresholds in `Measure-CodeQuality.ps1` are unchanged (`git diff master -- CodeMetricsConfig.txt Measure-CodeQuality.ps1` shows no threshold change).
- [x] No `[ExcludeFromCodeCoverage]` attribute or coverage exclusion is added to `PhysicalFileSystem` or its members.

## Notes

- Cause: every disk test in `PhysicalFileSystemTests` carried `[TestCategory("Integration")]`, and `Measure-CodeQuality.ps1` measures the fast run only. With `-IncludeIntegration` the library was already 100%/100%.
- Choice (default taken, unattended run): remove the `Integration` category from those tests rather than add a seam. They use only a temporary directory under `Path.GetTempPath()` and the null device, which the task allows; no production code changed. The POSIX-only tests stay behind `OSCondition`, and `setsUnixCreateMode: true` on Windows was already covered by the fast test that expects `PlatformNotSupportedException`.
- Also updated the class summary and `Curl.Core.UnitLibrary/CLAUDE.md`, which said the disk tests were `Integration`.
- Ran the change directly rather than the full `/feature` pipeline: it is a test-category change with no production code to plan or implement.
- Measured: Curl.Core.UnitLibrary 100% line, 100% branch, 19 members, 0 failing, worst CRAP 8; `Measure-CodeQuality.ps1` exits 0. Fast suite: Curl.Core.UnitTests 67 passed, 2 skipped (POSIX-only).

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. PhysicalFileSystem is 100% line and branch covered by the fast suite; Measure-CodeQuality.ps1 -Library Curl.Core.UnitLibrary exits 0
