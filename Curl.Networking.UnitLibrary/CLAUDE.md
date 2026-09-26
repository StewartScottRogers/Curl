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
UDP `Socket`. The TLS provider, when it lands, is the only type that may
construct an `SslStream`. No type here constructs an `HttpClient`.

Everything else takes the Abstractions contracts (`IDnsResolver`, `ITlsProvider`,
`IConnection`, `IDatagramChannel`) or `ITcpDialer`, plus an injected `TimeProvider`, so the tests in
`Curl.Networking.UnitTests` drive every branch with fakes and no network.
`StreamConnection` wraps any `Stream`, so it is tested over a `MemoryStream`. `UdpDatagramConnector` opens channels
through an internal seam, so its tests need no socket. `UdpDatagramChannelTests` opens,
cancels and disposes local UDP sockets without sending anything. The tests that send
bytes are the loopback tests in `TcpDialerTests` and `UdpDatagramChannelTests`, tagged
`[TestCategory("Integration")]`.
