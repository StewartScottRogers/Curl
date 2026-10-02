# ADR-0383: The HTTP handler writes the upload readers' [READ] trace lines

- Status: Accepted
- Date: 2026-10-02
- Task: BL-1189
- Decided by Claude under Stewart's delegation.

## Context

Under `-v --trace-config read` (and `all`, `-vvv`, `-vvvv`) curl 8.21.0 writes `[READ]` lines as its
client readers take a request body in, and one more `[READ] client_reset, clear readers` as each
followed redirect hop starts (measured 2026-10-02, BL-1189 Notes). The body lines carry the reads'
sizes, which depend on how much of the 64 KiB upload buffer the request head took; only the code that
sends the body knows them. A body that must be rewound for a redirect, a chunked body, one sent after
`100 Continue`, a multipart body and HTTP/2 or HTTP/3 bodies write other lines, not yet measured in
full.

## Decision

1. `Curl.Protocol.Http`'s `HttpRequestBodyWriter` writes the body lines
   (`HttpClientReaderTraceLines`) when `HttpProtocolHandler.TracesClientReaders` is on, which
   `Curl.Console` sets from `CurlComposition.TracesRead` through `CurlTransports.TracesRead`: for a
   non-empty `-d` body `add buf reader`, then `cr_buf_read` and `client_read` per read; for a `-T`
   upload of known, non-zero length `add fread reader`, then `cr_in_read` and `client_read` per read.
   A traced `-d` body is sent in the buffer's pieces, as curl sends it, so its `}` lines match too.
2. Only HTTP/1.x bodies sent unchunked without waiting for `100 Continue` write them, the shapes
   measured; every other body writes none until it is measured (follow-up task).
3. `Curl.Console`'s `ClientReaderResetTraceEvents` writes `[READ] client_reset, clear readers` after
   each `Issue another request to this URL` line, as the next hop starts.

## Consequences

`-d`, `-T` (small and over 64 KiB) and `-L` over one redirect match curl's `[READ]` lines byte for
byte. The rewind lines of a redirect that resends or drops a body (`client reader needs rewind`,
`client_reset, will rewind reader`, `client start, rewind readers`, and the plain
`Need to rewind upload for next request`) are a follow-up.
