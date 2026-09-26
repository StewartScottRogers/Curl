# Curl.Networking.UnitLibrary

Phase 1.

Sockets, DNS, TLS via SslStream, proxy and SOCKS handling, connection reuse: the
production implementations of the transport contracts in
`Curl.Protocol.Abstractions.UnitLibrary` (ADR-0005). It references that project and
nothing else.

This is the one project allowed to construct a `Socket`, and only inside a dialer:
today `TcpDialer` (behind `ITcpDialer`) is the only type that constructs a `Socket`
or a `NetworkStream`. The TLS provider, when it lands, is the only type that may
construct an `SslStream`. No type here constructs an `HttpClient`.

Everything else takes the Abstractions contracts (`IDnsResolver`, `ITlsProvider`,
`IConnection`) or `ITcpDialer`, plus an injected `TimeProvider`, so the tests in
`Curl.Networking.UnitTests` drive every branch with fakes and no network.
`StreamConnection` wraps any `Stream`, so it is tested over a `MemoryStream`. The
only test that opens a socket is the loopback test in `TcpDialerTests`, tagged
`[TestCategory("Integration")]`.
