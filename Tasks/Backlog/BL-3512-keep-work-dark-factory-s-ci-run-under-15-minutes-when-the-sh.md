---
id: BL-3512
title: Keep work/dark-factory's CI run under 15 minutes when the shift-end merge's runs share the runners
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [.github/workflows/ci.yml]
lane: no
requirement: none
created: 2026-10-25
completed:
---
# BL-3512 — Keep work/dark-factory's CI run under 15 minutes when the shift-end merge's runs share the runners

## Goal

A `CI` run on `work/dark-factory` that overlaps a shift-end merge still goes from its first job's start to its last job's end in under 15 minutes, because its jobs are not left waiting for runners behind the merge branch's and `master`'s runs.

## Context

Found while doing BL-3511 (BL-3464's 15-minute target). Run 40206727905 (head fb6591edc) took 18m38s from first job start to last job end, but its SSH shard took only 5m25s. Its jobs started anywhere from 15:29 to 15:40 UTC (`Build and test (windows-latest)` waited 11 minutes) because, at the same moment, the shift-end merge started CI and Integration tests on `factory/merge-20261025-081939` and then CI, Release, Integration tests and Gource on `master` (runs 40206122116, 40206127122, 40206127169 and others). That is more concurrent jobs than the account's runner limit. Separately, every run on `work/dark-factory` waits 5 to 8.5 minutes before its first job (runs 40204269817, 40205671348), because ci.yml's concurrency group `ci-${{ github.ref }}` has `cancel-in-progress: false` there. BL-3464's measure (first job start to last job end) leaves that wait out, but a person watching the run sees it.

Options to weigh: give `master` and `factory/merge-*` runs a lower share (for example, skip jobs on the merge PR's push run that its `pull_request` run already covers); or let `work/dark-factory` cancel a run that is still queued. Mind what that breaks: the coordinator's CI watch (BL-987, ADR-0811) and the shift-end merge both read runs for exact commits.

Interactive only: ci.yml is a guard file and changes only through the `audit` branch or a guard branch from `master` (CLAUDE.md, Audit office).

## Acceptance criteria

- [ ] Notes name, from `gh run view <id> --json jobs`, which runs competed for runners during a `work/dark-factory` run that overlapped a shift-end merge, and the change made to ci.yml (or to the workflows that compete with it) so that they no longer delay it.
- [ ] The first `work/dark-factory` CI run after the change that overlaps a shift-end merge goes from first job start to last job end in under 15 minutes; Notes record its id and time.
- [ ] The shift-end merge and the coordinator's CI watch still find a completed `CI` run for each commit they check (Notes cite a shift-end merge that went through after the change).

## Notes
- 2026-10-31 19:30 Arizona (interactive session): after PR #256, master's CI for shift-end merges PR #260/#262 took 8 s and 9 s (runs 41207646668, 41237470640), but work/dark-factory runs stayed over 15 min with or without a merge: 41215194658 16.8, 41223732155 19.7, 41227713497 17.5, 41236102295 17.0, and 41239174672 20.8 (first after PR #262; 7 min of Linux queueing behind master's Integration tests 41237470654 and Gource 41237470669). Causes: quality shards weighted SSH at a stale 707 s (shard 1 2 min, shards 2/3 10 and 13.5); Quality gates scoring 5m40s in PowerShell; `Pack Curl.Mcp`'s Mcp Integration tests 5 to 12 min; dark-factory-unix/-shift with no concurrency group (a dozen macOS jobs 22:14 to 22:50 UTC, runs 41249043176 and 41251396957 32 min); master's Integration run repeating tested trees. PR #265 (guard/bl-3512-ci-runtime, ADR-1179), merged to master 2026-10-31 and into work/dark-factory at adadddaf0: C# Cobertura merge (74 s against 360 s locally, identical results), shard weights remeasured (38/38/38), mcp-pack skips the duplicate native AOT publish class and checks tools/list, dark-factory-unix/-shift queue per ref, integration.yml gets tested-tree. PR #265's CI push run 41262981949: scoring step 54 s, Quality gates 75 s, shard collect 5.5 to 8.3 min; span 30.2 min, mostly runner queueing during that PR's own runs. Watch: `Pack Curl.Mcp (windows)` came to about 13 min (39 Mcp Integration tests 5m36s on the runner against 33 s locally), now the job nearest the limit. Criteria 2 and 3 still need the next shift-end merge after PR #265.
- 2026-10-30 20:55 Arizona (interactive session): the competitor that remained after PR #238 is master's own push run after a merge (run 40844580348 behind work/dark-factory run 40845247630, 15m01s; PR #254's master run 41036183434 ran 09:49 to 10:28 UTC and ended cancelled, its jobs API answering 502). PR #256 (d5de8ef1e) adds ci.yml job `tested-tree`, run only on a push to master: when a parent of the merge commit has the identical tree and a passed CI push run, every build, test, pack and quality job is skipped. Checked against PR #254's merge 540e8b3d4 (parent b79175cc5: same tree, passed push run) and live on PR #256's own master run 41165523210: only `Tree already tested` ran, every other job skipped, run green. Criteria 2 and 3 still need the next shift-end merge: an overlapping work/dark-factory run under 15 min, and that merge going through. Back to Backlog for that sample.
- 2026-10-25 19:40 Arizona (interactive session): measured after PR #238. Run 40233303425 (13:59, no competing PRs) went first job start to last job end in 14.5 min. Runs 40253936975 (18:25, 25.1 min) and 40255860809 (18:51, 20.8 min) overlapped the CI of gap PR #243 and audit PRs #244 and #245, which compete for the Windows runners; the last job in both was Quality gates (windows-latest). The shift-end merge PR #246 (19:12) found its completed CI run and went through. Criterion 2 is not met yet: no run after the change that overlapped a shift-end merge finished under 15 min. Stays in Backlog.
- 2026-10-29 06:00 Arizona (interactive session): shift-end merge PR #250 (08:46 UTC). Its `factory/merge-20261028-201954` PR run 40844535485 was skipped as PR #238 intends; the competitor was the `master` push run 40844580348 (08:46:38 to 09:03:45 UTC, 17.1 min). The overlapping work/dark-factory run 40845247630 went first job start to last job end 08:52:39 to 09:07:40 UTC, 15.0 min (15m01s), missing criterion 2 by about a second. PR #250 itself merged, so criterion 3 holds again. Remaining idea if the next overlapping run also misses: skip the `master` push run's jobs whose tree a green work/dark-factory run already tested, keeping Release's needs intact. Stays in Backlog for the next shift-end merge's sample.

## Log

- 2026-10-25: Created.
- 2026-10-25: Backlog -> Doing. Interactive session: skip the factory/merge PR duplicate CI and Integration runs on guard/bl-3512 from master Criteria at claim: af56919a b59972c4 59fec9cb.
- 2026-10-25: Doing -> Backlog. Waiting on CI for PR #238 (guard/bl-3512, ece8175aa); reclaim to merge and measure
- 2026-10-30: Backlog -> Doing. Interactive session: skip master push CI jobs whose tree a passing run already tested, on guard/bl-3512-tested-tree from master Criteria at claim: af56919a b59972c4 59fec9cb.
- 2026-10-30: Doing -> Backlog. PR #256 merged; waiting on the next shift-end merge's sample
