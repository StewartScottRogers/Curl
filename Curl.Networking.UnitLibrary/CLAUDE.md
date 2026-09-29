# Curl.Networking.UnitLibrary

Phase 1.

Sockets, DNS, TLS via SslStream, proxy and SOCKS handling, connection reuse: the
production implementations of the transport contracts in
`Curl.Protocol.Abstractions.UnitLibrary` (ADR-0005). It references that project and
`Curl.Tls.UnitLibrary`, the hand-built TLS client (ADR-0120, ADR-0140), and nothing else.

Per ADR-0140 and ADR-0162 (BL-708) there are two TLS providers, and `TlsClientRouting.Choose`
picks one from a `TlsClientOptions` as one pure function: `HandBuiltTlsProvider` when a row of
ADR-0140's table holds (today only a `MaximumVersion` of TLS 1.0 or 1.1; each option task adds
its row and a data row in `TlsClientRoutingTests`), `SslStreamTlsProvider` otherwise. Both
implement `ITlsProviderWithWarnings`. `HandBuiltTlsProvider` runs `Tls13ClientConnection` for a
range reaching TLS 1.3 and `Tls12ClientConnection` below it over the internal `ConnectionStream`,
and returns a `HandBuiltTlsConnection`. Both providers judge the server's certificate with
`ServerCertificateVerification` (trust anchors, tolerated chain errors, each build's name check,
exit 60 and 77); the hand-built path reaches it through `HandBuiltCertificateVerifier`, which
builds the chain and the `SslPolicyErrors` `SslStream` would. Both load `--cert` through
`ClientCertificateLoader.Load`. The hand-built path's other exit 35 texts are
`TlsFailureMessages.SchannelHandBuiltHandshakeFailure` and `OpenSslHandBuiltHandshakeFailure`.
Its tests run it against a server-side `SslStream` over `Fakes/InMemoryDuplexStream`, TLS 1.3
excluded on macOS, and compare each failure with `SslStreamTlsProvider`'s.

This is the one project allowed to construct a `Socket`, and only inside a transport
type: `TcpDialer` (behind `ITcpDialer`) and `TcpConnectionListener` with its
`TcpPendingConnection` (behind `IConnectionListener`) are the only types that construct a TCP
`Socket` or a `NetworkStream` (`TcpDialer` also connects the Unix domain socket, BL-507), and `UdpDatagramChannel` (opened by
`UdpDatagramConnector`, behind `IDatagramConnector`, and by `DnsSocketOpener`) the only one that constructs a
UDP `Socket`; `DnsSocketOpener` (behind `IDnsSocketOpener`) also constructs the TCP `Socket` and
`NetworkStream` a truncated DNS reply is asked again over. `SslStreamTlsProvider` (behind `ITlsProvider`, configured by
`TlsClientOptions`) is the only type that constructs an `SslStream`; it runs the
handshake over the plaintext `IConnection` through the internal `ConnectionStream`
adapter and returns an `SslStreamConnection`. With `--cacert` (`TlsClientOptions.CaCertificateFile`)
it trusts only the certificates in that PEM file; there the Schannel build also checks
revocation below the root unless `TlsClientOptions.SkipRevocationCheck` (`--ssl-no-revoke`)
is set, and names the first of a certificate out of date, an incomplete chain, an untrusted
root and an unknown revocation status (ADR-0086, BL-368). There too the Schannel build accepts
a certificate with no DNS subjectAltName whose CN matches the host, as `SchannelCommonNameCheck`
matches it the way curl's `Curl_cert_hostcheck` does (ADR-0103, BL-415). Against the system store a
certificate that is only out of date is exit 35 with `SEC_E_CERT_EXPIRED`. Per ADR-0009 it behaves like the curl
build the platform usually runs, the Schannel build on Windows and the OpenSSL build
elsewhere; its internal constructor names the build so tests pin both on any platform.
The builds differ in message text, in which `--cacert` files are exit 77, and in `--capath`
(`TlsClientOptions.CaCertificateDirectory`): the OpenSSL build trusts its certificates, the
Schannel build ignores it and reports its one warning, unwrapped, in `SslStreamTlsProvider.Warnings`
for the console to print wrapped at the terminal width (two lines at curl's default 79 columns). With `--cert` (`TlsClientOptions.ClientCertificate`, split into
file and passphrase by `ClientCertificateArgument` as curl splits it) it presents a client
certificate that `ClientCertificateLoader` loads: PKCS#12 in the Schannel build, PEM with
`--key` (`TlsClientOptions.PrivateKey`) in the OpenSSL build. Per ADR-0066 the Schannel build
first reads the value as a store path (`CurrentUser\MY\<thumbprint>`, parsed by
`ClientCertificateStorePath`) and finds the certificate in the store `IClientCertificateStore`
opens (`SystemClientCertificateStore`, through `X509Store`, in production; a fake in tests). A certificate that does not
load is exit 58; in the OpenSSL build a key that does not load is exit 43, as curl reports
it. `--ciphers` and `--tls13-ciphers` (`TlsClientOptions.Ciphers`, `Tls13Ciphers`) follow
ADR-0011: the Schannel build refuses `--ciphers` with exit 59 and ignores `--tls13-ciphers`;
the OpenSSL build turns both into one `CipherSuitesPolicy` through `OpenSslCipherSuites`,
the hand-written OpenSSL-name table, and a list naming no known suite is exit 59. On
Windows `CipherSuitesPolicy` cannot be constructed, so there the OpenSSL build (reached
only from tests) reports exit 59 instead of throwing. The provider builds the policy through
`ICipherSuitesPolicyFactory` (`CipherSuitesPolicyFactory.ForThisPlatform` in production, whose
`[UnsupportedOSPlatformGuard]` property satisfies CA1416) and runs the handshake through its
`AuthenticateSslStreamAsClientAsync` step; both are internal `init` seams, so the tests follow
the policy into `SslClientAuthenticationOptions` on Windows too (BL-268).
Per ADR-0138 (BL-502) the handshake offers every TLS version from `TlsClientOptions.MinimumVersion`
to `MaximumVersion` (`TlsVersion`; `--tlsv1.x`, `--tls-max`), as `TlsVersionRange` computes it,
the one place here that names the obsolete TLS 1.0 and 1.1 members; a minimum above the ceiling
throws, since the parser refuses it. With a ceiling of TLS 1.0 or 1.1 the Schannel build reports
any security status as `failed to receive handshake`, as curl's did in every measured case.
The messages for its exit 35, exit 43, exit 58, exit 59, exit 60 and exit 77 live in
`TlsFailureMessages` and nowhere else; the `More details here` block after an exit 60 is
the console's to print. No type here constructs an `HttpClient`.

`TcpConnector` fills `ConnectResult.Timings` and `LocalEndPoint` per ADR-0030: it takes
`Started`, `NameResolved` and `Connected` from its `TimeProvider`, the local end point from
the `DialedTcpConnection` that `ITcpDialer` returns, and `TlsHandshakeCompleted` from the
timings `SslStreamTlsProvider` reports on its own `TimeProvider`. Per ADR-0054 the provider
also reports `ConnectResult.PeerCertificates`, the DER of every certificate the server sent
(its own first, then the validation callback's `ChainPolicy.ExtraStore` in the order sent),
and `TcpConnector` passes them on. Per ADR-0085's BL-404 amendment a successful handshake is
also reported as a `TlsHandshakeEvent` through the provider's four-argument overload
(`IHandshakeReportingTlsProvider`), to which `TcpConnector` passes the target's `Events`; the
event's `CertificateVerifyResult` is OpenSSL's `X509_V_` code as `OpenSslVerifyResult` maps it.
Per BL-452 the provider reports a `TlsTrustEvent` before each handshake, once the cipher suites
and client certificate are ready (`-k`, the `--cacert` file or else the reference build's
default bundle name `/cacert.pem`, which is named but never read, and `--capath`), sets the
event's `VerifiedHostName` (the host without IPv6 brackets, `null` with `-k`) and `IsProxy`;
`TcpConnector` reports the HTTPS proxy's handshake, and a forward proxy's, with `IsProxy` set
through `IHandshakeReportingTlsProvider`'s `isProxy` argument. `SslStream` exposes no TLS
records, so no `TlsMessageEvent` is reported. Per ADR-0124 the origin handshake of an `https://`
transfer offers `http/1.1` through ALPN (`TcpConnector.ApplicationProtocolsFor`), none under `--no-alpn`
(`TlsClientOptions.UseAlpn`); the Schannel build under `--ssl-revoke-best-effort`
(`TlsClientOptions.RevocationCheckBestEffort`) accepts a `--cacert` chain whose only faults are an
unknown or offline revocation status; and `TcpDialer` sets `TCP_NODELAY` and `SO_KEEPALIVE` from
`TcpSocketOptions` (`--no-tcp-nodelay`, `--no-keepalive`) in `ApplySocketOptions`, which unit tests measure.
Per ADR-0100 it also reports curl's `-v` connect lines on the target's `Events`: `Trying` before
each dial, `connect to ... failed: <reason>` after each failed one (the reason from
`ConnectFailureReason`), the exit 7 message, and `ReportConnectionOpened` once any tunnel and
TLS handshake are done, numbering its connections from `0` in `ConnectResult.ConnectionNumber`.
Per ADR-0109 a connect that fails after its options parse takes the next number too, through
`NumberedConnectFailure`, as curl 8.21.0 numbers the connection it tried.
Per ADR-0113 it keeps curl's DNS cache for its life (one command line): a host and port it
resolved before, or one a `--resolve` entry answers, is answered without `IDnsResolver` and
reported as `Hostname H was found in DNS cache` before `Trying`. Per ADR-0114 every answer is
then reported as `Host H:P was resolved.`, `IPv6: ...` and `IPv4: ...`, naming the host as
cached (none for an IP address), and `LoadResolveEntries` loads the `--resolve` entries
(`ResolveOverrides.Entries`, as `ResolveEntry`) into the cache with curl's `Added H:P:A to DNS
cache` lines at a transfer's start; until a transfer calls it, the first connect loads them.
Per ADR-0117 (BL-510) each `ConnectAsync` runs its resolve, dials, tunnel and TLS handshakes under
one limit, the constructor's `connectTimeout` (`CurlComposition.ConnectTimeoutOf`: `--connect-timeout`,
or a smaller `-m`), else `DefaultConnectTimeout` (300 s), on its `TimeProvider`; when it passes the
connect fails with exit 28 and `Connection timed out after N milliseconds`, N from the connect's
start, also reported as a `-v` line. A cancellation arriving once the limit has passed is that
failure; an earlier one escapes. Tests stall through `Fakes/StallingTcpDialer`,
`StallingTlsProvider` and `StallingConnection` and fire the limit with `ManualTimeProvider.Advance`.
Per ADR-0143 (BL-500) `TcpConnector` and `UdpDatagramConnector` take the `-4`/`-6` choice as an
`AddressFamily` (`Unspecified` for either): `AddressFamilyFilter` keeps a name's addresses of that
family only, and leaves an IP address literal alone, as curl 8.21.0 does. A name left with none is
exit 6 (exit 5 for a proxy). A looked-up answer is cached and reported with the one family;
`localhost` and `--resolve` entries are reported whole, and an entry left empty is reported as
`Negative DNS entry`.

`TcpConnector` tunnels through `ConnectTarget.Proxy` when it is an HTTP proxy
(`ProxyKind.Http`, `Http10`) per ADR-0023: `HttpProxyTunnel` writes curl 8.21.0's CONNECT
request (its `User-Agent`, credential encoding and `--proxy-header` values from `HttpProxyTunnelOptions`, ADR-0077) and reads
the reply one byte at a time, so the tunnel's bytes stay on the connection; TLS then runs
over the tunnel for an https target. Through a SOCKS proxy (`Socks4`, `Socks4a`, `Socks5`,
`Socks5Hostname`) `SocksProxyTunnel` runs curl 8.21.0's handshake, measured byte for byte
(BL-213): `Socks4Handshake` resolves the target locally and sends its first IPv4 address,
SOCKS4a sends the host as written; `Socks5Handshake` offers no authentication and GSSAPI (and
user name and password with a credential), resolves locally for SOCKS5 and sends the name for
SOCKS5h. Every read takes exactly the reply's bytes, so the tunnel's bytes stay on the
connection. A refused or cut-short handshake is exit 97 with curl's message; GSSAPI is offered
but not implemented, so a proxy that picks it fails with the message the reference build's SSPI
printed. ADR-0084 records these choices (first address, literals, UTF-8, disposal). Through an HTTPS proxy (`Https`, BL-266) TLS runs to the proxy host first, then the same CONNECT
over it, then TLS to the target inside that; each handshake failure is the TLS provider's result.
The handshake to the proxy runs through the proxy's `ITlsProvider` (the `--proxy-*` TLS options,
ADR-0095), and so does the handshake to an HTTPS forward proxy (`ConnectTarget.IsForwardProxy`
with `UseTls`), which curl 8.21.0 verifies with `--proxy-insecure` and never `-k` (measured, BL-441).

`TcpConnector` applies `--resolve` through `ResolveOverrides` and `--connect-to` through
`ConnectToMappings`, both built from the verbatim option values and parsed as curl 8.21.0
parses them (measured; BL-214). The first `--connect-to` mapping matching the URL's host
and port gives the `ConnectDestination` that is resolved, dialled and named in the CONNECT
request; TLS still verifies the URL's host. A `--resolve` entry for the host and port being
resolved, the proxy's included, answers in place of `IDnsResolver`. An entry or a matching
mapping that does not parse fails the connect with exit 49 and curl's message.

`SystemDnsResolver` reports a host `Dns` refuses as over 255 characters as not resolved, so it
is exit 6 (exit 5 for a proxy) as in curl 8.21.0, which accepts hosts up to 65535 bytes. Every
`Could not resolve host:` and `Could not resolve proxy:` message goes through `CurlErrorBuffer`,
which cuts it to 255 characters as curl's 256-byte error buffer does (ADR-0072).

`TcpConnectionListener` is FTP active mode's listener (ADR-0102, BL-456): it binds the
`ListenTarget` address on each port of its range in turn and listens on the first it can bind.
Every failure is exit 30 with curl 8.21.0's `lib/ftp.c` message: a port in use or not permitted
moves on to the next, and a range with no free port is `bind() failed, ran out of ports`
(measured); an address that is not local (`EADDRNOTAVAIL`) is
`bind(port=<port>) on non-local address failed: <reason>`, the `-v` line curl prints before it
binds again on the control connection's address, which the FTP handler tells apart and retries
on (ADR-0107, BL-464); any other bind error is `bind(port=<port>) failed: <reason>`, and a socket
that cannot be opened or put to listening is `socket failure: <reason>`, reached in tests through
the internal `OpenSocket` and `StartListening` seams.
`TcpPendingConnection.AcceptAsync` returns the accepted socket as a `StreamConnection`; a failed
accept is exit 10, `Error accept()ing server connect: <reason>`, as curl's `lib/cf-socket.c`
words it. Every `<reason>` is `ConnectFailureReason`'s. Every TCP connection reports
`IConnection.LocalEndPoint`: `StreamConnection` carries the socket's, and `SslStreamConnection`
and `PooledConnection` forward the one underneath, so FTP's `-P -` can announce the control
connection's own address.

`PoolingConnector` wraps another `IConnector` and keeps connections for reuse per ADR-0050.
Every connection it returns is a `PooledConnection`; one marked with `MarkReusable` goes back
to the pool on dispose, anything else closes. `ConnectionPoolKey` (with
`ConnectionPoolProxyKey`) is the key: `PoolScheme`, host ignoring case, port, TLS choice,
`IsForwardProxy` and tunnelling proxy with its credential by value; a target without `PoolScheme` is never pooled.
The pool holds at most five idle connections in total, closing the oldest with curl's
`Connection pool is full` and `shutting down connection #N` lines on the returning target's
`Events`, and drops one idle longer than 118 seconds on its `TimeProvider`. It numbers
connections from `0` (`ConnectResult.ConnectionNumber`), the inner connector's failures
included (ADR-0109), and returns a reused one with
`IsReused`, its original local end point and certificates, and no timings. A reused
connection is reported `with proxy` (`ConnectionReusedEvent.IsProxy`) when the target is a
forward proxy (`ConnectTarget.IsForwardProxy`) or tunnels through one, naming the proxy's host
and port for a tunnel, as curl 8.21.0 prints it (BL-360).

Per ADR-0149 (BL-507) `TcpConnector` takes an optional `UnixSocketAddress` (`--unix-socket`,
`--abstract-unix-socket`, whose name starts with a NUL). With one, every connect dials it through
`ITcpDialer.DialUnixSocketAsync` in place of the host, port and proxy, resolving nothing, then runs
TLS to the URL's host when asked. `-v` shows curl 8.21.0's Windows lines on every platform: `Trying
<name>:0...`, and for a failure `Immediate connect fail for <name>: <reason>` and `connect to <name>
port 0 from  port 0 failed: <reason>` before exit 7 `Failed to connect to <host>:<port> over
unix://<path> after N ms: Could not connect to server`; `<name>` is `UnixSocketAddress.RemoteIpText`,
the path cut to 45 characters (empty for an abstract name). A success is reported opened with the
path as the host and `ConnectionOpenedEvent.UnixSocketRemoteIp`. A path too long for `sun_path`
(108 bytes, 104 on macOS, with its NUL) is exit 6 `Unix socket path too long: '<path>'`. Pools are
per option group, so different sockets never share a connection.

The DNS-over-HTTPS message codec (ADR-0152, BL-640) is pure code, bytes in and bytes out, as
curl 8.21.0's `lib/doh.c` does it. `DnsQueryEncoder` writes the measured query (ID 0, flags
`0x0100`, one question, QCLASS IN) for a `DnsRecordType`, refusing an empty label or one over 63
bytes and a query over 272 bytes. `DnsAnswerDecoder` returns a `DnsAnswer`: the addresses of the
type asked for (at most 24), the CNAME targets followed through compression pointers (at most 4),
the smallest TTL, or a `DnsMessageFailure`, a pointer loop ending as `LabelLoop` after 128 steps.
`DnsMessageFailureText` gives curl's `--trace-config doh` text for each failure. For an SRV query
the decoder also keeps each SRV record as `DnsAnswer.ServiceRecords` (`DnsServiceRecord`).

Per ADR-0170 (BL-694) `DnsServerResolver` is the hand-built DNS client behind `--dns-servers`,
`--dns-interface`, `--dns-ipv4-addr` and `--dns-ipv6-addr`, measured against curl 8.22.0's c-ares
1.34.8 build. It takes the options verbatim (`DnsServerResolverOptions`) and parses them when it
resolves: `DnsServerList` for the list, `DnsSourceBinding` for the bind addresses; one that does not
parse is `DnsLookupFailure.BadConfiguration`, exit 43. Without `--dns-servers` it asks
`SystemDnsServers`. It sends AAAA and A at once (one family under `-4`/`-6`), each built by
`DnsServerQuery` (the encoder's question plus an ID and an EDNS OPT record with a client cookie
over UDP), to the servers in list order for `Rounds` rounds, waiting `FirstTimeout` doubled each
round on its `TimeProvider`; a truncated reply is asked again over TCP. Replies are matched
(`DnsReplyMatch`) and read through `DnsAnswerDecoder` into a `DnsQueryOutcome`. It implements
`IDnsResolverWithFailureReason`, so `TcpConnector` and `UdpDatagramConnector` add c-ares' reason
(`DnsLookupFailureText`) in brackets, or make it exit 43, through `NameResolutionFailure`.
`ResolveServiceAsync` looks up SRV records for Kerberos KDC location (BL-689). Sockets come from
`IDnsSocketOpener`; tests use `Fakes/ScriptedDnsSocketOpener` and `ManualTimeProvider`.

Everything else takes the Abstractions contracts (`IDnsResolver`, `ITlsProvider`,
`IConnection`, `IDatagramChannel`) or `ITcpDialer`, plus an injected `TimeProvider`, so the tests in
`Curl.Networking.UnitTests` drive every branch with fakes and no network.
`StreamConnection` wraps any `Stream`, so it is tested over a `MemoryStream`. `UdpDatagramConnector` opens channels
through an internal seam, so its tests need no socket. `UdpDatagramChannelTests` opens,
cancels and disposes local UDP sockets without sending anything.
`SslStreamTlsProviderTests` runs real handshakes against a server-side `SslStream` over
the in-memory `Fakes/InMemoryDuplexStream` pair, with a self-signed certificate made in
the test, so TLS is tested without a socket. `TcpConnectionListenerTests` and
`TcpPendingConnectionTests` bind local TCP sockets without connecting to them. The tests that
connect or send bytes are the loopback tests in `TcpDialerTests` (TCP and Unix socket), `UdpDatagramChannelTests`,
`TcpConnectorTests.LocalEndPoint` (plain and over TLS) and the accepting test in `TcpConnectionListenerTests`, tagged
`[TestCategory("Integration")]`, as is `DnsSocketOpenerTests`' TCP connect. Per ADR-0083 the six members only those tests can reach,
`TcpDialer.DialAsync`, `TcpDialer.DialUnixSocketAsync`, `TcpPendingConnection.AcceptStreamConnectionAsync` (behind the internal
`AcceptConnectionAsync` seam), `UdpDatagramChannel.SendAsync`, `UdpDatagramChannel.ReceiveAsync` and `DnsSocketOpener.ConnectStreamAsync`,
carry `[ExcludeFromCodeCoverage]`, so the fast-run coverage gate holds without the network.
Keep them thin: logic added there is not measured.
