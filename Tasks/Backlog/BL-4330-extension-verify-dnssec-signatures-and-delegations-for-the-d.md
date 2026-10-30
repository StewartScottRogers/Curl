---
id: BL-4330
title: Extension: Verify DNSSEC signatures and delegations for the Dane extension
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4328]
touches: [Curl.Extension.Dane.UnitLibrary/Dnssec, Curl.Extension.Dane.UnitTests/Dnssec]
requirement: none
created: 2026-10-29
completed:
---
# BL-4330 — Extension: Verify DNSSEC signatures and delegations for the Dane extension

## Goal

`Curl.Extension.Dane.UnitLibrary/Dnssec/` verifies an RRset's RRSIG against a DNSKEY, a DNSKEY against a DS, and holds the root trust anchors, with no network.

## Context

Part of the split of BL-4151 (DNSSEC and DANE TLSA extension). Built on BL-4328's decoded records. Specification: RFC 4034 section 6 (canonical name order, canonical RR form, canonical RRset order), section 3.1.8.1 (signature data), appendix B (key tag), section 5.1.4 (DS digest); RFC 4035 section 5.3 (RRSIG validity: signer name, labels, inception and expiration against an injected `TimeProvider`, wildcard expansion); RFC 4509 and RFC 6605 (SHA-256 DS digest type 2, SHA-384 type 4). Algorithms (RFC 8624 MUST/RECOMMENDED): 8 RSA/SHA-256 and 10 RSA/SHA-512 (RFC 5702), 13 ECDSA P-256/SHA-256 and 14 P-384/SHA-384 (RFC 6605) through the BCL, 15 Ed25519 and 16 Ed448 (RFC 8080) through `Curl.Cryptography.UnitLibrary`. An unsupported algorithm or digest makes the RRset insecure, not bogus (RFC 4035 section 5.2). Root trust anchors: KSK-20326 and KSK-38696 as DS records from IANA's root-anchors.xml.

## Acceptance criteria

- [ ] Unit tests verify recorded real-world signed RRsets (root DNSKEY, a TLD DS, a signed TLSA RRset) for algorithms 8 and 13, and generated keys for 10, 14, 15 and 16, each named after its RFC section, with a fixed `TimeProvider`.
- [ ] A tampered RRset, an expired or not-yet-valid signature, a wrong signer name and a key tag mismatch are each refused; unit tests.
- [ ] DS digest types 2 and 4 match their DNSKEY and an unsupported algorithm or digest type gives insecure; unit tests.
- [ ] The library meets the four quality gates by `Measure-CodeQuality.ps1 -Library Curl.Extension.Dane.UnitLibrary`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

- Filed by BL-4151's run on 2026-10-29 when it split that task for size. Document the folder in XML comments; BL-4151 adds it to the library's CLAUDE.md. NSEC/NSEC3 denial proofs are BL-4332's to decide (it walks the chain).

## Log

- 2026-10-29: Created.
