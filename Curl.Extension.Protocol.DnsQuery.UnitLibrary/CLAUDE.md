# Curl.Extension.Protocol.DnsQuery.UnitLibrary

The DNS query extension (ADR-0999, ADR-1124, ADR-1140, ADR-1143, ADR-1148). Namespaces `Curl.Extension.Protocol.DnsQuery.Wire`,
`Curl.Extension.Protocol.DnsQuery.Rendering`, `Curl.Extension.Protocol.DnsQuery.Client` and `Curl.Extension.Protocol.DnsQuery.Transport`. It
references only `Curl.Extension.Abstractions.UnitLibrary` and the shared layers `Curl.Networking.UnitLibrary` (for
`SystemDnsServers`) and `Curl.Quic.UnitLibrary` (the DNS-over-QUIC client); never a `Curl.Protocol.*` library or another extension (not even `Curl.Extension.EncryptedDns`).

- `Wire/` is the RFC 1035 message codec. `DnsQueryEncoder` writes a one-question query for any QTYPE and QCLASS;
  `DnsMessageDecoder` reads header, question, answer, authority and additional sections through `DnsMessageReader`,
  which follows compression pointers only backwards, and `DnsRecordDataDecoder` turns RDATA into named
  `DnsRecordField`s (A, AAAA, NS, CNAME, PTR, MX, TXT, SOA, SRV, CAA, and RFC 3597 `\# len hex` for any other type).
  Malformed input gives a failed `DnsOutcome`, never an exception. `DnsMnemonics` maps types, classes, opcodes and
  response codes to mnemonics and back (`TYPE65`, `CLASS2`).
- `Rendering/` writes a decoded `DnsMessage`: `DnsTextRenderer` as dig-like text, `DnsJsonRenderer` as JSON with
  `Utf8JsonWriter` (no serializer reflection, for AOT).

- `Client/` serves `dns://[server[:port]]/name[?type=T;class=C]` (RFC 4501). `DnsQueryExtension` holds the description,
  `--dns-transport` and `--dns-json`, the handler factory and the hook activator; `DnsQueryTransferHook` hands the
  options to `DnsQueryProtocolHandler` as a `DnsQueryContext`; `DnsQueryRequest` reads the URL (no server: the
  system's first DNS server); failures are `DnsQueryException`s carrying the exit code.
- `Transport/` holds `DnsQueryTransportSelection` (each `--dns-transport` name and its `IDnsQueryTransport`, which
  also gives the port a URL naming none is queried on), `Udp/DnsUdpTransport` (3 tries of 5
  seconds, TCP retry when TC is set), `Tcp/DnsTcpTransport` (two-octet length prefix), `Tls/DnsTlsTransport` (DNS
  over TLS, RFC 7858: port 853, the TLS provider's handshake, then TCP's framing), `Quic/DnsQuicTransport` (DNS
  over QUIC, RFC 9250: port 853, ALPN `doq`, one stream with Message ID 0 and FIN, the certificate judged by the TLS
  provider through `Quic/DnsQuicCertificateVerifier`) and `Https/DnsHttpsTransport` (DNS over HTTPS, RFC 8484: port 443,
  the TLS provider's handshake, then `DohRequest`'s HTTP/1.1 POST to `/dns-query` and `DohResponseReader`'s read of the
  200 `application/dns-message` body, chunked, by `Content-Length` or to the connection's end; `DohResponseHead` parses
  the status line and headers).

Registered in `ExtensionCatalogue.cs`, `ExtensionDescriptionCatalogue.cs`, `ExtensionOptionCatalogue.cs` and
`ExtensionTransferHookCatalogue.cs`. Held to the four quality gates in the root `CLAUDE.md`.
Its tests are in `Curl.Extension.Protocol.DnsQuery.UnitTests`, which name the RFC sections they check.
