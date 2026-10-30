---
id: BL-4329
title: Extension: Match a certificate chain against DANE TLSA records
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4328]
touches: [Curl.Extension.Dane.UnitLibrary/Tlsa, Curl.Extension.Dane.UnitTests/Tlsa]
requirement: none
created: 2026-10-29
completed:
---
# BL-4329 — Extension: Match a certificate chain against DANE TLSA records

## Goal

`Curl.Extension.Dane.UnitLibrary/Tlsa/` decides whether a server's certificate chain (DER, its own first) matches a set of TLSA records.

## Context

Part of the split of BL-4151 (DNSSEC and DANE TLSA extension). Built on BL-4328's library and its decoded TLSA record. Specification: RFC 6698 section 2.1 (fields), RFC 7671 sections 5.1 to 5.4 (usages DANE-EE(3), DANE-TA(2), PKIX-EE(1), PKIX-TA(0)) and section 5.1's rule that DANE-EE ignores names and expiry. Selectors 0 (full certificate) and 1 (SubjectPublicKeyInfo, via `X509Certificate2.PublicKey.ExportSubjectPublicKeyInfo`), matching types 0 (exact), 1 (SHA-256), 2 (SHA-512). Records with an unknown usage, selector or matching type are unusable and ignored (RFC 6698 section 4.1); a set with no usable record gives "no usable records", distinct from "no match". Curl's own PKIX verification has already accepted the chain when this runs (ADR-1016), so PKIX-TA/PKIX-EE match against the chain as sent.

## Acceptance criteria

- [ ] Unit tests, named after the RFC sections, check each usage, selector and matching type against generated certificates, with no network.
- [ ] Unusable records are ignored and a set with none usable is reported apart from a mismatch; unit tests.
- [ ] The library meets the four quality gates by `Measure-CodeQuality.ps1 -Library Curl.Extension.Dane.UnitLibrary`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

- Filed by BL-4151's run on 2026-10-29 when it split that task for size. Document the folder in XML comments; BL-4151 adds it to the library's CLAUDE.md.

## Log

- 2026-10-29: Created.
