---
id: BL-730
title: Read and write HTTP/3 frames, control streams and QPACK streams
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-729]
touches: [Curl.Http3.UnitLibrary, Curl.Http3.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-730 — Read and write HTTP/3 frames, control streams and QPACK streams

## Goal

`Curl.Http3.UnitLibrary` reads and writes HTTP/3 frames (DATA, HEADERS, CANCEL_PUSH, SETTINGS, PUSH_PROMISE, GOAWAY, MAX_PUSH_ID, and reserved/grease types skipped), opens the client's control stream with the SETTINGS curl's build sends and its QPACK encoder and decoder streams, reads the server's control and QPACK streams, and turns protocol errors into the RFC 9114 error codes.

## Context

- Standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28). Builds on BL-729. The client's SETTINGS values come from BL-718's ADR (measured from curl's official build). Streams are byte streams handed in by the caller (the QUIC streams arrive through BL-721's contracts in `Curl.Protocol.Http.UnitLibrary`), so this library references no QUIC code.
- References: RFC 9114 sections 6 (stream mapping, unidirectional stream types), 7 (frames), 8 (error codes), 9 (extensions and grease).

## Acceptance criteria

- [ ] `Curl.Http3.UnitTests` pin the client control stream's first bytes (stream type and SETTINGS) as measured, round-trip every frame type, skip grease frames and settings, and raise `H3_FRAME_UNEXPECTED`, `H3_MISSING_SETTINGS`, `H3_CLOSED_CRITICAL_STREAM` and `H3_FRAME_ERROR` for the RFC's cases.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Http3.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
