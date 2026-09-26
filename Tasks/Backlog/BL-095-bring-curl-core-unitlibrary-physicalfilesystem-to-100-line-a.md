---
id: BL-095
title: Bring Curl.Core.UnitLibrary PhysicalFileSystem to 100% line and branch coverage
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-095 — Bring Curl.Core.UnitLibrary PhysicalFileSystem to 100% line and branch coverage

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

- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Core.UnitLibrary` exits 0 on Windows.
- [ ] `dotnet build Curl.Core.UnitLibrary -warnaserror` and `dotnet build Curl.Core.UnitTests -warnaserror` are clean.
- [ ] `dotnet test --filter "TestCategory!=Integration"` is green, and no new test carries `TestCategory=Integration`.
- [ ] `CodeMetricsConfig.txt` and the thresholds in `Measure-CodeQuality.ps1` are unchanged (`git diff master -- CodeMetricsConfig.txt Measure-CodeQuality.ps1` shows no threshold change).
- [ ] No `[ExcludeFromCodeCoverage]` attribute or coverage exclusion is added to `PhysicalFileSystem` or its members.

## Notes

## Log

- 2026-09-26: Created.
