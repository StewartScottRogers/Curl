---
id: BL-2068
title: Fail CI when Measure-CodeQuality.ps1 finds a member outside a gate, so the coverage page stays green
priority: High
assignee: Claude
pipeline: direct
depends-on: [BL-2061, BL-2062, BL-2063, BL-2064, BL-2065, BL-2066, BL-2067]
touches: [.github/workflows/ci.yml]
lane: no
requirement: none
created: 2026-10-11
completed: 2026-10-11
---
# BL-2068 — Fail CI when Measure-CodeQuality.ps1 finds a member outside a gate, so the coverage page stays green

## Goal

A commit that puts any member outside a quality gate turns the `CI` workflow red, so it never reaches `master` and the coverage page stays green.

## Context

On 2026-10-11 the coverage page (<https://stewartscottrogers.github.io/Curl/coverage/>, commit 53f17a0) showed 13 members outside a gate in 6 libraries (BL-2061 to BL-2067 fix them). They got in because two checks measure complexity differently:

- The build's `CA1502` passes them, because it measures the source method.
- `Measure-CodeQuality.ps1` fails them, because it uses the coverage report's complexity, which counts the compiled method. It runs only in `.github/workflows/gource.yml`, on a schedule, where it gates nothing.

Lanes don't run the script either. Add a Windows job to `.github/workflows/ci.yml` that runs `Measure-CodeQuality.ps1` and fails when it exits 1. The shift's merge to `master` waits for the whole `CI` workflow, so the new job blocks a red merge without any branch-protection change. Thresholds stay as they are in `CodeMetricsConfig.txt` and the script.

`ci.yml` is a guard file, so this task is interactive only. Make the change on the `audit` branch, and merge it only once BL-2061 to BL-2067 are Done, so the new job is green when it lands.

## Acceptance criteria

- [x] `.github/workflows/ci.yml` has a Windows job that runs `Measure-CodeQuality.ps1` on every push and pull request and fails when the script reports a member outside a gate.
- [x] The job passed on `master` once BL-2061 to BL-2067 were merged.
- [x] `CLAUDE.md`'s "Quality gates" section says CI enforces the four gates through this job.

## Notes

- 2026-10-11 (interactive): on master 9db3133d4, which has BL-2061 to BL-2067, `Measure-CodeQuality.ps1` exits 0: 0 failing members across 34 assemblies, in 152 s locally. The `quality` job and the CLAUDE.md wording are on the `audit` branch in commit 5ebb3b8c1, in PR #115. Tick criteria 1 and 3 once #115 merges, and criterion 2 once the job passes on master.

## Log

- 2026-10-11: Created.
- 2026-10-11: Backlog -> Doing.
- 2026-10-11: Doing -> Done. PR #115 merged; the Quality gates job passed on master's merge commit 905ca5f90 (run 38158290319).
