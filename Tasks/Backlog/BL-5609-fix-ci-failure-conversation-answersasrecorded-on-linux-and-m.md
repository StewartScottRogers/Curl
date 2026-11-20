---
id: BL-5609
title: Fix CI failure Conversation_AnswersAsRecorded on Linux and macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Mcp.IntegrationTests, Curl.Mcp.UnitLibrary, Curl.Mcp]
requirement: none
created: 2026-11-20
completed:
---
# BL-5609 — Fix CI failure Conversation_AnswersAsRecorded on Linux and macOS

## Goal

`Conversation_AnswersAsRecorded` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `Conversation_AnswersAsRecorded` failed on Linux and macOS in CI run 44773274042 (https://github.com/StewartScottRogers/Curl/actions/runs/44773274042). First failing commit: 4f0f8edc.

    Assertion failed. Expected condition to be true.

Failing data rows: `Conversation_AnswersAsRecorded ("tools-list")`, `Conversation_AnswersAsRecorded ("request-before-initialize")`.

Lanes test only on Windows, so reproduce with `gh run view 44773274042 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `Conversation_AnswersAsRecorded` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-11-20: Created.
