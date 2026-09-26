# Curl.Protocol.Rtsp.UnitLibrary

Phase 5.

Real Time Streaming Protocol. HTTP-like syntax, stateful sessions with CSeq tracking.

**URL schemes:** `rtsp`

This library may reference `Curl.Protocol.Abstractions.UnitLibrary` and nothing
else horizontal. Referencing another protocol library is a build break, and
`Curl.Protocol.Abstractions.UnitTests` fails if one appears.

Never construct a `Socket`, `SslStream` or `HttpClient` here. Take `IConnection`
so the tests in the matching `.UnitTests` project can drive this code from a
recorded byte stream with no network.
