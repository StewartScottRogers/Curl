---
id: BL-1653
title: Fail the test run when a test host crashes instead of reporting Passed
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [MSTestSettings.cs, Directory.Build.props]
requirement: none
created: 2026-10-07
completed:
---
# BL-1653 — Fail the test run when a test host crashes instead of reporting Passed

## Goal

When a test host process dies part way through a run (a stack overflow, `Environment.FailFast`), `dotnet test` exits non-zero and prints a failure, instead of printing `Passed!` with only the tests that finished before the crash counted.

## Context

- Seen on 2026-10-07 during BL-1499: a test helper in `Curl.Http2.UnitTests` recursed into itself and overflowed the stack. `dotnet test Curl.Http2.UnitTests --filter "TestCategory!=Integration"` printed `Passed!  - Failed: 0, Passed: 312` on one run, `Passed: 224` on the next and `Passed: 277` inside the solution run; `--list-tests` listed 358. Only `--logger "console;verbosity=normal"` showed `Stack overflow.`. The fast run, the lanes' gate and CI would all have gone green with a third of the project's tests silently not run.
- Reproduce: add a test that calls a method recursing without end, run the fast tests, and compare the `Total` with `dotnet test --list-tests`.
- Find why the crash is not reported (MSTest runner or VSTest adapter settings in `MSTestSettings.cs`/`Directory.Build.props`, `TestingPlatformDotnetTestSupport`, the `TestDiagnostics` hooks) and make it fail the run. If the fix needs another file, add it to `touches`.

## Acceptance criteria

- [ ] With a deliberately crashing test present (kept out of the commit), `dotnet test <project> --filter "TestCategory!=Integration"` exits non-zero and its output names the crash; the steps and output are recorded under Notes.
- [ ] Without it, `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-07: Created.
