---
id: BL-1218
title: Bring Http2FrameTrace.Describe under cyclomatic complexity 10
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-02
completed: 2026-10-02
---
# BL-1218 — Bring Http2FrameTrace.Describe under cyclomatic complexity 10

## Goal

`Http2FrameTrace.Describe` measures at most 10 cyclomatic complexity, so `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary` reports no failing member.

## Context

- Found in BL-1215: `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary` reports `Http2FrameTrace.Describe(Curl.Http2.Http2Frame)` (Http2FrameTrace.cs:72, added by BL-1167) failing on complexity 12, though the build's CA1502 does not flag its switch expression. Extract the per-type descriptions into helpers; the output must not change.

## Acceptance criteria

- [x] `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary` reports 0 failing members, with 100% line and branch coverage.
- [x] Every existing `Http2FrameTrace` test passes unchanged.

## Notes

- Split `Describe` into the five frame types the request side sends (DATA, HEADERS, PRIORITY, RST_STREAM, SETTINGS) and `DescribeOtherFrame` for PUSH_PROMISE, PING, GOAWAY, WINDOW_UPDATE and unknown types; the SETTINGS ack ternary moved to `DescribeSettings`. Format strings are unchanged byte for byte.
- `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary`: 100% line, 100% branch, 0 failing members. Curl.Protocol.Http.UnitTests 1722 passed, 4 skipped, unchanged tests.

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. Http2FrameTrace.Describe is split under complexity 10; the HTTP library measures 0 failing members at 100% coverage
