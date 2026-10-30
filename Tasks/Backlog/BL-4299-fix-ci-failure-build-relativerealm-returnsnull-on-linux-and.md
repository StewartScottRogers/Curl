---
id: BL-4299
title: Fix CI failure Build_RelativeRealm_ReturnsNull on Linux and macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.OciRegistry.UnitTests, Curl.Extension.OciRegistry.UnitLibrary]
requirement: none
created: 2026-10-29
completed:
---
# BL-4299 — Fix CI failure Build_RelativeRealm_ReturnsNull on Linux and macOS

## Goal

`Build_RelativeRealm_ReturnsNull` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `Build_RelativeRealm_ReturnsNull` failed on Linux and macOS in CI run 40985475541 (https://github.com/StewartScottRogers/Curl/actions/runs/40985475541). First failing commit: 4c618d0f.

    Assertion failed. Expected value to be null.

Lanes test only on Windows, so reproduce with `gh run view 40985475541 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `Build_RelativeRealm_ReturnsNull` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-10-29: Created.
