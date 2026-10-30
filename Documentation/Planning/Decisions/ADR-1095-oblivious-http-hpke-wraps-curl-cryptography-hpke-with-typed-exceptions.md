# ADR-1095 — Oblivious HTTP's HPKE wraps Curl.Cryptography's Hpke with typed exceptions

- **Status:** Accepted
- **Date:** 2026-10-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-4311.

## Context

BL-4311 asks for HPKE base mode (RFC 9180 sections 4, 5.1, 5.2 and 5.3) with DHKEM(X25519, HKDF-SHA256),
HKDF-SHA256 and AES-128-GCM, AES-256-GCM or ChaCha20Poly1305 in the new `Curl.Extension.ObliviousHttp.UnitLibrary`,
for Oblivious HTTP's encapsulation (RFC 9458, BL-4310). `Curl.Cryptography.UnitLibrary`, a shared layer an extension
may reference, already has exactly that for Encrypted Client Hello: `Hpke`, `HpkeContext` and the suite enumerations,
tested against the same RFC 9180 Appendix A vectors. Its failures are ADR-0118's `false` (`TrySetupBaseSender`,
`TryOpen`), while the task asks for a typed exception when open fails.

## Decision

- **No second HPKE.** The extension's `Hpke/` holds no KEM, key schedule or AEAD of its own; `HpkeSender` and
  `HpkeReceiver` call `Curl.Cryptography`'s `Hpke` and own the `HpkeContext` it gives. One concept keeps one
  implementation, and the ECH and Oblivious HTTP paths cannot drift apart.
- **Exceptions, not `bool`.** Oblivious HTTP treats a failed decapsulation or open as the end of the exchange, so the
  wrapper turns `false` into `HpkeSetupException` (an unusable recipient public key or encapsulated key) and
  `HpkeOpenException` (a ciphertext that does not authenticate), both `CryptographicException`s. A caller mistake
  (an unsupported suite value, a private key that is not 32 bytes) stays `Curl.Cryptography`'s `ArgumentException`.
- **Arrays in and out.** `Seal`, `Open` and `Export` return new arrays sized from the input, which is what RFC 9458's
  encapsulated request and response framing needs.
- **Suites.** `HpkeSuite` carries `Curl.Cryptography`'s `HpkeKem`, `HpkeKdf` and `HpkeAead`; DHKEM(P-256) passes
  through untested here because Oblivious HTTP does not need it.

## Consequences

The extension's tests reproduce A.1.1 and A.2.1 through the wrapper; a change to the suites happens once, in
`Curl.Cryptography`.
