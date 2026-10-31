# ADR-1018: The S3 extension speaks HTTP/1.1 over IConnection with its own small client

- Status: Accepted
- Date: 2026-10-29
- Decided by Claude under Stewart's delegation (BL-4199)

## Context

The `s3://` handler must send requests to S3, MinIO or R2 and read their responses. An extension may
not reference `Curl.Original.Protocol.Http` (ADR-0999), so the extension needs its own HTTP/1.1 exchange over
the `IConnection` seam.

## Decision

`Curl.Extension.Protocol.S3.UnitLibrary/Http/S3HttpClientConnection` sends one request at a time over one
`IConnection` (RFC 9112 §3, §5), streaming a request body of a declared length with `Content-Length`,
and reads the status line (§4) and fields (§5), exposing the body as a forward-only asynchronous
stream framed by `Content-Length`, the chunked coding (§7.1) or the close (§6.3).

- Every failure of the exchange is an `S3HttpException` whose `S3HttpFailure` names the cause: a
  malformed status line or field (an obs-fold is rejected, §5.2 allows it), an invalid
  `Content-Length` (a list such as `1, 1` included) or chunk, a line over 64 KiB, a connection closed
  before the head or a framed body ended, or a request body shorter than its declared length.
- `ConnectionPersists` is true for an HTTP/1.1 response without `Connection: close` whose body is not
  delimited by the close (§9.3). The client does not call `IConnection.MarkReusable`: the caller does,
  once it has read the body to its end, since only it knows when that is.
- `Expect: 100-continue` is not sent, so no interim 1xx response is awaited; a 1xx response is
  returned as a response without a body. Chunked trailer fields are discarded.
- A request field holding CR or LF is an `ArgumentException`, so no caller can inject a field.

## Consequences

The extension stays independent of curl's HTTP code; anything beyond one HTTP/1.1 exchange at a time
(pipelining, HTTP/2, `100-continue`) is a later task's to add.
