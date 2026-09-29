---
id: BL-723
title: Protect QUIC packets with the RFC 9001 initial secrets, AEAD and header protection
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-722, BL-673, BL-697]
touches: [Curl.Quic.UnitLibrary, Curl.Quic.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-723 — Protect QUIC packets with the RFC 9001 initial secrets, AEAD and header protection

## Goal

`Curl.Quic.UnitLibrary` derives initial secrets from the destination connection ID (RFC 9001 section 5.2), protects and unprotects packet payloads with AES-128-GCM, AES-256-GCM and ChaCha20-Poly1305, applies and removes header protection with AES-ECB and ChaCha20 (section 5.4), handles key update (section 6), and builds Retry integrity tags (section 5.8), matching RFC 9001 Appendix A byte for byte.

## Context

- Standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28). Builds on BL-722 (packets). HKDF-Expand-Label with the `quic` labels from `Curl.Tls.UnitLibrary` (BL-697); raw ChaCha20 and the AEAD from `Curl.Cryptography.UnitLibrary` (BL-673); AES-ECB and `AesGcm` from the BCL (or the fallback BL-669's ADR names). Add both references here (allowed by BL-667's ADR).
- References: RFC 9001 Appendix A.1 (keys), A.2 (client Initial), A.3 (server Initial), A.4 (Retry), A.5 (ChaCha20-Poly1305 short header packet).

## Acceptance criteria

- [x] `Curl.Quic.UnitTests` reproduce every key, IV and header-protection key of A.1, the protected packets of A.2, A.3 and A.5 from their plaintext, the Retry tag of A.4, and remove protection from each back to the plaintext; a tampered packet fails AEAD with the typed failure and is dropped, not fatal, as RFC 9001 requires.
- [x] A key update round trip (phase bit flip, next-generation keys) is covered.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Quic.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Plan, carried out in the session: `QuicInitialSecrets` (salt, `client in`/`server in`),
  `QuicPacketKeys` (`quic key`/`iv`/`hp`/`ku` over `Tls13KeySchedule.ExpandLabel`),
  `QuicHeaderProtection` (AES-ECB or ChaCha20 mask behind the internal
  `IQuicHeaderProtectionMask`), internal `QuicPayloadProtection` (nonce = IV XOR packet
  number, AEAD behind `IQuicPacketAead`), public `QuicPacketProtection` (one direction per
  level, `Protect`/`Unprotect`/`UpdateKeys`/`DiscardPreviousKeys`), `QuicUnprotectResult` +
  `QuicUnprotectStatus`, and `QuicRetryIntegrity`. References to `Curl.Tls.UnitLibrary`
  and `Curl.Cryptography.UnitLibrary` added, as the library's CLAUDE.md and BL-667's ADR allow.
- `Curl.Tls`'s `ITlsAead` is internal, so QUIC has its own two-method `IQuicPacketAead`
  over the same primitives (BCL `AesGcm`, hand-built `AeadChaCha20Poly1305`) rather than
  widening `Curl.Tls`'s surface, which is outside this task's `touches`.
- The typed failure: `Unprotect` returns `QuicUnprotectStatus.DroppedTooShortForSample` or
  `DroppedAuthenticationFailed` with the packet's length, so coalesced packets after it are
  still read; only a Reserved Bit set on an authentic packet throws (`ProtocolViolation`,
  RFC 9000 section 17.2). A header the codec cannot read still throws, as `QuicPacketCodec` does.
- Key-phase choice (RFC 9001 section 6.3 and 6.5, the RFC's own recommendation): a short
  header of the other phase opens with the previous keys while no packet of the current
  phase has arrived or its number is below the first that did; otherwise with the lazily
  derived next keys, and success moves the receiver's phase and reports `KeyPhaseChanged`.
  Timing out the previous keys (three PTOs) belongs to loss recovery; it calls
  `DiscardPreviousKeys`.
- The AES-CCM suites are refused with `ArgumentException` until BL-738's hand-built AES-CCM
  lands; filed BL-818 to add `TLS_AES_128_CCM_SHA256`. No ADR: this is sequencing, not a
  decision to leave it out.
- RFC 9001 Appendix A was read from https://www.rfc-editor.org/rfc/rfc9001.txt; every
  vector is pinned in `QuicPacketProtectionTests` and all pass on the first run.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. Curl.Quic protects and unprotects packets per RFC 9001: Initial secrets, AES-GCM and ChaCha20-Poly1305 AEAD, header protection, key update and Retry tags, all Appendix A vectors pinned
