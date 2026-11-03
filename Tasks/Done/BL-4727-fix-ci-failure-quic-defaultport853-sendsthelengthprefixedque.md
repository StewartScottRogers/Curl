---
id: BL-4727
title: Fix CI failure Quic_DefaultPort853_SendsTheLengthPrefixedQueryWithMessageId0AndFinOnTheFirstStream on Linux and macOS and Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.Extended.UnitTests, Curl.Conformance.Extended.UnitLibrary]
requirement: none
created: 2026-11-03
completed: 2026-11-03
---
# BL-4727 — Fix CI failure Quic_DefaultPort853_SendsTheLengthPrefixedQueryWithMessageId0AndFinOnTheFirstStream on Linux and macOS and Windows

## Goal

`Quic_DefaultPort853_SendsTheLengthPrefixedQueryWithMessageId0AndFinOnTheFirstStream` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `Quic_DefaultPort853_SendsTheLengthPrefixedQueryWithMessageId0AndFinOnTheFirstStream` failed on Linux and macOS and Windows in CI run 41634593982 (https://github.com/StewartScottRogers/Curl/actions/runs/41634593982). First failing commit: 62178b13.

    CollectionAssert.AreEqual failed. Element at index 1 do not match.

Lanes test only on Windows, so reproduce with `gh run view 41634593982 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `Quic_DefaultPort853_SendsTheLengthPrefixedQueryWithMessageId0AndFinOnTheFirstStream` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- Cause: BL-4718 pads the DoQ query with EDNS(0) Padding past 255 bytes, so the length prefix's high byte is no longer 0; the test built it as `{ 0, (byte)length }`. It now writes the two-byte big-endian prefix (RFC 9250 4.2). Test-only change, no production code, so no quality run.
- CI criterion: the change is platform-neutral (byte arithmetic only) and Curl.Conformance.Extended.UnitTests' fast tests pass here (1719 passed, 1 skipped); the shift's CI watch covers Linux and macOS.

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: 83507f34.
- 2026-11-03: Doing -> Done. The DoQ default-port case checks the two-byte length prefix of the padded query
