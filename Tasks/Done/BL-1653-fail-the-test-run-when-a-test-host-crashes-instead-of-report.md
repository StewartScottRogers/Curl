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
completed: 2026-10-07
---
# BL-1653 — Fail the test run when a test host crashes instead of reporting Passed

## Goal

When a test host process dies part way through a run (a stack overflow, `Environment.FailFast`), `dotnet test` exits non-zero and prints a failure, instead of printing `Passed!` with only the tests that finished before the crash counted.

## Context

- Seen on 2026-10-07 during BL-1499: a test helper in `Curl.Http2.UnitTests` recursed into itself and overflowed the stack. `dotnet test Curl.Http2.UnitTests --filter "TestCategory!=Integration"` printed `Passed!  - Failed: 0, Passed: 312` on one run, `Passed: 224` on the next and `Passed: 277` inside the solution run; `--list-tests` listed 358. Only `--logger "console;verbosity=normal"` showed `Stack overflow.`. The fast run, the lanes' gate and CI would all have gone green with a third of the project's tests silently not run.
- Reproduce: add a test that calls a method recursing without end, run the fast tests, and compare the `Total` with `dotnet test --list-tests`.
- Find why the crash is not reported (MSTest runner or VSTest adapter settings in `MSTestSettings.cs`/`Directory.Build.props`, `TestingPlatformDotnetTestSupport`, the `TestDiagnostics` hooks) and make it fail the run. If the fix needs another file, add it to `touches`.

## Acceptance criteria

- [x] With a deliberately crashing test present (kept out of the commit), `dotnet test <project> --filter "TestCategory!=Integration"` exits non-zero and its output names the crash; the steps and output are recorded under Notes.
- [x] Without it, `dotnet build` is clean and the fast tests pass.

## Notes

- 2026-10-07 (lane 3): **Finding: VSTest already fails the run. No configuration change was needed, so nothing in `MSTestSettings.cs` or `Directory.Build.props` changed.** The solution runs MSTest 4.4.1 through VSTest (no `EnableMSTestRunner` or `TestingPlatformDotnetTestSupport`, SDK 10.0.401). When a test host crashes, `dotnet test` exits 1 and prints `The active test run was aborted. Reason: Test host process crashed : <reason>`. Below that it prints the per-assembly line `Passed!  - Failed: 0, Passed: N ...` for the tests that finished before the crash, then `Test Run Aborted.`. That `Passed!` line is the misleading part. It counts only the tests that ran, and in BL-1499 it was read as the verdict. The exit code was the real verdict, and it was red.
- Steps, each with a throwaway `Curl.Http2.UnitTests/ZzCrashProbeTests.cs` (deleted afterwards, never committed):
  1. Unbounded recursion (`private static int Recurse(int d) => Recurse(d + 1) + d;`): `dotnet test Curl.Http2.UnitTests --filter "TestCategory!=Integration"` gave EXIT=1 with `The active test run was aborted. Reason: Test host process crashed : Stack overflow.`, `Repeated 24082 times:`, `Passed!  - Failed: 0, Passed: 355, ... Total: 355` and `Test Run Aborted.`. Across runs `Passed` varied (349, 352, 355) because the tests run in parallel.
  2. The lanes' gate form (`--no-build -nologo --filter 'TestCategory!=Integration' --blame-hang-timeout 10m`, `RunDarkFactory.ps1` line ~3253): EXIT=1, the same abort line, plus `The test running when the crash occurred: Curl.Http2.ZzCrashProbeTests.RecursesUntilTheStackOverflows`.
  3. `Environment.FailFast("probe")`: EXIT=1, `The active test run was aborted. Reason: Test host process crashed : Process terminated.` and `probe`.
- The lanes' gate judges by `$LASTEXITCODE` (so it goes red). Its `Get-FailedTestNames` already matches `test run was aborted|Test host process crashed` and reports "a test host aborted". CI's `dotnet test` step also fails on the non-zero exit. A crash cannot pass silently through either one.
- Rule for anyone reading a run: the exit code and any `Test Run Aborted.` / `Test host process crashed` line are the verdict, not a `Passed!` line. Moving to Microsoft.Testing.Platform could change the summary text, but it would change the coverage collection that Measure-CodeQuality and CI depend on. That is far too big for this task and is not needed for correctness.
- Gates: `dotnet build` is clean (0 errors). In the fast tests, 32 of 33 test assemblies passed. The one failure was `Curl.Cookies.CookieAdversarialTests.EveryMember_ManyConcurrentCallers_EndWithTheSameCookiesAsOneAfterAnother`, the known flaky test BL-1651 is fixing on another lane. It has nothing to do with this task, which changed no code.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. A crashing test host already fails dotnet test (exit 1, 'Test host process crashed'); verified for stack overflow and FailFast, findings recorded
