---
id: BL-4019
title: Fix CI failure SetOption_ProxyTlsauthType_TakesSrpInAnyCaseOrNullAndRefusesAnythingElse on Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.slnx]
requirement: none
created: 2026-10-28
completed:
---
# BL-4019 — Fix CI failure SetOption_ProxyTlsauthType_TakesSrpInAnyCaseOrNullAndRefusesAnythingElse on Windows

## Goal

`SetOption_ProxyTlsauthType_TakesSrpInAnyCaseOrNullAndRefusesAnythingElse` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `SetOption_ProxyTlsauthType_TakesSrpInAnyCaseOrNullAndRefusesAnythingElse` failed on Windows in CI run 40803338913 (https://github.com/StewartScottRogers/Curl/actions/runs/40803338913). First failing commit: 62ebfe30.

    Assertion failed. Expected values to be equal.

Lanes test only on Windows, so reproduce with `gh run view 40803338913 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `SetOption_ProxyTlsauthType_TakesSrpInAnyCaseOrNullAndRefusesAnythingElse` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-10-28: Created.
