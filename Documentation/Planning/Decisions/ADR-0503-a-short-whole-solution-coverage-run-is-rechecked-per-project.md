# ADR-0503: A short whole-solution coverage run is rechecked per project

- Status: Accepted
- Date: 2026-10-15
- Task: BL-2269
- Decided by Claude under Stewart's delegation

## Context

CI's quality job runs `Measure-CodeQuality.ps1` with no `-Library`, so it runs `dotnet test Curl.slnx`
once with Cobertura coverage, which writes one merged report for every test project. In CI run
38700283564 (commit 639a0aec) that report lost `Curl.Cryptography.UnitTests`' coverage although all
1462 of its tests passed: 108 Cryptography members, mostly argument guards that only its own tests
reach, were reported uncovered. The other CI run of the same commit passed the gate, and the
Cryptography tests alone measure 100% line and branch coverage locally. The code was not short; the
run lost a report.

## Decision

- After a whole-solution run, when any member is short of line or branch coverage, the script runs
  the `*.UnitTests` projects whose references reach that member's library again, one project at a
  time (the `-Library` mechanism, BL-1318), into `recheck` under the results directory, merges those
  reports with the best-hit rule already used, and scores again.
- Runs with `-Library` or `-SkipTestRun` are not rechecked: `-Library` already runs one project at a
  time, and `-SkipTestRun` runs no tests.

## Consequences

- A lost project report no longer turns the quality job red; a real gap still fails, because merging
  only adds lines some test executed.
- A run with a real gap takes longer, by one run of the test projects reaching the short libraries.
