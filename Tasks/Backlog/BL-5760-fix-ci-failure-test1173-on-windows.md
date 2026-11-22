---
id: BL-5760
title: Fix CI failure test1173: on Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests, Curl.Conformance.UnitLibrary]
requirement: none
created: 2026-11-22
completed:
---
# BL-5760 — Fix CI failure test1173: on Windows

## Goal

`test1173:` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `test1173:` failed on Windows in CI run 45065328172 (https://github.com/StewartScottRogers/Curl/actions/runs/45065328172). First failing commit: ad04ddb5.

    Assertion failed.

Lanes test only on Windows, so reproduce with `gh run view 45065328172 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `test1173:` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-11-22: Created.
