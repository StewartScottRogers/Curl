---
id: BL-2135
title: Fix CI failure test1136 on Linux and macOS and Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests, Curl.Conformance.UnitLibrary]
requirement: none
created: 2026-10-13
completed:
---
# BL-2135 — Fix CI failure test1136 on Linux and macOS and Windows

## Goal

`test1136` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `test1136` failed on Linux and macOS and Windows in CI run 38431484503 (https://github.com/StewartScottRogers/Curl/actions/runs/38431484503). First failing commit: 48919fca.

    Assertion failed.

Lanes test only on Windows, so reproduce with `gh run view 38431484503 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `test1136` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- 2026-10-13: BL-2134's fix (a public-suffix `Domain` naming the host is kept host-only, curl 8.22.0)
  makes test1136 pass locally too; this task should only need to confirm CI is green.

## Log

- 2026-10-13: Created.
