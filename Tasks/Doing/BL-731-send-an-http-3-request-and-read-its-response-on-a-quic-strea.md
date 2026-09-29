---
id: BL-731
title: Send an HTTP/3 request and read its response on a QUIC stream
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-730, BL-721, BL-658, BL-668]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-731 — Send an HTTP/3 request and read its response on a QUIC stream

## Goal

When a transfer runs over HTTP/3, the HTTP handler sends the request as a QPACK HEADERS frame (pseudo-headers and header order as curl 8.21.0 sends them) and DATA frames on a new bidirectional QUIC stream, reads the response's HEADERS, DATA and trailers, and feeds the same output, `-i`/`-D`, `-f`, redirect, auth, cookie and progress paths as HTTP/1.1 and HTTP/2, with stream and connection errors mapped to exit 95 `CURLE_HTTP3` as BL-718's ADR states.

## Context

- Standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28). Builds on BL-730 (frames and QPACK), BL-721 (the multiplexed-connection contract the handler receives) and BL-658 (the HTTP/2 path, whose request/response plumbing this reuses). `Curl.Protocol.Http.UnitLibrary` gains a reference to `Curl.Http3.UnitLibrary` (allowed by BL-668); amend `Curl.Protocol.Http.UnitLibrary/CLAUDE.md` to name it.
- The handler never touches QUIC or UDP: it receives the multiplexed connection from the connector. Tests use a fake multiplexed connection replaying stream bytes.

## Acceptance criteria

- [ ] `Curl.Protocol.Http.UnitTests` pin the HEADERS field section for a GET, a POST with `-d` and custom `-H` headers, and the output for a response with a body, with trailers, and with a stream reset mid-body (exit 95), through the fake multiplexed connection.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
