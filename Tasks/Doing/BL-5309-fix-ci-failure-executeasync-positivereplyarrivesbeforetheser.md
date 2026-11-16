---
id: BL-5309
title: Fix CI failure ExecuteAsync_PositiveReplyArrivesBeforeTheServerConnects_KeepsWaitingAndDownloads on Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Ftp.UnitTests, Curl.Original.Protocol.Ftp.UnitLibrary]
requirement: none
created: 2026-11-16
completed:
---
# BL-5309 — Fix CI failure ExecuteAsync_PositiveReplyArrivesBeforeTheServerConnects_KeepsWaitingAndDownloads on Windows

## Goal

`ExecuteAsync_PositiveReplyArrivesBeforeTheServerConnects_KeepsWaitingAndDownloads` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `ExecuteAsync_PositiveReplyArrivesBeforeTheServerConnects_KeepsWaitingAndDownloads` failed on Windows in CI run 43977592307 (https://github.com/StewartScottRogers/Curl/actions/runs/43977592307). First failing commit: fec71ff5.

    Assertion failed. Expected values to be equal.

Lanes test only on Windows, so reproduce with `gh run view 43977592307 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `ExecuteAsync_PositiveReplyArrivesBeforeTheServerConnects_KeepsWaitingAndDownloads` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing. Criteria at claim: 074f7c4b.
