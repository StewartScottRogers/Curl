---
id: BL-894
title: Build the des3-cbc-sha1 Kerberos encryption type
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-828]
touches: [Curl.Kerberos.UnitLibrary, Curl.Kerberos.UnitTests, Documentation/Planning/Decisions/ADR-0232-the-hand-built-kerberos-has-des3-cbc-sha1-as-mit-1-22-does.md]
requirement: none
created: 2026-09-29
completed: 2026-09-29
---
# BL-894 — Build the des3-cbc-sha1 Kerberos encryption type

## Goal

`KerberosEncryption.Create` gives `des3-cbc-sha1` (16) to RFC 3961 section 6.3, so a `krb5.conf` that names `des3` (e.g. `des3 DEFAULT`) is offered and served as MIT 1.22 does.

## Context

- Follow-up from BL-828 (ADR-0209): `KerberosEncryptionTypeList` resolves `des3` and its aliases to 16, but `KerberosKdcClient` drops it because the library cannot encrypt with it.
- Triple DES is in the BCL (`TripleDES`); the DES3 string-to-key (n-fold, key parity fix-up, `DR`/`DK` with `kerberos` constant) follows RFC 3961 sections 6.3.1 to 6.3.3.
- RFC 3961 appendix A.4 has the DES3 string-to-key vectors.

## Acceptance criteria

- [x] `KerberosEncryptionType` names 16, and `Curl.Kerberos.UnitTests` pass RFC 3961 appendix A.4's vectors and an encrypt/decrypt and checksum round trip.
- [x] A `KerberosKdcClient` whose `permitted_enctypes` is `des3 DEFAULT` offers 16 first in its AS-REQ.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Kerberos.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Built `Des3CbcSha1KerberosEncryption` over the BCL's `TripleDES`, `HMACSHA1` and `SHA1`,
  with `KerberosNFold` for DR and the 168-fold; pinned by RFC 3961 A.3 (DR and DK), A.4
  (string-to-key), MIT `t_decrypt.c` (five decryptions) and `t_cksums.c` (one checksum).
- Decisions in ADR-0232 (Decided by Claude under Stewart's delegation): random-to-key fixes
  parity only, as MIT does; decryption returns the zero-padded plaintext as MIT does, and
  the KDC client and GSS context drop the bytes after a decrypted value
  (`KerberosAsn1.WithoutPadding`) as MIT's decoder does; an encrypted part that is not whole
  blocks fails as the new `KerberosCryptographyError.CiphertextNotWholeBlocks`.
- Found while testing: the AS exchange with a des3 client key failed to decode the padded
  plaintext, which is why `WithoutPadding` exists. `FakeKdc.cs` is linked into
  `Curl.Authentication.UnitTests`, so it trims with `AsnDecoder` directly, not the internal helper.
- Tests that used 16 as the "unsupported" type now use 3 (`des-cbc-md5`), 1 (`des-cbc-crc`)
  and `arcfour-hmac-exp` with `allow_weak_crypto`.
- Added the ADR file to `touches` (no task in Doing names it).
- Follow-up: BL-962, RFC 1964-style DES3 GSS Wrap/MIC tokens for a des3 context key (today
  such a key would get RFC 4121 tokens, which MIT refuses).

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. KerberosEncryption.Create gives des3-cbc-sha1 (16) to RFC 3961 6.3; des3 in krb5.conf is offered and served
