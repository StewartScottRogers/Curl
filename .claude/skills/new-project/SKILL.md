---
name: new-project
description: Add a new C# project (and its matching test project) to the Curl solution following repository conventions. Use when asked to create, scaffold, or add a project, library, console app, or web app to the solution.
---
# Add a project to the Curl solution

Layout is flat: every project is a directory immediately under the repository root.
There is no `src/` and no `tests/` — do not create them.

1. Confirm the name and template. Libraries are `Curl.<Area>.UnitLibrary` (protocols
   `Curl.Protocol.<Name>.UnitLibrary`); the template is `classlib` unless it is the
   `Curl.Console` executable. Ask if unclear.
2. If no solution file exists at the repository root, create one:
   `dotnet new sln --name Curl --format slnx`
3. Create the production project at the root:
   `dotnet new classlib --name Curl.<Area>.UnitLibrary --output Curl.<Area>.UnitLibrary --framework net10.0`
4. Create the matching test project beside it:
   `dotnet new mstest --name Curl.<Area>.UnitTests --output Curl.<Area>.UnitTests --framework net10.0`
5. Add both to the solution with no solution folder, so they stay in the flat run:
   `dotnet sln Curl.slnx add Curl.<Area>.UnitLibrary Curl.<Area>.UnitTests`
6. Reference production from test:
   `dotnet add Curl.<Area>.UnitTests reference Curl.<Area>.UnitLibrary`
7. For a protocol library, also reference the contracts, and nothing else horizontal:
   `dotnet add Curl.Protocol.<Name>.UnitLibrary reference Curl.Protocol.Abstractions.UnitLibrary`
8. Strip any `Version` attributes and settings duplicated by `Directory.Build.props` /
   `Directory.Packages.props` (create those at the root if missing).
9. Add `Curl.<Area>.UnitLibrary/CLAUDE.md` with a short purpose statement and any
   project-specific rules.
10. Leave ahead-of-time compilation alone. `Directory.Build.props` sets
    `IsAotCompatible` for every project that is not `*.UnitTests`, and its
    `VerifyAotCompatibility` target fails the build if a project overrides it, so a new
    project is AOT-checked the moment it exists. Never add `IsAotCompatible` or
    `PublishAot` to a new csproj, and never set `IsAotCompatible` on a test project -
    MSTest finds tests by reflection and the AOT analyzers forbid it.
11. Run `dotnet build` and `dotnet test`; both must pass before finishing.
