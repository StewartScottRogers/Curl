# ADR-1089: WebTransport opens one session over HTTP/3 and carries the data by the wt query field

- Status: Accepted
- Date: 2026-10-29
- Decided by Claude under Stewart's delegation (BL-4113)

## Context

BL-4113 adds WebTransport over HTTP/3 as an extension (ADR-0999), tested against draft-ietf-webtrans-http3 and
RFC 9297 (HTTP Datagrams), not against curl, which has no WebTransport. It needs a scheme or option name, the draft
version to follow, how a transfer chooses between bidirectional streams, unidirectional streams and datagrams, and
exit codes for the failures. It builds on the shared layers Curl.Quic (with RFC 9221 DATAGRAM frames, BL-4222),
Curl.Http3 (server-opened streams handed to the caller, BL-4223) and Curl.HttpDatagram.

## Decision

- Scheme `webtransport` (no command-line option, so no `https://` behaviour changes): `webtransport://host[:port]/path`
  dials QUIC over curl's datagram connector to UDP port 443 by default, offers ALPN `h3`, and sends an extended CONNECT
  (`:protocol webtransport`, `:scheme https`) for the path. The library is `Curl.Extension.Protocol.WebTransport.UnitLibrary`.
- Version followed: draft-ietf-webtrans-http3-16. The client sends SETTINGS_ENABLE_CONNECT_PROTOCOL, SETTINGS_H3_DATAGRAM
  and SETTINGS_WT_ENABLED (section 3.1) and refuses a server that does not allow sessions.
- The query field `wt=` chooses how the `-d` data travels, and is not sent to the server: `wt=bidi` (the default) on a
  client bidirectional stream (signal `0x41`, section 4.3), whose answer is the output; `wt=uni` on a client
  unidirectional stream (type `0x54`, section 4.2), whose output is the first server unidirectional stream of the
  session (streams of other sessions are stopped with WT_BUFFERED_STREAM_REJECTED); `wt=datagram` in one HTTP/3
  datagram (RFC 9297 section 2.1, quarter stream ID), whose output is the first datagram for the session. Any other
  value ends with exit 3. Afterwards the session is closed with a CLOSE_WEBTRANSPORT_SESSION capsule (section 6).
- Exit codes: a non-2xx `:status` is exit 22, a server stream ending early or an HTTP/3, QPACK or datagram error is
  exit 56, a datagram larger than the peer accepts is exit 55, and a failed dial or handshake keeps the connector's
  exit code.
- Certificates are judged by curl's TLS provider, so `-k` and `--cacert` apply.

## Left out

- Several streams or datagrams per transfer, and an interactive mode: a curl transfer has one request body and one
  output, so one exchange per transfer is the simplest form that fits curl's model.
- WebTransport over HTTP/2 (draft-ietf-webtrans-http2): a separate specification, left to its own task if wanted.
- Session-level flow control capsules (section 5): the client opens one stream each way, within the initial limits.

## Consequences

`curl --extensions` and `curl --ai-help extensions` list `webtransport`. With `CURL_EXTENSIONS=0` a `webtransport://`
URL fails exactly as curl fails an unsupported scheme.
