# Curl.Http3.UnitLibrary

A hand-built HTTP/3 client layer: QPACK header compression (RFC 9204) and HTTP/3
framing (RFC 9114) - frames, the control and QPACK encoder and decoder streams,
`SETTINGS` and `GOAWAY` - over QUIC streams it is given as byte streams. It carries
`--http3` and `--http3-only` under ADR-0144
(`Documentation/Planning/Decisions/ADR-0144-http-3-is-hand-built-over-a-hand-built-quic-and-http3-races-tcp-as-curls-ngtcp2-build-does.md`).

Namespace `Curl.Http3`. QPACK is here (BL-729, ADR-0163): `QpackEncoder` and
`QpackDecoder` over `QpackStaticTable`, `QpackDynamicTable`, `QpackPrimitives` and
`QpackRequiredInsertCount`, failing with `QpackException` and its `QpackErrorCode`. Both
work on byte spans and queue their stream instructions for the caller to take; nothing
reads or writes a stream yet. The frame, control stream and QPACK stream layer lands with
BL-730.

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
