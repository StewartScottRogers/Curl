---
id: BL-4548
title: Fix CI failure Frame_Malformed_IsAnsweredWithConnectionClose501 on Linux and macOS and Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.Extended.UnitTests, Curl.Conformance.Extended.UnitLibrary]
requirement: none
created: 2026-11-02
completed:
---
# BL-4548 — Fix CI failure Frame_Malformed_IsAnsweredWithConnectionClose501 on Linux and macOS and Windows

## Goal

`Frame_Malformed_IsAnsweredWithConnectionClose501` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `Frame_Malformed_IsAnsweredWithConnectionClose501` failed on Linux and macOS and Windows in CI run 41519828305 (https://github.com/StewartScottRogers/Curl/actions/runs/41519828305). First failing commit: cf0cb775.

    Test method Curl.Conformance.Extended.UnitTests.Amqp0.Amqp0ConnectionTests.Frame_Malformed_IsAnsweredWithConnectionClose501 threw exception:

Lanes test only on Windows, so reproduce with `gh run view 41519828305 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `Frame_Malformed_IsAnsweredWithConnectionClose501` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-11-02: Created.
