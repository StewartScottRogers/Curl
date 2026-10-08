---
id: BL-1601
title: Move PhysicalFileSystemTests' Windows NUL date test into a new Curl.Core.IntegrationTests project
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1597]
touches: [Curl.Core.UnitTests, Curl.Core.IntegrationTests, Curl.slnx]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1601 — Move PhysicalFileSystemTests' Windows NUL date test into a new Curl.Core.IntegrationTests project

## Goal

`Curl.Core.UnitTests` holds no `[TestCategory("Integration")]` test; `OpenForReadAsync_WindowsNullDevice_ReportsCurlsMeasuredLastModifiedDate` runs, unchanged in what it asserts, from a new `Curl.Core.IntegrationTests` project in `Curl.slnx`.

## Context

- Rule approved by Stewart on 2026-10-07: an integration test (one that touches something real outside the process - socket, disk, OS, native API, system agent) carries `[TestCategory("Integration")]` and lives only in a `Curl.<Area>.IntegrationTests` project; no `*.UnitTests` project contains one, and every test in an `*.IntegrationTests` project carries the category. BL-1598 records it in an ADR; this task does not wait for that.
- The one test (counted 2026-10-07; the file has two matches for the string, the other is its class doc comment): `Curl.Core.UnitTests/FileSystem/PhysicalFileSystemTests.cs`, `OpenForReadAsync_WindowsNullDevice_ReportsCurlsMeasuredLastModifiedDate` (around line 278), `[OSCondition(OperatingSystems.Windows)]`, which opens the real `NUL` device and pins curl 8.21.0's measured `Last-Modified: Thu, 01 Jan 1970 00:00:00 GMT`. Move it with the comment above it that records the measurement. It uses the class's `TestContext` property and its private static `ActResult(TestDiagnostics, FileOpenResult)` helper (around line 672): copy `ActResult` into the new class, and keep the original only if other tests still call it.
- New file `Curl.Core.IntegrationTests/FileSystem/PhysicalFileSystemIntegrationTests.cs`, class `PhysicalFileSystemIntegrationTests`, namespace `Curl.Core.FileSystem`, with the same `using`s the test needs (`Curl.Protocol.Abstractions` for `FileOpenResult`; `TestDiagnostics` is linked into every test project by `Directory.Build.props`).
- Template: `Curl.Networking.IntegrationTests/Curl.Networking.IntegrationTests.csproj` (made buildable by BL-1597): the MSTest `PackageReference` with no version, the `Using`, one `ProjectReference`, here `..\Curl.Core.UnitLibrary\Curl.Core.UnitLibrary.csproj`. No `MSTestSettings.cs` of its own (a copy is `error CS0579`). `PhysicalFileSystem` is public, so no `InternalsVisibleTo` is needed. If the test needs anything from `Curl.Testing` that only `Curl.Core.UnitTests` sees, say what in Notes and link that file with `<Compile Include=... Link=...>` rather than referencing the `.UnitTests` project.
- Update `PhysicalFileSystemTests`' class doc comment (lines 9-15), which says the NUL pin is the one Integration test in the class: after the move it is true only if it says that pin now lives in `Curl.Core.IntegrationTests`.
- `Curl.slnx` is one flat alphabetical run: add `Curl.Core.IntegrationTests/Curl.Core.IntegrationTests.csproj` immediately before `Curl.Core.UnitLibrary/Curl.Core.UnitLibrary.csproj` (line 60 today).
- Coverage is measured from the fast run only, and the test was already outside it, so `Curl.Core.UnitLibrary`'s coverage should not move. If it does, add a fast test in `Curl.Core.UnitTests` that reaches the lost lines without the `NUL` device.
- Leave the other, untagged temp-directory tests in `PhysicalFileSystemTests` where they are; whether they count as Integration tests is BL-1598's decision 4.

## Acceptance criteria

- [x] `Curl.Core.IntegrationTests/Curl.Core.IntegrationTests.csproj` exists, matches the template's shape, has no `MSTestSettings.cs` of its own, and is listed in `Curl.slnx` immediately before `Curl.Core.UnitLibrary`.
- [x] `grep -rn '^\s*\[.*TestCategory("Integration")' Curl.Core.UnitTests --include=*.cs` finds nothing, every `[TestMethod]` in `Curl.Core.IntegrationTests` carries `[TestCategory("Integration")]`, and `PhysicalFileSystemTests`' class doc comment says where the NUL pin lives.
- [x] `dotnet build -warnaserror` at the repository root is clean.
- [x] `dotnet test --filter "TestCategory!=Integration"` is green.
- [x] `dotnet test Curl.Core.IntegrationTests --filter "TestCategory=Integration"` runs `OpenForReadAsync_WindowsNullDevice_ReportsCurlsMeasuredLastModifiedDate` and it passes on Windows.
- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Core.UnitLibrary` reports 100% line and 100% branch coverage for `Curl.Core.UnitLibrary`. Notes record its line and branch figures before and after the move; if it was below 100% before, the after figures are no lower and Notes name the follow-up task for the existing gap.

## Notes

- Nothing from `Curl.Testing` beyond the shared `TestDiagnostics` (linked by `Directory.Build.props`) was needed, so no `Compile Link` and no reference to `Curl.Core.UnitTests`. `ActResult` was copied; the original stays because 20-odd unit tests still call it.
- Coverage of `Curl.Core.UnitLibrary` (Measure-CodeQuality -Library, 2026-10-07 20:19): after the move 100% line, 100% branch, 577 members, 0 failing, worst CRAP 10. Before is the same by construction: coverage is measured from the `TestCategory!=Integration` run, which already excluded this test, so the fast tests that run are unchanged; not re-measured to save the 30-minute run.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. NUL date pin moved to new Curl.Core.IntegrationTests; build clean, fast tests green, Core coverage 100/100
