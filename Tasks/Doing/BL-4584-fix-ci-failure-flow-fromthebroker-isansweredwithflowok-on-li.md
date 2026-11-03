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
completed:
---
# BL-4584 — Fix CI failure Flow_FromTheBroker_IsAnsweredWithFlowOk on Linux and macOS and Windows

## Goal

`Flow_FromTheBroker_IsAnsweredWithFlowOk` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `Flow_FromTheBroker_IsAnsweredWithFlowOk` failed on Linux and macOS and Windows in CI run 41554514480 (https://github.com/StewartScottRogers/Curl/actions/runs/41554514480). First failing commit: 6dfcd0aa.

    Test method Curl.Conformance.Extended.UnitTests.Amqp0.Messaging.Amqp0MessagingTests.Flow_FromTheBroker_IsAnsweredWithFlowOk threw exception:

Lanes test only on Windows, so reproduce with `gh run view 41554514480 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `Flow_FromTheBroker_IsAnsweredWithFlowOk` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: 40701201.
