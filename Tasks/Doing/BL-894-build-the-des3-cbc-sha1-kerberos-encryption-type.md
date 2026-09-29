---
id: BL-894
title: Build the des3-cbc-sha1 Kerberos encryption type
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-828]
touches: [Curl.Kerberos.UnitLibrary, Curl.Kerberos.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-894 — Build the des3-cbc-sha1 Kerberos encryption type

## Goal

`KerberosEncryption.Create` gives `des3-cbc-sha1` (16) to RFC 3961 section 6.3, so a `krb5.conf` that names `des3` (e.g. `des3 DEFAULT`) is offered and served as MIT 1.22 does.

## Context

- Follow-up from BL-828 (ADR-0209): `KerberosEncryptionTypeList` resolves `des3` and its aliases to 16, but `KerberosKdcClient` drops it because the library cannot encrypt with it.
- Triple DES is in the BCL (`TripleDES`); the DES3 string-to-key (n-fold, key parity fix-up, `DR`/`DK` with `kerberos` constant) follows RFC 3961 sections 6.3.1 to 6.3.3.
- RFC 3961 appendix A.4 has the DES3 string-to-key vectors.

## Acceptance criteria

- [ ] `KerberosEncryptionType` names 16, and `Curl.Kerberos.UnitTests` pass RFC 3961 appendix A.4's vectors and an encrypt/decrypt and checksum round trip.
- [ ] A `KerberosKdcClient` whose `permitted_enctypes` is `des3 DEFAULT` offers 16 first in its AS-REQ.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Kerberos.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
