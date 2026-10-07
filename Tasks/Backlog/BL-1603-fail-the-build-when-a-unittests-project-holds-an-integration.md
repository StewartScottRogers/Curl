---
id: BL-1603
title: Fail the build when a UnitTests project holds an Integration test or an IntegrationTests project holds a test without one
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1597, BL-1598, BL-1599, BL-1600, BL-1601, BL-1602]
touches: [Directory.Build.props, CLAUDE.md]
requirement: none
created: 2026-10-07
completed:
---
# BL-1603 — Fail the build when a UnitTests project holds an Integration test or an IntegrationTests project holds a test without one

## Goal

`dotnet build` fails, naming the file, when a `*.UnitTests` project's own source carries `[TestCategory("Integration")]` or an `*.IntegrationTests` project's source has a test method without it, on Windows, Linux and macOS, with no package and no test that reads the disk.

## Context

- The rule (Stewart, 2026-10-07; ADR written by BL-1598): Integration tests live only in `Curl.<Area>.IntegrationTests` projects; no `*.UnitTests` project contains one; every test in an `*.IntegrationTests` project carries `[TestCategory("Integration")]`, so `dotnet test --filter "TestCategory!=Integration"` skips them all.
- Why a build check and not a fast test: a test that reads the repository's source files touches the disk, which by the rule makes it an Integration test, and the fast run would never run it. An MSBuild target runs in every `dotnet build` (lanes, CI on three platforms, Visual Studio), needs no package and no exec, and sits beside `VerifyAotCompatibility` in `Directory.Build.props`, where shared build settings go (root `CLAUDE.md`).
- Why it waits on the moves: BL-1599 (Cli), BL-1600 (Console), BL-1601 (Core) and BL-1602 (SSH) take the last real Integration tests out of those `*.UnitTests` projects, and BL-1597 makes `Curl.Networking.IntegrationTests` build. The one area left is `Curl.Cryptography.UnitTests`, whose four slow, CPU-only tests BL-1598's decision 3 reclassifies as LongRunning in a later task that depends on BL-1525's speed-up. So this check ships with an allow-list holding exactly those four files - `X25519Tests.cs`, `X448Tests.cs`, `Cast128Tests.cs` and `BrainpoolEcdsaTests.cs` in `Curl.Cryptography.UnitTests` - and the build stays green; the later task empties it.
- Shape of the check (adjust the mechanics, keep the behaviour):
  - A target, e.g. `VerifyIntegrationTestPlacement`, `BeforeTargets="CoreCompile"`, conditioned on `$(MSBuildProjectName)` ending in `.UnitTests` or `.IntegrationTests`.
  - It reads each `@(Compile)` item that is the project's own (no `Link` metadata - that skips the linked `MSTestSettings.cs`, `TestDiagnostics.cs` and any fakes linked from another project, which are checked in their home project) with `$([System.IO.File]::ReadAllText(...))` and matches with `System.Text.RegularExpressions.Regex` property functions, multiline.
  - An Integration attribute is a line whose first non-blank character is `[` and which names `TestCategory("Integration")` (with optional spaces), e.g. `^\s*\[[^\]\r\n]*\bTestCategory\s*\(\s*"Integration"\s*\)`. Comment lines must not count: `Curl.Console.UnitTests/DiskWriteOutFileOpenerTests.cs`, `PhysicalOutputPathsTests.cs`, `KerberosDiskFileReaderTests.cs` and `KerberosDiskFileWriterTests.cs` mention `[TestCategory("Integration")]` inside `///` doc comments and hold no such test.
  - In a `*.UnitTests` project: any Integration attribute in a file not on the allow-list is an `<Error>` naming the project, the file and the rule.
  - In a `*.IntegrationTests` project: a file passes when it carries a class-level Integration attribute, or when its count of Integration attributes is at least its count of `[TestMethod]`/`[DataTestMethod]` attributes; otherwise an `<Error>` naming the file. Note in a comment that this is a source count, so a method with two category attributes could hide one without it, and that is accepted.
  - The allow-list is an item group (e.g. `<IntegrationTestInUnitTestsAllowed Include="..." />`) with a comment naming the task that empties it.
  - A comment above the target says what it enforces and names the ADR from BL-1598.
- Root `CLAUDE.md`: add one sentence where BL-1598 states the rule ("Build and test commands" or "Quality gates") saying `Directory.Build.props` enforces it at build time.
- Platform-neutral: property functions only, no `Exec`, no Windows path separators in the patterns or the allow-list.

## Acceptance criteria

- [ ] `Directory.Build.props` has the target, its comment and the four-file allow-list for `Curl.Cryptography.UnitTests`; nothing else in the solution changes except the one `CLAUDE.md` sentence.
- [ ] `dotnet build -warnaserror` at the repository root is clean and `dotnet test --filter "TestCategory!=Integration"` is green.
- [ ] Notes record each of these checked by hand and then reverted, with the error text the build printed: (1) adding `[TestCategory("Integration")]` to a test in `Curl.Protocol.Dict.UnitTests` fails `dotnet build Curl.Protocol.Dict.UnitTests` naming that file; (2) removing `[TestCategory("Integration")]` from one test in `Curl.Networking.IntegrationTests` fails its build naming that file; (3) the four `Curl.Console.UnitTests` files whose doc comments mention the attribute build clean; (4) removing `X25519Tests.cs` from the allow-list fails `dotnet build Curl.Cryptography.UnitTests`.
- [ ] `git status` after the hand checks shows only `Directory.Build.props`, `CLAUDE.md` and this task file changed.
- [ ] Root `CLAUDE.md` names `Directory.Build.props` as where the rule is enforced.

## Notes

## Log

- 2026-10-07: Created.
