# ADR-1179: CI keeps its critical path short and its neighbouring workflows from piling up

- Status: Accepted
- Date: 2026-10-31
- Task: BL-3512
- Decided by Claude under Stewart's delegation.

## Context

BL-3512 asks that a `CI` run on `work/dark-factory` go from its first job's start to its last job's end in under
15 minutes, also when a shift-end merge's runs share the runners. PR #238 and PR #256 (ADR-0814) took the merge
pull request's and `master`'s own CI jobs off the runners; after them `master`'s CI push run for the shift-end
merges PR #260 and PR #262 took 8 and 9 seconds (runs 41207646668, 41237470640). Yet no `work/dark-factory` run
on 2026-10-31 finished under 15 minutes, with or without a merge beside it:

| Run | Span | Last job |
| --- | --- | --- |
| 41215194658 | 16.8 min | Quality gates |
| 41223732155 | 19.7 min | Quality gates |
| 41227713497 | 17.5 min | Pack Curl.Mcp (windows-latest) |
| 41236102295 | 17.0 min | Quality gates |
| 41239174672 (after PR #262's merge) | 20.8 min | Quality gates |

Four causes, read from `gh run view <id> --json jobs` and the job logs:

1. **Unbalanced quality shards.** `Measure-CodeQuality.ps1` deals the test projects to the three shards by measured
   seconds, and still weighted the SSH protocol's tests at the 707 seconds of run 40108510840. They now take 25,
   so shard 1 held them alone and finished in 2 minutes, while shards 2 and 3 held 56 and 57 projects and took
   10 and 13.5 minutes (run 41239174672).
2. **Slow scoring.** The `Quality gates` job then spent 5 minutes 40 seconds merging the shards' 1.5 GB of
   Cobertura XML (every test project's report lists every production assembly it loaded) node by node in
   PowerShell. Under PowerShell 7 it took as long (360 seconds locally), so changing the host would not help.
3. **A second native AOT publish.** `Pack Curl.Mcp`'s `Curl.Mcp Integration tests` step took 5 to 12 minutes
   (8 minutes 21 seconds on Windows in run 41227713497, which it ended), most of it
   `CurlMcpNativeAotIntegrationTests`' class set-up publishing Curl.Mcp native AOT, which the job's next step,
   `Pack the runtime package`, publishes again. Locally the 42 tests took 2 minutes 33 seconds, the 39 others 33.
4. **Piled-up neighbours.** `dark-factory-unix.yml` (Linux and macOS) and `dark-factory-unix-shift.yml` (Linux) ran
   once per lane push with no concurrency group: between 22:14 and 22:50 UTC a dozen of each overlapped, a dozen
   macOS jobs among them, and CI runs 41249043176 and 41251396957 then ended on macOS jobs after 32 minutes.
   After a shift-end merge `master`'s Integration tests push run held three more runners, a macOS one among them,
   for up to 36 minutes, repeating tests its parent's tree had already passed.

## Decision

1. The shard weights are remeasured from run 41239174672 (seconds between one project's coverage run and the
   next's, so each includes the 4 to 6 seconds every project costs), with the default at 5. The shards now hold
   38, 38 and 38 projects of about 300 seconds each.
2. The Cobertura merge in `Measure-CodeQuality.ps1` is C# compiled by `Add-Type` (C# 5, so Windows PowerShell and
   PowerShell 7 both compile it), reading each report with an `XmlReader`. On run 41239174672's reports the whole
   `-FromShards` scoring took 74 seconds locally under Windows PowerShell, against 360, and produced the same
   totals, library rows and failing members; on three single reports with 1,375 failing members the old and new
   scripts agree under both hosts.
3. `Pack Curl.Mcp` runs every Curl.Mcp Integration test but `CurlMcpNativeAotIntegrationTests`, and its package
   check now drives the packed native binary through `initialize` and `tools/list` (the five tools) where it
   checked `initialize` alone. That class's native `transfer` test still runs in `integration.yml` on all three
   operating systems, which reports rather than blocks; that is the coverage the gate gives up.
4. `dark-factory-unix.yml` and `dark-factory-unix-shift.yml` get a concurrency group per ref with
   `cancel-in-progress: false`, as CI has on `work/dark-factory` (ADR-0693): the running run finishes and only the
   newest waiting push runs next. Nothing reads their result for an exact commit.
5. `integration.yml` gets ci.yml's `tested-tree` job: a push to `master` whose merge commit has the tree of a
   parent with a passed Integration tests push run runs no Integration job.

Required job names are unchanged. The shift-end merge and the coordinator's CI watch read only `CI` runs, whose
jobs and names are as before.

## Consequences

- The critical path becomes the slowest of `Pack Curl.Mcp` and a shard plus `Quality gates`; BL-3512's Notes
  record the first measured runs.
- A shard runs long again when a test project's time changes a lot; the comment over the weights says to
  remeasure then, and `-SelfTest` still proves every shard count partitions the projects.
- On `work/dark-factory` the Linux and macOS self-tests and the one-task shift proof report on the newest pushed
  commit rather than on every commit, as CI does.
