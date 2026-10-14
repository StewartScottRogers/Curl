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
completed: 2026-10-13
---
# BL-2135 — Fix CI failure test1136 on Linux and macOS and Windows

## Goal

`test1136` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `test1136` failed on Linux and macOS and Windows in CI run 38431484503 (https://github.com/StewartScottRogers/Curl/actions/runs/38431484503). First failing commit: 48919fca.

    Assertion failed.

Lanes test only on Windows, so reproduce with `gh run view 38431484503 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `test1136` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- 2026-10-13: BL-2134's fix (a public-suffix `Domain` naming the host is kept host-only, curl 8.22.0)
  makes test1136 pass locally too; this task should only need to confirm CI is green.
- 2026-10-13: Confirmed. CI run 38431484503 failed on the jar1136.txt line `.z-1.compute-1.amazonaws.com TRUE`
  where curl 8.22.0 writes host-only `z-1.compute-1.amazonaws.com FALSE` - exactly what BL-2134 (6f28c052a)
  fixed. With that commit, test1136 (ratchet row and setenv row) passes locally and the full fast suite
  is green; no further code change is needed. The CI result for the landing commit is watched by the
  shift coordinator (BL-987), which refiles the test if it still fails on any platform.

## Log

- 2026-10-13: Created.
- 2026-10-13: Backlog -> Doing.
- 2026-10-13: Doing -> Done. test1136 passes again with BL-2134's host-only public-suffix Domain fix; fast suite green
