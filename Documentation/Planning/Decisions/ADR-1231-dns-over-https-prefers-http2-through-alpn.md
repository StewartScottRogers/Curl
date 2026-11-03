# ADR-1231: DNS over HTTPS prefers HTTP/2 through ALPN

- Status: Accepted
- Date: 2026-11-03
- Task: BL-4690
- Decided by Claude under Stewart's delegation

## Context

RFC 8484 section 5.2 names HTTP/2 the minimum recommended version of HTTP for DoH. `DnsHttpsTransport` ran the TLS
handshake without ALPN and always wrote an HTTP/1.1 request, so conformance case `DnsQuery:RFC8484-5.2-1` failed.
curl's own DoH resolver (`--doh-url`) goes through its HTTP stack and so uses HTTP/2 whenever the server selects `h2`.

## Decision

- The handshake offers ALPN `h2` then `http/1.1` through the TLS provider's ALPN overload. When the server selects
  `h2`, `DohHttp2Exchange` sends the client preface and one stream carrying the POST to `/dns-query` (pseudo-headers,
  `accept`, `content-type`, `content-length`, the query as one DATA frame with END_STREAM), skips informational
  responses, checks the final one as a 200 `application/dns-message` with `DohResponseHead`, joins its DATA frames and
  ignores trailers. Any other selection, or none, keeps the HTTP/1.1 request unchanged.
- It uses the shared layer `Curl.Http2.UnitLibrary` (frames, flow control, HPACK) rather than hand-written frames, as
  the gRPC extension does; `DohConnectionStream` presents the `IConnection` as the `Stream` that layer runs on. A
  stream that ends, or a connection that closes, before the answer is exit 56, like HTTP/1.1's early close; an HTTP/2
  protocol error from the layer propagates as the HTTP/1.1 path's I/O errors do.
- The conformance side adds `DohHttp2ReferenceServer`: on a plain TCP route it serves TLS selecting ALPN `h2` (a TLS
  route's front end offers no ALPN) and serves HTTP/2 through `GrpcReferenceServer`'s session with a service that
  answers each ended stream with a DNS reply. `DohReferenceServer` stays HTTP/1.1 on a TLS route, so the HTTP/1.1
  fallback stays covered by the existing cases.

## Consequences

DoH matches the RFC's recommendation and curl's own resolver against any modern DoH server; servers without `h2`
still work over HTTP/1.1. `Curl.Extended.Protocol.DnsQuery.UnitLibrary` now references `Curl.Http2.UnitLibrary`.
