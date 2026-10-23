---
id: BL-3203
title: Fix CI failure Parse_BlankArgument_IsNotRefusedAsBlankWhereCurlRefusesItForAnotherReason on Linux and macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitTests, Curl.Cli.UnitLibrary]
requirement: none
created: 2026-10-22
completed:
---
# BL-3203 — Fix CI failure Parse_BlankArgument_IsNotRefusedAsBlankWhereCurlRefusesItForAnotherReason on Linux and macOS

## Goal

`Parse_BlankArgument_IsNotRefusedAsBlankWhereCurlRefusesItForAnotherReason` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `Parse_BlankArgument_IsNotRefusedAsBlankWhereCurlRefusesItForAnotherReason` failed on Linux and macOS in CI run 39903809116 (https://github.com/StewartScottRogers/Curl/actions/runs/39903809116). First failing commit: 560a15da.

    Assertion failed. Expected collection to not contain the specified element.

Lanes test only on Windows, so reproduce with `gh run view 39903809116 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `Parse_BlankArgument_IsNotRefusedAsBlankWhereCurlRefusesItForAnotherReason` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: 315a2185.
