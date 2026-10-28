---
id: BL-3983
title: Fix CI failure Parse_LocationHostOfUtf8Bytes_ReadsTheBytesAsUtf8OffWindows on Linux and macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitTests, Curl.Protocol.Abstractions.UnitLibrary]
requirement: none
created: 2026-10-28
completed:
---
# BL-3983 — Fix CI failure Parse_LocationHostOfUtf8Bytes_ReadsTheBytesAsUtf8OffWindows on Linux and macOS

## Goal

`Parse_LocationHostOfUtf8Bytes_ReadsTheBytesAsUtf8OffWindows` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `Parse_LocationHostOfUtf8Bytes_ReadsTheBytesAsUtf8OffWindows` failed on Linux and macOS in CI run 40755947165 (https://github.com/StewartScottRogers/Curl/actions/runs/40755947165). First failing commit: 48ba6ffd.

    Assertion failed. Expected strings to be equal.

Lanes test only on Windows, so reproduce with `gh run view 40755947165 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `Parse_LocationHostOfUtf8Bytes_ReadsTheBytesAsUtf8OffWindows` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-10-28: Created.
