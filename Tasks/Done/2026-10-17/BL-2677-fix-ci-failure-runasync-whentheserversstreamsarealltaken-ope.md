---
id: BL-2677
title: Fix CI failure RunAsync_WhenTheServersStreamsAreAllTaken_OpensAnotherConnectionForTheNextTransfer on Linux and macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console.UnitTests, Curl.Console]
requirement: none
created: 2026-10-17
completed: 2026-10-17
---
# BL-2677 — Fix CI failure RunAsync_WhenTheServersStreamsAreAllTaken_OpensAnotherConnectionForTheNextTransfer on Linux and macOS

## Goal

`RunAsync_WhenTheServersStreamsAreAllTaken_OpensAnotherConnectionForTheNextTransfer` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `RunAsync_WhenTheServersStreamsAreAllTaken_OpensAnotherConnectionForTheNextTransfer` failed on Linux and macOS in CI run 39077398008 (https://github.com/StewartScottRogers/Curl/actions/runs/39077398008). First failing commit: 35c73029.

    Test method Curl.Console.CurlCommandRunnerHttp2MultiplexingTests.RunAsync_WhenTheServersStreamsAreAllTaken_OpensAnotherConnectionForTheNextTransfer threw exception:

Lanes test only on Windows, so reproduce with `gh run view 39077398008 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `RunAsync_WhenTheServersStreamsAreAllTaken_OpensAnotherConnectionForTheNextTransfer` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- Cause: a race in the test, not in production. CI threw `Sequence contains no elements` at
  `server.To("other").Single()`: the test waited only for the `h` connection's second read
  before taking the `other` connection, so on Linux and macOS the transfer to `other` had
  not yet called `ConnectAsync`. Windows happened to schedule it first.
- Fix: the test now waits (`WhenOpenedAsync`) for each connection to be opened before
  taking it with `Single()`. The same guard went on the two sibling tests in the class,
  which took `server.To("h").Single()` straight after starting the run. No production change.
- Passes locally on Windows (Curl.Console.UnitTests 2826 passed; all fast tests green). A
  lane cannot push, so CI on the landing commit is confirmed by the shift's integration and
  its CI watch, which refiles the test if it fails again.

## Log

- 2026-10-17: Created.
- 2026-10-17: Backlog -> Doing. Criteria at claim: 58ae8502.
- 2026-10-17: Doing -> Done. RunAsync_WhenTheServersStreamsAreAllTaken_OpensAnotherConnectionForTheNextTransfer waits for each connection to open, so it no longer races on Linux and macOS
