---
id: BL-687
title: Encode and decode Kerberos V5 messages with System.Formats.Asn1
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-685]
touches: [Curl.Kerberos.UnitLibrary, Curl.Kerberos.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-687 — Encode and decode Kerberos V5 messages with System.Formats.Asn1

## Goal

`Curl.Kerberos.UnitLibrary` encodes and decodes, in DER with the BCL's `System.Formats.Asn1`, the Kerberos V5 messages a client needs: AS-REQ, AS-REP, TGS-REQ, TGS-REP, AP-REQ, AP-REP, KRB-ERROR, and the structures inside them (Ticket, EncKDCRepPart, Authenticator, EncAPRepPart, PA-DATA including PA-ENC-TIMESTAMP and PA-ETYPE-INFO2, PrincipalName, KerberosTime, EncryptedData, Checksum).

## Context

- Standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28). RFC 4120 section 5 and Appendix A (the ASN.1 module, application tags, explicit context tags, `KerberosString` as GeneralString, `KerberosTime` as GeneralizedTime without fractions).
- Pure code: bytes in, typed records out and back. A malformed or unexpected message is a typed failure, never an unhandled `AsnContentException`.
- Test data: messages captured once from an MIT KDC (`kinit` against a local `krb5kdc`, captured with `Record-CurlExchange.ps1 -NoServer` or a packet capture) committed as test bytes with their source in a comment, or messages built from RFC 4120's definitions in the test.

## Acceptance criteria

- [ ] `Curl.Kerberos.UnitTests` round-trip every listed message type byte for byte, decode at least one captured AS-REP and one KRB-ERROR (`KDC_ERR_PREAUTH_REQUIRED` with PA-ETYPE-INFO2), and reject a wrong application tag and a truncated message with the typed failure.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Kerberos.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
