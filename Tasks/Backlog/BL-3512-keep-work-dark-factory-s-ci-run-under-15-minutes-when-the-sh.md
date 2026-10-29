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
- 2026-10-25 19:40 Arizona (interactive session): measured after PR #238. Run 40233303425 (13:59, no competing PRs) went first job start to last job end in 14.5 min. Runs 40253936975 (18:25, 25.1 min) and 40255860809 (18:51, 20.8 min) overlapped the CI of gap PR #243 and audit PRs #244 and #245, which compete for the Windows runners; the last job in both was Quality gates (windows-latest). The shift-end merge PR #246 (19:12) found its completed CI run and went through. Criterion 2 is not met yet: no run after the change that overlapped a shift-end merge finished under 15 min. Stays in Backlog.
- 2026-10-29 06:00 Arizona (interactive session): shift-end merge PR #250 (08:46 UTC). Its `factory/merge-20261028-201954` PR run 40844535485 was skipped as PR #238 intends; the competitor was the `master` push run 40844580348 (08:46:38 to 09:03:45 UTC, 17.1 min). The overlapping work/dark-factory run 40845247630 went first job start to last job end 08:52:39 to 09:07:40 UTC, 15.0 min (15m01s), missing criterion 2 by about a second. PR #250 itself merged, so criterion 3 holds again. Remaining idea if the next overlapping run also misses: skip the `master` push run's jobs whose tree a green work/dark-factory run already tested, keeping Release's needs intact. Stays in Backlog for the next shift-end merge's sample.

## Log

- 2026-10-25: Created.
- 2026-10-25: Backlog -> Doing. Interactive session: skip the factory/merge PR duplicate CI and Integration runs on guard/bl-3512 from master Criteria at claim: af56919a b59972c4 59fec9cb.
- 2026-10-25: Doing -> Backlog. Waiting on CI for PR #238 (guard/bl-3512, ece8175aa); reclaim to merge and measure
