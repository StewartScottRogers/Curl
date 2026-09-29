---
id: BL-893
title: Build the camellia128-cts-cmac and camellia256-cts-cmac Kerberos encryption types
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-828]
touches: [Curl.Kerberos.UnitLibrary, Curl.Kerberos.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-893 — Build the camellia128-cts-cmac and camellia256-cts-cmac Kerberos encryption types

## Goal

`KerberosEncryption.Create` gives `camellia128-cts-cmac` (25) and `camellia256-cts-cmac` (26) to RFC 6803, so `KerberosKdcClient` offers MIT 1.22's whole default list, 18 17 20 19 25 26, as `kinit` does.

## Context

- Follow-up from BL-828 (ADR-0209): the resolved lists keep only the types `KerberosEncryptionType` names, so the default offer is 18 17 20 19 where MIT's `kinit` sends 18 17 20 19 25 26 (`RecordedKerberosMessagesTests`).
- The Camellia block cipher is already hand-built: `Curl.Cryptography.UnitLibrary/Camellia.cs` (BL-783). CMAC and the RFC 6803 KDF (counter mode, CMAC-based) are built in `Curl.Kerberos.UnitLibrary` beside `AesSha2KerberosEncryption`.
- RFC 6803 section 10 has test vectors for string-to-key, key derivation, encryption and checksums.

## Acceptance criteria

- [ ] `KerberosEncryptionType` names 25 and 26, and `Curl.Kerberos.UnitTests` pass RFC 6803 section 10's vectors for both.
- [ ] `KerberosKdcClient.AsRequestEncryptionTypes` with no `krb5.conf` is 18 17 20 19 25 26.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Kerberos.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
