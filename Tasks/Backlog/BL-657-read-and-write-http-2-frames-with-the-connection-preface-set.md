---
id: BL-657
title: Read and write HTTP/2 frames with the connection preface, SETTINGS and flow control
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-655, BL-715]
touches: [Curl.Http2.UnitLibrary, Curl.Http2.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-657 — Read and write HTTP/2 frames with the connection preface, SETTINGS and flow control

## Goal

An HTTP/2 connection layer in `Curl.Http2.UnitLibrary`, over a byte stream the caller hands in (in production the `IConnection`'s stream, passed by the HTTP handler), sends the client preface and the SETTINGS (and initial WINDOW_UPDATE) curl 8.21.0's library sends, reads and writes every frame type of RFC 9113, acknowledges SETTINGS and PING, tracks connection and stream flow-control windows, and turns GOAWAY, RST_STREAM and protocol errors into typed failures.

## Context

- Conformance audit 2026-09-28, row 32. Standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28): HTTP/2 is offered on every platform; the hand-built frame layer lives in `Curl.Http2.UnitLibrary` (BL-715) with HPACK (BL-656), and allocates stream IDs so several streams can share the connection (BL-717 multiplexes on it).
- The preface and SETTINGS values curl sends are measured in BL-655 (record them there if missing: `Record-CurlExchange.ps1` with `--http2-prior-knowledge` on an OpenSSL build captures the preface bytes in `request.bin`).

## Acceptance criteria

- [ ] `Curl.Http2.UnitTests` pin the preface and SETTINGS bytes as measured, and frame round trips and error handling for each frame type through an in-memory stream, including frames split across reads and flow-control exhaustion.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Http2.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
