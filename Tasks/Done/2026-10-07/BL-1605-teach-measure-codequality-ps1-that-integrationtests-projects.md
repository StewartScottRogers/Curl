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
completed: 2026-10-07
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

- [x] `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary -IncludeIntegration` prints `dotnet test Curl.Networking.IntegrationTests.csproj` among the projects it runs; without `-IncludeIntegration` it does not.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` and a whole-solution `Measure-CodeQuality.ps1` report the same line and branch coverage for `Curl.Networking.UnitLibrary`; Notes record both figures.
- [x] No `*.IntegrationTests` assembly appears in the report's table, and no file under a `*.IntegrationTests` folder appears under "Coverage exclusions in production code".
- [x] The script's help, and the comments above the changed functions, say what they now do for both test-project suffixes.

## Notes

- `Get-TestProjectsReaching` takes `-IncludeIntegration` and collects `*.IntegrationTests` projects only then. `Test-IsProductionAssembly` now rejects both test suffixes by name; its behaviour is unchanged, because it only ever accepted `*.UnitLibrary` and `Curl.Console`. The exclusion scan skips `\.(UnitTests|IntegrationTests)[\\/]`, and its bin/obj/data filter also uses `[\\/]`, so it is right off Windows.
- Measured 2026-10-07: `-Library Curl.Networking.UnitLibrary -IncludeIntegration` ran the Conformance, Console, Networking.IntegrationTests, Networking and Protocol.Http test projects. Without the switch it ran the same four `*.UnitTests` projects and not the IntegrationTests one.
- Coverage of `Curl.Networking.UnitLibrary`: whole-solution run 100% line / 100% branch (1531 lines, 0 failing members); `-Library` run 100% / 100% (1531 lines); `-IncludeIntegration` run 100% / 100%. The merge keeps the best hit count and condition count per line, so a coverage file from an IntegrationTests project that ran nothing cannot lower a figure. No merge exclusion was needed.
- No `*.IntegrationTests` assembly appears in the table or the exclusion list of any of the three reports.
- Fast tests: 28,303 passed, 0 failed.
- Resumed on lane 1 (2026-10-07) from lane 7's branch by cherry-pick; the script had not changed since. Re-checked by extracting the functions: with `-IncludeIntegration`, `Get-TestProjectsReaching` returns the five projects above, without it the four `*.UnitTests` ones; `Test-IsProductionAssembly` rejects `Curl.Networking.IntegrationTests`; the exclusion scan lists no IntegrationTests file. Coverage figures are lane 7's measurement, not repeated (each run costs 30-45 minutes). Fast tests on lane 1: 28,384 passed, 0 failed.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Backlog. Lane 7 could not integrate: push kept being refused. The work is on branch factory/BL-1605-lane-7-20261007-111121; start with git cherry-pick --no-commit factory/BL-1605-lane-7-20261007-111121 and fix it.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. Measure-CodeQuality.ps1 runs IntegrationTests projects under -Library -IncludeIntegration and never reports them as production code
