---
id: BL-1602
title: Move Curl.Protocol.Ssh.UnitTests' agent and Pageant Integration tests into a new Curl.Protocol.Ssh.IntegrationTests project
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1597]
touches: [Curl.Protocol.Ssh.UnitTests, Curl.Protocol.Ssh.IntegrationTests, Curl.Protocol.Ssh.UnitLibrary, Curl.slnx]
requirement: none
created: 2026-10-07
completed:
---
# BL-1602 — Move Curl.Protocol.Ssh.UnitTests' agent and Pageant Integration tests into a new Curl.Protocol.Ssh.IntegrationTests project

## Goal

`Curl.Protocol.Ssh.UnitTests` holds no `[TestCategory("Integration")]` test; the five tests that connect to a real named pipe, Unix socket or Win32 window run, unchanged in what they assert, from a new `Curl.Protocol.Ssh.IntegrationTests` project in `Curl.slnx`.

## Context

- Rule approved by Stewart on 2026-10-07: an integration test (one that touches something real outside the process - socket, disk, OS, native API, system agent) carries `[TestCategory("Integration")]` and lives only in a `Curl.<Area>.IntegrationTests` project; no `*.UnitTests` project contains one, and every test in an `*.IntegrationTests` project carries the category. BL-1598 records it in an ADR; this task does not wait for that.
- The five tests (counted 2026-10-07):
  - `Curl.Protocol.Ssh.UnitTests/Authentication/SystemSshAgentConnectorTests.cs`: `ConnectAsync_WindowsPipeServed_ConnectsToIt` (two data rows, `[OSCondition(OperatingSystems.Windows)]`) and `ConnectAsync_UnixSocketServed_ConnectsToIt` (a Unix domain socket in the temp directory), with their private helper `AssertCarriesBytesAsync` (around line 127). The class's other six tests (`PipeNameOf_...` and the rest) are not Integration and stay; fix its class doc comment (lines 6-11, "The connections themselves run in the Integration tests ... served here") so it names where they now run.
  - `Curl.Protocol.Ssh.UnitTests/Authentication/WindowsPageantWindowTests.cs`: the whole class, all three tests Integration, `[DoNotParallelize]` and `[SupportedOSPlatform("windows")]` kept. Move the file as it is (`git mv`), renamed `WindowsPageantWindowIntegrationTests.cs` with the class renamed to match.
- Fakes: `Fakes/FakePageantWindow.cs` is used only by `WindowsPageantWindowTests` (and its doc comment says so), so move it into `Curl.Protocol.Ssh.IntegrationTests/Fakes/`. `Fakes/InMemorySshAgent.cs` and `Fakes/TestUserKeys.cs` are used by many unit tests and stay; link them, and whatever they in turn need (e.g. `SshTestEncoding`), into the new project with `<Compile Include="..\Curl.Protocol.Ssh.UnitTests\Fakes\InMemorySshAgent.cs" Link="Fakes\InMemorySshAgent.cs" />`. Do not add a `ProjectReference` to `Curl.Protocol.Ssh.UnitTests`: it would bring a second linked `TestDiagnostics` and `CS0436`, which `Directory.Build.props` silences only for `Curl.Console.UnitTests`. If a linked fake needs an embedded resource, add the same `EmbeddedResource` item; list in Notes everything linked.
- Template: `Curl.Networking.IntegrationTests/Curl.Networking.IntegrationTests.csproj` (made buildable by BL-1597): the MSTest `PackageReference` with no version, the `Using`, one `ProjectReference`, here `..\Curl.Protocol.Ssh.UnitLibrary\Curl.Protocol.Ssh.UnitLibrary.csproj`. No `MSTestSettings.cs` of its own (a copy is `error CS0579`). `SystemSshAgentConnector`, `WindowsPageantWindow`, `PageantSshAgentConnector` and the fakes are `internal`: add `<InternalsVisibleTo Include="Curl.Protocol.Ssh.IntegrationTests" />` beside the existing one in `Curl.Protocol.Ssh.UnitLibrary/Curl.Protocol.Ssh.UnitLibrary.csproj` (line 14 today).
- Namespaces stay as they are (`Curl.Protocol.Ssh.Authentication`, `Curl.Protocol.Ssh.Fakes`), folders mirror them.
- `Curl.slnx` is one flat alphabetical run: add `Curl.Protocol.Ssh.IntegrationTests/Curl.Protocol.Ssh.IntegrationTests.csproj` immediately before `Curl.Protocol.Ssh.UnitLibrary/Curl.Protocol.Ssh.UnitLibrary.csproj` (line 103 today).
- `Curl.Console.UnitTests` references `Curl.Protocol.Ssh.UnitTests` for its fakes; check it does not use `FakePageantWindow` before moving it (on 2026-10-07 it did not).
- Coverage: `WindowsPageantWindow` is already excluded from the fast run's measurement under ADR-0083 (its test class's doc comment says so), and these tests were already outside the fast run, so `Curl.Protocol.Ssh.UnitLibrary`'s coverage should not move. If it does, add a fast test in `Curl.Protocol.Ssh.UnitTests` through `IPageantWindow` or the connector's injected functions that reaches the lost lines.

## Acceptance criteria

- [ ] `Curl.Protocol.Ssh.IntegrationTests/Curl.Protocol.Ssh.IntegrationTests.csproj` exists, matches the template's shape plus the linked fakes, has no `MSTestSettings.cs` of its own, and is listed in `Curl.slnx` immediately before `Curl.Protocol.Ssh.UnitLibrary`; the library's csproj has `InternalsVisibleTo` for it.
- [ ] `grep -rn '^\s*\[.*TestCategory("Integration")' Curl.Protocol.Ssh.UnitTests --include=*.cs` finds nothing, every `[TestMethod]` in `Curl.Protocol.Ssh.IntegrationTests` carries `[TestCategory("Integration")]`, and `FakePageantWindow.cs` lives only in the new project.
- [ ] `dotnet build -warnaserror` at the repository root is clean.
- [ ] `dotnet test --filter "TestCategory!=Integration"` is green.
- [ ] `dotnet test Curl.Protocol.Ssh.IntegrationTests --filter "TestCategory=Integration"` runs the five tests named in Context and none fails on Windows (the Unix-socket test runs on Windows too, as it does today). Notes record the runner's summary line.
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Ssh.UnitLibrary` reports 100% line and 100% branch coverage for `Curl.Protocol.Ssh.UnitLibrary`. Notes record its line and branch figures before and after the move; if it was below 100% before, the after figures are no lower and Notes name the follow-up task for the existing gap.

## Notes

## Log

- 2026-10-07: Created.
