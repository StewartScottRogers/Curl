# ADR-1274: https offers h2 by ALPN by default on every platform

Decided by Claude under Stewart's delegation (BL-5288).

## Context

ADR-0141 measured the Schannel reference build offering `http/1.1` alone with no HTTP version option, and so Curl did the same on Windows. Surl's Phase 7 measurement (Http2.md, finding 1) shows upstream curl 8.21.0 with OpenSSL offers `h2,http/1.1` by default and speaks HTTP/2 when the server accepts. The curl manual says of `--http2`: "For HTTPS, this means curl negotiates HTTP/2 in the TLS handshake. curl does this by default." Upstream is the oracle.

## Decision

With no HTTP version option, `https` offers `h2,http/1.1` on every platform, Windows included. `--http1.1` and `-0` still offer `http/1.1` alone. The Windows `-V` Features line lists `HTTP2`, as the line already must for a build that serves it (ADR-0786).

## Consequences

ADR-0141's Windows default is amended; its other measurements stand.
