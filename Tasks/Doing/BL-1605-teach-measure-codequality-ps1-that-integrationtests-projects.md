---
id: BL-1605
title: Teach Measure-CodeQuality.ps1 that IntegrationTests projects are test projects and run them under -IncludeIntegration
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1597]
touches: [Measure-CodeQuality.ps1]
requirement: none
created: 2026-10-07
completed:
---
# BL-1605 — Teach Measure-CodeQuality.ps1 that IntegrationTests projects are test projects and run them under -IncludeIntegration

## Goal

`Measure-CodeQuality.ps1` treats `Curl.<Area>.IntegrationTests` projects as test projects everywhere it treats `*.UnitTests` ones: with `-Library -IncludeIntegration` it runs the IntegrationTests projects that reach the library, it never reports one as production code, and the "Coverage exclusions in production code" list never lists a file under one.

## Context

- Rule (Stewart, 2026-10-07; ADR by BL-1598): Integration tests live only in `Curl.<Area>.IntegrationTests` projects. `Curl.Networking.IntegrationTests` exists now (built by BL-1597); Cli, Console, Core and SSH follow (BL-1599 to BL-1602).
- Places in `Measure-CodeQuality.ps1` (line numbers as of 2026-10-07) that know only `*.UnitTests`:
  - `Get-TestProjectsReaching` (around line 124) collects only `*.UnitTests` projects (line 140), so `-Library X -IncludeIntegration` never runs the IntegrationTests project that holds X's Integration tests. Include `*.IntegrationTests` projects when `-IncludeIntegration` is set; without it, leave them out (all their tests would be filtered out anyway, and skipping them saves a build-and-run per project). Update the comment and the `-IncludeIntegration` and `-Library` help text (around lines 37-64) to match.
  - The coverage-exclusion scan (around line 467) skips files whose path matches `\.UnitTests\\`; make it skip `*.IntegrationTests` folders too, and make the separator class `[\\/]` so it is right off Windows as well.
  - `Test-IsProductionAssembly` (around line 223) only accepts `*.UnitLibrary` and `Curl.Console`, so an IntegrationTests assembly is already excluded; leave its behaviour, but say in its comment that both test suffixes are excluded.
- The whole-solution run (no `-Library`) passes `--filter "TestCategory!=Integration"` to `dotnet test Curl.slnx`; each IntegrationTests project then runs no test and prints "No test matches the given testcase filter", which exits 0 under the SDK 10.0.401 VSTest runner (measured 2026-10-07 on `Curl.Protocol.Dict.UnitTests`). Confirm that run still reports `Curl.Networking.UnitLibrary` at the same coverage as `-Library Curl.Networking.UnitLibrary` does, i.e. that a coverage file from an IntegrationTests project that ran nothing does not lower the merged figures; if it does, exclude those projects' coverage files from the merge and say so in a comment.
- `.github/workflows/gource.yml` runs this script on every push to publish the coverage report, so a wrong figure here is public.

## Acceptance criteria

- [ ] `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary -IncludeIntegration` prints `dotnet test Curl.Networking.IntegrationTests.csproj` among the projects it runs; without `-IncludeIntegration` it does not.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` and a whole-solution `Measure-CodeQuality.ps1` report the same line and branch coverage for `Curl.Networking.UnitLibrary`; Notes record both figures.
- [ ] No `*.IntegrationTests` assembly appears in the report's table, and no file under a `*.IntegrationTests` folder appears under "Coverage exclusions in production code".
- [ ] The script's help, and the comments above the changed functions, say what they now do for both test-project suffixes.

## Notes

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Backlog. Lane 7 could not integrate: push kept being refused. The work is on branch factory/BL-1605-lane-7-20261007-111121; start with git cherry-pick --no-commit factory/BL-1605-lane-7-20261007-111121 and fix it.
- 2026-10-07: Backlog -> Doing.
