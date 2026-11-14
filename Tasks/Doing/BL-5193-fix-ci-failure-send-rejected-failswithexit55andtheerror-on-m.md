---
id: BL-5193
title: Fix CI failure Send_Rejected_FailsWithExit55AndTheError on macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.Extended.UnitTests, Curl.Conformance.Extended.UnitLibrary]
requirement: none
created: 2026-11-14
completed:
---
# BL-5193 — Fix CI failure Send_Rejected_FailsWithExit55AndTheError on macOS

## Goal

`Send_Rejected_FailsWithExit55AndTheError` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `Send_Rejected_FailsWithExit55AndTheError` failed on macOS in CI run 43609016480 (https://github.com/StewartScottRogers/Curl/actions/runs/43609016480). First failing commit: 5c170e2b.

    Test method Curl.Conformance.Extended.UnitTests.Amqp1.Messaging.Amqp1SendCases.Send_Rejected_FailsWithExit55AndTheError threw exception:

Lanes test only on Windows, so reproduce with `gh run view 43609016480 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `Send_Rejected_FailsWithExit55AndTheError` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-11-14: Created.
- 2026-11-14: Backlog -> Doing. Criteria at claim: 1eb73227.
