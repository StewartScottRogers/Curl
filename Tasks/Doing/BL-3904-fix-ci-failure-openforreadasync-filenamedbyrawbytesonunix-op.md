---
id: BL-3904
title: Fix CI failure OpenForReadAsync_FileNamedByRawBytesOnUnix_OpensThoseBytes on macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Core.UnitTests, Curl.Core.UnitLibrary]
requirement: none
created: 2026-10-26
completed:
---
# BL-3904 — Fix CI failure OpenForReadAsync_FileNamedByRawBytesOnUnix_OpensThoseBytes on macOS

## Goal

`OpenForReadAsync_FileNamedByRawBytesOnUnix_OpensThoseBytes` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `OpenForReadAsync_FileNamedByRawBytesOnUnix_OpensThoseBytes` failed on macOS in CI run 40428708184 (https://github.com/StewartScottRogers/Curl/actions/runs/40428708184). First failing commit: 1928e3e7.

    Assertion failed. Expected value to be greater than or equal to the lower bound.

Lanes test only on Windows, so reproduce with `gh run view 40428708184 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `OpenForReadAsync_FileNamedByRawBytesOnUnix_OpensThoseBytes` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-10-26: Created.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 29a3025a.
