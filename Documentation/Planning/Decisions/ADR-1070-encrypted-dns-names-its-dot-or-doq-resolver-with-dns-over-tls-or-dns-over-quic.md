# ADR-1070 — The encrypted DNS extension names its DoT or DoQ resolver with --dns-over-tls or --dns-over-quic

- **Status:** Accepted
- **Date:** 2026-10-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-4148.

## Context

`Curl.Extension.EncryptedDns.UnitLibrary` already holds a DNS-over-TLS resolver (RFC 7858, ADR-1024) and a
DNS-over-QUIC resolver (RFC 9250, ADR-1029), but nothing let a user pick one. ADR-1012 gives extensions a resolver
hook: an activator per extension in `ExtensionResolverHookCatalogue`, at most one hook per option group, and none
beside curl's `--doh-url`. upstream curl has no option for either protocol, so nothing here changes what curl does.

## Decision

- **Two options, `--dns-over-tls <host[:port]>` and `--dns-over-quic <host[:port]>`**, each with a value, after
  curl's `--doh-url` naming style. The value is a host name, an IPv4 address or a bracketed IPv6 address, then an
  optional decimal port from 1 to 65535; port 853, which RFC 7858 section 3.1 and RFC 9250 section 4.1 assign, by
  default. A bare IPv6 address is taken whole as the host. Any other value is a command-line error naming the
  option (`--dns-over-tls needs a resolver as host[:port], not '...'`), exit 2 like any bad option value.
- **Two activators, one per option**, so `ResolverHooks.ActivatedBy` refuses both options together, or either beside
  `--doh-url`, with its message naming each option, as for any two resolver hooks (ADR-1012).
- **Production connectors from `Curl.Networking.UnitLibrary`**, a shared layer the extension rules allow: the DoT
  hook dials with a `TcpConnector` over a default `TcpDialer` and runs the handshake with `SslStreamTlsProvider`
  under default `TlsClientOptions`; the DoQ hook opens a `UdpDatagramConnector` channel. Both resolve the server's own
  name with curl's resolver, the one the hook is handed. The DoQ handshake's certificate goes through
  `TlsProviderCertificateVerifier`, which hands the chain to `ITlsProvider.VerifyServerCertificateChain` (ADR-1052),
  so it is judged against the system trust store as curl's TLS judges one.
- **One extension, `encrypted-dns`**, listed by `curl --extensions` and `curl --ai-help extensions` with both
  options; its description's specification link is RFC 7858, the summary naming RFC 9250 too.
- **Specifications followed:** RFC 7858 (May 2016) and RFC 9250 (May 2022), with RFC 1035's message format.

## Left out, and why

- **A separate authentication name** (RFC 8310's ADN when it differs from the host): the certificate is checked
  against the host the option names. `DnsOverTlsServer` and `DnsOverQuicServer` already carry a `TlsServerName`, so an
  option for it can be added without changing the resolvers; nobody has asked for one yet.
- **Curl's TLS options** (`-k`, `--cacert`, `--capath`) do not reach the resolver's handshake: the hook is handed only
  curl's resolver (ADR-1012), not the option group's TLS settings, and a resolver that silently trusted any
  certificate under `-k` would defeat the point of encrypted DNS.
- **RFC 7858's padding (RFC 8467) and connection reuse across look-ups**: each look-up opens its own connection, as
  ADR-1024 and ADR-1029 decided.

## Consequences

`curl --dns-over-tls 1.1.1.1 https://example.com/` looks `example.com` up over TLS on port 853; without either option
every transfer keeps curl's resolver, and with `CURL_EXTENSIONS=0` both options are unknown (exit 2).
