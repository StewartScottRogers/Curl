---
id: BL-175
title: Send HTTP request bodies from BytesBody and StreamBody with curl's Expect: 100-continue
priority: High
assignee: Claude
pipeline: protocol
depends-on: [BL-173]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-175 — Send HTTP request bodies from BytesBody and StreamBody with curl's Expect: 100-continue

## Goal

The handler sends `BytesBody` and `StreamBody` request bodies with Content-Length and Content-Type as curl does, and handles `Expect: 100-continue` with curl's threshold and wait.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item H7. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- Measured `curl -d x=1 http://127.0.0.1:18081/`: `POST / HTTP/1.1\r\nHost: 127.0.0.1:18081\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\nContent-Length: 3\r\nContent-Type: application/x-www-form-urlencoded\r\n\r\nx=1`.
- `--json` sets its own Content-Type and Accept (measure). curl sends `Expect: 100-continue` above a size threshold and waits a fixed time for 100 (measure both on curl 8.21.0).
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [ ] `-d x=1` equivalent (`BytesBody`, form content type) is byte-equal to the measured POST.
- [ ] A `--json` body sends the measured Content-Type and Accept headers.
- [ ] The Expect threshold and wait are measured and pinned; the wait runs on `FakeTimeProvider`.
- [ ] A failed `StreamBody` read returns `CurlExitCode.ReadError` (26) with the measured message.
- [ ] `dotnet build Curl.Protocol.Http.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.Http`. Tests use the fakes in `Curl.Protocol.Http.UnitTests/Fakes` (added by BL-169), never a socket; every parser test also runs with 1-byte chunks.

## Notes

- Plan item: H7 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
