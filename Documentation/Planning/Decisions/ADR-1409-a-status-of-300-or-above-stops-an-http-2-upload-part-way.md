# ADR-1409: A status of 300 or above stops an HTTP/2 upload part way

- Status: Accepted
- Date: 2026-11-25
- Task: BL-5973
- Decided by Claude under Stewart's delegation.
- Amends ADR-1403, which let a body being sent on a stream go on.

## Context

curl 8.18.0 (nghttp2 1.68.0) was measured with `Record-CurlExchange.ps1 -Http2Origin
prior-knowledge -RespondAfterBodyBytes 65535`: a 200000-byte upload with
`Expect: 100-continue` sends 65535 body bytes after the wait runs out, and a `500` then
arrives while it waits for window. curl writes `HTTP error before end of send, stop
sending` and `abort upload after having sent 65536 bytes` before the head's empty line,
reads the response, resets the stream with STREAM_CLOSED and leaves the connection intact
(exit 0). The one byte it had read and not sent goes out only when window opens before the
response ends; END_STREAM is never sent.

## Decision

- Once the wait has run out, `Http2StreamConnection` is told to stop the upload at a status
  of 300 or above. A body write then sends what the windows allow, keeps the rest to send
  when window opens while the response is read, and returns, so the whole piece counts as
  sent, as curl's count does. The body writer sends no further piece.
- The response read and the body write take the stream's frames one at a time, so whichever
  takes the status, neither is left waiting for a frame that never comes.
- A cut upload ends only its stream on HTTP/2: the connection is left intact.
- HTTP/3 is unchanged: no HTTP/3 measurement was made here.
