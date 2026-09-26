# Curl.Protocol.Http.UnitLibrary

Phase 1.

HTTP/1.0, HTTP/1.1, HTTP/2 and HTTP/3. Also serves ipfs and ipns, which curl rewrites into HTTP gateway requests.

**URL schemes:** `http`, `https`, `ipfs`, `ipns`

This library may reference `Curl.Protocol.Abstractions.UnitLibrary` and nothing
else horizontal. Referencing another protocol library is a build break, and
`Curl.Protocol.Abstractions.UnitTests` fails if one appears.

Never construct a `Socket`, `SslStream` or `HttpClient` here. Take `IConnection`
so the tests in the matching `.UnitTests` project can drive this code from a
recorded byte stream with no network.
