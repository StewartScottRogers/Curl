---
id: BL-483
title: Accept a head the peer closes right after a refused header, as curl 8.21.0 does
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-483 — Accept a head the peer closes right after a refused header, as curl 8.21.0 does

## Goal

When the peer closes the connection right after the line of a header curl 8.21.0 would refuse, with no byte of a next line (or only a blank) before the close, `HttpProtocolHandler` does what curl 8.21.0 does - measured: exit 0, the refused header's `-v` line reported, `* Connection #0 to host ... left intact` - instead of failing with the refused header's error.

## Context

- curl 8.21.0 does not act on a header until a byte of the next line shows it is whole (a blank would fold the next line into it), so a header the head ends on at close is never checked (BL-480 Notes).
- Since BL-475/BL-479, `HttpResponseHeadReader.ReadAsync` asks `FindRefusal` of the whole head when the peer closes, the last header included, so this case fails with exit 8 (`Invalid Content-Length: value`).
- Measured (2026-09-27, `Record-CurlExchange.ps1`, `-s -v http://127.0.0.1:<port>/`) against `HTTP/1.1 200 OK\r\nX-Before: 1\r\nContent-Length: x\r\n` then close: exit 0, stderr `< HTTP/1.1 200 OK`, `< X-Before: 1`, `< Content-Length: x`, `* Connection #0 to host 127.0.0.1:<port> left intact`. Measure again with `-sS -D -`, `-w '%{num_headers}'` and a `Transfer-Encoding: bogus` last header before pinning.
- A route: at close, exclude the last header from `FindRefusal` unless a byte after it arrived.

## Acceptance criteria

- [x] A test over a scripted connection that closes right after `Content-Length: x\r\n` pins the measured exit code, `-v` events and `-D` output.
- [x] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.Http.UnitLibrary`.

## Notes

- Filed from BL-480 (2026-09-27).
- Measured (2026-09-27, curl 8.21.0 Schannel, `Record-CurlExchange.ps1`, server sends then closes), `HTTP/1.1 200 OK\r\nX-Before: 1\r\n` followed by:
  - `Content-Length: x\r\n`: `-s -v` exit 0, `< ... Content-Length: x`, `* Connection #0 ... left intact`; `-sS -D -` exit 0, stdout is the whole head, stderr empty; `-s -w %{num_headers}` prints `2` (same as for a valid last header or a whole head).
  - `Transfer-Encoding: bogus\r\n`: exit 0, line reported, left intact.
  - `Content-Length: 5\r\n` (valid, body never comes): exit 0, left intact - not exit 18.
  - `X-After: 1\r\n`, and the status line alone: exit 0, left intact.
  - `Content-Length: x\r\nX` (a byte of a next line, then close): exit 8, only the lines before it.
  - `Content-Length: x` with no line end, or `\r\n` then a blank: exit 0, left intact (curl's `-v` then runs the header into `Connection #0 ...` with no `* `; not pinned here).
  - `Content-Length: 5\r\nX-Before: 1\r\n` (Content-Length not last): exit 18, `transfer closed with 5 bytes remaining to read` - filed as BL-485.
  - `HTTP/1.1 302 Found\r\nX-Before: 1\r\nLocation: /b\r\n` with `-L -w "[%{redirect_url}][%{num_redirects}]"`: `[][0]`, exit 0.
  - `Set-Cookie: a=1\r\n` last with `-c -`: no cookie stored - filed as BL-484.
- So the rule is wider than refusals: curl 8.21.0 never acts on the header a head ends on at close - no refusal, no framing, no redirect - while it still reports, writes and counts it. No ADR: the behaviour is measured, not chosen.
- Route: `HttpResponseHeadReader.ReadAsync` asks `FindRefusal` of the head without its last header when the peer closed (not when BL-480's early stop ended the lines, tracked by `endedAtRefusedHeader`). `HttpResponseHeadReader.HeadActedOn` gives the head without that header; `HttpProtocolHandler` uses it for the redirect, retry, `-f`, body framing and reading, and reuse decisions, and the full head for `-D` and the report. `KeepsAlive` treats a head closed among its headers as one with no body, since curl decides a body runs to close only at the empty line - this also fixes a head closed after a plain header, which we used to report as `shutting down connection #0`.
- Tests: `ExecuteAsync_PeerClosesRightAfterAHeader_NeverActsOnIt` (4 rows), `ExecuteAsync_PeerClosesRightAfterTheStatusLine_LeavesTheConnectionIntact`, `ExecuteAsync_PeerClosesRightAfterALocation_NeitherFollowsNorReportsIt`. HTTP tests 1150 pass; Http library 100% line and branch, worst CRAP 10.
- Delivered in-session rather than through the full `/feature` agent chain: a small change in two classes, with the route set by measurement.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. A head the peer closes right after a header is accepted without acting on that header - no refusal, framing or redirect - as curl 8.21.0 does
