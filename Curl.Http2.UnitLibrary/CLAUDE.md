# Curl.Http2.UnitLibrary

A hand-built HTTP/2 client layer: HPACK header compression (RFC 7541) and HTTP/2
framing (RFC 9113) over a byte stream. The BCL's HPACK and HTTP/2 framing are internal
to `System.Net.Http`, so they are built here. ADR-0141
(`Documentation/Planning/Decisions/ADR-0141-http-2-is-hand-built-and-accepted-everywhere-with-h2-offered-by-default-off-windows.md`)
decides what HTTP/2 does and when Curl speaks it.

Namespace `Curl.Http2`. What is here so far: HPACK (BL-656) - `HpackEncoder` and
`HpackDecoder` over `HeaderField` lists, the public `HpackHuffman` codec, and
`HpackDecodingException` carrying an `HpackDecodingError`. The encoder chooses each
field's representation as nghttp2's deflater does (ADR-0148). The frame layer, connection
preface and `SETTINGS` come with BL-657, and a request and response on a stream with
BL-658.

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
