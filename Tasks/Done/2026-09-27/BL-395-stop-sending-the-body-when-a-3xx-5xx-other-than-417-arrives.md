---
id: BL-395
title: Stop sending the body when a 3xx-5xx other than 417 arrives mid-upload
priority: Low
assignee: Claude
pipeline: protocol
depends-on: [BL-319]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Record-CurlExchange.ps1]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-395 — Stop sending the body when a 3xx-5xx other than 417 arrives mid-upload

## Goal

A final status of 300 or above other than 417 that arrives once the `100 Continue` wait ran out, while a POST or PUT body is being sent, stops the sending and closes the connection after the response, as curl 8.21.0 does (`HTTP error before end of send, stop sending`).

## Context

- BL-319 made `HttpRequestBodyWriter`, under `HttpContinueWaitConnection.SendUnlessExpectationFailedAsync`, stop at the first piece a 417 beats; every other status that arrives mid-upload still lets the whole body go.
- curl's `http.c` stops sending for any status of 300 or above (not during authentication negotiation, not when the body will be rewound) and marks the connection to close; a 2xx keeps sending.
- Measure first with the BL-319 loopback server answering 500 and 301 after reading body bytes: bytes sent, `%{size_request}`, `%{size_upload}`, `%{num_connects}`, `-D`, and `-L` for the 301.

## Acceptance criteria

- [x] The 500 and 301 cases are measured on curl 8.21.0 and the commands and bytes are in Notes.
- [x] A handler test replays each through `GatedConnection` with `StallsWritesOnceReleased` (1-byte reads too) and matches the measured requests, connections and output.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http*` reports no failing member.

## Notes

### Measured on curl 8.21.0 (Schannel, mingw64), 2026-09-27

`Record-CurlExchange.ps1` gained `-RespondAfterBodyBytes N`: it answers once the head and N
body bytes have arrived, then records whatever curl goes on sending until it stops (this
task needed it, so `Record-CurlExchange.ps1` joined `touches`; no task in Doing names it).
Every run: `-RespondAfterBodyBytes 1000`, `big.bin` = 1048577 bytes of `a`, `-v -D h -w
"%{http_code} %{size_request} %{size_upload} %{size_header} %{num_connects}"`, URL
`http://127.0.0.1:18395/u`. 500 reply: `HTTP/1.1 500 Internal Server Error` /
`Content-Length: 4` / `fail`. 301 reply: `HTTP/1.1 301 Moved Permanently` / `Location: /v` /
`Content-Length: 0`, and a second connection `HTTP/1.1 200 OK` / `Content-Length: 2` / `ok`.

| Command / reply | Result |
| --- | --- |
| `--data-binary @big.bin`, 500 | `* Done waiting for 100-continue`, `* HTTP error before end of send, stop sending`, `* abort upload after having sent 524288 bytes`, `* shutting down connection #0`. `-D` = the 500 head, stdout `fail`, `500 524465 524288 57 1`, exit 0; server got 524465 bytes (177-byte head + 524288). |
| `-T big.bin`, 500 | Same, 127-byte `PUT` head: `500 393343 393216 57 1`, exit 0. |
| `--data-binary @big.bin`, 301, no `-L` | Same stop: `301 393393 393216 67 1`, exit 0, stdout empty. |
| `-L --data-binary @big.bin`, 301 | `* Need to rewind upload for next request`, `* close instead of sending 655361 more bytes`, `* shutting down connection #0`, then `GET /v` on a new connection: `-D` = 301 + 200 heads, stdout `ok`, `200 393473 0 105 2`, exit 0. |
| `-L -T big.bin`, 301 | Same stop after 524288 bytes, then `PUT /v` with the whole file and `Expect` on a new connection (its 200 came mid-body and the file still went whole): `200 1573119 1048577 105 2`, exit 0. |
| `--data-binary @big.bin`, 200 mid-body | `* upload completely sent off: 1048577 bytes`, `Connection #0 ... left intact`, `200 1048754 1048577 38 1`: a 2xx does not stop sending. |

How many 64 KiB pieces go out before the reply is seen is timing (393216 to 524288 here).

### What was built

- `HttpContinueWaitConnection.StopsSending` (was `ExpectationFailed`) is true for any first
  status of 300 or above after the wait, not only 417; `SendUnlessStoppedAsync` (was
  `SendUnlessExpectationFailedAsync`) cancels the piece under way. `HttpRequestBodyWriter`'s
  `ExpectationWatch` is renamed `EarlyResponseWatch` to match. The rest was already there from
  BL-319: `CutShort` makes the connection not keep alive (`shutting down connection #0`), and
  only a 417 draws the resend without `Expect`. With `-L`, `RedirectFollower` (Curl.Core)
  already rewinds a seekable body or drops it for the POST-to-GET switch, so no Core change.
- Tests: `HttpProtocolHandlerTests.ErrorWhileSending.cs` (500 for `-d` and `-T`, 301 with and
  without `-L`, 200 keeps sending; 1-byte and 64 KiB reads); `HttpContinueWaitConnectionTests`
  pins 300/301/417/500 stop and 200/299 do not.

### Choices (sensible defaults; every pinned behaviour is measured, so no ADR)

- Tests pin one 65536-byte piece before the reply, as BL-319 does; curl's count is timing.
- curl's exceptions (not during authentication negotiation, not when the body will be
  rewound) change only its log wording here: with `-L` it still stops sending and closes
  ("close instead of sending"), so no distinction is built. An authentication retry only
  happens for a bytes body, which is resent whole on a new connection either way.
- Only the first status line after the wait is watched: a `100 Continue` arriving late and
  then a 500 mid-body would not stop sending. Not measured; left as is.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. A status of 300 or above other than 417 that arrives mid-upload stops sending the body and shuts the connection, as curl 8.21.0 does
