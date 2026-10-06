---
id: BL-962
title: Make RFC 1964 DES3 Wrap and MIC tokens for a des3-cbc-sha1 GSS context key
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-894]
touches: [Curl.Kerberos.UnitLibrary, Curl.Kerberos.UnitTests]
requirement: none
created: 2026-09-29
completed: 2026-09-29
---
# BL-962 — Make RFC 1964 DES3 Wrap and MIC tokens for a des3-cbc-sha1 GSS context key

## Goal

`KerberosGssMessageProtection.Create` gives a context key of `des3-cbc-sha1` (16) the RFC 1964-style DES3 Wrap and MIC tokens MIT 1.22 makes (SGN_ALG `HMAC SHA1 DES3-KD` 0x0400, SEAL_ALG `DES3-KD` 0x0200), not RFC 4121's.

## Context

- Follow-up from BL-894, which made `des3-cbc-sha1` an encryption type (`Des3CbcSha1KerberosEncryption`, ADR-0232), so a KDC can now hand out a des3 session key.
- RFC 4121 section 4.2 keeps RFC 1964's token formats for the enctypes defined before it, DES3 among them; MIT's `src/lib/gssapi/krb5/k5seal.c`, `k5unseal.c` and `util_crypt.c` make them (`KG_USAGE_SIGN` 23, `KG_USAGE_SEAL` 22, `KG_USAGE_SEQ` 24; the sequence number encrypted under the key with the checksum's first 8 bytes as IV).
- Today `KerberosGssMessageProtection.Create` sends any non-`rc4-hmac` key to `Rfc4121GssMessageProtection`, so a des3 context would make tokens MIT's acceptor refuses. `Rc4HmacGssMessageProtection` is the nearest model (RFC 1964 framing).

## Acceptance criteria

- [x] A `KerberosGssMessageProtection` made for a key of type 16 makes Wrap (sealed and integrity-only) and MIC tokens with MIT's DES3 header bytes, and reads its own tokens back; `Curl.Kerberos.UnitTests` pin a token recorded from MIT 1.22's `gss_wrap` or a vector derived from MIT's code.
- [x] An altered DES3 token fails as `KerberosGssError` the way an altered RFC 4121 token does.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Kerberos.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- New `Des3CbcSha1GssMessageProtection`; `KerberosGssMessageProtection.Create` sends a
  key of type 16 to it. It follows MIT's `make_seal_token_v1`, `kg_unseal_v1`,
  `kg_make_seq_num`/`kg_get_seq_num` and `kg_setup_keys`: header `SGN_ALG` bytes `04 00`,
  `SEAL_ALG` `02 00` (or `FF FF`), filler `FF FF`; checksum `hmac-sha1-des3-kd` usage 23
  over the 8 header bytes and the data (message for MIC; confounder, message and padding
  for Wrap); sequence number four little-endian bytes plus four direction bytes (00 from
  the initiator, FF expected from the acceptor), 3DES-CBC with the checksum's first 8
  bytes as IV; Wrap data padded with 1 to 8 bytes each holding the length and, when
  sealed, 3DES-CBC with a zero IV. MIT gives the context key the type `des3-cbc-raw` for
  the sequence and the data, so both are raw 3DES under the key itself - usages 22 and 24
  are passed by MIT but ignored by the raw encryption type.
- Vector: no MIT 1.22 build is reachable from a Windows lane, so the tests build tokens
  step by step as MIT's code does (independently of the class, from `TripleDES`,
  `HMACSHA1` and the RFC 3961-tested `DeriveKey`), assert the class matches them byte for
  byte, and pin one MIC over MIT `t_cksums.c`'s des3 key. Like MIT, the padding bytes are
  not checked beyond the last one's 1..8 range.
- No ADR: nothing was chosen beyond matching MIT's code.
- `Measure-CodeQuality.ps1` also flagged the pre-existing `KerberosEncryption.Create`
  (complexity 12, from BL-894); split into `Create` and `CreateNewerThanRfc3962` so the
  library reports no failing member.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. A des3-cbc-sha1 GSS context key now makes and reads MIT's RFC 1964 DES3 Wrap and MIC tokens
