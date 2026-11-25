# ADR-1403: An HTTP/2 or HTTP/3 request waits for 100 Continue when -H asks for it

- Status: Accepted. Decided by Claude under Stewart's delegation.
- Date: 2026-11-25
- Task: BL-5970

## Context

curl adds no `Expect: 100-continue` of its own over HTTP/2 or HTTP/3 (ADR-0172). Curl also
never waited on a stream, even when `-H "Expect: 100-continue"` asked it to, so the body went
out whatever the server answered. The reference Windows curl 8.22.0 has no HTTP/2, so the
behaviour was measured with curl.se's 8.18.0 nghttp2 build (`Record-CurlExchange.ps1
-Http2Origin prior-knowledge -Http2Frames`, `-v -d x=1`): it waits; a `100` sends the body at
once; a `200` has its head printed and the body goes when the wait runs out
(`Done waiting for 100-continue`), so it is held as over HTTP/1.x rather than waited out
before the head is read; a `500` or a followed `301` writes
`HTTP error before end of send, stop sending` and `abort upload`, sends no DATA and leaves the
connection intact.

## Decision

Curl does the same on every platform. HTTP/3 follows HTTP/2: curl's wait is the same code for
both, though HTTP/3 was not measured. A final status that arrives after the wait has run out,
while the body is being sent, still does not stop the body on a stream (unmeasured; since
measured over HTTP/2, where it does stop it: ADR-1409, BL-5973).

## Consequences

The read the wait starts can be pending while the body is written; `Http2Session` already
serialises frame reads and writes for multiplexed streams. The two differences this left over
HTTP/2, the reset of a stopped upload's stream and the exit 55 when the response ends the stream
before a held body is sent, are closed by ADR-1404.
