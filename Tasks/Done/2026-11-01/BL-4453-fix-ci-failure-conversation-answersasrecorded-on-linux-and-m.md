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
completed: 2026-11-01
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

- [x] `Conversation_AnswersAsRecorded` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- Cause: BL-4451 (2f4c7e0b) changed the parse_url, build_url, transfer and version tool descriptions and added `extendedProtocols` to the version tool's result, without re-recording the goldens `tools-list`, `request-before-initialize` (both list the tools) and `tools-call-version`.
- Fix: re-recorded those three with `CURL_MCP_RECORD_GOLDEN=1`. The other 25 re-recorded files differed only in key order (DeepEquals ignores it), so I restored them, which keeps the hand-written `tools-call-transfer-missing-url`.
- Decision: the test now normalises `extendedProtocols` to `<EXTENSION-SCHEMES>`, because the list grows with every extension that lands and would break this golden each time. `CurlMcpHostExtensionTests` in Curl.Mcp.UnitTests already pins the list against `ExtensionListing.Schemes`.
- Platform: the goldens hold no paths, OS text or platform fields (those were already `<PLATFORM>`), so the change is platform-neutral. The golden class (30 tests) and the fast tests pass on Windows; the CI criterion is ticked on that basis (lane rule 11).

## Log

- 2026-11-01: Created.
- 2026-11-01: Backlog -> Doing. Criteria at claim: e70e753a.
- 2026-11-01: Doing -> Done. Goldens re-recorded for BL-4451's tool changes; extendedProtocols normalised
