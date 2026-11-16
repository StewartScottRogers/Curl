---
id: BL-5298
title: Fix CI failure Conversation_AnswersAsRecorded on Linux and Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Mcp.IntegrationTests, Curl.Mcp.UnitLibrary, Curl.Mcp]
requirement: none
created: 2026-11-16
completed:
---
# BL-5298 — Fix CI failure Conversation_AnswersAsRecorded on Linux and Windows

## Goal

`Conversation_AnswersAsRecorded` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `Conversation_AnswersAsRecorded` failed on Linux and Windows in CI run 43936333856 (https://github.com/StewartScottRogers/Curl/actions/runs/43936333856). First failing commit: 1d6b1a5d.

    Assertion failed. Expected condition to be true.

Failing data rows: `Conversation_AnswersAsRecorded ("tools-call-version")`.

Lanes test only on Windows, so reproduce with `gh run view 43936333856 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `Conversation_AnswersAsRecorded` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-11-16: Created.
