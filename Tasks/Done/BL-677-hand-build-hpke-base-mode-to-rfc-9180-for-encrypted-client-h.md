---
id: BL-677
title: Hand-build HPKE base mode to RFC 9180 for Encrypted Client Hello
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-671, BL-673]
touches: [Curl.Cryptography.UnitLibrary, Curl.Cryptography.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-677 — Hand-build HPKE base mode to RFC 9180 for Encrypted Client Hello

## Goal

`Curl.Cryptography.UnitLibrary` seals messages with HPKE base mode (RFC 9180) for the suites Encrypted Client Hello uses: DHKEM(X25519, HKDF-SHA256) and DHKEM(P-256, HKDF-SHA256), HKDF-SHA256, and AES-128-GCM, AES-256-GCM or ChaCha20-Poly1305.

## Context

- Consumer: ECH in the hand-built TLS 1.3 client (BL-706) for `--ech`. Builds on X25519 (BL-671) and ChaCha20-Poly1305 (BL-673); P-256 via the BCL's `ECDiffieHellman`, HKDF via `System.Security.Cryptography.HKDF`, AES-GCM via `AesGcm` or the hand-built fallback BL-669's ADR names. API: BL-669's ADR.
- RFC 9180 sections 4 (DHKEM), 5.1 (key schedule), 5.2 (encryption and nonce sequence), Appendix A (test vectors A.1.1, A.2.1, A.3.1 and the ChaCha20-Poly1305 variants: base mode only).

## Acceptance criteria

- [x] `Curl.Cryptography.UnitTests` reproduce the base-mode Appendix A vectors for each supported suite: `enc`, shared secret, key, base nonce, and the first sealed ciphertexts, with a deterministic ephemeral key injected as the RFC's vectors require.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Cryptography.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Shape, within ADR-0118's API rules (so no new ADR): `Hpke` (static setups),
  `HpkeContext` (`Seal`, `TryOpen`, `Export`, `SequenceNumber`, `IDisposable`), enums
  `HpkeKem`, `HpkeKdf`, `HpkeAead` valued by their RFC 9180 identifiers, internal
  `HpkeDhkem` and `HpkeLabeledHkdf`. The recipient side and `Export` are built too: they
  are small, complete HPKE base mode, and let the vectors' decryptions and exports be
  pinned; ECH itself only seals.
- The injected ephemeral key is skEm itself, not ikmE through `DeriveKeyPair`: the RFC's
  vectors give skEm, and ADR-0118 asks for "the random bytes" as a parameter.
- Vectors: RFC 9180 A.1.1, A.2.1, A.3.1 and A.5.1 cover four suites. The appendix has no
  base-mode AES-256-GCM vector for these KEMs, so those two come from the CFRG's
  `test-vectors.json` (github.com/cfrg/draft-irtf-cfrg-hpke), the file the appendix is
  abridged from. Each pins enc, shared_secret, key, base_nonce, exporter_secret, the
  sequence-0 and -1 ciphertexts, the empty-context export, and the recipient opening
  both; A.1.1's sequence 255/256 ciphertexts and two other exports are pinned too.
- P-256 peer points are checked on the curve in `HpkeDhkem` (BigInteger, public data)
  before import: Windows CNG reports an off-curve point as `PlatformNotSupportedException`,
  OpenSSL as `CryptographicException`, so leaving it to the platform would differ by OS.
- P-256 private keys are imported D-only (`ECParameters` without Q) and the BCL derives
  Q; this is supported on all three platforms in .NET 10.
- Sequence number is a `ulong` with a `checked` increment: overflow throws before a nonce
  repeats, far below RFC 9180's 2^96 - 1.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. HPKE base mode (RFC 9180) seals, opens and exports for DHKEM X25519/P-256 with AES-128-GCM, AES-256-GCM and ChaCha20-Poly1305, pinned to the RFC and CFRG vectors
