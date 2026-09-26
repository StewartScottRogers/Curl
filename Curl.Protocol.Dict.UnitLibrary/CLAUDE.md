# Curl.Protocol.Dict.UnitLibrary

Phase 4.

Dictionary lookups.

**URL schemes:** `dict`

This library may reference `Curl.Protocol.Abstractions.UnitLibrary` and nothing
else horizontal. Referencing another protocol library is a build break, and
`Curl.Protocol.Abstractions.UnitTests` fails if one appears.

Never construct a `Socket`, `SslStream` or `HttpClient` here. The seam is
`IConnector` (ADR-0005): `DictProtocolHandler` takes one in its constructor and
asks it for the `IConnection` to each URL's host and port (2628 by default), so
the tests in the matching `.UnitTests` project drive this code from a scripted
byte stream with no network.

## Layout

- `DictProtocolHandler` runs the transfer: connects, sends the request whole,
  and writes everything the server sends to `Output` until it closes.
- `DictRequest` encodes a URL path as the request bytes (`CLIENT`, one `DEFINE`,
  `MATCH` or raw command line, `QUIT`); it is pure, with no I/O. Its
  `ClientLine` constant names `libcurl 8.21.0`, as upstream sends it.

Every byte these classes send was measured against curl 8.21.0; the captures are
in BL-041's Notes and pinned by `DictProtocolHandlerTests`. Change behaviour only
against a new measurement.
