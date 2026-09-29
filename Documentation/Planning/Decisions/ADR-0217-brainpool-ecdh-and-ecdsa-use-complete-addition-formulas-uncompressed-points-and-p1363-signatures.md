# ADR-0217 — Brainpool ECDH and ECDSA use complete addition formulas, uncompressed points and P1363 signatures

- **Status:** Accepted
- **Date:** 2026-09-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-742.

## Context

ADR-0118 puts ECDH and ECDSA over brainpoolP256r1, brainpoolP384r1 and brainpoolP512r1
in `Curl.Cryptography.UnitLibrary` as `BrainpoolEcdh` and `BrainpoolEcdsa`, each taking
the curve as a parameter, because the BCL has brainpool curves on Windows and only some
Linux distributions, and not on macOS. The task left open how points are added without a
secret-dependent branch, which point and signature encodings the API takes, where a
private key comes from, and which Wycheproof files pin verification.

## Decision

- **Curve choice:** a public enum `BrainpoolCurve` (`BrainpoolP256r1`, `BrainpoolP384r1`,
  `BrainpoolP512r1`), the r1 curves TLS 1.3 names in RFC 8734. The twisted t1 curves have
  no TLS 1.3 code point, and OpenSSL's `-tls-groups` list omits them, so they are not built.
- **Arithmetic:** the field and the group order run on `MontgomeryModulus`, whose
  Montgomery form the point coordinates stay in (it gains a public `ToMontgomeryForm`).
  Points are projective and added by algorithm 1 of Renes, Costello and Batina (2016),
  the complete formulas for any a. They are correct for doubling, for the point at
  infinity and for P + (-P) on every odd-order curve, and every brainpool r1 curve has
  cofactor 1. One formula with no special case means scalar multiplication never
  branches. Scalar multiplication is a fixed 4-bit window whose table look-up reads all
  16 entries by mask; inversion is Fermat's, a fixed exponentiation.
- **Points:** uncompressed only, `0x04 || x || y` (SEC 1 section 2.3.3). TLS 1.3's key
  share (RFC 8446 section 4.2.8.2) and RFC 8422's TLS 1.2 point formats carry only this
  form. A peer's point of the wrong length or form, the one-byte infinity `0x00`, a
  coordinate not below p, or a point off the curve is `false`, with the destination
  zeroed.
- **Shared secret:** the x-coordinate at the field's length with leading zeros kept, the
  TLS 1.3 and RFC 8422 premaster secret.
- **Signatures:** r || s at q's length (IEEE P1363), as `DsaSignature` does under
  ADR-0201. The caller encodes TLS's and X.509's DER `ECDSA-Sig-Value`. Signing uses
  RFC 6979's deterministic nonce over SHA-1 to SHA-512 through the existing
  `DeterministicDsaNonce`, since every brainpool order is a whole number of bytes.
  Verification takes a hash of any length and uses its leftmost bits up to q's length.
- **Key generation:** 8 more random bytes than the key, reduced modulo q, with 0 made 1
  by mask (FIPS 186-4 B.4.1's extra random bits). This avoids a rejection loop whose
  iteration count would depend on the random draw and that no test could cover
  deterministically.
- **Vectors:** RFC 7027 appendix A for ECDH, and Wycheproof's three IEEE P1363 ECDSA
  files that match the RFC 8734 schemes (P256r1 with SHA-256, P384r1 with SHA-384,
  P512r1 with SHA-512), valid and invalid alike. The DER files test a DER parser, which
  this API does not have, and the P224r1, P320r1 and SHA-3 files test combinations TLS
  never uses. The P256r1 file runs in the fast tests. The P384r1 and P512r1 files take
  several seconds each in a Debug build, so they are `TestCategory=Integration`
  (ADR-0118, "Tests").

## Consequences

- Brainpool key exchange and signatures behave the same on Windows, Linux and macOS. On
  Windows the tests also check the results against CNG's own brainpool ECDH and ECDSA.
- The complete formulas cost 12 multiplications per addition. That is slower than
  special-cased Jacobian formulas, and verification is not the constant-time path, but
  it reuses that path rather than adding a faster variable-time one. Speed can be
  revisited with measurements.
- Wiring the `brainpoolP*r1tls13` groups and `ecdsa_brainpoolP*r1tls13_*` schemes into
  the TLS client (BL-699, BL-709) is those tasks' work, including the DER encoding of
  signatures.
