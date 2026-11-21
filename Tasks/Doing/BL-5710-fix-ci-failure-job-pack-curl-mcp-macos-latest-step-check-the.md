---
id: BL-5710
title: Fix CI failure job Pack Curl.Mcp (macos-latest) step Check the packages on macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [.github/workflows/ci.yml]
lane: no
requirement: none
created: 2026-11-21
completed:
---
# BL-5710 — Fix CI failure job Pack Curl.Mcp (macos-latest) step Check the packages on macOS

## Goal

`job Pack Curl.Mcp (macos-latest) step Check the packages` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `job Pack Curl.Mcp (macos-latest) step Check the packages` failed on macOS in CI run 44941565490 (https://github.com/StewartScottRogers/Curl/actions/runs/44941565490). First failing commit: c3c60a47.

    ##[error]Process completed with exit code 1.

Lanes test only on Windows, so reproduce with `gh run view 44941565490 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `job Pack Curl.Mcp (macos-latest) step Check the packages` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

Interactive only: the cause is in `.github/workflows/ci.yml` (guard file, lanes may not change it). BL-5707 prefixed the package IDs with Surly., so the "Check the packages" step fails on every OS: line 265 expects `^Curl.Mcp.d` and line 272 expects `Curl.Mcp.$env:RID.*`; the packages are now `Surly.Curl.Mcp.<rid>.*` (log: `Surly.Curl.Mcp.win-x64.0.1.0.nupkg`). Fix: change both patterns to `^Surly.Curl.Mcp.d` and `Surly.Curl.Mcp.$env:RID.*`, and the message text. BL-5711 is the same failure on ubuntu. Remove `lane: no` once ci.yml is fixed (or just close this task then).

## Log

- 2026-11-21: Created.
- 2026-11-21: Backlog -> Doing. Criteria at claim: 4090b3a3.
- 2026-11-21: Doing -> Backlog. Interactive only: fix is in guard file ci.yml (Surly. package prefix); lane: no
- 2026-11-21: Backlog -> Doing. Criteria at claim: 4090b3a3.
