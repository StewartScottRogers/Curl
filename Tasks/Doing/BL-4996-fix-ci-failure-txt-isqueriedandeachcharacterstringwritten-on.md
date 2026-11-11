---
id: BL-4996
title: Fix CI failure Txt_IsQueriedAndEachCharacterStringWritten on Linux and Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.Extended.UnitTests, Curl.Conformance.Extended.UnitLibrary]
requirement: none
created: 2026-11-11
completed:
---
# BL-4996 — Fix CI failure Txt_IsQueriedAndEachCharacterStringWritten on Linux and Windows

## Goal

`Txt_IsQueriedAndEachCharacterStringWritten` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `Txt_IsQueriedAndEachCharacterStringWritten` failed on Linux and Windows in CI run 43117708980 (https://github.com/StewartScottRogers/Curl/actions/runs/43117708980). First failing commit: 0e3656a3.

    Assertion failed. Expected strings to be equal.

Lanes test only on Windows, so reproduce with `gh run view 43117708980 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `Txt_IsQueriedAndEachCharacterStringWritten` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing. Criteria at claim: 45a2fac6.
