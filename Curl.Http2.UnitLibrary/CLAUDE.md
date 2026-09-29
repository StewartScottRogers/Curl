# Curl.Http2.UnitLibrary

A hand-built HTTP/2 client layer: HPACK header compression (RFC 7541) and HTTP/2
framing (RFC 9113) over a byte stream. The BCL's HPACK and HTTP/2 framing are internal
to `System.Net.Http`, so they are built here. ADR-0141
(`Documentation/Planning/Decisions/ADR-0141-http-2-is-hand-built-and-accepted-everywhere-with-h2-offered-by-default-off-windows.md`)
decides what HTTP/2 does and when Curl speaks it.

Namespace `Curl.Http2`. What is here so far: HPACK (BL-656) - `HpackEncoder` and
`HpackDecoder` over `HeaderField` lists, the public `HpackHuffman` codec, and
`HpackDecodingException` carrying an `HpackDecodingError`. The encoder chooses each
field's representation as nghttp2's deflater does (ADR-0148).

The frame layer (BL-657): `Http2FrameCodec` reads and writes frames on a stream,
`Http2FrameFactory` creates each frame type and `Http2FramePayloadParser` reads and
validates each payload. `Http2Connection` drives one client connection over a stream the
caller owns: `SendPrefaceAsync` sends the preface, SETTINGS and connection WINDOW_UPDATE
curl sends (measured, pinned in `Http2ConnectionTests`), `OpenStream` allocates odd
stream identifiers (`OpenUpgradedStream` opens an h2c upgrade's stream 1 half closed by the
client, BL-866), `WriteHeadersAsync` and `WriteDataAsync` split into frames within the
peer's frame size and flow-control windows, and `ReadStreamFrameAsync` returns DATA and
whole header blocks while answering SETTINGS and PING and applying WINDOW_UPDATE itself;
`ReadFrameAsync` handles exactly one frame, so a sender waiting for window can take in
WINDOW_UPDATE, and `IsClosedByPeer` tells when the peer closed between frames.
Failures are typed: `Http2ProtocolException` (a connection error; GOAWAY already sent),
`Http2StreamResetException` (the peer's RST_STREAM) and `Http2GoAwayException` (the
peer's GOAWAY). The request and response on a stream are in `Curl.Protocol.Http.UnitLibrary`
(`Http2Session`, `Http2StreamConnection`; BL-658, ADR-0159).

## Rules

- **Base class library only.** It may reference `Curl.Protocol.Abstractions.UnitLibrary`;
  nothing else. `Curl.Protocol.Http.UnitLibrary` references this library (ADR-0120) and
  `Curl.Http3.UnitLibrary` reuses its Huffman codec for QPACK, never the other way round.
- **Never a `Socket` or `SslStream`**, and no `HttpClient`. Frames are read from and
  written to an injected stream, and the caller owns the transport (`IConnection` in
  `Curl.Protocol.Http.UnitLibrary`), so tests replay recorded bytes.
- **Time is injected.** Anything time-dependent (`PING` round trips, `SETTINGS`
  acknowledgement timeouts) takes a `TimeProvider`.
- **The RFCs' examples are the reference tests.** RFC 7541 appendix C's header blocks
  are replayed byte for byte in `Curl.Http2.UnitTests`, with the section cited beside
  each one. Tests are platform-neutral and never open a socket.
- Same quality gates as every library: 100% line and branch coverage, cyclomatic
  complexity of at most 10, CRAP of at most 30.
