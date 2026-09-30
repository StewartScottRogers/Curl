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

Diagnostic log (`--log-level`, ADR-0222, BL-929): `RtspTransferLog` writes under the
`rtsp` component - the request's method and `CSeq` and the reply's status, `CSeq` and
session ID (`info`), each reply header's name (`verbose`), a head the server closed before
its blank line (`warning`) - and, for every transfer, its end: bytes and milliseconds at
`info`, or its `CurlExitCode` at `error`. Header values and credentials are never written.
