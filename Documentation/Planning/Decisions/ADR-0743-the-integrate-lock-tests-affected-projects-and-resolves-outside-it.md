# ADR-0743 — Under the integrate lock a lane tests only the projects its commits can affect, and resolves a rebase conflict with the lock released

- Status: Accepted
- Date: 2026-10-21
- Task: BL-2967
- Approved by Stewart (2026-10-21: "I accept your recommendation to shortening the integration
  step"); the details decided by Claude under Stewart's delegation.

## Context

Lanes integrate one at a time under a cross-lane lock: rebase, build, fast tests, push. On shift
20261021-064209, between 11:07 and 11:53 Arizona, 14 pushes went through the lock at about 3.3
minutes each, and 4 of the 9 lanes were waiting for it. That caps a shift at about 18 tasks an
hour however many lanes run. Two things filled the lock: every lane ran all ~40,000 fast tests,
though most tasks change one or two projects, and a rebase conflict ran a 6 to 8 minute resolver
session while every other lane waited.

## Decision

1. `Test-Green` still builds the whole solution, then runs only the fast tests of the
   `*.UnitTests` projects that depend on a project the lane's commits change, through
   `ProjectReference`s at any depth or a linked source file (`Get-AffectedTestProjects`).
   - A change to a shared build file (`Directory.Build.*`, `Directory.Packages.props`,
     `global.json`, `NuGet.config`, a solution file, a root `*.props` or `*.targets`,
     `CodeMetricsConfig.txt`, `.editorconfig`, the root `MSTestSettings.cs`) or to any `.csproj`
     runs them all.
   - A change to no project runs none. A failed `git diff` runs them all.
   - The selected projects run together through a temporary solution filter, with the same
     filter, hang timeout, rerun and run-alone rules as the full run.
2. `Invoke-Integrate` rebases once before taking the lock. A rebase conflict, there or under the
   lock, is resolved with the lock released; the lane then takes the lock again and rebases
   afresh. After 3 resolver runs it gives up as before. Nothing is pushed but a rebase made under
   the lock. `Repair-IntegrationBreak` still runs under the lock, since it must fix the combined
   tree while nothing moves.

## Consequences

- The lock is expected to drop to about a minute a task, so integration stops capping a shift
  below its lane count.
- A break that only a test outside the affected set would catch, such as a test that reads
  another project's files at run time, reaches `work/dark-factory`. CI still runs every test on
  Windows, Linux and macOS for every push, and the coordinator files a High task for each
  failure (BL-987), so it is caught within one CI run. The shift-end merge to `master` still
  needs green CI.
- A lane that resolves while others push can meet a new conflict and run the resolver again,
  which costs tokens on files many tasks touch; no other lane waits for it.
- Self-tests: `-TestAffectedTests` and `-TestIntegrateLock`.
