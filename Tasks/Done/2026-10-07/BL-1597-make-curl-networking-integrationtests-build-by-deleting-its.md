---
id: BL-1597
title: Make Curl.Networking.IntegrationTests build by deleting its duplicate MSTestSettings.cs
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Networking.IntegrationTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1597 — Make Curl.Networking.IntegrationTests build by deleting its duplicate MSTestSettings.cs

## Goal

`dotnet build` of the solution is clean again: `Curl.Networking.IntegrationTests` compiles only the shared `MSTestSettings.cs` that `Directory.Build.props` links into every test project, not a second copy of its own.

## Context

- Stewart's commit `6ecb2781f` ("Add networking integration test project", 2026-10-07) created `Curl.Networking.IntegrationTests` and moved the 11 `[TestCategory("Integration")]` tests out of `Curl.Networking.UnitTests` into it. It also added `Curl.Networking.IntegrationTests/MSTestSettings.cs`, holding `[assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]`.
- `Directory.Build.props` (the `ItemGroup` that links `$(MSBuildThisFileDirectory)MSTestSettings.cs` and `TestDiagnostics.cs` into every project ending in `UnitTests` or `IntegrationTests`) already gives the project the root `MSTestSettings.cs`, `[assembly: Parallelize(Workers = 0, Scope = ExecutionScope.MethodLevel)]`. Two `Parallelize` attributes is `error CS0579: Duplicate 'Parallelize' attribute`.
- Measured 2026-10-07: `dotnet build Curl.Networking.IntegrationTests/Curl.Networking.IntegrationTests.csproj` fails with that one error, and CI run 37650397456 (the `CI` workflow on `work/dark-factory` for `6ecb2781f`) failed on Windows, Linux and macOS with the same error and no other. Every lane's build is red until this lands, so it is High.
- The fix is to delete the project's own `MSTestSettings.cs`; the shared one is the solution's single home for the parallelization policy (see the comment in the root `MSTestSettings.cs`). Do not edit the root file or `Directory.Build.props` here.
- The 11 moved tests were all tagged `Integration` before the move (checked against the commit's diff), so the fast run's coverage of `Curl.Networking.UnitLibrary` should be unchanged; this task confirms it.
- When this task was filed (2026-10-07), the main checkout `Z:\repos\Curl` held someone's uncommitted edits to this project: `MSTestSettings.cs` deleted, a `<Compile Remove="Z:\repos\Curl\MSTestSettings.cs" />` and a `ProjectReference` to `Curl.Networking.UnitTests` added to the csproj, and changes to three test files. Start from whatever `work/dark-factory` holds when you claim this. If a commit has already fixed the build, check each criterion below against it and finish the task with what is left. Either way the csproj must not carry an absolute, machine-specific path (it breaks the Linux and macOS CI jobs and every lane worktree); and if it references `Curl.Networking.UnitTests`, the build must stay free of `CS0436` (the second linked `TestDiagnostics` copy, silenced in `Directory.Build.props` only for `Curl.Console.UnitTests`), or the reference is replaced by linking the fake files the tests need with `<Compile Include=... Link=...>`.
- This project is the template the move tasks for Cli, Console, Core and SSH copy, so it must build first.

## Acceptance criteria

- [x] `Curl.Networking.IntegrationTests/MSTestSettings.cs` no longer exists, and no other file in that folder declares an assembly-level `Parallelize` attribute.
- [x] `dotnet build -warnaserror` at the repository root is clean.
- [x] `dotnet test --filter "TestCategory!=Integration"` is green.
- [x] `dotnet test Curl.Networking.IntegrationTests --filter "TestCategory=Integration"` runs the project's 11 test methods (with their data rows) and none fails on Windows; Notes record the runner's passed/skipped/total line.
- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` reports `Curl.Networking.UnitLibrary` at 100% line and 100% branch coverage. If it does not, Notes name the uncovered members and a follow-up task is filed for them; this task does not widen to fix them.

## Notes

- Stewart's 61ad7caaf had already deleted the duplicate `MSTestSettings.cs`, but added
  `<Compile Remove="Z:\repos\Curl\MSTestSettings.cs" />`, an absolute path that matches only
  his checkout. Removed it; the shared file linked by `Directory.Build.props` now applies on
  every machine.
- `AuthenticateAsClientAsync_WhenTheServerSendsAnIntermediate_ReportsItAfterTheServersCertificate`
  failed on Windows with and without parallel runs: the rewrite awaited the server task after the
  client closed, so the server's read threw "connection forcibly closed". The old version ignored
  that failure; the test now catches the `IOException` the same way.
- Runner line: `Passed! - Failed: 0, Passed: 10, Skipped: 2, Total: 12` (filter `TestCategory=Integration`).
- `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary`: 0 failing members.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. Machine-specific Compile Remove dropped and the moved TLS integration test fixed; build clean, fast tests green, coverage unchanged.
