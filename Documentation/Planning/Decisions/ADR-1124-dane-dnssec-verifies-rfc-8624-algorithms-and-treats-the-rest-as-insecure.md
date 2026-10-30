# ADR-1124: The Dane extension's DNSSEC verifier supports RFC 8624's algorithms and treats the rest as insecure

- Status: Accepted
- Date: 2026-10-29
- Task: BL-4335
- Decided by Claude under Stewart's delegation

## Context

BL-4335 gives `Curl.Extension.Dane.UnitLibrary` a `Dnssec/` folder that verifies an RRset's RRSIG against a DNSKEY,
a DNSKEY against a DS, and holds the root trust anchors, with no network. Upstream curl does no DNSSEC, so there is
no curl behaviour to match; the RFCs are the specification. Choices the RFCs leave to a validator had to be made.

## Decision

- Signature algorithms 8 and 10 (RSA, RFC 5702), 13 and 14 (ECDSA, RFC 6605) run through the BCL's `RSA` and
  `ECDsa`; 15 Ed25519 and 16 Ed448 (RFC 8080) through `Curl.Cryptography`. These are every algorithm RFC 8624
  section 3.1 marks MUST or RECOMMENDED for validation. Any other algorithm (5 RSA/SHA-1, 7, 3, 6, 12 included)
  gives `Insecure`, as RFC 4035 section 5.2 says, not `Bogus`.
- DS digest types 2 SHA-256 (RFC 4509) and 4 SHA-384 (RFC 6605) are supported. Type 1 SHA-1 is not verified and
  gives `Insecure` (RFC 4035 section 5.2), although RFC 8624 section 3.3 still lists it as MUST for validation: the
  task names only 2 and 4, and SHA-1 is collision-broken, so a SHA-1-only delegation is not called secure. A DS RRset holding SHA-1 beside SHA-256 still verifies through the SHA-256 record.
- `RrsigVerifier` checks an RRSIG's shape (owner, class, type covered, labels, signer name above the owner) and its
  validity period before looking at the algorithm, so a malformed or expired RRSIG with an unknown algorithm is
  `Bogus`, and a well-formed one is `Insecure`. Validity uses RFC 1982 serial arithmetic against an injected
  `TimeProvider`, inclusive at both ends (RFC 4035 section 5.3.1: "less than or equal").
- Canonical RDATA (RFC 4034 section 6.2) lowercases only a CNAME's target among the decoded types; NSEC's next name
  keeps its case (RFC 6840 section 5.1); duplicate records in an RRset are signed once (section 6.3).
- A malformed key, or one the platform refuses (Windows reports an ECDSA point off the curve as
  `PlatformNotSupportedException`, Linux as `CryptographicException`), makes that key fail to verify, never throws.
- The root trust anchors are KSK-2017 (20326) and KSK-2024 (38696) as SHA-256 DS records from IANA's
  root-anchors.xml, held in code; the unit tests check both against a recorded root DNSKEY RRset.

## Consequences

The verifier answers one RRSIG or one DNSKEY at a time; walking a chain from the root, combining several RRSIGs and
NSEC/NSEC3 denial proofs are BL-4332's.
