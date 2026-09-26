---
id: BL-177
title: Decode --compressed HTTP bodies with gzip, deflate and br
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-173, BL-154]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-177 — Decode --compressed HTTP bodies with gzip, deflate and br

## Goal

With `Compressed` set the handler sends the BL-154 ADR's Accept-Encoding and decodes gzip, deflate (zlib and raw) and br, including stacked encodings.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item H9. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- BL-154 ADR: `Accept-Encoding: deflate, gzip, br` (no zstd until a hand-written decoder exists). BCL: `GZipStream`, `ZLibStream`, `DeflateStream`, `BrotliStream`.
- Measured: bad gzip gives exit 61 `curl: (61) Error while processing content unencoding: incorrect header check`.
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [ ] The request carries `Accept-Encoding: deflate, gzip, br` exactly when `Compressed` is set.
- [ ] gzip, zlib-wrapped deflate, raw deflate, br and a stacked `gzip, br` encoding each decode in a test.
- [ ] Corrupt gzip returns `CurlExitCode.BadContentEncoding` (61) with the measured message.
- [ ] Without `Compressed`, an encoded body is written untouched.
- [ ] `dotnet build Curl.Protocol.Http.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.Http`. Tests use the fakes in `Curl.Protocol.Http.UnitTests/Fakes` (added by BL-169), never a socket; every parser test also runs with 1-byte chunks.

## Notes

- Plan item: H9 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.

## Log

- 2026-09-26: Created.
