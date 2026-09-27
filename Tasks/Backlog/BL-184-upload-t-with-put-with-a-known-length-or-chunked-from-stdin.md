---
id: BL-184
title: Upload -T with PUT, with a known length or chunked from stdin
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-175, BL-030]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-184 — Upload -T with PUT, with a known length or chunked from stdin

## Goal

An upload source on `ITransferContext.Upload` is sent as PUT with Content-Length when the length is known and `Transfer-Encoding: chunked` when it is not.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item H16. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- BL-030 wires the `-T` URL into the dispatcher.
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [ ] A file upload sends PUT with Content-Length byte-equal to curl 8.21.0 (measured).
- [ ] A non-seekable (stdin) upload sends `Transfer-Encoding: chunked` byte-equal to curl (measured).
- [ ] A read failure returns `CurlExitCode.ReadError` (26) with the measured message.
- [ ] `dotnet build Curl.Protocol.Http.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.Http`. Tests use the fakes in `Curl.Protocol.Http.UnitTests/Fakes` (added by BL-169), never a socket; every parser test also runs with 1-byte chunks.

## Notes

- Plan item: H16 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.

## Log

- 2026-09-26: Created.
