# Curl.Protocol.Http.UnitLibrary

Phase 1.

HTTP/1.0, HTTP/1.1, HTTP/2 and HTTP/3. Also serves ipfs and ipns, which curl rewrites into HTTP gateway requests.

**URL schemes:** `http`, `https`, `ipfs`, `ipns`

This library may reference `Curl.Protocol.Abstractions.UnitLibrary` and, of the
hand-built libraries ADR-0120 lets a protocol use, `Curl.Http2.UnitLibrary` (HPACK and
the HTTP/2 frame layer) and `Curl.Http3.UnitLibrary` (QPACK and the HTTP/3 frame layer,
allowed by BL-668); nothing else horizontal. Never `Curl.Quic.UnitLibrary`: QUIC arrives
as an `IMultiplexedConnection` from the connector. Referencing another protocol library
is a build break, and `Curl.Protocol.Abstractions.UnitTests` fails if one appears.

Never construct a `Socket`, `SslStream` or `HttpClient` here. Take `IConnection`
so the tests in the matching `.UnitTests` project can drive this code from a
recorded byte stream with no network.

HTTP/2 (BL-658, ADR-0159) runs over the same `IConnection`: `Http2Session` holds one connection's
`Http2Connection` and HPACK contexts, and `Http2StreamConnection` presents one request's
stream to the HTTP/1.1 head and body readers as the bytes curl's own HTTP/2 layer
writes - `HTTP/2 200 \r\n`, the headers as received, the empty line, then the DATA - so
output, `-i`/`-D`, `-f`, redirects, authentication, cookies and progress are shared.

HTTP/3 (BL-731, ADR-0172) does the same over QUIC: for `--http3-only`, and for `--http3`
before falling back to TCP, the handler asks the connector for an `IMultiplexedConnection`
(`IConnector.ConnectMultiplexedAsync`) and wraps it in an `Http3Session`, which opens the
client's control and QPACK streams and hands out an `Http3StreamConnection` per request
(`HTTP/3 200 \r\n`, the headers, the empty line, then the DATA). `IHttpStreamSession` and
`IHttpStreamConnection` are what the handler sees of either version. Tests drive it with
`Fakes/FakeMultiplexedConnection` and `Fakes/FakeMultiplexedStream`.
