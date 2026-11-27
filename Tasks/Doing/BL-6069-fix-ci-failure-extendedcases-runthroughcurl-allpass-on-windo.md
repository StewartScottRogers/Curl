---
id: BL-6069
title: Fix CI failure ExtendedCases_RunThroughCurl_AllPass on Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests, Curl.Conformance.UnitLibrary]
requirement: none
created: 2026-11-26
completed:
---
# BL-6069 — Fix CI failure ExtendedCases_RunThroughCurl_AllPass on Windows

## Goal

`ExtendedCases_RunThroughCurl_AllPass` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `ExtendedCases_RunThroughCurl_AllPass` failed on Windows in CI run 45939128920 (https://github.com/StewartScottRogers/Curl/actions/runs/45939128920). First failing commit: 4bf0fe70.

    Assertion failed. Expected values to be equal.

Lanes test only on Windows, so reproduce with `gh run view 45939128920 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `ExtendedCases_RunThroughCurl_AllPass` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-11-26: Created.
- 2026-11-26: Backlog -> Doing. Criteria at claim: 35574f54.
