---
id: BL-1662
title: Refuse a four-digit HTTP status code with exit 8 Invalid status line as curl does
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1662 — Refuse a four-digit HTTP status code with exit 8 Invalid status line as curl does

## Goal

A response whose status line is `HTTP/1.1 1000 X` (a status code of four or more digits) fails with exit 8 and the error `Invalid status line`, as curl 8.21.0 does, instead of Curl's current exit 1 `Received HTTP/0.9 when not allowed`.

## Context

- Found by BL-1510's adversarial attack on `HttpProtocolHandler`. Measured 2026-10-07 with `Record-CurlExchange.ps1 -Response 'HTTP/1.1 1000 X\r\nContent-Length: 2\r\n\r\nok' -CurlArgs '-sS','-o',<file>,<url>`: curl 8.21.0 (Schannel) exits 8, stderr `curl: (8) Invalid status line`, writes no body.
- Curl's `HttpProtocolHandler` exits 1 (`UnsupportedProtocol`) with `Received HTTP/0.9 when not allowed`: its status-line parse rejects the line as not HTTP/1.x and falls through to the HTTP/0.9 refusal.
- Neighbouring answers already match curl and must keep matching (pinned in `HttpProtocolHandlerTests.Adversarial.cs`): `HTTP/1.1 99 X`, `20 X`, `2x0 X` and `HTTP/1.2 200` give exit 1 `Unsupported HTTP/1 subversion in response`; `GARBAGE` gives exit 1 `Received HTTP/0.9 when not allowed`; codes 599, 600 and 999 are accepted.
- Start at `Curl.Protocol.Http.UnitLibrary/HttpStatusLine.cs` and `HttpResponseHeadReader.cs`.

## Acceptance criteria

- [x] A test in `Curl.Protocol.Http.UnitTests` (`ExecuteAsync_FourDigitStatusCode_FailsWithInvalidStatusLine`, replayed with 1-byte and whole reads) pins exit 8 `Invalid status line` and an empty body for `HTTP/1.1 1000 X`.
- [x] Every test in `HttpProtocolHandlerTests.Adversarial.cs` still passes.
- [x] `dotnet build` is clean and the fast tests pass; `Curl.Protocol.Http.UnitLibrary` stays at 100% line and branch coverage.

## Notes

- `HttpStatusLine.ParseHttp1` now refuses an HTTP/1.x status line whose three-digit code is followed by another digit with exit 8 `Invalid status line` (new `HttpTransferMessages.InvalidStatusLine`). Before, `1000` was read as `100` plus reason `0 X`, an interim response, and the next line fell through to the HTTP/0.9 refusal.
- Order decided by upstream test 1432 (`HTTP/1.1 0123...` with 100 digits, expected exit 1): the below-100 check (exit 1 `Unsupported response code in HTTP response`) runs first, the extra-digit check after it. Placing it first broke test 1432 in `Curl.Conformance.UnitTests`.
- Only a digit after the code is refused; other characters glued to the code (e.g. `200X`) were not measured and keep today's behaviour.
- `HttpResponseHeadReaderTests.ReadAsync_PeerClosingBeforeAFinalStatusLine_ReturnsGotNothing` lost its `Four digits read as 100` row, which pinned the old misreading.
- Measure-CodeQuality: Curl.Protocol.Http.UnitLibrary 100% line, 100% branch, 0 failing members.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. HTTP/1.1 1000 X now fails with exit 8 Invalid status line as curl 8.21.0 does
