# ADR-1123: The Dane extension's wire codec sends EDNS0 with DO and decodes without throwing

- Status: Accepted
- Date: 2026-10-29
- Task: BL-4333
- Decided by Claude under Stewart's delegation

## Context

BL-4151 (DNSSEC validation and DANE TLSA matching, ADR-0999) was split; BL-4333 scaffolds
`Curl.Extension.Dane.UnitLibrary` and gives it a DNS codec for DNSSEC. Upstream curl does no DNSSEC or DANE, so the
codec is tested against the RFCs. An extension references no other extension, so `Curl.Extension.EncryptedDns`'s
A/AAAA codec cannot be reused.

## Decision

- Every query carries an EDNS0 OPT record (RFC 6891 section 6.1.2) with the DO bit (RFC 4035 section 3.2.1),
  advertising a UDP payload of 1232 octets, the DNS flag day 2020 value that avoids IP fragmentation. CD (RFC 4035
  section 3.2.2) is the caller's choice. Recursion desired is always set: the extension asks a recursive resolver.
- `DnssecResponseDecoder.TryDecode` returns false for anything malformed, a query included, and never lets an
  exception out; the caller matches the ID itself. A truncated response (TC set) whose sections are complete still
  decodes, with `IsTruncated` set, so the caller decides to retry over TCP.
- Only the answer and authority sections are read; the additional section (OPT, glue) is not needed for validation.
- Compression pointers are followed only backwards, which bounds the work on a hostile message. RRSIG signer names
  and NSEC next names are read from the RDATA alone, so a pointer there is malformed, as RFC 4034 sections 3.1.7 and
  4.1.1 require.
- Each record keeps its RDATA octets as received, for canonical-form signing (RFC 4034 section 6.2); of the typed
  types only CNAME may hold a pointer there, and its typed target is decompressed for the verifier to re-encode.

## Consequences

BL-4334, BL-4335 and BL-4332 build on these types in their own folders; nothing in the shared layers changed.
