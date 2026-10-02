---
id: BL-1158
title: Fix CI failure TryConnect_OnMacOS_ConnectsThroughConnectxAndCarriesTheFirstBytes on macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitTests, Curl.Networking.UnitLibrary]
requirement: none
created: 2026-10-02
completed:
---
# BL-1158 — Fix CI failure TryConnect_OnMacOS_ConnectsThroughConnectxAndCarriesTheFirstBytes on macOS

## Goal

`TryConnect_OnMacOS_ConnectsThroughConnectxAndCarriesTheFirstBytes` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `TryConnect_OnMacOS_ConnectsThroughConnectxAndCarriesTheFirstBytes` failed on macOS in CI run 37004026335 (https://github.com/StewartScottRogers/Curl/actions/runs/37004026335). First failing commit: 92d5ec3e.

    Assertion failed. Expected condition to be true.

Lanes test only on Windows, so reproduce with `gh run view 37004026335 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `TryConnect_OnMacOS_ConnectsThroughConnectxAndCarriesTheFirstBytes` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
