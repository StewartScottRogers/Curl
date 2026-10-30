---
id: BL-4334
title: Extension: Match a certificate chain against DANE TLSA records
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4333]
touches: [Curl.Extension.Dane.UnitLibrary/Tlsa, Curl.Extension.Dane.UnitTests/Tlsa]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4334 — Extension: Match a certificate chain against DANE TLSA records

## Goal

`Curl.Extension.Dane.UnitLibrary/Tlsa/` decides whether a server's certificate chain (DER, its own first) matches a set of TLSA records.

## Context

Part of the split of BL-4151 (DNSSEC and DANE TLSA extension). Built on BL-4333's library and its decoded TLSA record. Specification: RFC 6698 section 2.1 (fields), RFC 7671 sections 5.1 to 5.4 (usages DANE-EE(3), DANE-TA(2), PKIX-EE(1), PKIX-TA(0)) and section 5.1's rule that DANE-EE ignores names and expiry. Selectors 0 (full certificate) and 1 (SubjectPublicKeyInfo, via `X509Certificate2.PublicKey.ExportSubjectPublicKeyInfo`), matching types 0 (exact), 1 (SHA-256), 2 (SHA-512). Records with an unknown usage, selector or matching type are unusable and ignored (RFC 6698 section 4.1); a set with no usable record gives "no usable records", distinct from "no match". Curl's own PKIX verification has already accepted the chain when this runs (ADR-1016), so PKIX-TA/PKIX-EE match against the chain as sent.

## Acceptance criteria

- [x] Unit tests, named after the RFC sections, check each usage, selector and matching type against generated certificates, with no network.
- [x] Unusable records are ignored and a set with none usable is reported apart from a mismatch; unit tests.
- [x] The library meets the four quality gates by `Measure-CodeQuality.ps1 -Library Curl.Extension.Dane.UnitLibrary`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Filed by BL-4151's run on 2026-10-29 when it split that task for size. Document the folder in XML comments; BL-4151 adds it to the library's CLAUDE.md.
- Done directly rather than through the /feature agents: one small static matcher in one folder (default chosen to save cost). Decisions in ADR-1128: EE usages match the first certificate, TA usages any later one, as OpenSSL does; an empty set is NoUsableRecords too; no name, expiry or signature checks (PKIX already ran, ADR-1016).
- Measured: Measure-CodeQuality.ps1 -Library Curl.Extension.Dane.UnitLibrary gives 100% line and branch, 54 members, 0 failing, worst CRAP 10. Dane unit tests 48 passed; full fast run has no failure. Tests use generated ECDSA certificates only, no paths or OS text, so they are platform-neutral.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 4a67649d e6ce7791 6db41805 3d836d5e.
- 2026-10-29: Doing -> Done. TlsaCertificateMatcher matches a certificate chain against TLSA records for all four usages, both selectors and three matching types
