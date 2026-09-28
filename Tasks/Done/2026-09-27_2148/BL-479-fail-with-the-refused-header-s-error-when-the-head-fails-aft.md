---
id: BL-479
title: Fail with the refused header's error when the head fails after it
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-475]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-479 — Fail with the refused header's error when the head fails after it

## Goal

When a final response head carries a header curl 8.21.0 refuses (an invalid Content-Length, a refused Transfer-Encoding, too many content codings) and then fails later - a line with no colon, a line or head too large, or a peer that never sends the empty line - `HttpProtocolHandler` fails with the refused header's exit code and message, reporting only the head before it, as curl 8.21.0 does.

## Context

- Since BL-475, `HttpResponseHeadReader` holds a final head's whole headers until the head ends, then asks `FindRefusal` once of the whole head and releases only the headers before the refused one. A head that fails before it ends never reaches `FindRefusal`: the later failure wins, and `ReportHeldLines` releases every held header, refused one included.
- curl 8.21.0 stops reading the head at the refused header (BL-475 Notes), so it should never see the later failure, and should not wait for a peer that never ends the head.
- Measure first with `Record-CurlExchange.ps1`: `-s -v http://127.0.0.1:<port>/` against `HTTP/1.1 200 OK`, `Content-Length: x`, `no colon`, empty line; and against a head with `Content-Length: x` that the server never ends (close after a delay).
- A route: on a failure inside the final head, ask `FindRefusal` of the headers already whole (`HttpResponseHeadBuilder` has them) before reporting the failure.

## Acceptance criteria

- [x] A test over a scripted connection pins the exit code, message and `-v` events for a head whose refused header is followed by a line with no colon, as measured.
- [x] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.Http.UnitLibrary`.

## Notes

- Filed from BL-475 (2026-09-27).
- Measured (2026-09-27, curl 8.21.0, `Record-CurlExchange.ps1`) against `HTTP/1.1 200 OK`, `X-Before: 1`, refused header, `no colon`, empty line:
  - `-s -v` with `Content-Length: x`: exit 8; `< HTTP/1.1 200 OK`, `< X-Before: 1`, then `* Invalid Content-Length: value` and `* closing connection #0`. No `Header without colon`.
  - `-s -v` with `Transfer-Encoding: bogus` (no `X-Before`): exit 61, `* Unsolicited Transfer-Encoding (bogus) found`.
  - `-sS -D -` with `Content-Length: x`: stdout `HTTP/1.1 200 OK\r\nX-Before: 1\r\n`, stderr `curl: (8) Invalid Content-Length: value`.
- Route taken: `HttpResponseHeadReader.ReadAsync` catches an `HttpTransferException` from reading a head's header lines and asks `FindRefusal` of the whole headers it holds (`HeadBeforeRefusalOrThrow`). When one is refused it drops the cut-off header's lines, releases the headers before the refused one, and returns the head with `Refusal` set, so the handler writes `-D` and fails exactly as for a refused head that ended normally. When none is refused the original failure is rethrown. A 1xx head holds no whole headers, so it never finds a refusal and needs no separate branch.
- A peer that closes without the empty line already reached `FindRefusal` (the head ends at close). A peer that holds the head open without closing still makes the reader wait: filed as BL-480, since stopping there needs a check before each network read and an extension of `Record-CurlExchange.ps1` to measure it.
- Tests: `ExecuteAsync_HeadFailsAfterTheRefusedHeader_FailsWithTheRefusedHeadersError` (both measured cases, every chunk size) and `ExecuteAsync_HeadFailsWithNoHeaderRefused_FailsWithTheHeadsError`. Both failed before the fix.
- Delivered in-session rather than through the full `/feature` agent chain: one method in one class, with the design already laid out in Context.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. A head that fails after a refused header fails with the refused header's error and reports and writes only the head before it, as curl 8.21.0 does
