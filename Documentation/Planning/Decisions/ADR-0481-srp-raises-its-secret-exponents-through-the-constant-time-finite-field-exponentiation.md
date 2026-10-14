# ADR-0481 — SRP raises its secret exponents through the constant-time finite-field exponentiation

- **Status:** Superseded by [ADR-0480](ADR-0480-the-six-tls-srp-options-warn-with-no-function-as-curl-8-22-0-does.md): curl 8.22.0 dropped TLS-SRP, and BL-2159 removed `SrpClient` with the rest of the TLS-SRP client
- **Date:** 2026-10-13
- **Amends:** ADR-0229 (which put `SrpClient`'s arithmetic on `System.Numerics.BigInteger`)

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-2141.

## Context

Audit finding AF-0182 (accepted): `SrpClient` raised g to the password-derived x and to
the private value a, and raised B - k * v to a + u * x, with `BigInteger.ModPow`, a
variable-time square-and-multiply. OpenSSL marks those exponents `BN_FLG_CONSTTIME`.
`Curl.Cryptography.UnitLibrary` already has a fixed-window Montgomery exponentiation
(`MontgomeryModulus`, internal) whose running time depends only on the exponent's length,
and exposes it publicly through `FiniteFieldDiffieHellman` and
`FiniteFieldDiffieHellmanGroup.TryCreate(prime, generator)`.

## Decision

1. Every power of a secret in `SrpClient` goes through `FiniteFieldDiffieHellman`:
   `ComputePublicValue` for g^x and g^a, `TryComputeSharedSecret` for (B - k * v)^(a + u * x).
   `SrpGroup.ExponentiationGroup` caches each Appendix A group as a
   `FiniteFieldDiffieHellmanGroup`, so the Montgomery set-up runs once per group.
2. The exponent a + u * x is written at a fixed width, one byte more than the longer of a
   and x + u, both public lengths, so its value never sets the window count.
3. `TryComputeSharedSecret` refuses the bases 0, 1 and N - 1. Their powers need no
   exponentiation: 0 and 1 are their own, and N - 1 gives 1 for an even exponent and N - 1
   for an odd one. Only a server that holds the verifier can steer B there.
4. `Curl.Cryptography` keeps `MontgomeryModulus` internal; the public Diffie-Hellman API
   already does what SRP needs, so no `InternalsVisibleTo` is added and no project
   outside `Curl.Tls` changes.

The remaining `BigInteger` work (k * v, B - k * v, u * x and the sum) is multiplication and
addition, not exponentiation; it is outside AF-0182, as is the hash of the password.

## Consequences

- The RFC 5054 Appendix B vectors and the SRP handshake tests still pass unchanged.
- `SrpClientTests.PremasterSecretOfADegenerateBasisIsItsPower` pins decision 3.
