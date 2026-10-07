---
id: BL-1599
title: Move Curl.Cli.UnitTests' two Integration tests into a new Curl.Cli.IntegrationTests project
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1597]
touches: [Curl.Cli.UnitTests, Curl.Cli.IntegrationTests, Curl.slnx]
requirement: none
created: 2026-10-07
completed:
---
# BL-1599 — Move Curl.Cli.UnitTests' two Integration tests into a new Curl.Cli.IntegrationTests project

## Goal

`Curl.Cli.UnitTests` holds no `[TestCategory("Integration")]` test; its two disk-checking tests run, unchanged in what they assert, from a new `Curl.Cli.IntegrationTests` project in `Curl.slnx`.

## Context

- Rule approved by Stewart on 2026-10-07: an integration test (one that touches something real outside the process - socket, disk, OS, native API, system agent) carries `[TestCategory("Integration")]` and lives only in a `Curl.<Area>.IntegrationTests` project; no `*.UnitTests` project contains one, and every test in an `*.IntegrationTests` project carries the category. BL-1598 records it in an ADR; this task does not wait for that.
- The two tests, in `Curl.Cli.UnitTests/CommandLineTlsOptionTests.cs` (around lines 142-164): `Parse_DefaultCheckGivenAnExistingFile_RecordsIt` and `Parse_DefaultCheckGivenAnExistingDirectory_RecordsIt`. They call `CommandLineParser.Parse` without a path-exists stub, so the real file system answers for the test assembly's own path and `AppContext.BaseDirectory`. They use the class's `private const string Url = "https://example.com/";`.
- Template: `Curl.Networking.IntegrationTests/Curl.Networking.IntegrationTests.csproj` (from `6ecb2781f`, made buildable by BL-1597): the MSTest `PackageReference` with no version, `<Using Include="Microsoft.VisualStudio.TestTools.UnitTesting" />`, and one `ProjectReference`, here `..\Curl.Cli.UnitLibrary\Curl.Cli.UnitLibrary.csproj`. Do not add an `MSTestSettings.cs` to the project: `Directory.Build.props` links the shared `MSTestSettings.cs` and `TestDiagnostics.cs` into every project whose name ends in `IntegrationTests`, and a second copy is `error CS0579` (BL-1597). `CommandLineParser` and `CommandLineParseResult` are public, so no `InternalsVisibleTo` is needed.
- Name the new file `CommandLineTlsOptionIntegrationTests.cs`, class `CommandLineTlsOptionIntegrationTests`, namespace `Curl.Cli` (matching `Curl.Networking.IntegrationTests`' naming). Each moved method keeps its name and its `[TestCategory("Integration")]`.
- `Curl.slnx` is one flat alphabetical run: add `Curl.Cli.IntegrationTests/Curl.Cli.IntegrationTests.csproj` immediately before `Curl.Cli.UnitLibrary/Curl.Cli.UnitLibrary.csproj` (line 52 today).
- Coverage is measured from the fast run only (`Measure-CodeQuality.ps1` passes `--filter "TestCategory!=Integration"`), and these tests were already outside it, so `Curl.Cli.UnitLibrary`'s coverage should not move. If it does, add a fast unit test in `Curl.Cli.UnitTests` that reaches the lost lines through the path-exists seam other tests in the file use (`NoPathExists`).
- Platform-neutral: both tests use paths the runtime gives them, so they pass on Windows, Linux and macOS as they did.

## Acceptance criteria

- [ ] `Curl.Cli.IntegrationTests/Curl.Cli.IntegrationTests.csproj` exists, matches the template's shape, has no `MSTestSettings.cs` of its own, and is listed in `Curl.slnx` immediately before `Curl.Cli.UnitLibrary`.
- [ ] `grep -rn '^\s*\[.*TestCategory("Integration")' Curl.Cli.UnitTests --include=*.cs` finds nothing, and every `[TestMethod]` in `Curl.Cli.IntegrationTests` carries `[TestCategory("Integration")]`.
- [ ] `dotnet build -warnaserror` at the repository root is clean.
- [ ] `dotnet test --filter "TestCategory!=Integration"` is green.
- [ ] `dotnet test Curl.Cli.IntegrationTests --filter "TestCategory=Integration"` runs `Parse_DefaultCheckGivenAnExistingFile_RecordsIt` and `Parse_DefaultCheckGivenAnExistingDirectory_RecordsIt` and both pass.
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary` reports 100% line and 100% branch coverage for `Curl.Cli.UnitLibrary`. Notes record its line and branch figures before and after the move; if it was below 100% before, the after figures are no lower and Notes name the follow-up task for the existing gap.

## Notes

## Log

- 2026-10-07: Created.
