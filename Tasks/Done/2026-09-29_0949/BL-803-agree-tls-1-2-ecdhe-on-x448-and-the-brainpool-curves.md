---
id: BL-803
title: Agree TLS 1.2 ECDHE on x448 and the brainpool curves
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-703, BL-740, BL-742]
touches: [Curl.Tls.UnitLibrary, Curl.Tls.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-803 — Agree TLS 1.2 ECDHE on x448 and the brainpool curves

## Goal

`Tls12ClientHandshake` agrees ECDHE on x448 and on brainpoolP256r1, brainpoolP384r1 and brainpoolP512r1, and verifies an ECDSA ServerKeyExchange signed with a brainpool certificate key.

## Context

- BL-703 built `Tls12ClientHandshake` in `Curl.Tls.UnitLibrary`: ECDHE today on X25519 and P-256/384/521 (`Tls12EcdheParameters`, `Tls12ServerKeyExchange`), and `Tls12ClientSettings.SupportedGroups`, whose validation currently requires "Every supported group must be X25519 or a NIST curve." Tests use the in-memory `Tls12TestServer` in `Curl.Tls.UnitTests`. Its decisions are in ADR-0154 (`Documentation/Planning/Decisions/ADR-0154-the-tls-1-2-client-handshake-refuses-legacy-renegotiation-ignores-hello-request-and-matches-openssls-alerts.md`).
- ADR-0140 lists the groups x448 and brainpoolP256r1/384r1/512r1 (TLS 1.2 codes 26 to 28).
- BL-740 hand-builds X448 (RFC 7748) and BL-742 hand-builds ECDH and ECDSA over the brainpool curves (RFC 5639), both in `Curl.Cryptography.UnitLibrary`. Use those; the BCL does not provide either on every platform.
- Group codes: x448 `0x001e` (a 56-byte key share, RFC 8422 section 5.11 encoding as for X25519); brainpoolP256r1 `0x001a`, brainpoolP384r1 `0x001b`, brainpoolP512r1 `0x001c` (uncompressed points, RFC 7027).
- Signatures: the TLS 1.3 `ecdsa_brainpoolP…r1tls13_sha…` schemes (`0x081a`-`0x081c`) are TLS 1.3 only. In TLS 1.2 a brainpool ECDSA key signs with the ordinary `ecdsa_sha256`/`ecdsa_sha384`/`ecdsa_sha512` codes (`0x0403`, `0x0503`, `0x0603`) and at TLS 1.0/1.1 with ECDSA over SHA-1; the curve comes from the certificate, not the code point.

## Acceptance criteria

- [x] `Tls12ClientSettings.SupportedGroups` accepts `0x001a`, `0x001b`, `0x001c` and `0x001e`, and its validation message names the groups it allows.
- [x] `Tls12ClientHandshakeTests` complete an in-memory handshake against `Tls12TestServer` with ECDHE on each of x448, brainpoolP256r1, brainpoolP384r1 and brainpoolP512r1, each with an application-data record protected by the derived keys decrypted by the other side.
- [x] `Tls12TestServer` gains a brainpoolP256r1 ECDSA credential, and a handshake on an ECDHE_ECDSA suite verifies its ServerKeyExchange signature; a test shows a TLS 1.3-only `0x081a` scheme in a TLS 1.2 ServerKeyExchange is refused with the alert ADR-0154 names.
- [x] A server key share that is not a valid point on the chosen brainpool curve, or an all-zero x448 shared secret, fails the handshake with the alert ADR-0154 names (named tests in `Tls12ClientHandshakeFailureTests`).
- [x] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Tls.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- The TLS 1.3 brainpool groups and schemes belong to the TLS 1.3 handshake, not this task.
- Plan and result: `TlsNamedGroup` gains `BrainpoolP256r1/384r1/512r1` and
  `IsTls12EcdheGroup`; `Tls12ClientSettings` validates with it; the new
  `BrainpoolKeyShare` wraps `BrainpoolEcdh`, and `SystemTlsRandomSource.CreateKeyShare`
  makes it (x448 reuses `X448KeyShare`, whose all-zero secret was already refused);
  `TlsCertificatePublicKey` routes a brainpool curve OID to `BrainpoolEcdsa.VerifyHash`.
  `Tls12ClientHandshake` itself needed no logic change.
- Decisions (ADR-0219): the default `SupportedGroups` is unchanged, the new groups are
  opt-in; a brainpool key of the wrong length is `bad_certificate`, an off-curve key of the
  right length `decrypt_error`; a `0x081a` scheme is refused as a scheme not offered.
- `touches` gained `Documentation/Planning/Decisions` for ADR-0219 and its index row; no
  task in Doing names it.
- The test credential (`TestServerCredential.Brainpool`) builds its certificate around a
  hand-written `SubjectPublicKeyInfo` and signs with `BrainpoolEcdsa` in test code, so the
  tests run on macOS, which has no brainpool curves in the BCL.
- Not done here: a brainpool client certificate (`TlsSigningKey`) - nothing asks for it yet.
- Measured: `Measure-CodeQuality.ps1 -Library Curl.Tls.UnitLibrary` 100% line, 100% branch,
  0 failing members (876); `Curl.Tls.UnitTests` 1058 passed.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. TLS 1.2 ECDHE agrees on x448 and brainpoolP256r1/384r1/512r1, and verifies a brainpool ECDSA ServerKeyExchange
