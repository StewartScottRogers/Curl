# ADR-0817: A refused HTTP/2 stream is retried on a new connection

- Status: Accepted
- Date: 2026-10-26
- Task: BL-3560
- Decided by Claude under Stewart's delegation.

## Context

Third-party gap report issue CGR-031: when an HTTP/2 server reset the request's stream with
RST_STREAM REFUSED_STREAM, Curl failed with exit 92 (`HTTP/2 stream 1 was not closed cleanly:
REFUSED_STREAM (err 7)`). When it sent a GOAWAY whose last stream ID was below the request's
stream, Curl failed with exit 56 (`Failure when receiving data from the peer`). Real curl 8.18.0
with nghttp2 1.68 (WSL, measured in BL-3560's Notes) sends the request again on a new
connection in both cases. With NO_ERROR or INTERNAL_ERROR in the GOAWAY, it prints
`hello h2` and `[2 200 2]`. When the server refuses every time, it fails with exit 56 and
`Connection died, tried 5 times before giving up` after six connections. nghttp2 closes a stream
that a GOAWAY leaves unprocessed with REFUSED_STREAM, so curl's `http2.c` sees both cases as the
same refusal and sets `refused_stream`. `Curl_retry_request` then retries the request. At `-v`
curl prints only multi.c's `REFUSED_STREAM, retrying a fresh connect` line: the line in
`http2.c` is a trace line that `-v` does not show.

The handler already had this retry for HTTP/3 `H3_REQUEST_REJECTED` (ADR-0187), keyed on
`HttpTransferException.IsStreamRefused`.

## Decision

`Http2StreamConnection` maps an `Http2StreamResetException` with REFUSED_STREAM, and an
`Http2GoAwayException` whose last stream ID is below its stream, to exit 56 marked
`IsStreamRefused`. The handler's `StreamRefusedOutcome` therefore retries it as it retries HTTP/3:
up to five times on new connections, then exit 56 with curl's give-up text. A refusal that comes
after the response has begun fails at once with `Failure when receiving data from the peer`. A
GOAWAY with an error that still covers the stream keeps exit 56 with no retry.

The `-v` line that reports a refused HTTP/3 stream now travels in
`HttpTransferException.InfoLines`, not in the exception's message. `ReportReceiveFailure` no longer
prints the message of every refusal, so an HTTP/2 refusal adds no `-v` line, as in curl.

## Consequences

- Each refuse-once repro in CGR-031 now matches curl: exit code, stdout, stderr and the frames
  sent. Each always-refuse repro matches too, except for two differences filed as their own tasks.
  On the connection it gives up, curl closes without the closing GOAWAY that Curl sends. curl
  also prints `* received GOAWAY, error=0, last_stream=0` at `-v`, which Curl does not.
