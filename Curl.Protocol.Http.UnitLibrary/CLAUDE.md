# Curl.Protocol.Http.UnitLibrary

Phase 1.

HTTP/1.0, HTTP/1.1, HTTP/2 and HTTP/3. Also serves ipfs and ipns, which curl rewrites into HTTP gateway requests.

**URL schemes:** `http`, `https`, `ipfs`, `ipns`

This library may reference `Curl.Protocol.Abstractions.UnitLibrary` and, of the
hand-built libraries ADR-0120 lets a protocol use, `Curl.Http2.UnitLibrary` (HPACK and
the HTTP/2 frame layer) and `Curl.Http3.UnitLibrary` (QPACK and the HTTP/3 frame layer,
allowed by BL-668) and `Curl.Zstandard.UnitLibrary` (the Zstandard decoder behind
`Content-Encoding: zstd`, BL-861, ADR-0287); nothing else horizontal. Never `Curl.Quic.UnitLibrary`: QUIC arrives
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

For `--http2` (`HttpVersionPreference.Http2`) over cleartext (BL-716), the HTTP/1.1
request asks to upgrade to h2c (`Upgrade: h2c`, `HTTP2-Settings`, `Connection: Upgrade,
HTTP2-Settings`) and runs on an `HttpH2cUpgradeConnection`: a response that is not
`HTTP/1.1 101` is read through unchanged, and after a `101` head the connection sends the
client preface and reads the response from stream 1 through an `Http2StreamConnection`.
Its `Http2Session` then carries the transfer's later requests (BL-866): a 401 retry goes out
as HEADERS on stream 3 of the same connection, the session is handed to the connection like
any HTTP/2 session so the next URL continues it, and `-v` ends with `left intact`. Stream 1
is opened half closed (`Http2Connection.OpenUpgradedStream`) and read to its end, so it
closes with its response; a body the handler ignores (the 401 before a retry) is not read
but reset with STREAM_CLOSED, as on any HTTP/2 stream (BL-970, ADR-0343). The first stream
opened after the upgrade is preceded by curl's extra SETTINGS, INITIAL_WINDOW_SIZE 65536.

HTTP/3 (BL-731, ADR-0172) does the same over QUIC: for `--http3-only`, and for `--http3`
before falling back to TCP, the handler asks the connector for an `IMultiplexedConnection`
(`IConnector.ConnectMultiplexedSessionAsync`) and wraps it in an `Http3Session`, which a pooling connector shares between `-Z` transfers up to the server's MAX_STREAMS (ADR-0338), which opens the
client's control and QPACK streams and hands out an `Http3StreamConnection` per request
(`HTTP/3 200 \r\n`, the headers, the empty line, then the DATA). `IHttpStreamSession` and
`IHttpStreamConnection` are what the handler sees of either version. The session also reads
the server's control and QPACK streams in the background for the connection's life
(BL-836, ADR-0172 section 8): a `GOAWAY` stops new requests on it, and a broken control or
QPACK stream fails the transfer's next read with exit 56. Tests drive it with
`Fakes/FakeMultiplexedConnection` and `Fakes/FakeMultiplexedStream`.
