---
name: new-project
description: Add a new C# project (and its matching test project) to the Curl solution following repository conventions. Use when asked to create, scaffold, or add a project, library, console app, or web app to the solution.
---
# Add a project to the Curl solution

Layout is flat: every project is a directory immediately under the repository root.
There is no `src/` and no `tests/` — do not create them.

1. Confirm the name and template. Libraries are `Curl.<Area>.UnitLibrary` (curl's own protocols
   `Curl.Original.Protocol.<Name>.UnitLibrary`, ADR-1176); the template is `classlib` unless it is the
   `Curl.Console` executable. Ask if unclear.
2. If no solution file exists at the repository root, create one:
   `dotnet new sln --name Curl --format slnx`
3. Create the production project at the root:
   `dotnet new classlib --name Curl.<Area>.UnitLibrary --output Curl.<Area>.UnitLibrary --framework net10.0`
4. Create the matching test project beside it:
   `dotnet new mstest --name Curl.<Area>.UnitTests --output Curl.<Area>.UnitTests --framework net10.0`
5. Add both to the solution in a folder for the area, under the group it belongs to
   (Hosts, CommandLine, Transfer, Wire or Security; see CLAUDE.md, ADR-0731):
   `dotnet sln Curl.slnx add --solution-folder <Group>/<Area> Curl.<Area>.UnitLibrary Curl.<Area>.UnitTests`.
   A protocol goes in its own folder under `Original/Protocols`:
   `dotnet sln Curl.slnx add --solution-folder Original/Protocols/<Name> Curl.Original.Protocol.<Name>.UnitLibrary Curl.Original.Protocol.<Name>.UnitTests`.
   A conformance part goes in its folder under `Conformance` the same way:
   `dotnet sln Curl.slnx add --solution-folder Conformance/<Part> Curl.Conformance.<Part>.UnitLibrary Curl.Conformance.<Part>.UnitTests`
6. Reference production from test:
   `dotnet add Curl.<Area>.UnitTests reference Curl.<Area>.UnitLibrary`
7. For a protocol library, also reference the contracts, and nothing else horizontal:
   `dotnet add Curl.Original.Protocol.<Name>.UnitLibrary reference Curl.Protocol.Abstractions.UnitLibrary`
8. Strip any `Version` attributes and settings duplicated by `Directory.Build.props` /
   `Directory.Packages.props` (create those at the root if missing).
9. Add `Curl.<Area>.UnitLibrary/CLAUDE.md` with a short purpose statement and any
   project-specific rules.
10. Leave ahead-of-time compilation alone. `Directory.Build.props` sets
    `IsAotCompatible` for every project that is not `*.UnitTests` or `*.IntegrationTests`, and its
    `VerifyAotCompatibility` target fails the build if a project overrides it, so a new
    project is AOT-checked the moment it exists. Never add `IsAotCompatible` or
    `PublishAot` to a new csproj, and never set `IsAotCompatible` on a test project -
    MSTest finds tests by reflection and the AOT analyzers forbid it.
11. Run `dotnet build` and `dotnet test`; both must pass before finishing.

## Add an IntegrationTests project when an area first needs one

Integration tests - tests that touch a socket, the disk, the OS, a native API or a system
agent - live only in `Curl.<Area>.IntegrationTests`, never in `Curl.<Area>.UnitTests`
(ADR-0421). Every test in it carries `[TestCategory("Integration")]`.

1. Create `Curl.<Area>.IntegrationTests/Curl.<Area>.IntegrationTests.csproj` by copying
   `Curl.Networking.IntegrationTests/Curl.Networking.IntegrationTests.csproj`: the MSTest
   `PackageReference` with no version, the `Using` for
   `Microsoft.VisualStudio.TestTools.UnitTesting`, and one `ProjectReference` to
   `Curl.<Area>.UnitLibrary`. Reference the area's `.UnitTests` project only to share its
   test doubles, as `Curl.Networking.IntegrationTests` does.
2. Give it no `MSTestSettings.cs` of its own: `Directory.Build.props` links the root one
   into every `*.UnitTests` and `*.IntegrationTests` project, and a copy is `error CS0579`.
3. Add `<InternalsVisibleTo Include="Curl.<Area>.IntegrationTests" />` to the library's
   csproj only when the tests use its internals.
4. Add it to `Curl.slnx` in the area's own solution folder, before the library there:
   `dotnet sln Curl.slnx add --solution-folder <Group>/<Area> Curl.<Area>.IntegrationTests`.
   It sorts immediately before `Curl.<Area>.UnitLibrary` (for `Curl.Console`, before
   `Curl.Console.UnitTests`). A protocol's goes in its `Original/Protocols/<Name>` folder:
   `dotnet sln Curl.slnx add --solution-folder Original/Protocols/<Name> Curl.Original.Protocol.<Name>.IntegrationTests`.
5. Run `dotnet build` and `dotnet test Curl.<Area>.IntegrationTests --filter "TestCategory=Integration"`.
