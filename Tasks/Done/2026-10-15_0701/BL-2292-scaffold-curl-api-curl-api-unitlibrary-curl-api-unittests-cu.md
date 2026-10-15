---
id: BL-2292
title: Scaffold Curl.Api, Curl.Api.UnitLibrary, Curl.Api.UnitTests, Libcurl and Libcurl.UnitTests and record their naming exceptions
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Api, Curl.Api.UnitLibrary, Curl.Api.UnitTests, Libcurl, Libcurl.UnitTests, Curl.slnx, CLAUDE.md, Measure-CodeQuality.ps1]
requirement: none
created: 2026-10-15
completed: 2026-10-15
---
# BL-2292 — Scaffold Curl.Api, Curl.Api.UnitLibrary, Curl.Api.UnitTests, Libcurl and Libcurl.UnitTests and record their naming exceptions

## Goal

The five API projects - `Curl.Api`, `Curl.Api.UnitLibrary`, `Curl.Api.UnitTests`, `Libcurl`, `Libcurl.UnitTests` - exist in the solution, empty of logic, and root `CLAUDE.md` and `Measure-CodeQuality.ps1` know them.

## Context

ADR-0506 and `Documentation/Planning/Curl-Api-And-libcurl.md` (phase 2 step 1). Stewart approved the split on 2026-10-15 and named the libcurl-faithful project exactly `Libcurl` (no `Curl.` prefix, capital L; renamed from `Curl.libcurl` the same day) to match upstream's library name; its tests are `Libcurl.UnitTests` and its root namespace is `Libcurl`. Use the `new-project` skill's conventions.

- References: `Curl.Api.UnitLibrary` references `Curl.Protocol.Abstractions.UnitLibrary` only (for `CurlExitCode`; never a protocol library or the engine). `Curl.Api` and `Libcurl` reference `Curl.Api.UnitLibrary` and never each other; neither references `Curl.Engine.UnitLibrary` yet (their engine-building overloads come in phases 3 and 4). `Curl.Api.UnitTests` references `Curl.Api` and `Curl.Api.UnitLibrary`; `Libcurl.UnitTests` references `Libcurl`.
- `Curl.Api.UnitTests` tests both `Curl.Api` and `Curl.Api.UnitLibrary` (ADR-0506); give both `InternalsVisibleTo` it.
- `Curl.slnx` order (ADR-0506): `Curl.Api`, `Curl.Api.UnitLibrary`, `Curl.Api.UnitTests` first, before `Curl.Authentication.UnitLibrary`; `Libcurl`, `Libcurl.UnitTests` after every `Curl.*` project (after `Curl.Zstandard.UnitTests`).
- Root `CLAUDE.md`, "Project naming": `Curl.Console` stops being "the only exception". Say that `Curl.Console`, `Curl.Api` and `Libcurl` are the exceptions to the `Curl.<Area>.UnitLibrary` rule - `Libcurl` also has no `Curl.` prefix, matching upstream's library name - that `Curl.Api` and `Libcurl` are held to the four quality gates, that `Curl.Api.UnitTests` tests both `Curl.Api` and `Curl.Api.UnitLibrary`, and that `Libcurl` and `Libcurl.UnitTests` sort after every `Curl.*` project; update the layout block's project count to the real count. "Quality gates" names `Curl.Api` and `Libcurl` beside `Curl.Console`.
- Any script that finds projects by a `Curl.*` folder or name pattern (search the repository for `Curl.*` globs outside `Gap/`, `Audit/` and `.github/`) must also find `Libcurl` and `Libcurl.UnitTests`; fix those in this task, or list them in Notes and file a task for any in a guarded path.
- `Measure-CodeQuality.ps1`: its production predicate (`*.UnitLibrary` or `Curl.Console`, around line 258) also accepts `Curl.Api` and `Libcurl`; update its help text (`-Library`) to say so.
- Each new production project gets a `CLAUDE.md` saying what it is for and what it must never reference.

## Acceptance criteria

- [x] The five project folders and `.csproj` files exist with the references in Context and are listed in `Curl.slnx` at the positions in Context.
- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Api,Libcurl` reports both as production assemblies (an assembly with no members passes).
- [x] Root `CLAUDE.md`'s "Project naming" and "Quality gates" sections name `Curl.Api` and `Libcurl` as stated in Context, and the layout block's project count matches `Curl.slnx`.
- [x] `dotnet build` is clean (warnings are errors), including `VerifyAotCompatibility` for the three production projects.
- [x] `dotnet test --filter "TestCategory!=Integration"` passes.
- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1` passes.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged.

## Notes

- Scaffolded with empty projects. `Curl.Api.UnitLibrary` and `Curl.Api` give `InternalsVisibleTo` to `Curl.Api.UnitTests`; `Libcurl` to `Libcurl.UnitTests`. Root namespaces come from `Directory.Build.props` (`Curl.Api`, and the project name `Libcurl`).
- `Curl.slnx` count is 84 projects (was 79); `CLAUDE.md` layout block updated.
- `Measure-CodeQuality.ps1`: predicate and help now accept `Curl.Api` and `Libcurl`. With no members yet, a run prints no table row for either (no coverage package is emitted for an empty assembly) and reports 0 failing members; they appear once code lands.
- Full `Measure-CodeQuality.ps1` passed: 0 failing members across 37 production assemblies.
- Scripts searched for `Curl.*` project globs: none outside guarded paths. One guarded script selects projects by `*.UnitLibrary` or `Curl.Console` (the audit tool `Find-BooleanNameMismatches.ps1`, line 82); a lane may not file or edit it, so an interactive session on the `audit` branch must add `Curl.Api` and `Libcurl` to it.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. Curl.Api, Curl.Api.UnitLibrary, Curl.Api.UnitTests, Libcurl and Libcurl.UnitTests exist in the solution, empty, and CLAUDE.md and Measure-CodeQuality.ps1 know them
