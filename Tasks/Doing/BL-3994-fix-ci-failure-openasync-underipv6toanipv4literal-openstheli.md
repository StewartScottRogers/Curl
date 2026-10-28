---
id: BL-3994
title: Fix CI failure OpenAsync_UnderIPv6ToAnIPv4Literal_OpensTheLiteral on Linux and macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitTests, Curl.Networking.UnitLibrary]
requirement: none
created: 2026-10-28
completed:
---
# BL-3994 — Fix CI failure OpenAsync_UnderIPv6ToAnIPv4Literal_OpensTheLiteral on Linux and macOS

## Goal

`OpenAsync_UnderIPv6ToAnIPv4Literal_OpensTheLiteral` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `OpenAsync_UnderIPv6ToAnIPv4Literal_OpensTheLiteral` failed on Linux and macOS in CI run 40774593052 (https://github.com/StewartScottRogers/Curl/actions/runs/40774593052). First failing commit: 81764fdf.

    Assertion failed. Expected value to not be null.

Lanes test only on Windows, so reproduce with `gh run view 40774593052 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `OpenAsync_UnderIPv6ToAnIPv4Literal_OpensTheLiteral` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 2a4d83b7.
