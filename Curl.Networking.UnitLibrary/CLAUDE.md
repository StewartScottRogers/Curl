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
it trusts only the certificates in that PEM file. Per ADR-0009 it behaves like the curl
build the platform usually runs, the Schannel build on Windows and the OpenSSL build
elsewhere; its internal constructor names the build so tests pin both on any platform.
The builds differ in message text, in which `--cacert` files are exit 77, and in `--capath`
(`TlsClientOptions.CaCertificateDirectory`): the OpenSSL build trusts its certificates, the
Schannel build ignores it and reports the two warning lines in `SslStreamTlsProvider.Warnings`
for the console to print. With `--cert` (`TlsClientOptions.ClientCertificate`, split into
file and passphrase by `ClientCertificateArgument` as curl splits it) it presents a client
certificate that `ClientCertificateLoader` loads: PKCS#12 in the Schannel build, PEM with
`--key` (`TlsClientOptions.PrivateKey`) in the OpenSSL build. A certificate that does not
load is exit 58; in the OpenSSL build a key that does not load is exit 43, as curl reports
it. `--ciphers` and `--tls13-ciphers` (`TlsClientOptions.Ciphers`, `Tls13Ciphers`) follow
ADR-0011: the Schannel build refuses `--ciphers` with exit 59 and ignores `--tls13-ciphers`;
the OpenSSL build turns both into one `CipherSuitesPolicy` through `OpenSslCipherSuites`,
the hand-written OpenSSL-name table, and a list naming no known suite is exit 59. On
Windows `CipherSuitesPolicy` cannot be constructed, so there the OpenSSL build (reached
only from tests) reports exit 59 instead of throwing.
The messages for its exit 35, exit 43, exit 58, exit 59, exit 60 and exit 77 live in
`TlsFailureMessages` and nowhere else; the `More details here` block after an exit 60 is
the console's to print. No type here constructs an `HttpClient`.

`TcpConnector` fills `ConnectResult.Timings` and `LocalEndPoint` per ADR-0030: it takes
`Started`, `NameResolved` and `Connected` from its `TimeProvider`, the local end point from
the `DialedTcpConnection` that `ITcpDialer` returns, and `TlsHandshakeCompleted` from the
timings `SslStreamTlsProvider` reports on its own `TimeProvider`.

`TcpConnector` tunnels through `ConnectTarget.Proxy` when it is an HTTP proxy
(`ProxyKind.Http`, `Http10`) per ADR-0023: `HttpProxyTunnel` writes curl 8.21.0's CONNECT
request (its `User-Agent` and credential encoding from `HttpProxyTunnelOptions`) and reads
the reply one byte at a time, so the tunnel's bytes stay on the connection; TLS then runs
over the tunnel for an https target. HTTPS and SOCKS proxies throw `NotSupportedException`
until their tasks land.

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
