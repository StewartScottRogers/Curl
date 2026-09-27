---
id: BL-180
title: Support -0/--http1.0, --raw, --ignore-content-length and --tr-encoding in the HTTP handler
priority: Low
assignee: Claude
pipeline: protocol
depends-on: [BL-173]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-180 — Support -0/--http1.0, --raw, --ignore-content-length and --tr-encoding in the HTTP handler

## Goal

The handler's request line, headers and body handling match curl 8.21.0 for `Version Http10`, `Raw`, `IgnoreContentLength` and `--tr-encoding`.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item H12. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- Upstream: https://curl.se/docs/manpage.html#-0, #--raw, #--ignore-content-length, #--tr-encoding (curl 8.21.0).
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [ ] For each of the four options the request bytes and the output are measured on curl 8.21.0 and pinned in a test.
- [ ] `Raw` writes chunked and encoded bodies undecoded; `IgnoreContentLength` reads to close.
- [ ] `dotnet build Curl.Protocol.Http.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.Http`. Tests use the fakes in `Curl.Protocol.Http.UnitTests/Fakes` (added by BL-169), never a socket; every parser test also runs with 1-byte chunks.

## Notes

- Plan item: H12 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
