---
id: BL-176
title: Send HEAD for -I and fail with exit 22 for -f and --fail-with-body
priority: High
assignee: Claude
pipeline: protocol
depends-on: [BL-173]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-176 — Send HEAD for -I and fail with exit 22 for -f and --fail-with-body

## Goal

`NoBody` sends HEAD and writes only the header block; `Fail` and `FailWithBody` return exit 22 on a status of 400 or above as curl does.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item H8. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- Measured `curl -I`: `HEAD / HTTP/1.1\r\nHost: 127.0.0.1:18082\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n\r\n`.
- Measured `curl -f` on 404: exit 22 `curl: (22) The requested URL returned error: 404`, no body written. `--fail-with-body` writes the body then fails 22.
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [ ] `NoBody` sends HEAD byte-equal to the measured bytes and writes the header block, no body.
- [ ] `Fail` on 404 returns `CurlExitCode.HttpReturnedError` (22) `The requested URL returned error: 404` and writes no body; `FailWithBody` writes it and returns 22.
- [ ] Every status >= 400 fails; 401 with credentials is measured on curl 8.21.0 and pinned.
- [ ] `dotnet build Curl.Protocol.Http.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.Http`. Tests use the fakes in `Curl.Protocol.Http.UnitTests/Fakes` (added by BL-169), never a socket; every parser test also runs with 1-byte chunks.

## Notes

- Plan item: H8 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
