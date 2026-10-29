---
id: BL-4089
title: Fix CI failure SocketAction_ConnectCompletesAtOnceThenReadWaits_KickTellsTheSocketForReadingAndTheDeadline on macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitTests, Curl.Engine.UnitLibrary]
requirement: none
created: 2026-10-29
completed:
---
# BL-4089 — Fix CI failure SocketAction_ConnectCompletesAtOnceThenReadWaits_KickTellsTheSocketForReadingAndTheDeadline on macOS

## Goal

`SocketAction_ConnectCompletesAtOnceThenReadWaits_KickTellsTheSocketForReadingAndTheDeadline` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `SocketAction_ConnectCompletesAtOnceThenReadWaits_KickTellsTheSocketForReadingAndTheDeadline` failed on macOS in CI run 40891090016 (https://github.com/StewartScottRogers/Curl/actions/runs/40891090016). First failing commit: a0be05ee.

    Assertion failed. Expected values to be equal.

Lanes test only on Windows, so reproduce with `gh run view 40891090016 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `SocketAction_ConnectCompletesAtOnceThenReadWaits_KickTellsTheSocketForReadingAndTheDeadline` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-10-29: Created.
