---
id: BL-3837
title: Fix CI failure job Quality gates (windows-latest) step Measure coverage, complexity and CRAP on Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-3831]
touches: [Measure-CodeQuality.ps1]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3837 — Fix CI failure job Quality gates (windows-latest) step Measure coverage, complexity and CRAP on Windows

## Goal

`job Quality gates (windows-latest) step Measure coverage, complexity and CRAP` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `job Quality gates (windows-latest) step Measure coverage, complexity and CRAP` failed on Windows in CI run 40348984024 (https://github.com/StewartScottRogers/Curl/actions/runs/40348984024). First failing commit: 9555ef0d.

    [36;1m  Write-Host "::error file=$($m.file.Replace('\', '/')),line=$($m.line)::$($m.member) is outside a gate: $($m.fails -join ', ')"[0m; CurlCommandRunner.WriteRefusalAsync (async) is outside a gate: branch

Lanes test only on Windows, so reproduce with `gh run view 40348984024 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `job Quality gates (windows-latest) step Measure coverage, complexity and CRAP` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- 2026-10-26 (lane 2): The only member outside a gate is CurlCommandRunner.WriteRefusalAsync (branch), the exact work of BL-3831 (Backlog, ready, touches Curl.Console.UnitTests/CurlCommandRunnerWriteRefusalTests.cs). Left to that task (AF-0148) rather than redone here; this task waits on it and then only confirms the quality job is green.

- 2026-10-26 13:45 Arizona (interactive session): the cause, from CI's own coverage (run 40393706707, artifact `quality-shard-3`, `Curl.Console.UnitTests` Cobertura): `<WriteRefusalAsync>d__305` line 5173 is 2/2, but **line 5176 is 1/2 with 1 hit**. Line 5176 is `await WriteTlsSessionLinesAsync(optionsRead, TlsSessionFileLines.Load)`: the method group becomes a delegate the compiler caches in a static field behind a null check, and that check is the branch. With one test reaching the `FoundAtTransferSetup` path per process, only the "create" side runs; locally more tests reach it, so BL-3831 saw it pass and changed nothing. Red on every finished CI run since 15:24 UTC (40348984024, 40351840757, 40354746312, 40357305225, 40362462083, 40393706707). Fix so it does not depend on test count or order, e.g. pass a lambda `(sessions, options) => TlsSessionFileLines.Load(sessions, options)` as line 5178 does (a capturing lambda has no cache check) or keep the delegate in a `static readonly` field; then check `Measure-CodeQuality.ps1 -Library Curl.Console` and, to be sure, the next CI run's `Quality gates (windows-latest)`.

- 2026-10-26 15:35 Arizona (interactive session): fixed. Both uses of `TlsSessionFileLines.Load` as a delegate (lines 1203 and 5176) now pass `LoadTlsSessionFileLines`, a `static readonly` delegate made once with `new(...)`, as `NoEnvironmentVariables` already is, so no method-group cache check (and no branch) is left in `WriteRefusalAsync`. Correction to the note above: a non-capturing lambda is cached behind the same check, so it would not have helped. `Measure-CodeQuality.ps1 -Library Curl.Console`: 0 failing members; `dotnet build` 0 warnings; `Curl.Console.UnitTests` fast tests 2931 passed. The CI half of the criterion waits on the run for the pushed commit.

- 2026-10-26 16:50 (lane 2): BL-3878 is Done. Locally `Measure-CodeQuality.ps1 -Library Curl.Console`: 0 failing members; `dotnet build` clean. CI run 40414014932 on the fix commit was green on every Quality gates job, Windows and macOS. CI run 40420942031 (9be076388, with BL-3878) passed Build and test (windows-latest) and Quality gates shard 1, but Linux and macOS Fast tests fail on an unrelated test, Cgr062Tests drive-letter DataRow from 5d16740db, filed as BL-3897. That failure is not this step. The fix is platform-neutral (a static readonly delegate), so the CI half is ticked under the lane rule 11.

## Log

- 2026-10-26: Created.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 10cc6e68.
- 2026-10-26: Doing -> Backlog. Waits on BL-3831, which covers WriteRefusalAsync's untaken branch, the only member outside a gate
- 2026-10-26: Backlog -> Doing. Criteria at claim: 10cc6e68.
- 2026-10-26: Doing -> Blocked. Quality gates (windows-latest) now passes: CI run 40414014932 on the fix commit 85b5f4962 is green on Windows, macOS and every quality job. Only Build and test (ubuntu-latest) fails, on Format_OpenSslUnprintableNames_PrintsEmptySubjectAndIssuer, which is BL-3878. Clears when BL-3878 lands and a CI run is green on all three platforms; then tick the criterion and move to Done. Claude clears it.
- 2026-10-26: Blocked -> Backlog. Unblocked: BL-3878 now Done
- 2026-10-26: Backlog -> Doing. Criteria at claim: 10cc6e68.
- 2026-10-26: Doing -> Done. Quality gates (windows-latest) is green: WriteRefusalAsync has no method-group cache branch; Curl.Console 0 failing members locally
