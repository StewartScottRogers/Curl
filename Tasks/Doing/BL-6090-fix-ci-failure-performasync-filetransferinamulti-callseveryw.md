---
id: BL-6090
title: Fix CI failure PerformAsync_FileTransferInAMulti_CallsEveryWriteOnThePerformingThreadInsidePerform on Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitTests, Curl.Engine.UnitLibrary]
requirement: none
created: 2026-11-27
completed:
---
# BL-6090 — Fix CI failure PerformAsync_FileTransferInAMulti_CallsEveryWriteOnThePerformingThreadInsidePerform on Windows

## Goal

`PerformAsync_FileTransferInAMulti_CallsEveryWriteOnThePerformingThreadInsidePerform` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `PerformAsync_FileTransferInAMulti_CallsEveryWriteOnThePerformingThreadInsidePerform` failed on Windows in CI run 46108135370 (https://github.com/StewartScottRogers/Curl/actions/runs/46108135370). First failing commit: b334fc27.

    Assertion failed. Expected value to be less than the upper bound.

Lanes test only on Windows, so reproduce with `gh run view 46108135370 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `PerformAsync_FileTransferInAMulti_CallsEveryWriteOnThePerformingThreadInsidePerform` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing. Criteria at claim: 8761eeff.
