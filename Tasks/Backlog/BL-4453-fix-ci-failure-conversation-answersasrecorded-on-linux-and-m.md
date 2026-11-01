---
id: BL-4453
title: Fix CI failure Conversation_AnswersAsRecorded on Linux and macOS and Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Mcp.IntegrationTests, Curl.Mcp.UnitLibrary, Curl.Mcp]
requirement: none
created: 2026-11-01
completed:
---
# BL-4453 — Fix CI failure Conversation_AnswersAsRecorded on Linux and macOS and Windows

## Goal

`Conversation_AnswersAsRecorded` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `Conversation_AnswersAsRecorded` failed on Linux and macOS and Windows in CI run 41342005884 (https://github.com/StewartScottRogers/Curl/actions/runs/41342005884). First failing commit: 9d4102dd.

    Assertion failed. Expected condition to be true.

Failing data rows: `Conversation_AnswersAsRecorded ("tools-list")`, `Conversation_AnswersAsRecorded ("tools-call-version")`, `Conversation_AnswersAsRecorded ("request-before-initialize")`.

Lanes test only on Windows, so reproduce with `gh run view 41342005884 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `Conversation_AnswersAsRecorded` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-11-01: Created.
