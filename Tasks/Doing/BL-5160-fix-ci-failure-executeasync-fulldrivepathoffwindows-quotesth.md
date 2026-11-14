---
id: BL-5160
title: Fix CI failure ExecuteAsync_FullDrivePathOffWindows_QuotesThePathAsWritten on Linux and macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.File.UnitTests, Curl.Original.Protocol.File.UnitLibrary]
requirement: none
created: 2026-11-13
completed:
---
# BL-5160 — Fix CI failure ExecuteAsync_FullDrivePathOffWindows_QuotesThePathAsWritten on Linux and macOS

## Goal

`ExecuteAsync_FullDrivePathOffWindows_QuotesThePathAsWritten` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `ExecuteAsync_FullDrivePathOffWindows_QuotesThePathAsWritten` failed on Linux and macOS in CI run 43538809167 (https://github.com/StewartScottRogers/Curl/actions/runs/43538809167). First failing commit: 97661f2f.

    Test method Curl.Original.Protocol.File.FileDriveRelativeOpenFailureMessageTests.ExecuteAsync_FullDrivePathOffWindows_QuotesThePathAsWritten threw exception:

Lanes test only on Windows, so reproduce with `gh run view 43538809167 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `ExecuteAsync_FullDrivePathOffWindows_QuotesThePathAsWritten` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-11-13: Created.
- 2026-11-13: Backlog -> Doing. Criteria at claim: c1341748.
