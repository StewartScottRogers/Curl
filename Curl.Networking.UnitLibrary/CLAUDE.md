# Curl.Networking.UnitLibrary

Phase 1.

Sockets, DNS, TLS via SslStream, proxy and SOCKS handling, connection reuse.

Never construct a `Socket`, `SslStream` or `HttpClient` here. Take `IConnection`
so the tests in the matching `.UnitTests` project can drive this code from a
recorded byte stream with no network.
