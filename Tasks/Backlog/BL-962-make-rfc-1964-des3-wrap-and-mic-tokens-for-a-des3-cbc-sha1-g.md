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
completed:
---
# BL-962 — Make RFC 1964 DES3 Wrap and MIC tokens for a des3-cbc-sha1 GSS context key

## Goal

`KerberosGssMessageProtection.Create` gives a context key of `des3-cbc-sha1` (16) the RFC 1964-style DES3 Wrap and MIC tokens MIT 1.22 makes (SGN_ALG `HMAC SHA1 DES3-KD` 0x0400, SEAL_ALG `DES3-KD` 0x0200), not RFC 4121's.

## Context

- Follow-up from BL-894, which made `des3-cbc-sha1` an encryption type (`Des3CbcSha1KerberosEncryption`, ADR-0232), so a KDC can now hand out a des3 session key.
- RFC 4121 section 4.2 keeps RFC 1964's token formats for the enctypes defined before it, DES3 among them; MIT's `src/lib/gssapi/krb5/k5seal.c`, `k5unseal.c` and `util_crypt.c` make them (`KG_USAGE_SIGN` 23, `KG_USAGE_SEAL` 22, `KG_USAGE_SEQ` 24; the sequence number encrypted under the key with the checksum's first 8 bytes as IV).
- Today `KerberosGssMessageProtection.Create` sends any non-`rc4-hmac` key to `Rfc4121GssMessageProtection`, so a des3 context would make tokens MIT's acceptor refuses. `Rc4HmacGssMessageProtection` is the nearest model (RFC 1964 framing).

## Acceptance criteria

- [ ] A `KerberosGssMessageProtection` made for a key of type 16 makes Wrap (sealed and integrity-only) and MIC tokens with MIT's DES3 header bytes, and reads its own tokens back; `Curl.Kerberos.UnitTests` pin a token recorded from MIT 1.22's `gss_wrap` or a vector derived from MIT's code.
- [ ] An altered DES3 token fails as `KerberosGssError` the way an altered RFC 4121 token does.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Kerberos.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-29: Created.
