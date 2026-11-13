---
id: BL-5148
title: Fix flaky Engine test Dispose_NeverSignalled_ClosesTheListenerAndLaterSignalsDoNothing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitTests/SocketActionGatedResolverTests.cs]
requirement: none
created: 2026-11-13
completed:
---
# BL-5148 — Fix flaky Engine test Dispose_NeverSignalled_ClosesTheListenerAndLaterSignalsDoNothing

## Goal

The test no longer fails when a parallel test is given the closed listener's handle number.

## Context

LoopbackLookupWakeup disposal closes the handle, but another test may be given the same handle number, so asserting the handle names no socket was racy.

## Acceptance criteria

- [x] The test passes whether or not the handle number is reused: it checks the endpoint no longer belongs to the closed listener.

## Notes

## Log

- 2026-11-13: Created.
- 2026-11-13: Backlog -> Doing. Criteria at claim: fbecace0.
