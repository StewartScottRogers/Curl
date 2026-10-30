---
id: BL-4335
title: Extension: Verify DNSSEC signatures and delegations for the Dane extension
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4333]
touches: [Curl.Extension.Dane.UnitLibrary/Dnssec, Curl.Extension.Dane.UnitTests/Dnssec]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4335 — Extension: Verify DNSSEC signatures and delegations for the Dane extension

## Goal

`Curl.Extension.Dane.UnitLibrary/Dnssec/` verifies an RRset's RRSIG against a DNSKEY, a DNSKEY against a DS, and holds the root trust anchors, with no network.

## Context

Part of the split of BL-4151 (DNSSEC and DANE TLSA extension). Built on BL-4333's decoded records. Specification: RFC 4034 section 6 (canonical name order, canonical RR form, canonical RRset order), section 3.1.8.1 (signature data), appendix B (key tag), section 5.1.4 (DS digest); RFC 4035 section 5.3 (RRSIG validity: signer name, labels, inception and expiration against an injected `TimeProvider`, wildcard expansion); RFC 4509 and RFC 6605 (SHA-256 DS digest type 2, SHA-384 type 4). Algorithms (RFC 8624 MUST/RECOMMENDED): 8 RSA/SHA-256 and 10 RSA/SHA-512 (RFC 5702), 13 ECDSA P-256/SHA-256 and 14 P-384/SHA-384 (RFC 6605) through the BCL, 15 Ed25519 and 16 Ed448 (RFC 8080) through `Curl.Cryptography.UnitLibrary`. An unsupported algorithm or digest makes the RRset insecure, not bogus (RFC 4035 section 5.2). Root trust anchors: KSK-20326 and KSK-38696 as DS records from IANA's root-anchors.xml.

## Acceptance criteria

- [x] Unit tests verify recorded real-world signed RRsets (root DNSKEY, a TLD DS, a signed TLSA RRset) for algorithms 8 and 13, and generated keys for 10, 14, 15 and 16, each named after its RFC section, with a fixed `TimeProvider`.
- [x] A tampered RRset, an expired or not-yet-valid signature, a wrong signer name and a key tag mismatch are each refused; unit tests.
- [x] DS digest types 2 and 4 match their DNSKEY and an unsupported algorithm or digest type gives insecure; unit tests.
- [x] The library meets the four quality gates by `Measure-CodeQuality.ps1 -Library Curl.Extension.Dane.UnitLibrary`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Filed by BL-4151's run on 2026-10-29 when it split that task for size. Document the folder in XML comments; BL-4151 adds it to the library's CLAUDE.md. NSEC/NSEC3 denial proofs are BL-4332's to decide (it walks the chain).
- 2026-10-29 (lane 5): built `Dnssec/` - `RrsigVerifier` (RFC 4035 section 5.3 over RFC 4034 section 3.1.8.1 signed data, wildcard expansion, serial-arithmetic validity against an injected `TimeProvider`), `DelegationVerifier` (DS digest types 2 and 4), `DnskeyKeyTag` (appendix B), `RootTrustAnchors` (KSK 20326 and 38696), and internal `DnssecCanonicalForm`, `DnssecSignedData`, `DnssecSignatureAlgorithm` (8, 10, 13, 14 via BCL; 15, 16 via Curl.Cryptography). Decisions in ADR-1124: unsupported algorithm or digest (SHA-1 type 1 included) gives Insecure; a malformed or refused key fails verification instead of throwing (Windows reports an off-curve ECDSA point as PlatformNotSupportedException, so the catch is SystemException).
- Test data: real responses recorded on 2026-10-29 over TCP from 1.1.1.1 with a throwaway C# file-based app (root DNSKEY signed by KSK 38696, se. DS signed by the root ZSK, five-record TLSA RRset of _443._tcp.www.huque.com and huque.com DNSKEY, algorithm 13), verified at a fixed 2026-11-01 01:46:40 UTC; generated keys for 10, 14, 15 and 16; RFC 6605 section 6 DNSKEY/DS examples for digest types 2 and 4. All platform-neutral (no paths, OS text or certificates).
- Quality: `Measure-CodeQuality.ps1 -Library Curl.Extension.Dane.UnitLibrary` gives 100% line and branch, 0 failing members, worst CRAP 10. Full `dotnet build` clean; fast tests green (Dane.UnitTests 86 of 86). CLAUDE.md for the folder is left to BL-4151 as this task said.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 4d6cf9cf 865afc22 278e6712 6db41805 3d836d5e.
- 2026-10-29: Doing -> Done. Dane's Dnssec folder verifies RRSIGs (algorithms 8, 10, 13, 14, 15, 16), DS delegations (digest 2 and 4) and holds the root trust anchors
