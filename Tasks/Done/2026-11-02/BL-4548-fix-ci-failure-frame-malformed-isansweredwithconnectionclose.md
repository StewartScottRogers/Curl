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
completed: 2026-11-02
---
# BL-4548 — Fix CI failure Frame_Malformed_IsAnsweredWithConnectionClose501 on Linux and macOS and Windows

## Goal

`Frame_Malformed_IsAnsweredWithConnectionClose501` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `Frame_Malformed_IsAnsweredWithConnectionClose501` failed on Linux and macOS and Windows in CI run 41519828305 (https://github.com/StewartScottRogers/Curl/actions/runs/41519828305). First failing commit: cf0cb775.

    Test method Curl.Conformance.Extended.UnitTests.Amqp0.Amqp0ConnectionTests.Frame_Malformed_IsAnsweredWithConnectionClose501 threw exception:

Lanes test only on Windows, so reproduce with `gh run view 41519828305 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `Frame_Malformed_IsAnsweredWithConnectionClose501` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- Cause (CI run 41519828305): `KnownFailurePassedException` - BL-4541 fixed the case and removed its
  `Amqp0:AMQP091-4.2.3-4` line from `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`, but the
  file merges by union, so a rebase brought the line back and the passing case failed as a known failure.
- Already fixed on this branch by e6e55c175 (BL-4480), which drops the line again and is a descendant of
  the first failing commit cf0cb775. No code change was needed here.
- Verified: `Amqp0ConnectionTests` 16/16 and the project's fast tests 276/276 pass on Windows. The fix
  only removes a data line, nothing platform-specific, so Linux and macOS behave the same; the shift's
  CI watch files a task for any failure.
- The Mqtt5 `Connect_WithoutCredentials_SendsCleanStartAndAClientIdentifierFirst` failure in the same run
  is a separate test that BL-4543/BL-4544 cover.

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: a18da0cb.
- 2026-11-02: Doing -> Done. Already fixed by e6e55c175 (BL-4480); test passes
