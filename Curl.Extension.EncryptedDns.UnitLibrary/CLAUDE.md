# Curl.Extension.EncryptedDns.UnitLibrary

Encrypted DNS lookups (ADR-0999, ADR-1024): a DNS message codec and `IDnsResolver`s that ask a DNS-over-TLS
server (RFC 7858) or a DNS-over-QUIC server (RFC 9250). Namespaces `Curl.Extension.EncryptedDns.Wire`,
`Curl.Extension.EncryptedDns.DnsOverTls` and `Curl.Extension.EncryptedDns.DnsOverQuic`. Tested against RFC 1035,
RFC 7858 and RFC 9250, not against curl.

- `EncryptedDnsExtension` (ADR-1070): the `encrypted-dns` description and the options `--dns-over-tls <host[:port]>`
  and `--dns-over-quic <host[:port]>` (port 853 by default, read by the internal `EncryptedDnsServerAddress`), each
  with its own resolver hook activator in `Curl.Extension.Composition`'s `ExtensionResolverHookCatalogue`
  (ADR-1012). `DnsOverTlsResolverHook` builds the DoT resolver over `Curl.Networking.UnitLibrary`'s `TcpConnector`
  and `SslStreamTlsProvider`; `DnsOverQuicResolverHook` builds the DoQ resolver over `UdpDatagramConnector`, its
  certificate judged by `TlsProviderCertificateVerifier` through `ITlsProvider.VerifyServerCertificateChain`. Both
  resolve the server's own name with curl's resolver.

- `Wire/`: `DnsQueryEncoder` writes an A or AAAA query (`DnsRecordType`) as RFC 1035 section 4.1 lays it out
  (the caller's ID, recursion desired, one question, QCLASS IN), refusing an empty label, a label over 63 octets or
  a name over 255 with an `ArgumentException`. `DnsAnswerDecoder` returns a `DnsAnswer`: the addresses of the type
  asked for and class IN, owner names skipped with compression pointers (section 4.1.4) through the internal
  `DnsMessageReader`, or a `DnsAnswerFailure` (malformed, not the answer to the query, truncated, non-zero RCODE).
  `Curl.Networking.UnitLibrary`'s DoH codec is internal there, so this one is the extension's own.
- `DnsOverTls/`: `DnsOverTlsResolver` takes an `IConnector`, an `ITlsProvider` and a `DnsOverTlsServer` (port 853
  by default, an optional TLS server name to verify). It answers an address literal itself; for a name it connects
  with `UseTls: false`, runs the handshake, sends the AAAA query (ID 2) and the A query (ID 1) pipelined, each with
  the two-octet length prefix, and reads two answers matched by ID, returning AAAA addresses then A. A name that
  cannot be encoded, a failed connect or handshake, or a server that closes inside a message gives no addresses,
  which a connector reports as exit 6.
- `DnsOverQuic/` (ADR-1029): `DnsOverQuicResolver` takes an `IDatagramConnector`, an `IServerCertificateVerifier`,
  a `DnsOverQuicServer` (port 853 by default, an optional TLS server name) and a `TimeProvider`, and runs
  `Curl.Quic.UnitLibrary`'s client offering only the ALPN token `doq` (RFC 9250 section 4.1.1). It sends the AAAA
  and the A query each on its own client bidirectional stream with Message ID 0, the two-octet length prefix and
  FIN, reads the answers in that order, and closes with DOQ_NO_ERROR, or with DOQ_PROTOCOL_ERROR when an answer
  lacks its prefix or carries another Message ID. Any failure gives no addresses. Its tests run the real client
  against a copy of `Curl.Quic.UnitTests`' in-memory `QuicTestServer`.

Held to the four quality gates in the root `CLAUDE.md`. Its tests are in `Curl.Extension.EncryptedDns.UnitTests`.
