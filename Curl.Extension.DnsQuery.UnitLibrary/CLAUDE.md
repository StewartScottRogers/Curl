# Curl.Extension.DnsQuery.UnitLibrary

The DNS query extension (ADR-0999, ADR-1124, ADR-1126). Namespaces `Curl.Extension.DnsQuery.Wire`,
`Curl.Extension.DnsQuery.Rendering`, `Curl.Extension.DnsQuery.Client` and `Curl.Extension.DnsQuery.Transport`. It
references only `Curl.Extension.Abstractions.UnitLibrary` and the shared layer `Curl.Networking.UnitLibrary` (for
`SystemDnsServers`); never a `Curl.Protocol.*` library or another extension (not even `Curl.Extension.EncryptedDns`).

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
- `Transport/` holds `DnsQueryTransportSelection` (each `--dns-transport` name and its `IDnsQueryTransport`; DoT, DoQ
  and DoH add theirs there), `Udp/DnsUdpTransport` (3 tries of 5 seconds, TCP retry when TC is set) and
  `Tcp/DnsTcpTransport` (two-octet length prefix).

Registered in `ExtensionCatalogue.cs`, `ExtensionDescriptionCatalogue.cs`, `ExtensionOptionCatalogue.cs` and
`ExtensionTransferHookCatalogue.cs`. Held to the four quality gates in the root `CLAUDE.md`.
Its tests are in `Curl.Extension.DnsQuery.UnitTests`, which name the RFC sections they check.
