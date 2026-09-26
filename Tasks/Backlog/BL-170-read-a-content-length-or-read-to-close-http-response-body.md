---
id: BL-170
title: Read a Content-Length or read-to-close HTTP response body
priority: High
assignee: Claude
pipeline: protocol
depends-on: [BL-169]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-170 — Read a Content-Length or read-to-close HTTP response body

## Goal

The Http library reads a response body framed by Content-Length or by connection close, writes it to the output stream, and returns curl's exits for a short body and a failed write.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item H2. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- Measured: a body 7 bytes short of Content-Length gives exit 18 `curl: (18) end of response with 7 bytes missing` (curl 8.21.0).
- HEAD, 204 and 304 have no body even with Content-Length. `OutputWriteFailedException` (Abstractions) carries the accepted byte count of a failed write (BL-114); follow how the file and telnet handlers report it.
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [ ] A short body returns `CurlExitCode.PartialFile` (18) `end of response with N bytes missing` with N computed.
- [ ] HEAD, 204 and 304 read no body; Content-Length 0 reads nothing; tests for each.
- [ ] A failed output write returns `WriteError` (23) with the measured message and the bytes that reached the output.
- [ ] Peer close mid-body under Content-Length returns 18; read-to-close ends cleanly at close.
- [ ] `dotnet build Curl.Protocol.Http.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.Http`. Tests use the fakes in `Curl.Protocol.Http.UnitTests/Fakes` (added by BL-169), never a socket; every parser test also runs with 1-byte chunks.

## Notes

- Plan item: H2 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.

## Log

- 2026-09-26: Created.
