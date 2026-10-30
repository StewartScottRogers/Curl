---
id: BL-4359
title: Fix CI failure job Pack Curl.Mcp (macos-latest) step Run actions/upload-artifact@v4 on macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.slnx]
requirement: none
created: 2026-10-30
completed:
---
# BL-4359 — Fix CI failure job Pack Curl.Mcp (macos-latest) step Run actions/upload-artifact@v4 on macOS

## Goal

`job Pack Curl.Mcp (macos-latest) step Run actions/upload-artifact@v4` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `job Pack Curl.Mcp (macos-latest) step Run actions/upload-artifact@v4` failed on macOS in CI run 41098931622 (https://github.com/StewartScottRogers/Curl/actions/runs/41098931622). First failing commit: 767c799e.

    if-no-files-found: error

Lanes test only on Windows, so reproduce with `gh run view 41098931622 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `job Pack Curl.Mcp (macos-latest) step Run actions/upload-artifact@v4` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-10-30: Created.
