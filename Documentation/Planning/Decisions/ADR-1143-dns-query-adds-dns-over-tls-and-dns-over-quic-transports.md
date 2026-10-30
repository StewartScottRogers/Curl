# ADR-1143: The DNS query extension adds DNS over TLS and DNS over QUIC as --dns-transport tls and quic

- Status: Accepted
- Date: 2026-10-30
- Task: BL-4338
- Decided by Claude under Stewart's delegation

## Context

ADR-1140 gave `dns:` URLs the UDP and TCP transports and a transport selection that later transports add one name and
one case to. BL-4338 adds DNS over TLS (RFC 7858, May 2016) and DNS over QUIC (RFC 9250, May 2022). Upstream curl has
no `dns:` scheme, so both are tested against their RFCs, not against curl. The extension may not reference
`Curl.Extension.EncryptedDns`, whose resolvers already speak both protocols to look host names up; it may reference the
shared layers `Curl.Networking.UnitLibrary` and `Curl.Quic.UnitLibrary`.

## Decision

- **Names.** `--dns-transport tls` is DNS over TLS and `--dns-transport quic` is DNS over QUIC, in either case, listed
  after `udp` and `tcp`. They name the security layer each adds, as `udp` and `tcp` name theirs; `dot` and `doq` were
  the alternative, but they read as jargon beside the other two.
- **Default port.** Each transport says its own default port (`IDnsQueryTransport.DefaultPort`): 53 for UDP and TCP,
  853 for TLS (RFC 7858 section 3.1) and QUIC (RFC 9250 section 4.1.1). A URL naming a port uses that port. A URL
  naming no server queries the system's first DNS server's address on the transport's default port, so
  `--dns-transport tls dns:///example.com` asks that server on 853 rather than sending TLS to port 53.
- **DNS over TLS.** A plaintext TCP connection through curl's connector, then the handshake through
  `ProtocolHandlerServices.TlsProvider` with the URL's host as the name the certificate is checked against, before any
  DNS message (section 3.2). The query and its answer are framed as over TCP (section 3.3): the TCP transport's
  framing is shared, not copied. One query per connection, so pipelining (section 3.3) and connection reuse (section
  3.4) do not arise. A failed handshake passes the provider's exit code and message through.
- **DNS over QUIC.** `Curl.Quic`'s client over curl's datagram connector, with OpenSSL's QUIC hello offering only the
  ALPN token `doq` and the URL's host as the server name. The certificate chain is judged by
  `ITlsProvider.VerifyServerCertificateChain` through the extension's own `DnsQuicCertificateVerifier` (written here
  after EncryptedDns's, ADR-1052), so a DoQ server is trusted exactly when curl's TLS would trust it. The query goes
  on the first client bidirectional stream, length-prefixed and ended with FIN (section 4.2), with its Message ID set
  to 0 (section 4.2.1); the answer is handed back with the query's own ID put back, so the handler checks every
  transport's answer the same way. An answer that is not exactly one length-prefixed message with Message ID 0 closes
  the connection with DOQ_PROTOCOL_ERROR and fails with exit 8, as a malformed answer does on the other transports;
  otherwise the connection closes with DOQ_NO_ERROR (sections 4.3 and 4.5). A stream the server resets is exit 56;
  a failed handshake passes the QUIC handshake's exit code through.
- **No 0-RTT** (RFC 9250 section 4.5): one query per connection gains nothing from it.

## Consequences

- `curl --dns-transport tls dns://9.9.9.9/example.com` and `curl --dns-transport quic dns://dns.adguard-dns.com/example.com`
  work, with `--dns-json` as before; `--dns-transport`'s help and its exit 2 message list `udp, tcp, tls, quic`.
- The extension references `Curl.Quic.UnitLibrary`, and `Curl.Quic` lets `Curl.Extension.DnsQuery.UnitTests` see its
  internals so the in-memory QUIC test server (copied from EncryptedDns's tests) can run the real client.
- DNS over HTTPS (BL-4339) adds its name and case the same way.
