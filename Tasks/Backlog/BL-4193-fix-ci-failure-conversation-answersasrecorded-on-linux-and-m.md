---
id: BL-4193
title: Fix CI failure Conversation_AnswersAsRecorded on Linux and macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Mcp.IntegrationTests, Curl.Mcp.UnitLibrary, Curl.Mcp]
requirement: none
created: 2026-10-29
completed:
---
# BL-4193 — Fix CI failure Conversation_AnswersAsRecorded on Linux and macOS

## Goal

`Conversation_AnswersAsRecorded` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `Conversation_AnswersAsRecorded` failed on Linux and macOS in CI run 40917729875 (https://github.com/StewartScottRogers/Curl/actions/runs/40917729875). First failing commit: 8f42b7de.

    Assertion failed. Expected condition to be true.

Failing data rows: `Conversation_AnswersAsRecorded ("resources-read")`.

Lanes test only on Windows, so reproduce with `gh run view 40917729875 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `Conversation_AnswersAsRecorded` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-10-29: Created.
