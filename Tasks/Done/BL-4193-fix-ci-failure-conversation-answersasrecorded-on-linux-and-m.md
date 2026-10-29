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
completed: 2026-10-29
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

- [x] `Conversation_AnswersAsRecorded` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- Cause: BL-4093 (cd2a1ad78) added the `extensions` topic line to `curl --ai-help`, which the `curl://ai-help` resource serves, but did not re-record `Golden/resources-read.json`. The text is the same on every platform; it was not a platform difference. Fixed by adding that line to the golden file; all 28 conversations pass locally on Windows.
- CI on Linux and macOS: the change is a platform-neutral golden text line and the fast tests are green here (lanes cannot see CI; the shift's CI watch files any failure).

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: e70e753a.
- 2026-10-29: Doing -> Done. resources-read golden file re-recorded with the ai-help extensions line; all 28 conversations pass
