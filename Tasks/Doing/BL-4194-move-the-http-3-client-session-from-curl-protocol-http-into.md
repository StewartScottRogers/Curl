---
id: BL-4194
title: Move the HTTP/3 client session from Curl.Protocol.Http into Curl.Http3.UnitLibrary so extensions can open request streams
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary/Http3Session.cs, Curl.Protocol.Http.UnitLibrary/Http3StreamConnection.cs, Curl.Protocol.Http.UnitTests/Http3SessionTests.cs, Curl.Protocol.Http.UnitTests/Http3StreamConnectionTests.cs, Curl.Http3.UnitLibrary/Http3ClientSession.cs, Curl.Http3.UnitLibrary/Http3ClientStreamConnection.cs, Curl.Http3.UnitTests/Http3ClientSessionTests.cs, Curl.Http3.UnitTests/Http3ClientStreamConnectionTests.cs, Curl.Http3.UnitLibrary/Curl.Http3.UnitLibrary.csproj]
requirement: none
created: 2026-10-29
completed:
---
# BL-4194 — Move the HTTP/3 client session from Curl.Protocol.Http into Curl.Http3.UnitLibrary so extensions can open request streams

## Goal

The HTTP/3 client session (QUIC connection, control and QPACK streams, request streams) lives in `Curl.Http3.UnitLibrary`, so a caller outside `Curl.Protocol.Http` can open a request stream with the pseudo-headers it chooses (such as `:protocol` for RFC 9220 extended CONNECT) and read the peer's SETTINGS, with no change to what curl does.

## Context

Found by BL-4112's first pass: `Http3Session.cs` and `Http3StreamConnection.cs` are in `Curl.Protocol.Http.UnitLibrary`, which an extension may not reference (ADR-0999). `Curl.Http2.UnitLibrary` already holds `Http2Connection`, so HTTP/2 needs no such move. Move the tests with the code; keep the names that say what they do (`Http3ClientSession` is a suggestion).

## Acceptance criteria

- [ ] The HTTP/3 client session and stream connection are public types in `Curl.Http3.UnitLibrary`, with their unit tests in `Curl.Http3.UnitTests`, and `Curl.Protocol.Http.UnitLibrary` uses them.
- [ ] A caller can send a request stream's HEADERS with any pseudo-header list and read whether the peer's SETTINGS carried `SETTINGS_ENABLE_CONNECT_PROTOCOL` (0x08); unit tests.
- [ ] The changed libraries meet the four quality gates by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: d757f172 d98609d6 478f867b 3d836d5e.
