---
id: BL-840
title: Match curl on RTSP reply header lines without a colon and Content-Length lists
priority: Low
assignee: Claude
pipeline: protocol
depends-on: [BL-591]
touches: [Curl.Protocol.Rtsp.UnitLibrary, Curl.Protocol.Rtsp.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-840 — Match curl on RTSP reply header lines without a colon and Content-Length lists

## Goal

`RtspReplyReader` treats the reply header lines BL-591 did not measure as curl 8.21.0 does: a header line with no colon, a carriage return inside a line, a folded continuation line, and a `Content-Length` given as a comma list (`2, 2`) or twice.

## Context

- BL-591 built `RtspReplyHeadParser` and `RtspReplyReader` (ADR-0169). It reads `CSeq` and `Content-Length` only; any other line is accepted and written, and a `Content-Length` that is not one decimal number fails with 8, `Invalid Content-Length: value`.
- Over HTTP curl fails such lines with 8 (`Header without colon`, `Carriage return found in header`) and accepts a list of equal numbers (`Curl.Protocol.Http.UnitLibrary`'s `HttpTransferMessages`). Whether RTSP does the same is unmeasured.
- Measure each case with `Record-CurlExchange.ps1` against Git for Windows' mingw64 curl 8.21.0 (Windows' own `curl.exe` has no `rtsp`), `-sS -i rtsp://127.0.0.1:<port>/media`.

## Acceptance criteria

- [x] Each measurement (reply, stdout, stderr, exit) copied into Notes.
- [x] `Curl.Protocol.Rtsp.UnitTests` pin the output and outcome of each measured reply through a fake connection.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Rtsp.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Measured 2026-09-29, curl 8.21.0 (Git for Windows mingw64, Schannel), `Record-CurlExchange.ps1
  -HoldOpenMilliseconds 1500`, `-sS -i -w "[%{http_code} %{size_download}]" rtsp://127.0.0.1:<port>/media`.
  `S` = `RTSP/1.0 200 OK\r\nCSeq: 1\r\n`. `%{http_code}` was 200 and `%{size_download}` 0 in every
  row but the weird status line (000 0). stdout shown without the `-w` suffix.

  | Reply | stdout | stderr | Exit |
  | --- | --- | --- | --- |
  | `S` `X-NoColon\r\n\r\n` | `S` | `curl: (8) Header without colon` | 8 |
  | `RTSP/1.0 200 OK\r\nX-NoColon\r\nCSeq: 1\r\n\r\n` | status line | `curl: (8) Header without colon` | 8 |
  | `S` `X A\r\n\r\n` | `S` | `curl: (8) Header without colon` | 8 |
  | `RTSP/1.0 200 OK\r\n cont\r\nCSeq: 1\r\n\r\n` | status line | `curl: (8) Header without colon` | 8 |
  | `S` `X-A: b\rc\r\n\r\n` | `S` | `curl: (8) Carriage return found in header` | 8 |
  | `RTSP/1.0 200 OK\r\nX-A: b\rc\r\nCSeq: 1\r\n\r\n` | status line | `curl: (8) Carriage return found in header` | 8 |
  | `S` `X-A: b\r\r\n\r\n` | `S` | `curl: (8) Carriage return found in header` | 8 |
  | `S` `X-A b\rc\r\n\r\n` | `S` | `curl: (8) Carriage return found in header` (CR wins over colon) | 8 |
  | `S` `X-A: 1\r\n c\rd\r\n\r\n` | `S` | `curl: (8) Carriage return found in header` | 8 |
  | `RTSP/1.0 200 O\rK\r\nCSeq: 1\r\n\r\n` | nothing | `curl: (8) Carriage return found in header` | 8 |
  | `RTSP/1.0 abc\rx\r\nCSeq: 1\r\n\r\n` | nothing | `curl: (8) Weird server reply` (status check first) | 8 |
  | `S` `X-A: 1\r\n  cont\r\n\r\n` | `S` `X-A: 1 cont\r\n\r\n` | | 0 |
  | `S` `X-A: 1  \r\n\t \tcont  \r\n\r\n` | `S` `X-A: 1 cont  \r\n\r\n` | | 0 |
  | `S` `X-A: 1\r\n a\r\n b\r\nX-B: 2\r\n\r\n` | `S` `X-A: 1 a b\r\nX-B: 2\r\n\r\n` | | 0 |
  | `S` `X-A: 1\r\n   \r\n\r\n` | `S` `X-A: 1 \r\n\r\n` | | 0 |
  | `S` `X-A: 1\n cont\r\n\r\n` | `S` `X-A: 1 cont\r\n\r\n` | | 0 |
  | `RTSP/1.0 200 OK\nCSeq: 1\nX-A: 1\n cont\n\n` | the same with `X-A: 1 cont\n` | | 0 |
  | `RTSP/1.0 200 OK\r\nCSeq:\r\n 1\r\n\r\n` | `S` `\r\n` (CSeq read as 1) | | 0 |
  | `S` `Session:\r\n abc\r\n\r\n` | `S` `Session: abc\r\n\r\n` | | 0 |
  | `S` `Content-Length:\r\n 2\r\n\r\nab` | `S` `Content-Length: 2\r\n\r\n` | | 0 |
  | `S` `Content-Length: 2\r\n , 2\r\n\r\nab` | `S` `Content-Length: 2 , 2\r\n\r\n` | | 0 |
  | `S` `X-A: 1\r\n cont\r\n`, closed | `S` `X-A: 1 cont\r\n` | | 0 |
  | `S` `X-A: 1\r\n cont\r\nPubl`, closed | `S` `X-A: 1 cont\r\nPubl` | | 0 |
  | `-v`; `S` `X-A: 1\r\n cont\r\n\r\n` | | `< X-A: 1 cont` among the reply lines | 0 |
  | `S` `Content-Length: 2, 2\r\n\r\nab` | the head | | 0 |
  | `S` `Content-Length: 2 ,2 , 2\r\n\r\nab` | the head | | 0 |
  | `S` `Content-Length: 02, 2\r\n\r\nab` | the head | | 0 |
  | `S` `Content-Length: 2\r\nContent-Length: 2\r\n\r\nab` | the head | | 0 |
  | `S` `Content-Length: 2, 3\r\n\r\nab` | `S` | `curl: (8) Invalid Content-Length: value` | 8 |
  | `S` `Content-Length: 2,,2`, `2,`, `,2` | `S` | `curl: (8) Invalid Content-Length: value` | 8 |
  | `S` `Content-Length: 2\r\nContent-Length: 3\r\n\r\nabc` | `S` `Content-Length: 2\r\n` | `curl: (8) Invalid Content-Length: value` | 8 |
  | `S` `Content-Length: 3\r\nContent-Length: 2\r\n\r\nabc` | `S` `Content-Length: 3\r\n` | `curl: (8) Invalid Content-Length: value` | 8 |
  | `S` `Content-Length: 2, 2\r\nContent-Length: 3\r\n\r\nab` | `S` `Content-Length: 2, 2\r\n` | `curl: (8) Invalid Content-Length: value` | 8 |
  | `S` `Content-Length: 99999999999999999999\r\n\r\nab` | the head | | 0 |
  | `S` `Content-Length: 2` then `Content-Length: 99999999999999999999` | the head | | 0 |
  | `S` `Content-Length: 99999999999999999999` then `Content-Length: 3` | the head | | 0 |
  | `S` `Content-Length: 2, 99999999999999999999\r\n\r\nab` | the head | | 0 |

- Built: `RtspHeaderFolding` joins a header line and its continuation lines (one space between;
  the line before loses its ending and trailing blanks, the continuation its leading blanks);
  `RtspReplyReader` reads a header line only once a byte after its last continuation line has
  arrived, and joins them in what it writes at close too. `RtspReplyHeadParser` refuses a CR
  inside a line (after the status check on the status line) and a header line with no colon,
  and reads `Content-Length` as a list of equal numbers that must agree across headers, as the
  HTTP library's `HttpContentLength` does.
- Defaults taken (no ADR: they refine ADR-0169's reply reading without changing it, as BL-591's
  did): a `Content-Length` number too large for 64 bits leaves the length unknown for the rest
  of the head, every later value unchecked, as `HttpContentLength` does; RTSP then reads no
  body, since `%{size_download}` is 0 for every RTSP reply and cannot show otherwise. A head
  closed mid-continuation (`X-A: 1\r\n co`) is written as received, only whole continuation
  lines being joined (unmeasured).
- Tests: 42 new in `RtspReplyHeaderLineTests`, 162 in `Curl.Protocol.Rtsp.UnitTests`. Quality:
  100% line and branch, 0 failing members, worst CRAP 10. Review done in-session against the
  measurements.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. RTSP replies fail a header line with no colon or an inner CR with 8, join continuation lines, and accept Content-Length lists of equal numbers as curl 8.21.0 does
