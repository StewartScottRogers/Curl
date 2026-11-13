---
id: BL-5061
title: Accept a keyAgreement-only server certificate on the OpenSSL profile as OpenSSL's KU_TLS does
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Networking.UnitLibrary/OpenSslKeyUsageRefusal.cs, Curl.Networking.UnitTests/ServerCertificateVerificationTests.VerifyChain.cs]
requirement: none
created: 2026-11-12
completed: 2026-11-12
---
# BL-5061 — Accept a keyAgreement-only server certificate on the OpenSSL profile as OpenSSL's KU_TLS does

## Goal

On the OpenSSL profile (Linux and macOS), Curl accepts a server certificate whose keyUsage allows keyAgreement but neither digitalSignature nor keyEncipherment, as curl's OpenSSL build accepts it.

## Context

The shift-end review of 2026-11-12 found this in the BL-5009 change (type-design-analyzer and code-reviewer, ADR-0710).

- `Curl.Networking.UnitLibrary/OpenSslKeyUsageRefusal.cs:13` defines `ServerUse` as `DigitalSignature | KeyEncipherment`.
- OpenSSL's TLS server purpose check is `check_purpose_ssl_server` in `crypto/x509/v3_purp.c`. It rejects with `ku_reject(x, KU_TLS)`, and `KU_TLS` is `KU_DIGITAL_SIGNATURE | KU_KEY_ENCIPHERMENT | KU_KEY_AGREEMENT`.
- So real curl accepts a static-ECDH or DH certificate whose keyUsage is `keyAgreement`.
- Curl refuses the same certificate: it adds `RemoteCertificateChainErrors` (`ServerCertificateVerification.cs` ~561) and fails with exit 60 and `unsuitable certificate purpose (26)` (`TlsFailureMessages.cs:866`).

That breaks the drop-in promise.

## Acceptance criteria

- [x] `OpenSslKeyUsageRefusal.ServerUse` includes `X509KeyUsageFlags.KeyAgreement`, and its summary and `<returns>` text name all three bits.
- [x] A unit test in `Curl.Networking.UnitTests` shows that `OpenSslKeyUsageRefusal.ForbidsServerUse` does not refuse a certificate whose keyUsage is `keyAgreement` only.
- [x] The existing test that refuses a certificate whose keyUsage is only `keyCertSign` still passes.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

KeyAgreement added to ServerUse (OpenSSL KU_TLS); a KeyAgreement DataRow joins the accepted-leaf test, the KeyCertSign refusal test still passes. Quality run: 0 failing members. Commit carries the code.

## Log

- 2026-11-12: Created.
- 2026-11-12: Backlog -> Doing. Criteria at claim: fc682092 5db41aa6 31edfc9f 3d836d5e.
- 2026-11-12: Doing -> Done. KeyAgreement accepted
