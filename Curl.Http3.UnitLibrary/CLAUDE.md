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

HTTP/3 framing is here too (BL-730, ADR-0165): `Http3Frame` and its seven subclasses
(`Http3DataFrame`, `Http3HeadersFrame`, `Http3CancelPushFrame`, `Http3SettingsFrame`,
`Http3PushPromiseFrame`, `Http3GoawayFrame`, `Http3MaxPushIdFrame`) write themselves with
`ToBytes`; `Http3FrameReader` reads them off a stream, skipping unknown and grease types.
`Http3LocalUnidirectionalStreams` opens the client's control stream with curl's
`SETTINGS` and its QPACK encoder and decoder streams; `Http3PeerUnidirectionalStreams`
sorts the server's streams by type, `Http3ControlStreamReader` reads its control stream
and `Http3PeerQpackStreams` feeds its QPACK streams to `QpackDecoder` and `QpackEncoder`.
Every RFC 9114 violation is an `Http3Exception` carrying its `Http3ErrorCode`. Request
streams (sending a request, reading a response) are the HTTP handler's:
`Curl.Protocol.Http.UnitLibrary`'s `Http3StreamConnection` builds them from these frames and
QPACK (BL-731, ADR-0172).

## Rules

- **Base class library plus `Curl.Http2.UnitLibrary`, and nothing else.** QPACK reuses
  HPACK's Huffman code from `Curl.Http2.UnitLibrary` (ADR-0144), never the other way
  round. `Curl.Protocol.Abstractions.UnitLibrary` is added by
  the first task that needs its stream contracts. No package, and no other project
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
