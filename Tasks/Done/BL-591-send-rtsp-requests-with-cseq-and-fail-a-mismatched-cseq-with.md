---
id: BL-591
title: Send RTSP requests with CSeq and fail a mismatched CSeq with exit 85
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-590]
touches: [Curl.Protocol.Rtsp.UnitLibrary, Curl.Protocol.Rtsp.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-591 — Send RTSP requests with CSeq and fail a mismatched CSeq with exit 85

## Goal

An `RtspProtocolHandler` sends the requests BL-590's ADR says the curl tool sends, byte for byte (request line, `CSeq`, `User-Agent`, header order), reads each reply head and body, writes the output curl writes, and fails a reply whose `CSeq` does not match with exit 85 and curl's message.

## Context

- Conformance audit 2026-09-28, row 38. Design and measurements: BL-590's ADR.
- Add any case BL-590 did not measure: a reply with no `CSeq`, a reply with a body (`Content-Length`), a `404`, with `Record-CurlExchange.ps1`.

## Acceptance criteria

- [x] Any extra measurement copied into Notes.
- [x] `Curl.Protocol.Rtsp.UnitTests` pin the request bytes and the output and outcome for each measured reply through a fake connection.
- [x] No test needs `TestCategory=Integration`; tests are platform-neutral.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Rtsp.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Built per ADR-0169 decision 1: `RtspRequestFormatter`, `RtspReplyHeadParser`,
  `RtspReplyReader`, `RtspProtocolHandler` (one `OPTIONS *` with `CSeq: 1`, port 554), plus
  `RtspMethod`, `RtspCustomHeader` (a copy of the Ws/HTTP `-H` rules, as ADR-0128/0169 allow),
  `RtspIoFailures` (55/56/23 as the HTTP library) and `RtspTransferException`. Not registered in
  `Curl.Console` and no `-v` events: both are BL-593's. `Session` handling is BL-592; the
  formatter already takes a session ID.
- ADR-0169 already measured the three cases the Context asks for (row 14: no `CSeq`; rows 2 and
  5: a body with `Content-Length`; rows 16-17: a `404`). Extra measurements, curl 8.21.0 (Git for
  Windows mingw64, Schannel), `Record-CurlExchange.ps1`, `-sS rtsp://127.0.0.1:<port>/media`,
  reply `RTSP/1.0 200 OK\r\nCSeq: 1\r\n\r\n` unless named:

  | Arguments / reply | Request or stdout | stderr | Exit |
  | --- | --- | --- | --- |
  | `-H 'User-Agent: mine'` | `…CSeq: 1\r\nUser-Agent: mine\r\n\r\n` | | 0 |
  | `-H 'User-Agent:'` | `…CSeq: 1\r\n\r\n` (no UA) | | 0 |
  | `-H 'X-A: 1' -H 'User-Agent: mine' -e http://r/ -u u:p` | `CSeq`, `Referer: http://r/`, `Authorization: Basic dTpw`, `X-A: 1`, `User-Agent: mine` | | 0 |
  | `-u u:p -H 'Authorization: Bearer t'` | `…User-Agent: curl/8.21.0\r\nAuthorization: Bearer t\r\n\r\n` | | 0 |
  | `-H 'X-Empty;' -H 'X-Gone:' -e http://r/ -H 'Referer: other'` | `…User-Agent…\r\nX-Empty:\r\nReferer: other\r\n\r\n` | | 0 |
  | `-H 'CSeq: 5'`, `-H 'CSeq:'`, `-H 'cseq;'` | nothing sent (connects first: a closed port gives 7) | `curl: (85) CSeq cannot be set as a custom header.` | 85 |
  | `-i`; `RTSP/1.0 404 Not Found\r\nContent-Length: 3\r\n\r\nabc` | the head, no body | `…did not match the response 0` | 85 |
  | `-f -i`; 404 with `CSeq: 1` and a body | the whole head | `curl: (22) The requested URL returned error: 404` | 22 |
  | `-f`; `RTSP/1.0 404 Not Found\r\n\r\n` (no CSeq) | | `(22) …error: 404` (22 wins over 85) | 22 |
  | `-i`; `…Content-Length: 10\r\n\r\nab`, closed | the head | | 0 |
  | `-i`; `RTSP/1.0 200 OK\r\nCSeq: 1\r\n`, closed | those bytes | `…did not match the response 0` | 85 |
  | `-i`; `RTSP/1.0 200 OK\r\nCSeq: 1\r\nPubl`, closed | those bytes | | 0 |
  | `-i`; `RTSP/1.0 200 OK`, closed | those bytes | `…did not match the response 0` | 85 |
  | `-i`; empty, `RTSPX`, or `rtsp/1.0 200 OK…` | | `curl: (52) Empty reply from server` | 52 |
  | `-i`; `RTSP/1.0 abc…` or `RTSP/2.0 200 OK…` | nothing (`%{http_code}` 000) | `curl: (8) Weird server reply` | 8 |
  | `-i`; `RTSP/1.0 099 X\r\nCSeq: 1\r\n\r\n` | the whole head, `%{http_code}` 099 | `curl: (1) Unsupported response code in HTTP response` | 1 |
  | `-i`; `…CSeq: 1\r\nContent-Length: x\r\n\r\n` | status and CSeq lines only | `curl: (8) Invalid Content-Length: value` | 8 |
  | `-i`; `…CSeq: abc\r\n\r\n` | status line only | `curl: (85) Unable to read the CSeq header: [CSeq: abc\r\n]` | 85 |
  | `-i`; `cseq:   1 \r\n`, `CSeq: 1x\r\n`, LF-only head | the head as received | | 0 |
  | `-i`; `CSeq: 1\r\nCSeq: 2\r\n` | the head | `…did not match the response 2` | 85 |

- Decisions taken from these (defaults, recorded here rather than in a new ADR because they
  refine ADR-0169 decision 1 without changing it): a head line is acted on only once a byte
  after it has arrived, or when it is the blank line — the one rule that explains the three
  "closed" rows; unread bytes are still written at close. `-f` and the below-100 check apply
  only to a head that ended; a head cut short goes straight to the `CSeq` check (unmeasured
  for a cut 404 under `-f`). Non-`RTSP/` bytes fail with 52 as soon as they arrive rather than
  at close (only timing differs). A head is capped at 102400 bytes with exit 100, as the HTTP
  and Ws libraries do (unmeasured for RTSP). A reply with no `Content-Length` has no body.
- Header lines with no colon, a CR inside a line, and `Content-Length` lists are unmeasured
  and filed as BL-840.
- Tests: 85 in `Curl.Protocol.Rtsp.UnitTests`, data rows included. Review done
  in-session against ADR-0169 and the measurements rather than through a separate agent.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. RtspProtocolHandler sends curl's OPTIONS * request byte for byte, writes the reply head, discards the body, and fails a mismatched or missing CSeq with exit 85
