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
completed: 2026-09-26
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

- [x] `ScriptedConnection`, `QueueConnector` and `FakeTimeProvider` exist in `Curl.Protocol.Http.UnitTests/Fakes` with their own tests.
- [x] HTTP/1.0 and 1.1 status lines parse; 1xx responses are skipped; LF-only and folded headers parse; tests name each case.
- [x] No status line returns `CurlExitCode.UnsupportedProtocol` (1) `Received HTTP/0.9 when not allowed`; header without colon returns `WeirdServerReply` (8) `Header without colon`; empty reply returns `GotNothing` (52) `Empty reply from server`; close mid-headers returns `RecvError` (56) with the measured message; oversized headers return `TooLarge` (100) with the measured message.
- [x] Every parser test also passes with 1-byte chunks.
- [x] `dotnet build Curl.Protocol.Http.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.Http`. Tests use the fakes in `Curl.Protocol.Http.UnitTests/Fakes` (added by BL-169), never a socket; every parser test also runs with 1-byte chunks.

## Notes

- Plan item: H1 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.
- Delivered: `HttpResponseHeadReader` (internal) over `HttpLineReader`, `HttpLine`, `HttpStatusLine` and `HttpResponseHeadBuilder`; failures throw `HttpTransferException(CurlExitCode, message)` for the future handler to turn into a `TransferResult`. The head keeps `HeadBytes` (every head, 1xx included, folded, as `-D` writes them; length = `%{size_header}`) and `BodyPrefix` (bytes read past the head). Fakes: `ScriptedConnection`, `QueueConnector`, `FakeTimeProvider` (clock + timestamp only; no timers yet, add them when a timeout task needs them).
- Measured 2026-09-26 with `/mingw64/bin/curl` 8.21.0 (Schannel): a Python loopback server read the request, sent the bytes, shut down its write side and closed; command `curl -sS -o body.out -D hd.out -w "code=%{http_code} hdr=%{size_header}" http://127.0.0.1:PORT/`. BL-165's `Record-CurlExchange.ps1` does not exist yet. Results (response bytes -> outcome):
  - `hello world\r\n\r\n`, `ICY 200 OK`, ` HTTP/1.1 404 X` -> exit 1 `Received HTTP/0.9 when not allowed`.
  - empty; `HT`; `HTTP/1.1 20`; `HTTP/1.1 100 Continue\r\n\r\n` then close; `101`/`199` then close; `HTTP/1.1 1000 X` (read as 100); close inside a 1xx head -> exit 52 `Empty reply from server`.
  - `HTTP/1.1 abc OK`, `HTTP/1.1 20 OK`, `HTTP/1.2 200 OK`, `HTTP/1.1  200 OK` -> exit 1 `Unsupported HTTP/1 subversion in response`; `HTTP/9 500` -> exit 1 `Unsupported HTTP version in response`; `HTTP/1.0 099 X` -> exit 1 `Unsupported response code in HTTP response`.
  - `http/1.1 404 x`, `HTTP/2 404`, `HTTP/3.0 200 OK` -> exit 0, code 200, version 1.0. `HTTP/1.1 200`, `HTTP/1.1 200OK`, `HTTP/1.1\t404 X`, `HTTP/1.1 999 X` accepted.
  - `BadHeader` line (final or 1xx head), or a continuation line straight after the status line -> exit 8 `Header without colon`. Bare CR inside a status or header line -> exit 8 `Carriage return found in header`. `: x` and `X-A : 1` accepted.
  - Folding: `X-A: 1  \r\n   b  \r\n \t c\r\n` -> `-D` wrote `X-A: 1 b c\r\n`, size_header 50 (previous line loses trailing blanks, one space, continuation loses leading blanks); LF-only fold gave `X-A: 1 b\n`; blank continuation gave `X-A: 1 \r\n`.
  - Close (FIN) after the status line or among the final headers -> exit 0, code 200, partial header line dropped (size_header 17 / 25 / 26). Reset (RST, SO_LINGER 0) inside the headers -> exit 56 `Recv failure: Connection was reset`.
  - One header line of 102399 bytes with CRLF accepted, 102400 -> exit 100 `A value or data field grew larger than allowed`; the same limit applies to a header after folding (9308 x ` bbbbbbbbbb` + ` bb` accepted with size_header 102418, + ` bbb` -> exit 100).
  - Heads totalling 307200 bytes accepted; 307201 at a header line -> exit 56 `Too large response headers: 307201 > 307200`; passing it with the final empty line -> `307202 > 307200`; with a 25-byte `100 Continue` head first -> `307223 > 307200` (1xx heads count).
- Decision (Claude, under Stewart's delegation, standing rule "measure real curl before pinning output text"): two acceptance criteria assumed outcomes that measurement contradicted, so the code follows curl 8.21.0: (1) a peer that closes mid-headers is not exit 56 - a clean close ends the head and the response succeeds; exit 56 with `Recv failure: Connection was reset` is what a reset gives, and that is what is pinned; (2) "oversized headers" are two limits - one line or folded header of 102400 bytes is exit 100 `TooLarge`, and heads over 307200 bytes in total are exit 56 with the message above. The criteria were ticked against these measured outcomes.
- Not measured, so chosen: an `IOException` whose inner `SocketException` is not ConnectionReset reports curl's generic exit 56 text `Failure when receiving data from the peer` (as Gopher and MQTT do); a status line `HTTP/` with nothing after it is treated as an unsupported version; the `%{size_header}` of a close right after a lone CR is not pinned (curl counted 26 for `...X-A: 1\r\n\r` and this reader counts 25).
- Verification: `dotnet build Curl.Protocol.Http.UnitLibrary -warnaserror` clean; `dotnet test --filter "TestCategory!=Integration"` all green (Curl.Protocol.Http.UnitTests 103 passed); `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary`: 100% line, 100% branch, 0 failing members, worst CRAP 10. Every reader test runs with 1-, 7- and 65536-byte reads.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. HttpResponseHeadReader reads HTTP/1.0/1.1 heads from an IConnection, skipping 1xx, unfolding headers, with curl 8.21.0's measured exit codes and messages
