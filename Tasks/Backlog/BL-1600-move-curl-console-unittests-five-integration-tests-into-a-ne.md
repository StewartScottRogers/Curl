---
id: BL-1600
title: Move Curl.Console.UnitTests' five Integration tests into a new Curl.Console.IntegrationTests project
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1597]
touches: [Curl.Console.UnitTests, Curl.Console.IntegrationTests, Curl.Console, Curl.slnx]
requirement: none
created: 2026-10-07
completed:
---
# BL-1600 — Move Curl.Console.UnitTests' five Integration tests into a new Curl.Console.IntegrationTests project

## Goal

`Curl.Console.UnitTests` holds no `[TestCategory("Integration")]` test; its five loopback, disk and extended-attribute tests run, unchanged in what they assert, from a new `Curl.Console.IntegrationTests` project in `Curl.slnx`.

## Context

- Rule approved by Stewart on 2026-10-07: an integration test (one that touches something real outside the process - socket, disk, OS, native API, system agent) carries `[TestCategory("Integration")]` and lives only in a `Curl.<Area>.IntegrationTests` project; no `*.UnitTests` project contains one, and every test in an `*.IntegrationTests` project carries the category. BL-1598 records it in an ADR; this task does not wait for that.
- The five tests (counted 2026-10-07; the request that led here listed seven files, but `DiskWriteOutFileOpenerTests.cs`, `KerberosDiskFileReaderTests.cs`, `KerberosDiskFileWriterTests.cs` and `PhysicalOutputPathsTests.cs` mention `[TestCategory("Integration")]` only in their class doc comments and hold no such test, so they stay where they are):
  - `CurlCompositionDnsServersTests.cs`: `CreateTransports_WithDnsServersOnLoopback_ConnectsToTheAddressTheServerAnswered` (a loopback UDP DNS server and TCP listener), with its private helpers `AnswerOneQueryAsync` and the class's `Parse`.
  - `CurlCompositionTests.cs`: `CreateRunner_WriteOutOutputFile_OpensItOnDisk` and `CreateRunnerOverConnectors_WriteOutFileOpenerGiven_OpensTheOutputFileOnDisk` (temp-directory files through `-w %output{}`). The second uses `RecordingConnector`, `RecordingDatagramConnector` (`Curl.Console.UnitTests/RecordingConnector.cs`, `RecordingDatagramConnector.cs`) and the class's `ConnectFailure` constant.
  - `NativeExtendedAttributeWriterTests.cs`: `TryWrite_OnAFileOnLinuxOrMacOS_SetsTheAttribute` and `TryWrite_OnAMissingFile_FailsWithTheSystemText` (real xattr calls, `[OSCondition(Linux | OSX | FreeBSD)]`, kept). `ForCurrentPlatform_OnWindows_GivesNoWriter` is not Integration and stays.
- Put each moved test in a file named after its source class with `IntegrationTests` in place of `Tests` (e.g. `CurlCompositionDnsServersIntegrationTests.cs`), namespace `Curl.Console`, each method keeping its name, attributes and `[TestCategory("Integration")]`.
- Template: `Curl.Networking.IntegrationTests/Curl.Networking.IntegrationTests.csproj` (made buildable by BL-1597): the MSTest `PackageReference` with no version, the `Using`, one `ProjectReference`, here `..\Curl.Console\Curl.Console.csproj`. No `MSTestSettings.cs` of its own: `Directory.Build.props` links the shared one and `TestDiagnostics.cs`, and a copy is `error CS0579`.
- `CurlComposition` and `NativeExtendedAttributeWriter` are `internal`: add `<InternalsVisibleTo Include="Curl.Console.IntegrationTests" />` beside the existing ones in `Curl.Console/Curl.Console.csproj` (line 49 today).
- Shared fakes: do not reference `Curl.Console.UnitTests` from the new project (`Curl.Console.UnitTests` already needs `NoWarn CS0436` in `Directory.Build.props` for a second `TestDiagnostics` copy, and a project reference would repeat that). Link the two fake files instead, e.g. `<Compile Include="..\Curl.Console.UnitTests\RecordingConnector.cs" Link="Fakes\RecordingConnector.cs" />`, plus whatever those files need, or copy the minimum the one test uses; say which in Notes. Remove a helper from the `.UnitTests` file only when nothing there still uses it (`ConnectFailure` is used by many remaining tests).
- `Curl.slnx` is one flat alphabetical run: `Curl.Console.IntegrationTests/Curl.Console.IntegrationTests.csproj` goes immediately before `Curl.Console.UnitTests/Curl.Console.UnitTests.csproj` (line 56 today), which already sorts before `Curl.Console/Curl.Console.csproj`.
- Coverage is measured from the fast run only, and these tests were already outside it, so `Curl.Console`'s coverage should not move. If it does, add a fast unit test in `Curl.Console.UnitTests` through an injected fake (`IWriteOutFileOpener`, the connectors) that reaches the lost lines.
- Platform rule (root `CLAUDE.md`): keep the `OSCondition`s and the `OperatingSystem.IsWindows()` line-ending check exactly as they are.

## Acceptance criteria

- [ ] `Curl.Console.IntegrationTests/Curl.Console.IntegrationTests.csproj` exists, matches the template's shape, has no `MSTestSettings.cs` of its own, and is listed in `Curl.slnx` immediately before `Curl.Console.UnitTests`; `Curl.Console/Curl.Console.csproj` has `InternalsVisibleTo` for it.
- [ ] `grep -rn '^\s*\[.*TestCategory("Integration")' Curl.Console.UnitTests --include=*.cs` finds nothing, and every `[TestMethod]` in `Curl.Console.IntegrationTests` carries `[TestCategory("Integration")]`.
- [ ] `dotnet build -warnaserror` at the repository root is clean.
- [ ] `dotnet test --filter "TestCategory!=Integration"` is green.
- [ ] `dotnet test Curl.Console.IntegrationTests --filter "TestCategory=Integration"` runs the five tests named in Context; on Windows the three not limited by `OSCondition` pass and the two xattr tests are skipped by their condition. Notes record the runner's summary line.
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Console` reports 100% line and 100% branch coverage for `Curl.Console`. Notes record its line and branch figures before and after the move; if it was below 100% before, the after figures are no lower and Notes name the follow-up task for the existing gap.

## Notes

## Log

- 2026-10-07: Created.
