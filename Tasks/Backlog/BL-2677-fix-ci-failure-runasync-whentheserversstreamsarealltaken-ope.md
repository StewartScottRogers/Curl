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
completed:
---
# BL-2677 — Fix CI failure RunAsync_WhenTheServersStreamsAreAllTaken_OpensAnotherConnectionForTheNextTransfer on Linux and macOS

## Goal

`RunAsync_WhenTheServersStreamsAreAllTaken_OpensAnotherConnectionForTheNextTransfer` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `RunAsync_WhenTheServersStreamsAreAllTaken_OpensAnotherConnectionForTheNextTransfer` failed on Linux and macOS in CI run 39077398008 (https://github.com/StewartScottRogers/Curl/actions/runs/39077398008). First failing commit: 35c73029.

    Test method Curl.Console.CurlCommandRunnerHttp2MultiplexingTests.RunAsync_WhenTheServersStreamsAreAllTaken_OpensAnotherConnectionForTheNextTransfer threw exception:

Lanes test only on Windows, so reproduce with `gh run view 39077398008 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `RunAsync_WhenTheServersStreamsAreAllTaken_OpensAnotherConnectionForTheNextTransfer` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-10-17: Created.
