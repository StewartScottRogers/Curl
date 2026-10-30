# ADR-1115: Privacy Pass token type 1 builds the P-384 VOPRF by hand on BigInteger

- Status: Accepted
- Date: 2026-10-29
- Task: BL-4321
- Decided by Claude under Stewart's delegation

## Context

ADR-1110 left token type `0x0001` (RFC 9578 §5, VOPRF(P-384, SHA-384)) undone. The base class library has P-384
keys (`ECDiffieHellman`, `ECDsa`) but no way to add points, multiply an arbitrary point by a scalar, hash to the
curve or decode a compressed point, which RFC 9497's client and RFC 9380's hash-to-curve need.

## Decision

- `Curl.Extension.PrivacyPass.UnitLibrary/Tokens` holds three hand-built pieces on `System.Numerics.BigInteger`
  and `SHA384`: `P384Group` (point addition and double-and-add multiplication in Jacobian coordinates, a = -3
  doubling, SEC1 compressed encoding and decoding with an on-curve check, 48-byte scalars), `P384HashToCurve`
  (`expand_message_xmd` with SHA-384, `hash_to_field` with L = 72, simplified SWU with Z = -12, suite
  `P384_XMD:SHA-384_SSWU_RO_`) and `VoprfP384Sha384` (RFC 9497 mode `0x01`: Blind, VerifyProof with
  ComputeComposites, Finalize).
- The curve constants (p, n, b, G) are written in the source, copied from the BCL's explicit `nistP384`
  parameters, not read from `ExportExplicitParameters` at run time: explicit curve export is not available on every
  platform (macOS), and a test cross-checks the arithmetic against a BCL key (d·G = Q) on all three.
- The blind is drawn by rejection (RFC 9497 §4.7 RandomScalar): 48 random octets until they are a scalar in
  [1, n), so a fixed blind from the RFC's vectors reproduces their blinded element exactly.
- A proof that makes a commitment the identity (for example c = s = 0) does not verify, rather than throwing: the
  identity has no encoding, so `VerifyProof` treats the `CryptographicException` of encoding it as a failed proof.
- `PrivacyPassIssuance` runs VOPRF for a challenge of type `0x0001` and Blind RSA for any other type it is given
  (the hook offers it only `0x0002` until BL-4315 wires `0x0001` in). A `token-key` that is not a compressed P-384
  point, and a response whose proof does not verify, both fail the transfer with exit 8, as the Blind RSA type's
  bad key and bad signature do.
- Constant time is not attempted: the client's only secret is a one-use blind, and the issuer's key never reaches
  the client.

## Consequences

The RFC 9497 Appendix A.4.2 vectors and the RFC 9380 Appendix J.2.1 vectors pin the three pieces; a test issuer
in the unit tests evaluates and proves with the vectors' key for the issuance round trip.
