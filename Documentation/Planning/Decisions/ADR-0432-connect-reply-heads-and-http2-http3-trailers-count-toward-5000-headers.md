# ADR-0432: CONNECT reply heads and HTTP/2 and HTTP/3 trailers count toward the 5000 response headers

- Status: Accepted
- Date: 2026-10-07
- Task: BL-1609
- Decided by Claude under Stewart's delegation.

## Context

BL-1448 made Curl fail the 5001st stored response header with exit 100
`Too many response headers, 5000 is max`, counting every head of a transfer and HTTP/1.1 chunked
trailers. Two kinds of stored header were left uncounted: the head of the proxy's reply to the
CONNECT that opens a tunnel, and HTTP/2 and HTTP/3 trailers.

The CONNECT case was measured against curl 8.21.0 (Schannel) with `Record-CurlExchange.ps1 -Script`:
a CONNECT reply of 3000 headers followed by a final head of 3000 headers exits 100 after
`< X-H2000: v` - the CONNECT headers count (BL-1609 Notes).

HTTP/2 trailers could not be measured: the Windows curl 8.21.0 has no HTTP2 feature, and
`Record-CurlExchange.ps1` serves no h2 origin. By source, `lib/headers.c` (tag `curl-8_21_0`)
pushes every header write, `CLIENTWRITE_TRAILER` included, through `Curl_headers_push`, the same
path whose limit BL-1448 measured for chunked trailers.

## Decision

- `ConnectResult.ConnectReplyHeadersStored` carries the opening reply's header lines (its lines less
  the status line and the blank line), set by `TcpConnector` and passed on by `PoolingConnector`;
  the HTTP handler adds it to the headers stored before the first exchange on that connection.
- The HTTP handler counts HTTP/2 and HTTP/3 trailer lines against the same limit, writes those that
  fit to the header output, and fails the first past 5000 with exit 100, as chunked trailers do.

## Consequences

- A reused tunnel connection reports 0, so only the transfer that opened the tunnel counts its
  CONNECT reply, as curl stores it once.
- The HTTP/2 and HTTP/3 behaviour rests on curl's source, not a measurement; a build of curl with
  nghttp2 should confirm it when one is available.
