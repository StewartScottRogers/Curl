---
id: BL-169
title: Parse the HTTP/1.x status line and response headers from an IConnection
priority: High
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-169 — Parse the HTTP/1.x status line and response headers from an IConnection

## Goal

An internal response-head reader in `Curl.Protocol.Http.UnitLibrary` reads an HTTP/1.0 or 1.1 status line and headers from an `IConnection`, skipping 1xx responses, and fails with curl's exit codes and messages.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item H1. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- `Curl.Protocol.Http.UnitLibrary` is empty today (only its `CLAUDE.md`). This task also adds the test fakes every later H task uses, in `Curl.Protocol.Http.UnitTests/Fakes`: `ScriptedConnection` (asserts the exact request bytes, replays responses in chosen chunk sizes), `QueueConnector` (ordered `ConnectResult`s for redirect and auth retries) and a hand-written `FakeTimeProvider` (no Microsoft.Extensions.TimeProvider.Testing package).
- Measured failures (curl 8.21.0, loopback, 2026-09-26):
  - no status line: exit 1, `curl: (1) Received HTTP/0.9 when not allowed`
  - empty reply: exit 52, `curl: (52) Empty reply from server`
  - short body: exit 18, `curl: (18) end of response with 7 bytes missing`
  - bad chunk: exit 56, `curl: (56) chunk hex-length char not a hex digit: 0x7a`
  - header without colon: exit 8, `curl: (8) Header without colon`
  - `-L --max-redirs 0`: exit 47, `curl: (47) Maximum (0) redirects followed`
  - bad gzip: exit 61, `curl: (61) Error while processing content unencoding: incorrect header check`
  - `-C` against a server ignoring ranges: exit 33, `curl: (33) HTTP server does not seem to support byte ranges. Cannot resume.`
  - `-p -x` answered 407: exit 7, `curl: (7) CONNECT tunnel failed, response 407`
  - `-f` on 404: exit 22, `curl: (22) The requested URL returned error: 404`
- Boundary cases: CRLF split across reads, LF-only headers, obs-fold continuation lines, header block at and over curl's size limit (exit 100 `TooLarge`; measure the limit and message), 100 Continue before 200, peer close mid-headers (exit 56).
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [ ] `ScriptedConnection`, `QueueConnector` and `FakeTimeProvider` exist in `Curl.Protocol.Http.UnitTests/Fakes` with their own tests.
- [ ] HTTP/1.0 and 1.1 status lines parse; 1xx responses are skipped; LF-only and folded headers parse; tests name each case.
- [ ] No status line returns `CurlExitCode.UnsupportedProtocol` (1) `Received HTTP/0.9 when not allowed`; header without colon returns `WeirdServerReply` (8) `Header without colon`; empty reply returns `GotNothing` (52) `Empty reply from server`; close mid-headers returns `RecvError` (56) with the measured message; oversized headers return `TooLarge` (100) with the measured message.
- [ ] Every parser test also passes with 1-byte chunks.
- [ ] `dotnet build Curl.Protocol.Http.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.Http`. Tests use the fakes in `Curl.Protocol.Http.UnitTests/Fakes` (added by BL-169), never a socket; every parser test also runs with 1-byte chunks.

## Notes

- Plan item: H1 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.

## Log

- 2026-09-26: Created.
