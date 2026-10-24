---
id: BL-3464
title: Shard CI's Quality gates job so a CI run finishes in about 10 minutes instead of up to 27
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [.github/workflows/ci.yml, Measure-CodeQuality.ps1]
lane: no
requirement: none
created: 2026-10-24
completed:
---
# BL-3464 — Shard CI's Quality gates job so a CI run finishes in about 10 minutes instead of up to 27

## Goal

A `CI` run on `work/dark-factory` finishes in about the time of its slowest test job (about 10 minutes), because the Quality gates measurement is split across parallel Windows jobs instead of one job that measures every library.

## Context

Stewart, 2026-10-24: "file the ci.yml speed-up", after the BL-3454 to BL-3457 fix (ADR-0811) found that a CI-red spell can still pass 30 minutes because one full CI run takes 15 to 34 minutes, mostly in the Quality gates job.

Measured over the 5 latest green CI runs on `work/dark-factory` (40108926172, 40108510840, 40097820695, 40097272565, 40095886272), job minutes: Build and test 3 to 9, Pack Curl.Mcp 4 to 10, Quality gates (windows-latest) 10, 25, 27, 26, 10. The Quality gates job already runs in parallel with the others (no `needs:`), so it is the run's critical path on its own: `Measure-CodeQuality.ps1` runs the fast tests of every gated library with coverage, then measures complexity and CRAP, in one job.

Interactive only: `.github/workflows/ci.yml` is a guard file and changes only through the `audit` branch (CLAUDE.md, Audit office). Make the change on a branch from `origin/audit` (merge, never rebase), and merge its pull request under the standing audit exception once CI is green on Windows, Linux and macOS.

Watch out for:
- The job name `Quality gates (windows-latest)` may be a required status check on `master` and is read by the factory (grep `DarkFactory/`, `RunDarkFactory.ps1`, `Audit/`, `.github/` for it). Either keep one job with that exact name that `needs:` the shards and reports their combined result, or change every reader and the branch protection (branch protection changes need Stewart first).
- `Measure-CodeQuality.ps1` must still fail when any member of any gated library is outside a gate; a shard that measures nothing must fail, not pass, so a library is never silently dropped. The shards together must cover every gated library exactly once (a test asserts this).
- The coverage report the showcase publishes (`.github/workflows/gource.yml`, `showcase-publisher`) may read this job's JSON or artifact; keep what it reads.
- Thresholds in `CodeMetricsConfig.txt` do not change.

## Acceptance criteria

- [ ] `Measure-CodeQuality.ps1` takes a shard selector (for example `-Shard <n> -ShardCount <m>`, or a library list) and, with none given, measures every gated library as today; its self-test or a test shows the shards partition the gated libraries exactly, with none left out.
- [ ] `ci.yml` runs the measurement as parallel Windows shard jobs, and a job still named `Quality gates (windows-latest)` (or every reader updated) fails when any shard fails or a member is outside a gate, with the same `::error` annotations as today.
- [ ] Planted check: a branch with one deliberately uncovered branch in a gated library makes the Quality gates result fail (record the run id in Notes), and the branch is not merged.
- [ ] Over the first 3 green CI runs on `work/dark-factory` after the change, the run's wall time (first job start to last job end) is under 15 minutes each; Notes record the run ids and times.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-24: Created.
- 2026-10-24: Backlog -> Doing. Interactive session (Stewart: do BL-3464): sharding on guard/bl-3464 from master Criteria at claim: 68a57379 7d68909b f586dc79 a03ef6b7 3d836d5e.
