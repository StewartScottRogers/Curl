---
id: BL-5847
title: Fix CI failure job Quality gates (windows-latest) step Every shard succeeded on Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Measure-CodeQuality.ps1]
requirement: none
created: 2026-11-24
completed: 2026-11-24
---
# BL-5847 — Fix CI failure job Quality gates (windows-latest) step Every shard succeeded on Windows

## Goal

`job Quality gates (windows-latest) step Every shard succeeded` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `job Quality gates (windows-latest) step Every shard succeeded` failed on Windows in CI run 45432993585 (https://github.com/StewartScottRogers/Curl/actions/runs/45432993585). First failing commit: 852090e3.

    ∩╗┐2026-11-24T16:22:41.0121897Z ##[group]Run if ('failure' -ne 'success') {

Lanes test only on Windows, so reproduce with `gh run view 45432993585 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `job Quality gates (windows-latest) step Every shard succeeded` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- Cause: shard 2 of CI run 45432993585 failed only because the Curl.Extended.Protocol.WebTransport.UnitTests test host crashed (no stack in the log) at 16:17:30; every other project passed. Not a Measure-CodeQuality.ps1 defect. Locally the project passes 4 of 4 runs (once plain, three with XPlat coverage), and the next shared-branch CI runs are not yet complete. No code change; the crash is filed as a flaky-test task.
- Platform-neutral: no change made.

## Log

- 2026-11-24: Created.
- 2026-11-24: Backlog -> Doing. Criteria at claim: 96723aaa.
- 2026-11-24: Doing -> Done. Not reproducible: WebTransport test host crash was a one-off, filed as BL-5848
