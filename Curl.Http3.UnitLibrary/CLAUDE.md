# Curl.Http3.UnitLibrary

A hand-built HTTP/3 client layer: QPACK header compression (RFC 9204) and HTTP/3
framing (RFC 9114) - frames, the control and QPACK encoder and decoder streams,
`SETTINGS` and `GOAWAY` - over QUIC streams it is given as byte streams. It carries
`--http3` and `--http3-only` under ADR-0144
(`Documentation/Planning/Decisions/ADR-0144-http-3-is-hand-built-over-a-hand-built-quic-and-http3-races-tcp-as-curls-ngtcp2-build-does.md`).

Namespace `Curl.Http3`. QPACK is here (BL-729, ADR-0164): `QpackEncoder` and
`QpackDecoder` over `QpackStaticTable`, `QpackDynamicTable`, `QpackPrimitives` and
`QpackRequiredInsertCount`, failing with `QpackException` and its `QpackErrorCode`. Both
work on byte spans and queue their stream instructions for the caller to take.

HTTP/3 framing is here too (BL-730, ADR-0255): `Http3Frame` and its seven subclasses
(`Http3DataFrame`, `Http3HeadersFrame`, `Http3CancelPushFrame`, `Http3SettingsFrame`,
`Http3PushPromiseFrame`, `Http3GoawayFrame`, `Http3MaxPushIdFrame`) write themselves with
`ToBytes`; `Http3FrameReader` reads them off a stream, skipping unknown and grease types:
`ReadFrameAsync` reads each frame whole under the reader's limit, and
`ReadFrameOrDataAsync` hands `DATA` payload bytes over piece by piece into the caller's
buffer, whatever the frame's length, as nghttp3 does, returning an `Http3FrameOrData`
(BL-838).
`Http3LocalUnidirectionalStreams` opens the client's control stream with curl's
`SETTINGS` and its QPACK encoder and decoder streams; `Http3PeerUnidirectionalStreams`
sorts the server's streams by type, `Http3ControlStreamReader` reads its control stream
and `Http3PeerQpackStreams` feeds its QPACK streams to `QpackDecoder` and `QpackEncoder`.
Every RFC 9114 violation is an `Http3Exception` carrying its `Http3ErrorCode`.
`Http3ClientSession` (BL-4201) is the HTTP/3 client session: over an `IMultiplexedConnection` it opens the local streams, reads the peer's (reporting its SETTINGS, extended CONNECT and GOAWAY, and keeping a peer stream failure as `ConnectionError`), and opens `Http3ClientRequestStream`s, which send HEADERS from any field list and read the response, with no curl text in either. Any caller may use them (an extension
opening an extended CONNECT stream, say); `Curl.Original.Protocol.Http.UnitLibrary`'s `Http3Session`
and `Http3StreamConnection` are curl's adapters over them, adding its log lines, messages and
exit codes (BL-731, ADR-0172, BL-4194).

## Rules

- **Base class library plus `Curl.Http2.UnitLibrary`, and nothing else.** QPACK reuses
  HPACK's Huffman code from `Curl.Http2.UnitLibrary` (ADR-0144), never the other way
  round. `Curl.Protocol.Abstractions.UnitLibrary` gives it the QUIC stream contracts
  (`IMultiplexedConnection`, BL-4201). No package, and no other project
  reference.
- **Never a reference to `Curl.Quic.UnitLibrary`.** QUIC streams arrive through the
  Abstractions contracts (ADR-0144 section 2), so HTTP/3 is tested over in-memory streams
  with no QUIC underneath.
- **Never a `Socket` or `SslStream`**, and no `HttpClient`. The caller owns the transport.
- **Time is injected.** Anything time-dependent takes a `TimeProvider`.
- **The RFCs' examples are the reference tests.** RFC 9204 appendix B's encoded field
  sections are replayed byte for byte in `Curl.Http3.UnitTests`, with the section cited
  beside each.
- Tests in `Curl.Http3.UnitTests` are platform-neutral.
- Same quality gates as every library: 100% line and branch coverage, cyclomatic
  complexity of at most 10, CRAP of at most 30.
