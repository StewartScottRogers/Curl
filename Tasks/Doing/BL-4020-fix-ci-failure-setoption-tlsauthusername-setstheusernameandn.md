---
id: BL-4020
title: Fix CI failure SetOption_TlsauthUsername_SetsTheUserNameAndNullClearsIt on Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.IntegrationTests, Curl.Cli.UnitLibrary]
requirement: none
created: 2026-10-28
completed:
---
# BL-4020 — Fix CI failure SetOption_TlsauthUsername_SetsTheUserNameAndNullClearsIt on Windows

## Goal

`SetOption_TlsauthUsername_SetsTheUserNameAndNullClearsIt` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `SetOption_TlsauthUsername_SetsTheUserNameAndNullClearsIt` failed on Windows in CI run 40803338913 (https://github.com/StewartScottRogers/Curl/actions/runs/40803338913). First failing commit: 62ebfe30.

    Assertion failed. Expected values to be equal.

Lanes test only on Windows, so reproduce with `gh run view 40803338913 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `SetOption_TlsauthUsername_SetsTheUserNameAndNullClearsIt` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 1650aa5f.
