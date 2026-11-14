---
id: BL-5167
title: Fix CI failure job Build and test (ubuntu-latest) step Fast tests on Linux
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.slnx]
requirement: none
created: 2026-11-13
completed:
---
# BL-5167 — Fix CI failure job Build and test (ubuntu-latest) step Fast tests on Linux

## Goal

`job Build and test (ubuntu-latest) step Fast tests` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `job Build and test (ubuntu-latest) step Fast tests` failed on Linux in CI run 43565364047 (https://github.com/StewartScottRogers/Curl/actions/runs/43565364047). First failing commit: 01e39905.

    Passed!  - Failed:     0, Passed:    48, Skipped:     0, Total:    48, Duration: 264 ms - Curl.Config.UnitTests.dll (net10.0)

Lanes test only on Windows, so reproduce with `gh run view 43565364047 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `job Build and test (ubuntu-latest) step Fast tests` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-11-13: Created.
