---
id: BL-1404
title: Read a refused WebSocket upgrade reply's Content-Length as curl does: exit 8 for a bad or disagreeing value, the overflow line, exit 63 under --max-filesize
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Ws.UnitLibrary, Curl.Protocol.Ws.UnitTests]
requirement: none
created: 2026-10-03
completed: 2026-10-03
---
# BL-1404 — Read a refused WebSocket upgrade reply's Content-Length as curl does: exit 8 for a bad or disagreeing value, the overflow line, exit 63 under --max-filesize

## Goal

A `ws://` or `wss://` upgrade reply that is not `101` has its `Content-Length` header checked as curl 8.21.0's shared HTTP header code checks it: a value that is not a list of equal decimal numbers, or a second header that disagrees, fails with exit 8 `Invalid Content-Length: value` before that header line is written; a number too large for 64 bits writes `* Overflow Content-Length: value` before its header line, or with `--max-filesize` set fails with exit 63 `Maximum file size exceeded`.

## Context

- Today `Curl.Protocol.Ws.UnitLibrary` never looks at `Content-Length` (`git grep Content-Length -- Curl.Protocol.Ws.UnitLibrary` finds nothing): `WsUpgradeResponseReader.cs` reads the head and a non-101 reply ends with `Refused WebSocket upgrade: <code>` and exit 22 whatever its `Content-Length` says.
- curl 8.21.0 (tag `curl-8_21_0`), `lib/http.c` lines 3236-3275 (`Curl_http_header`, `Content-Length:` when the reply is not bodyless, so not for a `101`): each comma-separated item is read with `curlx_str_numblanks`; `STRE_OVERFLOW` gives `failf(data, "Maximum file size exceeded")` and `CURLE_FILESIZE_EXCEEDED` when `max_filesize` is set, else `streamclose` and `infof(data, "Overflow Content-Length: value")`; a non-number, or a value differing from an earlier one, gives `failf(data, "Invalid Content-Length: value")` and `CURLE_WEIRD_SERVER_REPLY`. These run as the header arrives, before `Refused WebSocket upgrade` is decided at the end of the head.
- Measured 2026-10-03 with curl 8.21.0 (mingw, Schannel) and `Record-CurlExchange.ps1`, `-sv ws://127.0.0.1:PORT/a`:
  - `HTTP/1.1 200 OK\r\nContent-Length: 99999999999999999999\r\n\r\nhello`: `< HTTP/1.1 200 OK`, `* Overflow Content-Length: value`, `< Content-Length: 99999999999999999999`, `* Refused WebSocket upgrade: 200`, `< `, `* closing connection #0`; exit 22.
  - the same with `--max-filesize 2`: `< HTTP/1.1 200 OK`, `* Maximum file size exceeded`, `* closing connection #0`; exit 63.
  - `Content-Length: abc` on a `200`: `< HTTP/1.1 200 OK`, `* Invalid Content-Length: value`, `* closing connection #0`; exit 8. The same on a `401`.
  - `Content-Length: 5` then `Content-Length: 6`: `< Content-Length: 5`, `* Invalid Content-Length: value`, `* closing connection #0`; exit 8.
  - `Content-Length: 5` with `--max-filesize 2`: `* Refused WebSocket upgrade: 200`, exit 22 (no size check before the refusal).
  - a `101` with `Content-Length: abc` is not checked (bodyless): the switch to WebSocket goes ahead.

## Acceptance criteria

- [x] Tests in `Curl.Protocol.Ws.UnitTests` drive the handler with each measured reply and pin the `-v` sequence, exit code and message above, the failing header line not written in the exit 8 and exit 63 cases.
- [x] A test pins that a `101` reply with `Content-Length: abc` still switches to WebSocket.
- [x] `Content-Length: 2, 2` on a `200` is accepted (exit 22 as today) and `2,3` gives exit 8.
- [x] `dotnet build Curl.Protocol.Ws.UnitTests -warnaserror` is clean; `dotnet test Curl.Protocol.Ws.UnitTests --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Ws.UnitLibrary` reports no failing member in the code this task changed.

## Notes

- The HTTP library's own copy of this parse is `Curl.Protocol.Http.UnitLibrary/HttpContentLength.cs`; protocol libraries never reference each other, so the WebSocket library needs its own.
- Done as `WsContentLength` (checks a refused head line by line) returning `WsContentLengthCheck` (how much of the head is accepted, the overflow lines, any failure); `WsProtocolHandler` writes only the accepted part to `-D` and `-v`, and `TransferReport.HeaderSize` counts only that part, since curl counts header bytes as it accepts each line.
- Defaults taken: `--ignore-content-length` skips the check, as curl's `!data->set.ignorecl` does; `--max-filesize 0` counts as unset, as curl's `if(data->set.max_filesize)`; a list item that overflows ends that header's check (overflow line once per header); the exit 8/63 failure returns before Negotiate is stepped, since curl fails while reading the header, before the head ends. None of these was measured beyond the cases in Context.
- Quality: Measure-CodeQuality -Library Curl.Protocol.Ws.UnitLibrary: 100% line, 100% branch, 0 failing members, worst CRAP 10. 331 fast tests pass.

## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
- 2026-10-03: Doing -> Done. A refused ws/wss upgrade's Content-Length is checked as curl does: exit 8 for a bad or disagreeing value, the overflow line, exit 63 under --max-filesize
