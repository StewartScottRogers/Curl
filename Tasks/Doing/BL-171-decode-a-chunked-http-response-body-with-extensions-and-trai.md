---
id: BL-171
title: Decode a chunked HTTP response body with extensions and trailers
priority: High
assignee: Claude
pipeline: protocol
depends-on: [BL-169]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-171 — Decode a chunked HTTP response body with extensions and trailers

## Goal

The Http library decodes `Transfer-Encoding: chunked` bodies, including chunk extensions and trailers, with curl's exits for malformed chunking.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item H3. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- Measured: a chunk size line starting `z` gives exit 56 `curl: (56) chunk hex-length char not a hex digit: 0x7a` (curl 8.21.0).
- Chunked wins over Content-Length when both are present (RFC 9112 6.3).
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [ ] Bad hex returns `CurlExitCode.RecvError` (56) with the measured message (the offending byte in `0x%02x`).
- [ ] An overflowing chunk size and a missing CRLF after chunk data are measured on curl 8.21.0 and pinned in tests with their exit and message.
- [ ] Chunk extensions are ignored, trailers are read and passed to header output as curl does (measured), and chunked plus Content-Length decodes as chunked.
- [ ] `dotnet build Curl.Protocol.Http.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.Http`. Tests use the fakes in `Curl.Protocol.Http.UnitTests/Fakes` (added by BL-169), never a socket; every parser test also runs with 1-byte chunks.

## Notes

- Plan item: H3 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
