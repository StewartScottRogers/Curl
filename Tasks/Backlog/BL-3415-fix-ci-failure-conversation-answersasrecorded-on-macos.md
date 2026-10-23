---
id: BL-3415
title: Fix CI failure Conversation_AnswersAsRecorded on macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Mcp.IntegrationTests, Curl.Mcp.UnitLibrary, Curl.Mcp]
requirement: none
created: 2026-10-23
completed:
---
# BL-3415 — Fix CI failure Conversation_AnswersAsRecorded on macOS

## Goal

`Conversation_AnswersAsRecorded` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `Conversation_AnswersAsRecorded` failed on macOS in CI run 40021720442 (https://github.com/StewartScottRogers/Curl/actions/runs/40021720442). First failing commit: 2640c8ca.

    Assertion failed. Expected condition to be true.

Lanes test only on Windows, so reproduce with `gh run view 40021720442 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `Conversation_AnswersAsRecorded` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-10-23: Created.
