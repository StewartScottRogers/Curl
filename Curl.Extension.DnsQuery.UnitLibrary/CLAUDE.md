# Curl.Extension.DnsQuery.UnitLibrary

The DNS query extension (ADR-0999, ADR-1124). Namespaces `Curl.Extension.DnsQuery.Wire` and
`Curl.Extension.DnsQuery.Rendering`. It references only `Curl.Extension.Abstractions.UnitLibrary` and the shared
layers it brings; never a `Curl.Protocol.*` library or another extension (not even `Curl.Extension.EncryptedDns`).

- `Wire/` is the RFC 1035 message codec. `DnsQueryEncoder` writes a one-question query for any QTYPE and QCLASS;
  `DnsMessageDecoder` reads header, question, answer, authority and additional sections through `DnsMessageReader`,
  which follows compression pointers only backwards, and `DnsRecordDataDecoder` turns RDATA into named
  `DnsRecordField`s (A, AAAA, NS, CNAME, PTR, MX, TXT, SOA, SRV, CAA, and RFC 3597 `\# len hex` for any other type).
  Malformed input gives a failed `DnsOutcome`, never an exception. `DnsMnemonics` maps types, classes, opcodes and
  response codes to mnemonics and back (`TYPE65`, `CLASS2`).
- `Rendering/` writes a decoded `DnsMessage`: `DnsTextRenderer` as dig-like text, `DnsJsonRenderer` as JSON with
  `Utf8JsonWriter` (no serializer reflection, for AOT).

Not registered in `ExtensionCatalogue.cs` yet (BL-4337). Held to the four quality gates in the root `CLAUDE.md`.
Its tests are in `Curl.Extension.DnsQuery.UnitTests`, which name the RFC sections they check.
