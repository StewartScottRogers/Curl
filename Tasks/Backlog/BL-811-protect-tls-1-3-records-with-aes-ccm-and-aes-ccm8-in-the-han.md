---
id: BL-811
title: Protect TLS 1.3 records with AES-CCM and AES-CCM8 in the hand-built TLS client
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-700, BL-738]
touches: [Curl.Tls.UnitLibrary, Curl.Tls.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-811 — Protect TLS 1.3 records with AES-CCM and AES-CCM8 in the hand-built TLS client

## Goal

`Tls13RecordProtection` protects and opens records of `TLS_AES_128_CCM_SHA256` (0x1304) and `TLS_AES_128_CCM_8_SHA256` (0x1305) with BL-738's hand-built `AeadAesCcm`, so a TLS 1.3 connection negotiating either suite completes instead of being refused before anything is sent.

## Context

- BL-700 built the TLS 1.3 record layer for the GCM and ChaCha20-Poly1305 suites; `Tls13RecordProtection.CanProtect` answers false for 0x1304 and 0x1305, and `Tls13ClientConnection` refuses them before sending the ClientHello (test `SuiteWhoseRecordsCannotBeProtectedIsRefusedBeforeAnythingIsSent`).
- CCM8's tag is 8 bytes, not 16: `Tls13RecordProtection`'s tag length (and so the shortest valid fragment and the `bad_record_mac` check) becomes per-suite.
- The AEAD goes behind `ITlsAead` like `AesGcmTlsAead`; RFC 8446 section 5.3 nonces and section 5.2 additional data are unchanged.
- ADR-0140 lists the suites; OpenSSL enables CCM only when named (`--tls13-ciphers`, BL-709).

## Acceptance criteria

- [ ] `Tls13RecordProtection.CanProtect` is true for 0x1304 and 0x1305, and `Curl.Tls.UnitTests` exchange application data both ways with the in-memory server over a pipe for each (the `ApplicationDataCrossesBothWaysWithEachCipherSuite` rows).
- [ ] A CCM8 record whose fragment is shorter than 8 bytes, or whose tag is corrupted, fails with `bad_record_mac`.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Tls.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Filed by BL-700, which could not cover the CCM suites before BL-738 builds AES-CCM.
- 2026-09-29 (lane 5): the `Curl.Tls` change is written and green (1066 `Curl.Tls.UnitTests`
  pass, `dotnet build Curl.slnx -warnaserror` clean): `CanProtect` is `0x1301..0x1305`,
  `Create` picks `AesCcmTlsAead` for 0x1304/0x1305, the tag length is per suite (8 for
  CCM8), new tests `CcmRecordsCarryTheSuitesTagAndOpenOnTheOtherSide`,
  `Ccm8FragmentShorterThanItsEightByteTagIsABadRecordMac`,
  `Ccm8RecordWithACorruptedTagIsABadRecordMac`, CCM rows in
  `ApplicationDataCrossesBothWaysWithEachCipherSuite`; the refusal tests now use the unknown
  suite 0x1306. It was left uncommitted (the shift stashes it).
- Why it went back to Backlog: `Curl.Networking.UnitTests` use `TLS_AES_128_CCM_SHA256` as
  "a TLS 1.3 suite the hand-built client cannot run" -
  `HandBuiltTlsProviderTests.AuthenticateAsClientAsync_WithOnlyTls13CiphersTheClientCannotRunAndNoCeiling_ConnectsOverTls12`
  (hangs once CCM is runnable) and the `TLS_AES_128_CCM_SHA256` row of
  `AuthenticateAsClientAsync_WithCiphersTheOpenSslBuildCannotOffer_FailsWithExit59`. With
  every TLS 1.3 suite runnable no OpenSSL name reaches that case, so those tests (and the
  `OffersSuiteFor(Tls13RecordProtection.CanProtect)` false branch in
  `HandBuiltTlsProvider.HandshakeAsync` / `CanOffer`, which may become unreachable) must be
  reworked in `Curl.Networking.UnitLibrary` / `.UnitTests`. That project is in BL-819's
  `touches`, so `Curl.Networking.UnitLibrary` and `Curl.Networking.UnitTests` were added
  here and the task waits for BL-819 to leave Doing.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Backlog. Needs Curl.Networking.UnitLibrary and Curl.Networking.UnitTests (their tests treat TLS_AES_128_CCM_SHA256 as unrunnable), which BL-819 in Doing touches
