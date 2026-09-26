---
id: BL-181
title: Authenticate HTTP requests through IHttpAuthenticator, retrying a 401 once
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-173, BL-161]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-181 — Authenticate HTTP requests through IHttpAuthenticator, retrying a 401 once

## Goal

The handler asks `IHttpAuthenticator` for an Authorization value, sends Basic up front, and on a 401 challenge retries once on the same connection when it is keep-alive, otherwise on a new one.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item H13. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- Measured `-u u:p`: `Authorization: Basic dTpw` is sent immediately after `Host` and before `User-Agent`.
- Challenges are the `WWW-Authenticate` values; `QueueConnector` scripts the reconnect case.
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [ ] Basic is sent on the first request in the measured position.
- [ ] A 401 with a challenge retries once; keep-alive reuses the connection, `Connection: close` reconnects (tests for both).
- [ ] An authentication failure returns the exit code measured on curl 8.21.0 (for example `CurlExitCode.AuthError` 94 or the 401 as a success) with its message.
- [ ] `dotnet build Curl.Protocol.Http.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.Http`. Tests use the fakes in `Curl.Protocol.Http.UnitTests/Fakes` (added by BL-169), never a socket; every parser test also runs with 1-byte chunks.

## Notes

- Plan item: H13 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.

## Log

- 2026-09-26: Created.
