# Curl.Networking.UnitLibrary

Phase 1.

Sockets, DNS, TLS via SslStream, proxy and SOCKS handling, connection reuse: the
production implementations of the transport contracts in
`Curl.Protocol.Abstractions.UnitLibrary` (ADR-0005). It references that project,
`Curl.Tls.UnitLibrary`, the hand-built TLS client (ADR-0120, ADR-0140), `Curl.Quic.UnitLibrary`,
the hand-built QUIC client (ADR-0180), and `Curl.Kerberos.UnitLibrary`, whose KDC transport and SRV lookup it implements, and nothing
else: `KerberosKdcSocketTransport` moves a KDC's UDP datagram through an `IDatagramConnector`
(one-second reply wait) and its TCP stream through an `IConnector` (the stream owns and
disposes the connection, `ConnectionStream`'s `ownsConnection`), and `KerberosDnsSrvLookup`
answers SRV lookups through `DnsServerResolver.ResolveServiceAsync` (BL-527, ADR-0176).

Per ADR-0140 and ADR-0162 (BL-708) there are two TLS providers, and `TlsClientRouting.Choose`
picks one from a `TlsClientOptions` as one pure function: `HandBuiltTlsProvider` when a row of
ADR-0140's table holds (today a `MaximumVersion` of TLS 1.0 or 1.1, `RequireCertificateStatus`
for `--cert-status` by ADR-0191, and `Curves` or `SignatureAlgorithms` by ADR-0151; each option task
adds its row and a data row in `TlsClientRoutingTests`), `SslStreamTlsProvider` otherwise. Per
ADR-0284 (BL-709) `CurvesAndSignatureAlgorithms.Apply` reads `--curves` through `OpenSslGroupList`
and `--sigalgs` through `OpenSslSignatureAlgorithmList` (OpenSSL 3.5's syntax on every platform)
and swaps the result into the profile's `supported_groups`, `key_share` and `signature_algorithms`;
a refused value is exit 59 and an empty list exit 35, with OpenSSL's text. With `--cert-status` the hand-built
client asks for the stapled OCSP response and a rejected one is exit 91 with
`CertificateStatusFailureMessages`' text on every platform. Per ADR-0191 `--ssl-auto-client-cert`
(`TlsClientOptions.AutoClientCertificate`) without `--cert` makes `ClientCertificateLoader.Load`
present the certificate `AutomaticClientCertificate.Choose` takes from `CurrentUser\MY`, in both
providers; `HandBuiltTlsProviderTests.CertificateStatus` drives it against `Fakes/Tls13Server`,
a copy of `Curl.Tls.UnitTests`' in-memory TLS 1.3 server and OCSP response builder. Both
implement `ITlsProviderWithWarnings`. `HandBuiltTlsProvider` runs `TlsClientConnection` (one
ClientHello offering TLS 1.3 and TLS 1.2, TLS 1.2 the default minimum, ADR-0205) for a range
spanning both, `Tls13ClientConnection` for a TLS 1.3 minimum and `Tls12ClientConnection` for a
ceiling below TLS 1.3, over the internal `ConnectionStream`, and returns a `HandBuiltTlsConnection`.
Per ADR-0235 (BL-820) its ClientHello is the platform curl's measured profile, `ClientHelloProfile.Schannel`
for the Schannel build and `ClientHelloProfile.OpenSsl` for the OpenSSL build, turned into the TLS
settings by `ClientHelloProfileMapping`: the profile's extension order and fixed extensions, its lists
cut to the signature schemes and groups the client can honour, and the options changing only the lists
(below a TLS 1.3 ceiling the order stays `Curl.Tls`'s, BL-941). `HandBuiltTlsProviderTests.ClientHello`
captures the first record and rebuilds it from the profile. Both providers judge the server's certificate with
`ServerCertificateVerification` (trust anchors, tolerated chain errors, each build's name check,
exit 60 and 77); the hand-built path reaches it through `HandBuiltCertificateVerifier`, which
builds the chain and the `SslPolicyErrors` `SslStream` would. Both load `--cert` through
`ClientCertificateLoader.Load`. Per ADR-0193 `ServerCertificateVerification.Judge` also checks
`--pinnedpubkey` (`TlsClientOptions.PinnedPublicKey`) once the certificate is accepted, `-k` included:
`PinnedPublicKey` matches `sha256//` hashes or a PEM or DER key file as curl's `Curl_pin_peer_pubkey`
does, and a mismatch is exit 90 in both providers. Per ADR-0197 the OpenSSL build, unless `-k`,
reads `--crlfile` (`TlsClientOptions.CertificateRevocationListFile`) in `ReadTrustAnchors` through
`CertificateRevocationListFile` (exit 82 through `CertificateRevocationListFileException` and
`TrustAnchorsUnusable`) and `Judge` checks every chain certificate against a list from its issuer,
decoded and signature-checked by `CertificateRevocationList`, exit 60 with OpenSSL's verify result;
the Schannel build ignores `--crlfile`. The hand-built path's other exit 35 texts are
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

`TcpConnector` dials a host's or proxy's addresses through `AddressFamilyRace` (ADR-0254): the
first address's family in turn, the other family beside it once `happyEyeballsTimeout`
(`--happy-eyeballs-timeout-ms`, 200 ms by default) has passed on its `TimeProvider` or the first
family has failed on every address; the first connection wins and the rest are cancelled or closed.

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
Per ADR-0282 (BL-661) the OpenSSL build also reports that code through
`ITransferEvents.ReportCertificateVerifyResult` once the certificate was judged, the handshake
failing or not (`PeerVerification.ReportVerifyResult`), `1` for a host name refused without `-k`;
the Schannel build reports none. A trust error beats a date error in `OfChainStatus`.
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
`TcpSocketOptions` (`--no-tcp-nodelay`, `--no-keepalive`), with the keepalive idle time, interval and probe
count from `--keepalive-time` and `--keepalive-cnt` (`TcpSocketOptions.FromCommandLine`), in `ApplySocketOptions`,
which unit tests measure; a timer the platform refuses is skipped, as libcurl skips it.
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
failure; an earlier one escapes. Per ADR-0286 (BL-797) `WithoutConnectTimeout()` gives a view of
the same connector (DNS cache, numbering, every setting) whose connects run under the longest
timer delay instead, FTP's passive data connector; `PoolingConnector.Over(inner)` gives a pooling
connector over the same cache, configuration and numbering that opens through `inner`. A dial whose
last attempt failed with `SocketError.TimedOut` is exit 28 with the usual `Failed to connect to`
message, as curl 8.21.0 ends a dial the system gave up on. Tests stall through `Fakes/StallingTcpDialer`,
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
over the tunnel for an https target. Per ADR-0186 (BL-602) the CONNECT's `Proxy-Authorization`
comes from `HttpProxyTunnelOptions.ProxyAuthenticator` (`PreemptiveBasicProxyAuthenticator`
when none is given) for `ProxyAuthSchemes`: asked first with no challenge, and after a `407` to a
CONNECT that sent none with its `Proxy-Authenticate` values, the answer sent on the same
connection after the `Content-Length` body unless the reply closes it or is chunked, else on a
newly dialled one. A `407` to a CONNECT that sent a credential goes on through `ContinueAuthorizationAsync`, which only NTLM (Type 3 for the Type 2) and Negotiate (the next token) answer (ADR-0270, BL-604); otherwise it is exit 7, or the authenticator's exit code (94) when it fails. Through a SOCKS proxy (`Socks4`, `Socks4a`, `Socks5`,
`Socks5Hostname`) `SocksProxyTunnel` runs curl 8.21.0's handshake, measured byte for byte
(BL-213): `Socks4Handshake` resolves the target locally and sends its first IPv4 address,
SOCKS4a sends the host as written; `Socks5Handshake` offers no authentication and GSSAPI (and
user name and password with a credential), resolves locally for SOCKS5 and sends the name for
SOCKS5h. Every read takes exactly the reply's bytes, so the tunnel's bytes stay on the
connection. A refused or cut-short handshake is exit 97 with curl's message. ADR-0084 records
these choices (first address, literals, UTF-8, disposal). Per ADR-0276 (BL-615) `TcpConnector`
takes optional `Socks5AuthenticationOptions` (`--socks5-basic`, `--socks5-gssapi`, and the
GSS-API service, NEC mode, delegation and `ISecurityContextFactory`): the greeting offers only
the methods allowed and a proxy picking another fails with curl's message, and a proxy picking
GSSAPI gets `Socks5GssapiNegotiation`, RFC 1961's Kerberos token exchange and its protection-level
message offering none, in the platform build's texts (`Socks5GssapiFailureText`). Tests script
the context with `Fakes/ScriptedSecurityContextFactory`. Through an HTTPS proxy (`Https`, BL-266) TLS runs to the proxy host first, then the same CONNECT
over it, then TLS to the target inside that; each handshake failure is the TLS provider's result.
The handshake to the proxy runs through the proxy's `ITlsProvider` (the `--proxy-*` TLS options,
ADR-0095), and so does the handshake to an HTTPS forward proxy (`ConnectTarget.IsForwardProxy`
with `UseTls`), which curl 8.21.0 verifies with `--proxy-insecure` and never `-k` (measured, BL-441).
Per ADR-0273 (BL-614) `TcpConnector` takes an optional `preProxy` (`--preproxy`, a SOCKS proxy):
an `Http`, `Http10` or `Https` proxy, and a forward-proxy target, are then reached through it -
the pre-proxy resolved (exit 5 names it) and dialled (exit 7 names the HTTP proxy `over proxy`
the pre-proxy), its SOCKS handshake opened to the HTTP proxy, and the CONNECT, proxy TLS or
forwarded request run inside. A SOCKS `ConnectTarget.Proxy` and a direct target never use it.
Per BL-616 `TcpConnector` takes an optional `HaproxyProtocolHeader` (`--haproxy-protocol`,
`--haproxy-clientip`) and writes its PROXY protocol v1 line first on every new connection, once any
tunnel is open and before the target's TLS handshake, from the socket's own ends (the proxy's through
a proxy): `PROXY TCP4|TCP6 <local> <remote> <local port> <remote port>`, the client IP verbatim for both
addresses (`TCP4` only for a strict dotted quad), `PROXY UNKNOWN` over a Unix domain socket, all
measured against curl 8.21.0.

`TcpConnector` applies `--resolve` through `ResolveOverrides` and `--connect-to` through
`ConnectToMappings`, both built from the verbatim option values and parsed as curl 8.21.0
parses them (measured; BL-214). The first `--connect-to` mapping matching the URL's host
and port gives the `ConnectDestination` that is resolved, dialled and named in the CONNECT
request; TLS still verifies the URL's host. A `--resolve` entry for the host and port being
resolved, the proxy's included, answers in place of `IDnsResolver`. An entry or a matching
mapping that does not parse fails the connect with exit 49 and curl's message. Per ADR-0208
(BL-878) a target's `AltSvcRoute` (`--alt-svc`) is dialled the same way when no mapping matched,
after curl's `Alt-svc connecting from [h1]H:P to [h1]H2:P2` line, and `ConnectionPoolKey` keys on
the alternative too.

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

Per ADR-0285 (BL-754) the pool's state - idle and leased connections, negotiations, numbering and
clock - lives in a `ConnectionCache`. `new PoolingConnector(inner, timeProvider)` makes and owns
one, closed by its `DisposeAsync`; `new PoolingConnector(inner, cache, configuration)` shares a
given cache, which only its owner closes, and puts `configuration` into `ConnectionPoolKey`
(`Configuration`, compared with `Equals`), so connectors over one cache - the `--next` option groups
of one run - reuse each other's connections only when their configurations are equal.

Per BL-717 a pooled connection whose `IConnectionSession` multiplexes (HTTP/2) is shared, not
checked out: while its session's `ConcurrentTransferLimit` has streams to spare, a transfer with the
same key gets another `PooledConnection` lease on it (`* Multiplexed connection found`), and once
the limit is taken the next opens its own after `* MAX_CONCURRENT_STREAMS reached, skip (N)`.
`IConnection.IsSharedWithAnotherTransfer` tells the handler whether another lease still holds it,
so only the last one to end reports it left intact; the connection goes back to the pool (or
closes) when the last lease ends, pooled only if every lease marked it reusable and none asked
`ClearTlsAsync`. With `WaitsForMultiplexing` (`-Z` without `--parallel-immediate`, curl's
`CURLOPT_PIPEWAIT`) a transfer that finds a connection with its key still being opened waits
(`MultiplexingNegotiation`) until it holds a session or turns out not to multiplex, as curl 8.18.0
does (measured, BL-717 Notes).

Per ADR-0269 (BL-600) `TcpConnector` takes an optional `LocalBinding` (`--interface`, `--local-port`)
and dials every TCP address, a proxy's included, through the internal `LocalBindingTcpDialer`, whose
`LocalBindingAddressChooser` picks the local address for the family dialled (an interface
`INetworkInterfaceLookup` finds, none on Windows; else a host, `localhost` as `::1` first; else the
unspecified address), and calls
`ITcpDialer.DialFromAsync`, whose `TcpDialer.BindLocalEnd` tries each port of the range. A bind that
fails throws `LocalBindException`, a `SocketException`, so `AddressFamilyRace` moves on to the next
address with curl's `from  port 0 failed:` line and returns the last `LocalBindFailure`:
`InterfaceFailed` is exit 45 `Failed binding local connection end`, `BadArgument` (an `ifhost!`
interface part over 254 characters) exit 43, `AddressFamilyMismatch` the usual exit 7.
Per ADR-0292 (BL-1025) QUIC's UDP sockets bind the same way: `TcpConnector` puts the chooser on
`QuicDialRequest.LocalBinding`, and `QuicDialer` binds each socket through
`IUdpChannelOpener.OpenFrom` (`TcpDialer.BindLocalEnd` walks the range) before it reports the trust
anchors; a failed bind moves on to the next address and ends with exit 45, 43 or 7 and the one line
`Failed to connect to <host> port <port> after N ms: <words>`, with no `QUIC connect to` line.

Per ADR-0149 (BL-507) `TcpConnector` takes an optional `UnixSocketAddress` (`--unix-socket`,
`--abstract-unix-socket`, whose name starts with a NUL). With one, every connect dials it through
`ITcpDialer.DialUnixSocketAsync` in place of the host, port and proxy, resolving nothing, then runs
TLS to the URL's host when asked. `-v` shows curl 8.21.0's Windows lines on every platform: `Trying
<name>:0...`, and for a failure `Immediate connect fail for <name>: <reason>` and `connect to <name>
port 0 from  port 0 failed: <reason>` before exit 7 `Failed to connect to <host>:<port> over
unix://<path> after N ms: Could not connect to server`; `<name>` is `UnixSocketAddress.RemoteIpText`,
the path cut to 45 characters (empty for an abstract name). A success is reported opened with the
path as the host and `ConnectionOpenedEvent.UnixSocketRemoteIp`, and returns the whole path (an
abstract name as given) as `ConnectResult.UnixSocketPath`, which `PoolingConnector` keeps in its
`PoolEntry` for the opened and every reused result, so the HTTP handler's left-intact line names
the socket (BL-884). A path too long for `sun_path`
(108 bytes, 104 on macOS, with its NUL) is exit 6 `Unix socket path too long: '<path>'`. The socket
is part of each option group's pool configuration, so different sockets never share a connection.

The DNS-over-HTTPS message codec (ADR-0152, BL-640) is pure code, bytes in and bytes out, as
curl 8.21.0's `lib/doh.c` does it. `DnsQueryEncoder` writes the measured query (ID 0, flags
`0x0100`, one question, QCLASS IN) for a `DnsRecordType`, refusing an empty label or one over 63
bytes and a query over 272 bytes. `DnsAnswerDecoder` returns a `DnsAnswer`: the addresses of the
type asked for (at most 24), the CNAME targets followed through compression pointers (at most 4),
the smallest TTL, or a `DnsMessageFailure`, a pointer loop ending as `LabelLoop` after 128 steps.
`DnsMessageFailureText` gives curl's `--trace-config doh` text for each failure. For an SRV query
the decoder also keeps each SRV record as `DnsAnswer.ServiceRecords` (`DnsServiceRecord`).

`DohDnsResolver` (ADR-0152 and its BL-641 amendment) is the `IDnsResolver` behind `--doh-url`. It
takes an `IConnector` for the DoH connections (a `TcpConnector` of its own, built with the system
resolver and the DoH TLS options) and the DoH URL, and for each name POSTs the A query and the AAAA
query in parallel on two connections, A first, each request written byte for byte as curl 8.21.0
writes it (no `User-Agent`; `Host` with the port only when it is not the default). The target
carries `PoolScheme` `https`, so the handshake offers ALPN `http/1.1`. `DohResponseReader` reads the
response as curl does: status and `Content-Type` ignored, a `Content-Length` or chunked body of at
most 3000 bytes, anything else a failure. It returns the AAAA answer's addresses, then the A
answer's; a query that fails yields none, and none from both makes `TcpConnector` fail with exit 6.
IP literals and `localhost` (`TcpConnector.IsLocalhost`) are answered without a query. Its tests
drive it through `Fakes/FakeConnector`'s `BytesToRead` and through a `TcpConnector` over fakes.

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

Per ADR-0180 (BL-728) `TcpConnector` takes an optional `QuicDialer`, and its
`ConnectMultiplexedAsync` resolves the host exactly as `ConnectAsync` does (the same DNS cache,
`--resolve`, `--connect-to`, `-4`/`-6` and `-v` lines) and hands the addresses, as a
`QuicDialRequest`, to the dialer. `QuicDialer` opens a UDP channel for each address in turn
through `IUdpChannelOpener` (`UdpChannelOpener` in production, binding `UdpDatagramChannel` to a
local address and port, or to the first free port of a range through `OpenFrom`), runs `Curl.Quic`'s `QuicClientConnector` with curl's ClientHello and a
`HandBuiltCertificateVerifier`, and returns a `QuicConnection`. Per BL-847 the ClientHello is
curl.se's LibreSSL build's (`QuicClientSettings.CreateLibreSslTlsSettings`) for the Windows build and
the OpenSSL profile's TLS 1.3 parts (`CreateOpenSslTlsSettings`) for the OpenSSL build; both builds
read `--ciphers`/`--tls13-ciphers` through `OpenSslCipherSuites`, cut to the suites QUIC can protect
(none left is exit 59 with `HandBuiltTlsProvider`'s text), and load `--cert` through
`ClientCertificateLoader.LoadAsOpenSslBuild`, the Windows build keeping a drive letter's colon; failures print curl's `QUIC connect
to` and `Failed to connect to <host> port <port>` lines, and a socket error is exit 56 `QUIC:
recvfrom() ...`. This project therefore references `Curl.Quic.UnitLibrary`, which lets
`Curl.Networking.UnitTests` see its internals: `Fakes/QuicTestServer` and `QuicTestTlsServer` are
copies of `Curl.Quic.UnitTests`' in-memory server, reached through `Fakes/QuicServerChannelOpener`.
`PoolingConnector.ConnectMultiplexedAsync` passes straight through to its inner connector.
Per ADR-0289 (BL-942) a target whose `Proxy` is an HTTP, HTTP/1.0 or HTTPS proxy is not
resolved: `TcpConnector.UdpTunnel.cs` dials the proxy, sends curl 8.22.0's CONNECT-UDP request
(`HttpProxyTunnel.BuildConnectUdpRequest`), takes a `101` or `2xx` (`OpensUdpTunnel`), and hands
`QuicDialer.DialThroughTunnelAsync` a `CapsuleDatagramChannel`, which carries each datagram as an
RFC 9297 `DATAGRAM` capsule over the proxy connection. Tests run the handshake through
`Fakes/CapsuleQuicProxyConnection`, which feeds the capsules to a `QuicTestServer`.

Per ADR-0222 (BL-920) `TcpConnector` and `PoolingConnector` write the connect steps to
`ConnectTarget.DiagnosticLog` (`--log-level`) through `NetworkDiagnosticLog`, the one place that
formats them and tests `IsEnabled` first: `dns` the addresses a name resolved to (cache or lookup,
elapsed ms) at `info` and a name with none at `warning`; `connect` each address dialled at
`verbose`, each failed dial at `warning`, the connection made at `info`, the pool's reuse decision
at `verbose`, and every failed connect (with its `CurlExitCode`) or escaping exception (type and
message) at `error`; `proxy` the CONNECT or SOCKS handshake at `verbose` and the tunnel at `info`;
`tls` the handshake's version, cipher suite, ALPN and route (`IHandshakeReportingTlsProvider.Route`)
at `info`, each certificate and the chain verdict at `verbose`, a failed handshake at `error`;
`quic` the dial at `verbose`, the connection at `info`, a failure at `error`. The handshake's details
come from the `TlsHandshakeEvent` the provider reports, caught by `HandshakeCapturingTransferEvents`,
which wraps the target's events only when `info` is on. A proxy is named by kind, host and port and
no credential, pass phrase or `Proxy-Authorization` reaches the log; `Curl.Tls` and `Curl.Quic`
log nothing themselves. Tests record lines through `Fakes/RecordingDiagnosticLog`.

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
`[TestCategory("Integration")]`, as is `DnsSocketOpenerTests`' TCP connect. Per ADR-0083 the members only those tests can reach,
`TcpDialer.DialAsync`, `TcpDialer.DialFromAsync` with the `DialBoundAsync` both run, `TcpDialer.DialUnixSocketAsync`, `TcpPendingConnection.AcceptStreamConnectionAsync` (behind the internal
`AcceptConnectionAsync` seam), `UdpDatagramChannel.SendAsync`, `UdpDatagramChannel.ReceiveAsync` and `DnsSocketOpener.ConnectStreamAsync`,
carry `[ExcludeFromCodeCoverage]`, so the fast-run coverage gate holds without the network.
Keep them thin: logic added there is not measured.
