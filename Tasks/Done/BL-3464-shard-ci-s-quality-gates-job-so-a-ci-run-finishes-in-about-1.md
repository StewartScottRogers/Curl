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
completed: 2026-10-29
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

- [x] `Measure-CodeQuality.ps1` takes a shard selector (for example `-Shard <n> -ShardCount <m>`, or a library list) and, with none given, measures every gated library as today; its self-test or a test shows the shards partition the gated libraries exactly, with none left out.
- [x] `ci.yml` runs the measurement as parallel Windows shard jobs, and a job still named `Quality gates (windows-latest)` (or every reader updated) fails when any shard fails or a member is outside a gate, with the same `::error` annotations as today.
- [x] Planted check: a branch with one deliberately uncovered branch in a gated library makes the Quality gates result fail (record the run id in Notes), and the branch is not merged.
- [x] Over the first 3 green CI runs on `work/dark-factory` after the change, the run's wall time (first job start to last job end) is under 15 minutes each; Notes record the run ids and times.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Done on `guard/bl-3464` from `origin/master` (not `origin/audit`, which an audit run held), merged as PR #225 (merge d22ba9b41) under the guard-work standing exception once CI was green on Windows, Linux and macOS for head 75fd6196d; master merged into `work/dark-factory` as d4bd0dbc0.
- Design: `Measure-CodeQuality.ps1 -Shard n -ShardCount m` builds a solution filter of shard n's `*.UnitTests` projects, runs them with coverage (one folder per project) and writes `shard-n-of-m.txt`; it scores nothing and fails if the shard is empty or wrote no Cobertura report. Projects are dealt longest first by the seconds they took on CI's slow runner (run 40108510840), so `Curl.Protocol.Ssh.UnitTests` (1 to 12 minutes under coverage depending on the runner) is alone in shard 1 and the other 45 split 22/23. `-FromShards` scores every shard's reports together and fails unless the manifests hold shards 1..m of one count, every test project ran exactly once, and all 48 gated libraries were measured; it keeps the short-coverage recheck. `-SelfTest` checks the partition for 1 to 8 shards and that every gated library is reached by a test project. With no shard switch the script measures everything as before (gource.yml unchanged). The shard unit is a whole test project: the merge keeps the best condition count per line, not a branch union, so splitting one project would change the measurement.
- ci.yml: matrix `quality-shard` (3 jobs) uploads the reports; `Quality gates (windows-latest)` keeps its name, `needs:` the shards, fails if they did not all succeed, scores with `-FromShards` and emits the same `::error` annotations (plus one per missing library). master has no branch protection (gh api: 404 Branch not protected), so no required check had to change.
- Self-test: all PASS (shards 1..8 of 46 test projects; none unreached). Local 3 shards + `-FromShards`: 0 failing members across 48 libraries; a missing shard fails with "needs shards 1 to 3 once each".
- Planted check: `guard/bl-3464-planted` (2a5103d0e, one uncovered `if` in `DictIoFailures.WriteFailed`), CI run 40116852201: all three shards green, `Quality gates (windows-latest)` failed with `DictIoFailures.WriteFailed(long, int, System.IO.IOException) is outside a gate: line, branch`. Not merged; the branch waits for Stewart to approve its deletion.
- Wall times (first job start to last job end, skipped jobs left out): guard/bl-3464 push run 40116837442 9m41s; PR run 40116871264 15m02s, of which about 5 minutes was shard jobs waiting for runners while three runs were going at once; first `work/dark-factory` run 40148677268 (d4bd0dbc0) 13m09s, green, SSH shard 11m23s. Runs 2 and 3 on `work/dark-factory` are still to come. The SSH project alone sets the floor: on a slow runner it runs about 12 minutes under coverage. If a run passes 15 minutes, the next lever is those tests (bcrypt_pbkdf key decryption, about 30 s each under coverage), not more shards.

- Done on `guard/bl-3464` from `origin/master` (not `origin/audit`, which an audit run held), merged as PR #225 (merge d22ba9b41) under the guard-work standing exception once CI was green on Windows, Linux and macOS for head 75fd6196d; master merged into `work/dark-factory` as d4bd0dbc0.
- Design: `Measure-CodeQuality.ps1 -Shard n -ShardCount m` builds a solution filter of shard n's `*.UnitTests` projects, runs them with coverage (one folder per project) and writes `shard-n-of-m.txt`; it scores nothing and fails if the shard is empty or wrote no Cobertura report. Projects are dealt longest first by the seconds they took on CI's slow runner (run 40108510840), so `Curl.Protocol.Ssh.UnitTests` (1 to 12 minutes under coverage depending on the runner) is alone in shard 1 and the other 45 split 22/23. `-FromShards` scores every shard's reports together and fails unless the manifests hold shards 1..m of one count, every test project ran exactly once, and all 48 gated libraries were measured; it keeps the short-coverage recheck. `-SelfTest` checks the partition for 1 to 8 shards and that every gated library is reached by a test project. With no shard switch the script measures everything as before (gource.yml unchanged). The shard unit is a whole test project: the merge keeps the best condition count per line, not a branch union, so splitting one project would change the measurement.
- ci.yml: matrix `quality-shard` (3 jobs) uploads the reports; `Quality gates (windows-latest)` keeps its name, `needs:` the shards, fails if they did not all succeed, scores with `-FromShards` and emits the same `::error` annotations (plus one per missing library). master has no branch protection (gh api: 404 Branch not protected), so no required check had to change.
- Self-test: all PASS (shards 1..8 of 46 test projects; none unreached). Local 3 shards + `-FromShards`: 0 failing members across 48 libraries; a missing shard fails with "needs shards 1 to 3 once each".
- Planted check: `guard/bl-3464-planted` (2a5103d0e, one uncovered `if` in `DictIoFailures.WriteFailed`), CI run 40116852201: all three shards green, `Quality gates (windows-latest)` failed with `DictIoFailures.WriteFailed(long, int, System.IO.IOException) is outside a gate: line, branch`. Not merged; the branch waits for Stewart to approve its deletion.
- Wall times (first job start to last job end, skipped jobs left out): guard/bl-3464 push run 40116837442 9m41s; PR run 40116871264 15m02s, of which about 5 minutes was shard jobs waiting for runners while three runs were going at once; first `work/dark-factory` run 40148677268 (d4bd0dbc0) 13m09s, green, SSH shard 11m23s. Runs 2 and 3 on `work/dark-factory` are still to come. The SSH project alone sets the floor: on a slow runner it runs about 12 minutes under coverage. If a run passes 15 minutes, the next lever is those tests (bcrypt_pbkdf key decryption, about 30 s each under coverage), not more shards.
- BL-3511 (fixtures re-encrypted with 2 bcrypt rounds, 75c6bb831) in `work/dark-factory` run 40211830827 (head 0c9a296c3): Quality gates shard 1 (SSH) 16:28:50 to 16:30:43 UTC, 1m53s, against 2m10s to 12m22s before. At 16:34 UTC the run was still going (Ubuntu build and test 4m39s, shards 2 and 3 and the Windows and macOS builds still running), so shard 1 no longer sets its wall time; recorded by BL-3511, which a lane cannot hold open for CI.
- Run 40211830827 finished green: first job start 16:28:49 to last job end 16:37:57 UTC, 9m08s (created 16:28:23, so 9m34s end to end, no queueing). Its long pole is now `Pack Curl.Mcp (windows-latest)` at 9m07s; shards 2 and 3 took 6m08s and 5m35s, and the scoring job ran 16:35:07 to 16:37:20. Before BL-3511, the three runs that came before it went from first job start to last job end in 14m37s (40204269817, SSH shard 12m22s, the long pole), 12m32s (40205671348, SSH shard 2m10s) and 18m38s (40206727905, SSH shard 5m25s; runner contention with the shift-end merge's CI, Release and Integration runs on `factory/merge-20261025-081939` and `master` held its jobs back as late as 15:40). Their created-to-updated times (23, 18 and 25 minutes) also count the 5 to 8.5 minute concurrency-group wait before the first job. Runner contention is filed as BL-3512. This counts as one green run under 15 minutes after BL-3511; 2 more are needed.
- 2026-10-25 19:40 Arizona (interactive session): latest work/dark-factory CI runs, first job start to last job end: 40233303425 14.5 min, 40253936975 25.1 min, 40255860809 20.8 min (the last two slowed by audit and gap PR CI on the same Windows runners). Not 3 green runs in a row under 15 min. Stays in Backlog.
- 2026-10-29 06:00 Arizona (interactive session): four green work/dark-factory runs in a row (cancelled superseded runs between them skipped), first job start to last job end: 40860207910 10.8 min, 40861648954 10.6 min, 40873605719 10.7 min, 40875770006 10.5 min; earlier the same night 40797809542 11.1 min and 40798810864 10.8 min. Criterion met. The one slow green run since the change, 40767644348 (2026-10-28, 28.2 min), waited for macOS runners (macOS jobs started 9 to 12 minutes after the rest); runner contention stays with BL-3512.

## Log

- 2026-10-24: Created.
- 2026-10-24: Backlog -> Doing. Interactive session (Stewart: do BL-3464): sharding on guard/bl-3464 from master Criteria at claim: 68a57379 7d68909b f586dc79 a03ef6b7 3d836d5e.
- 2026-10-24: Doing -> Backlog. Sharding merged (PR #225); 1 of 3 green work/dark-factory runs measured (40148677268, 13m09s). Waiting for 2 more runs to record their wall times; nothing else is left to build
- 2026-10-29: Backlog -> Doing. Criteria at claim: 68a57379 7d68909b f586dc79 a03ef6b7 3d836d5e.
- 2026-10-29: Doing -> Done. Four green work/dark-factory CI runs in a row at 10.5-10.8 min first job to last; sharding (PR #225) met its goal
