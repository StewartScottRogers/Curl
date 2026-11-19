---
id: BL-5535
title: Stamp the UDP channel's open time with the transfer's TimeProvider in EndPointRecordingDatagramConnector
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Engine.UnitLibrary/EndPointRecordingDatagramConnector.cs, Curl.Engine.UnitLibrary/EngineComposition.cs, Curl.Engine.UnitLibrary/EndPointReportingProtocolHandler.cs]
requirement: none
created: 2026-11-18
completed:
---
# BL-5535 — Stamp the UDP channel's open time with the transfer's TimeProvider in EndPointRecordingDatagramConnector

## Goal

The UDP channel's open time that `%{time_connect}` and `%{time_appconnect}` use for extension schemes is stamped on the same clock as the transfer's start and end.

## Context

Found by the shift-end review of 2026-11-18 (code-reviewer, ADR-0710), diff 2a473a41ba..43a2df5f77. CLAUDE.md: inject TimeProvider for anything time-dependent.
`Curl.Engine.UnitLibrary/EndPointRecordingDatagramConnector.cs:16,32` takes an optional `timeProvider` and defaults to TimeProvider.System, and `EngineComposition.cs:177` never passes one. `EndPointReportingProtocolHandler.ConnectTimingsOf` then mixes that System timestamp with `started` and `completed` from `context.TimeProvider`, and Math.Clamp hides the mismatch. Under any injected clock (Curl.Api hosts, a manual time provider in tests), the open time is clamped to the transfer's start or end, so the timings are wrong.

## Acceptance criteria

- [ ] The open time is taken from the transfer's own TimeProvider (`context.TimeProvider`, or one passed in by composition). EndPointRecordingDatagramConnector no longer falls back to TimeProvider.System on its own.
- [ ] A unit test with a manual TimeProvider advanced between start, channel open and end shows %{time_connect} equals the open offset, not a clamped value.
- [ ] `dotnet build` is clean, the fast tests pass, and Curl.Engine.UnitLibrary keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-18: Created.
