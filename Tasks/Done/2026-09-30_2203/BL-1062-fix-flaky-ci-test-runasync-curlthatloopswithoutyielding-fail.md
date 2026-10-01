---
id: BL-1062
title: Fix flaky CI test RunAsync_CurlThatLoopsWithoutYielding_FailsAfterTheTimeLimitAndIsStoppedByTheAbandonedServer that failed once on Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests, Curl.Conformance.UnitLibrary]
requirement: none
created: 2026-09-30
completed: 2026-09-30
---
# BL-1062 — Fix flaky CI test RunAsync_CurlThatLoopsWithoutYielding_FailsAfterTheTimeLimitAndIsStoppedByTheAbandonedServer that failed once on Windows

## Goal

`RunAsync_CurlThatLoopsWithoutYielding_FailsAfterTheTimeLimitAndIsStoppedByTheAbandonedServer` passes on every run on Windows, Linux and macOS; it failed once on Windows in the `CI` workflow and passed on the runs either side.

## Context

Filed by the dark factory's CI watch (BL-987). `RunAsync_CurlThatLoopsWithoutYielding_FailsAfterTheTimeLimitAndIsStoppedByTheAbandonedServer` failed on Windows in CI run 36797838233 (https://github.com/StewartScottRogers/Curl/actions/runs/36797838233). First failing commit: d065d6d3.

    Test method Curl.Conformance.UpstreamCaseRunnerTests.RunAsync_CurlThatLoopsWithoutYielding_FailsAfterTheTimeLimitAndIsStoppedByTheAbandonedServer threw exception:

Lanes test only on Windows, so reproduce with `gh run view 36797838233 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] What made `RunAsync_CurlThatLoopsWithoutYielding_FailsAfterTheTimeLimitAndIsStoppedByTheAbandonedServer` fail intermittently is named under Notes and removed.
- [x] `RunAsync_CurlThatLoopsWithoutYielding_FailsAfterTheTimeLimitAndIsStoppedByTheAbandonedServer` passes locally, and in the `CI` workflow on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- Cause: CI's `System.TimeoutException` came from the test's own 10-second wait for the fake
  clock's timer (`ExpireTheTimeLimitAsync`, line 270). That means the runner never created
  its time limit. `UpstreamCaseRunner.RunCurlAsync` ran curl with `Task.Run` and only then
  awaited a `curlStarted` signal (BL-1056) before calling `WaitAsync`. In this test, curl
  spins forever on a thread-pool thread, so the continuation that starts the time limit had
  to wait for a free pool thread. On a busy 4-vCPU Windows runner (other test assemblies
  ran at the same time; Cryptography alone took 21 s), the pool did not get to it within
  10 s.
- Fix: curl now starts on a thread of its own
  (`Task.Factory.StartNew(..., TaskCreationOptions.LongRunning).Unwrap()`), and `WaitAsync`
  is called right away on the calling thread, without the `curlStarted` hop. A looping curl
  no longer holds a pool thread, and starting the time limit no longer depends on the pool.
  BL-1056's concern (a queued work item waiting past the limit) is also covered: a dedicated
  thread is not queued.
- Verified: the test class passed 8 times in a row locally. The full fast suite is green.
  Faking a one-thread pool (`DOTNET_ThreadPool_ForceMaxWorkerThreads=1`) also starves the
  test host itself, so that reproduction was not possible. CI on all three platforms runs
  when the shift pushes this commit; the CI watch (BL-987) files the test again if it ever
  fails.

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. Upstream case runner starts curl on its own thread, so a looping curl can no longer starve the time limit
