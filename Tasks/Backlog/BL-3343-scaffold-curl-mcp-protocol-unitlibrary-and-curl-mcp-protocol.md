---
id: BL-3343
title: Scaffold Curl.Mcp.Protocol.UnitLibrary and Curl.Mcp.Protocol.UnitTests for the hand-written MCP server core
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-3341]
touches: [Curl.slnx, Curl.Mcp.Protocol.UnitLibrary, Curl.Mcp.Protocol.UnitTests, Curl.Mcp/Curl.Mcp.csproj, Curl.Mcp.UnitTests/Curl.Mcp.UnitTests.csproj]
model: sonnet
requirement: none
created: 2026-10-23
completed:
---
# BL-3343 — Scaffold Curl.Mcp.Protocol.UnitLibrary and Curl.Mcp.Protocol.UnitTests for the hand-written MCP server core

## Goal

The projects `Curl.Mcp.Protocol.UnitLibrary` and `Curl.Mcp.Protocol.UnitTests` exist, are in `Curl.slnx`, build, and are referenced by `Curl.Mcp` and `Curl.Mcp.UnitTests`.

## Context

- Use the `new-project` skill and follow ADR-0731: add both projects to the existing `/Hosts/Mcp/` solution folder in `Curl.slnx`, tests directly after the library. Root namespace `Curl.Mcp.Protocol`. BCL only: no package, no project reference to any other Curl project. AOT-compatible through `Directory.Build.props`, as every production project is.
- Add a ProjectReference to the new library from `Curl.Mcp/Curl.Mcp.csproj` and from `Curl.Mcp.UnitTests/Curl.Mcp.UnitTests.csproj`; add `InternalsVisibleTo` for `Curl.Mcp.Protocol.UnitTests` in the library. Do not touch the two PackageReference lines in `Curl.Mcp.csproj`: the last task of the plan removes them.
- Give each project the `CLAUDE.md` and `README.md` the skill asks for. The library's say: the protocol core of the hand-written MCP server (decision BL-3341), BCL only, source-generated JSON only, no dependency on other Curl projects, held to the four quality gates. Write them as intent where the code is not there yet.
- If a list of libraries elsewhere needs the new entry (check `Measure-CodeQuality.ps1`, the coverage page inputs and `Documentation/Wiki` project lists), add it. Do not edit guard files (`.github/workflows/ci.yml`): if CI needs an entry there, say so under Notes for an interactive session.

## Acceptance criteria

- [ ] `Curl.slnx` lists both projects under `/Hosts/Mcp/`, library then tests, and `dotnet build Curl.slnx` is clean with warnings as errors.
- [ ] `Curl.Mcp.Protocol.UnitLibrary.csproj` has no PackageReference and no ProjectReference; `Curl.Mcp.Protocol.UnitTests.csproj` references MSTest as the other test projects do, and the library.
- [ ] `Curl.Mcp` and `Curl.Mcp.UnitTests` reference `Curl.Mcp.Protocol.UnitLibrary`.
- [ ] Each new project folder has a `CLAUDE.md` and `README.md` that are true of the repository as it is, with intent written as intent.
- [ ] `dotnet build` of every project named in touches is clean with `-warnaserror`, and `dotnet test --filter "TestCategory!=Integration"` passes for the test projects named in touches, with no test deleted, skipped or weakened.

## Notes

## Log

- 2026-10-23: Created.
