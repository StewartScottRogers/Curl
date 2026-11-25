# ADR-1404: A stopped HTTP/2 upload resets its stream, and a held body sent to an ended stream exits 55

- Status: Accepted. Decided by Claude under Stewart's delegation.
- Date: 2026-11-25
- Task: BL-5972

## Context

ADR-1403 left two differences from curl's nghttp2 build. Measured with curl 8.18.0 (nghttp2
1.68.0, WSL), `Record-CurlExchange.ps1 -Http2Origin prior-knowledge -Http2Frames ...`,
`--http2-prior-knowledge -sS -v -H "Expect: 100-continue" -d x=1`:

- A `500` during the wait, whether or not it ends the stream: no DATA, and once the response is
  read `RST_STREAM` error 5 (STREAM_CLOSED), then GOAWAY.
- A `200` that ends the stream (on its head or on DATA, with or without `content-length`): the
  head, `Done waiting for 100-continue`, the DATA with END_STREAM, `stream 1 closed`,
  `Connection #0 ... left intact`, exit 55 `Failed sending data to the peer`. There is no
  `upload completely sent off`, no `} [3 bytes data]`, no `{ [0 bytes data]` for a head that
  ended the stream, and no RST_STREAM.

## Decision

Curl does the same over HTTP/2 on every platform. A stream's response never closes its
connection for want of a length, so a held body on a stream is sent whatever the response
head says about its length (Curl had given it up when the head had no `content-length`).

A body being sent when the response ends the stream, with no wait holding it, still stops
there (unmeasured). `-f` on a status that ended the stream during the wait still sends no
RST_STREAM, where curl does (measured, filed as a task of its own).

## Consequences

Over HTTP/3 a held body is also sent when the response has no length; the reset and exit 55
are HTTP/2 only, as HTTP/3 was not measured.
