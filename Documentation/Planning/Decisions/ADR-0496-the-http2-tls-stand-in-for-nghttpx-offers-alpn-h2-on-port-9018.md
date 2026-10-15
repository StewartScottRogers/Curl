# ADR-0496: The HTTP/2-over-TLS stand-in for nghttpx offers ALPN h2 alone on %HTTP2TLSPORT 9018

- Status: Accepted
- Date: 2026-10-14
- Decided by Claude under Stewart's delegation (BL-2245)

## Context

Upstream's nghttpx listens on two ports in front of sws (`tests/servers.pm` at `curl-8_22_0`):
`%HTTP2PORT` for h2c and `%HTTP2TLSPORT` for HTTP/2 over TLS. ADR-0491 and ADR-0495 put the h2c
stand-in on `%HTTP2PORT`; cases 2400 to 2406 and the TLS half of 359 need the other port. The
runner already fronts sws with TLS for `%HTTPSPORT` (`HttpsServerConnector`, no ALPN, as stunnel)
through `TlsServerStream`, which takes the ALPN list in `TlsServerOptions`.

## Decision

- `%HTTP2TLSPORT` is `9018` (`UpstreamCaseRunner.Http2TlsPort`, `UpstreamHttp2Server.Http2TlsPort`),
  the first port after the runner's others, and like every stunnel-fronted port it has a value
  only when a certificate directory is named.
- `UpstreamHttp2ServerFactory.CreateTls(backend, certificate)` builds the stand-in in
  `Curl.Conformance.Http2Server.UnitLibrary`: each connection runs `TlsServerStream` offering ALPN
  `h2` alone, then the same `Http2ServerSession` as h2c, which relays each stream to sws with
  `X-Forwarded-Proto: https`, as nghttpx does for a TLS frontend. A failed handshake ends the
  connection. Offering only `h2` keeps the stand-in to what upstream's HTTP/2 cases exercise; a
  client offering only `http/1.1` fails the handshake rather than reaching an HTTP/1.1 path the
  session does not speak.
- `UpstreamHttp2Server` takes the TLS factory as an optional second member, and `InFrontOf` puts
  it on 9018 only when given a certificate. The runner loads `%CERTDIR/certs/test-localhost.pem`
  for it only for a case naming the `http/2` server.

## Consequences

- On Windows the cases still skip for `http/2` (ADR-0495); they are measured on Linux and macOS
  (BL-2264). 359 and 2406 also need `Debug` (BL-2263); 2404 names a `<tool>`.
