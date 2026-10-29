# Curl.Extension.EncryptedDns.UnitLibrary

Encrypted DNS lookups (ADR-0999, ADR-1022): a DNS message codec and an `IDnsResolver` that asks a DNS-over-TLS
server (RFC 7858). Namespaces `Curl.Extension.EncryptedDns.Wire` and `Curl.Extension.EncryptedDns.DnsOverTls`.
Tested against RFC 1035 and RFC 7858, not against curl. Not yet registered in any catalogue: BL-4148 adds the
options, the resolver hook and the `ExtensionCatalogue` line.

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

Held to the four quality gates in the root `CLAUDE.md`. Its tests are in `Curl.Extension.EncryptedDns.UnitTests`.
