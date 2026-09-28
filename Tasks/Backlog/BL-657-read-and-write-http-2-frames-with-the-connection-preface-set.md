---
id: BL-657
title: Read and write HTTP/2 frames with the connection preface, SETTINGS and flow control
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-655]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-657 — Read and write HTTP/2 frames with the connection preface, SETTINGS and flow control

## Goal

An HTTP/2 connection layer over `IConnection` sends the client preface and the SETTINGS (and initial WINDOW_UPDATE) curl 8.21.0's library sends, reads and writes every frame type of RFC 9113, acknowledges SETTINGS and PING, tracks connection and stream flow-control windows, and turns GOAWAY, RST_STREAM and protocol errors into typed failures.

## Context

- Conformance audit 2026-09-28, row 32. If BL-655's ADR decides not to offer HTTP/2, move this task to `Deferred` with that reason.
- The preface and SETTINGS values curl sends are measured in BL-655 (record them there if missing: `Record-CurlExchange.ps1` with `--http2-prior-knowledge` on an OpenSSL build captures the preface bytes in `request.bin`).

## Acceptance criteria

- [ ] `Curl.Protocol.Http.UnitTests` pin the preface and SETTINGS bytes as measured, and frame round trips and error handling for each frame type through a fake connection, including frames split across reads and flow-control exhaustion.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
