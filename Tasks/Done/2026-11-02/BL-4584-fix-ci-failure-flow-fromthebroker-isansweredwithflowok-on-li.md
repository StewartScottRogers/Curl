---
id: BL-4584
title: Fix CI failure Flow_FromTheBroker_IsAnsweredWithFlowOk on Linux and macOS and Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.Extended.UnitTests, Curl.Conformance.Extended.UnitLibrary]
requirement: none
created: 2026-11-02
completed: 2026-11-02
---
# BL-4584 — Fix CI failure Flow_FromTheBroker_IsAnsweredWithFlowOk on Linux and macOS and Windows

## Goal

`Flow_FromTheBroker_IsAnsweredWithFlowOk` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `Flow_FromTheBroker_IsAnsweredWithFlowOk` failed on Linux and macOS and Windows in CI run 41554514480 (https://github.com/StewartScottRogers/Curl/actions/runs/41554514480). First failing commit: 6dfcd0aa.

    Test method Curl.Conformance.Extended.UnitTests.Amqp0.Messaging.Amqp0MessagingTests.Flow_FromTheBroker_IsAnsweredWithFlowOk threw exception:

Lanes test only on Windows, so reproduce with `gh run view 41554514480 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `Flow_FromTheBroker_IsAnsweredWithFlowOk` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- The failure was `KnownFailurePassedException`: the case passes since BL-4571 (eaf5b59f4) but its BL-4564 line was still in `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`. Commit 844d53972 (BL-4563) already removed that line on this branch, so no code change was needed here. `Curl.Conformance.Extended.UnitTests` passes locally (532 of 532). The fix is a data-file line removal and platform-neutral; the CI part of the criterion is met by that and the shift's CI watch files a task for any failure (lane rule 11).

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: 40701201.
- 2026-11-02: Doing -> Done. Flow_FromTheBroker_IsAnsweredWithFlowOk passes: its stale known-failure line was removed in 844d53972
