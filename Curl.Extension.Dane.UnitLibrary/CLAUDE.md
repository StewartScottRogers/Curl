# Curl.Extension.Dane.UnitLibrary

DNSSEC validation and DANE TLSA certificate matching (ADR-0999, ADR-1120), built up by BL-4151's split: BL-4333 the
wire codec, BL-4334, BL-4335 and BL-4332 their own folders, and BL-4151 the option, the hook and the catalogue line.
Namespace `Curl.Extension.Dane.Wire` so far. Tested against RFC 1035, RFC 4034, RFC 4035, RFC 5155, RFC 6698 and
RFC 6891, not against curl. It references `Curl.Protocol.Abstractions`, `Curl.Extension.Abstractions`,
`Curl.Networking` and `Curl.Cryptography`, and no other extension: `Curl.Extension.EncryptedDns`'s A/AAAA codec is
that extension's own, so this one is separate.

- `Wire/`: the DNSSEC-aware DNS message codec.
  - `DnssecQueryEncoder` writes a query for any QTYPE (`DnssecRecordType`, or any other value cast to it):
    the caller's ID, recursion desired, CD when asked (RFC 4035 section 3.2.2), one question of class IN, and an
    EDNS0 OPT record (RFC 6891 section 6.1.2) advertising a 1232-octet UDP payload with the DO bit set (RFC 4035
    section 3.2.1). An empty label, a label over 63 octets or a name over 255 is an `ArgumentException`.
  - `DnssecResponseDecoder.TryDecode` gives a `DnssecResponse`: the ID, TC, AD, CD and RCODE, and the answer and
    authority sections as `DnsResourceRecord`s (the additional section is not read). It returns false, never
    throwing, for a query (QR clear), a message that ends inside a field, a name RFC 1035 does not allow (an
    undefined label type, a compression pointer that does not point backwards, over 255 octets) or RDATA its
    type's RFC does not allow. The internal `DnsWireReader` reads fields and names, following compression pointers
    (RFC 1035 section 4.1.4) only backwards so no loop runs forever.
  - Each `DnsResourceRecord` has its owner as a `DnsName` (uncompressed wire form in its original case, label
    count, Latin-1 text), its TYPE, CLASS and TTL, its RDATA octets as received (`RawData`, for canonical-form
    signing, RFC 4034 section 6), and its RDATA's fields as a `DnsRecordData` from the internal
    `DnsRecordDataDecoder`: `CnameRecordData` (target decompressed), `TlsaRecordData` (RFC 6698 section 2.1),
    `DnskeyRecordData` (RFC 4034 section 2.1), `RrsigRecordData` (section 3.1), `DsRecordData` (section 5.1),
    `NsecRecordData` (section 4.1) and `Nsec3RecordData` (RFC 5155 section 3.2), the type bit maps read into
    ascending type lists. RRSIG signer names and NSEC next names must not be compressed (RFC 4034 sections 3.1.7
    and 4.1.1). Any other type has null `Data`.
