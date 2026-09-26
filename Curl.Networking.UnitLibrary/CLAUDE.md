# Curl.Networking.UnitLibrary

Phase 1.

Sockets, DNS, TLS via SslStream, proxy and SOCKS handling, connection reuse: the
production implementations of the transport contracts in
`Curl.Protocol.Abstractions.UnitLibrary` (ADR-0005). It references that project and
nothing else.

This is the one project allowed to construct a `Socket`, and only inside a transport
type: `TcpDialer` (behind `ITcpDialer`) is the only type that constructs a TCP
`Socket` or a `NetworkStream`, and `UdpDatagramChannel` (opened by
`UdpDatagramConnector`, behind `IDatagramConnector`) the only one that constructs a
UDP `Socket`. `SslStreamTlsProvider` (behind `ITlsProvider`, configured by
`TlsClientOptions`) is the only type that constructs an `SslStream`; it runs the
handshake over the plaintext `IConnection` through the internal `ConnectionStream`
adapter and returns an `SslStreamConnection`. With `--cacert` (`TlsClientOptions.CaCertificateFile`)
it trusts only the certificates in that PEM file. The messages for its exit 35, exit 60 and
exit 77 live in `TlsFailureMessages` and nowhere else. No type here constructs an `HttpClient`.

Everything else takes the Abstractions contracts (`IDnsResolver`, `ITlsProvider`,
`IConnection`, `IDatagramChannel`) or `ITcpDialer`, plus an injected `TimeProvider`, so the tests in
`Curl.Networking.UnitTests` drive every branch with fakes and no network.
`StreamConnection` wraps any `Stream`, so it is tested over a `MemoryStream`. `UdpDatagramConnector` opens channels
through an internal seam, so its tests need no socket. `UdpDatagramChannelTests` opens,
cancels and disposes local UDP sockets without sending anything.
`SslStreamTlsProviderTests` runs real handshakes against a server-side `SslStream` over
the in-memory `Fakes/InMemoryDuplexStream` pair, with a self-signed certificate made in
the test, so TLS is tested without a socket. The tests that send
bytes are the loopback tests in `TcpDialerTests` and `UdpDatagramChannelTests`, tagged
`[TestCategory("Integration")]`.
