# ADR-0195 — TLS 1.0 and 1.1 RSA client signatures use a hand-built, blinded CRT private-key operation

- **Status:** Accepted
- **Date:** 2026-09-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-804.

## Context

A TLS 1.0 or 1.1 CertificateVerify from an RSA key is a PKCS #1 block type 1 over the
36-byte MD5 + SHA-1 hash with no DigestInfo (RFC 2246 section 7.4.3, RFC 4346 the same).
The BCL's `RSA.SignHash` always wraps the hash in a DigestInfo, so `RsaTlsSigningKey`
could not make it, and a client with an RSA certificate answered a TLS 1.0 or 1.1
CertificateRequest with an empty Certificate. curl on either platform build signs it
(Schannel through CNG's `BCRYPT_PAD_PKCS1` with no algorithm identifier, OpenSSL through
`RSA_private_encrypt`). Only the raw private operation m^d mod n was missing, and the
root `CLAUDE.md` says to hand-build what the BCL lacks.

## Decision

- `Curl.Cryptography.UnitLibrary` gains `RsaCrtPrivateKey` (public, `IDisposable`):
  PKCS #1's RSASP1 on the key's CRT values (p, q, dP, dQ, qInv, as
  `RSA.ExportParameters(true)` gives them), m1 = c^dP mod p and m2 = c^dQ mod q
  recombined by Garner's formula. Pinned byte for byte to PKCS #1 v2.1's `pss-vect.txt`
  Examples 1.1 (1024 bits) and 2.1 (1025 bits), and to the non-CRT m^d mod n.
- **Blinding, as OpenSSL blinds.** Each call exponentiates m * r^e mod n for a random r
  and multiplies the result by r^-1. r^-1 is computed by Fermat's little theorem through
  the same CRT exponentiation with exponents p - 2 and q - 2, so no modular inverse with
  a secret-dependent running time (extended Euclid) is needed. r is drawn from the
  modulus length plus 8 random bytes reduced mod n (bias below 2^-64); an overload takes
  those bytes, as ADR-0118 asks of every operation that needs randomness.
- **Fault check, as OpenSSL checks.** The result is raised to e and compared with m
  (`CryptographicOperations.FixedTimeEquals`) before it is written; a mismatch throws
  `CryptographicException` and the destination is left untouched, so a fault can never
  release a signature that factors n.
- **Constant time with a secret modulus.** `MontgomeryModulus` now derives R mod m and
  R^2 mod m by doubling 1 with masked modular additions instead of `BigInteger`
  division, and gains `Reduce` (Horner over limb chunks), `Subtract`, `MultiplyModulo`,
  `IsBelowModulus` and `Clear`. Only byte lengths, which are public, shape the running
  time; FFDH keeps working unchanged on the same class.
- `RsaTlsSigningKey` signs the `RsaMd5Sha1` rule by exporting the private parameters,
  building the block with `TlsSignatureScheme.BuildMd5Sha1Block` and applying
  `RsaCrtPrivateKey`, zeroing the exported values afterwards. Every other rule keeps the
  BCL's `RSA`. A key whose private parameters refuse export (a non-exportable CNG key)
  does not fit the rule - checked once and remembered - so it still sends an empty
  Certificate, as before, rather than failing the handshake. A key certified as
  RSASSA-PSS has no TLS 1.0/1.1 signature and does not fit either.

## Consequences

- An RSA client certificate now authenticates to TLS 1.0 and 1.1 servers through the
  hand-built client, on every platform.
- A PKCS #12 file the Schannel-build loader opens without `X509KeyStorageFlags.Exportable`
  yields a non-exportable key on Windows, which still cannot sign at TLS 1.0/1.1. That is
  `Curl.Networking.UnitLibrary`'s to change and is filed as BL-946.
- The CRT operation is about four times faster than a plain m^d mod n, and blinding
  doubles it (two CRT exponentiations per signature); one CertificateVerify per handshake
  makes that immaterial.

## Alternatives considered

- **`BigInteger.ModPow(block, D, N)`.** Short, but `BigInteger` is not constant-time in
  the exponent, which `Curl.Cryptography`'s rules forbid for a secret. Rejected.
- **`RSA.Decrypt` with no padding as the raw operation.** `RSAEncryptionPadding` has no
  "none" mode in the BCL. Not available.
- **r^-1 by an extended Euclid on a masked r.** Needs a hand-written inverse and still
  runs `BigInteger`-style variable-time arithmetic on a secret-derived value; Fermat
  through the existing CRT path adds no new code path. Rejected.
