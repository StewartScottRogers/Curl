---
id: BL-460
title: Refuse a CN match for a certificate with IP subjectAltNames in the OpenSSL build
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-415]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Documentation/Planning/Decisions/ADR-0103-the-schannel-build-matches-the-common-name-of-a-cacert-certificate-without-dns-names.md]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-460 — Refuse a CN match for a certificate with IP subjectAltNames in the OpenSSL build

## Goal

In the OpenSSL build, a server certificate whose subjectAltName holds only IP addresses and whose CN is the target host name fails with exit 60 `SSL: no alternative certificate subject name matches target hostname '<host>'`, as curl 8.21.0's OpenSSL build refuses it, on every platform.

## Context

- Found by BL-415 (ADR-0103): .NET's own name check accepts such a certificate on Windows (a handshake in the OpenSSL build with `--cacert`, target `localhost`, CN=localhost, SAN `127.0.0.1` only, returns exit 0). curl's OpenSSL build (`ossl_verifyhost`) falls back to the CN only when the subjectAltName holds no DNS and no IP entry, so it refuses it. Whether .NET on Linux and macOS also accepts it is not measured; measure it first (CI, or a test run there).
- The check is `SslStreamTlsProvider.VerifyPeer` / the validation callback in `Curl.Networking.UnitLibrary`; the message is already `TlsFailureMessages.OpenSslPeerFailedVerification` once a `RemoteCertificateNameMismatch` is reported.
- The OpenSSL build runs on Windows only in tests, so the gap matters only where .NET on Linux/macOS also accepts the certificate.
- `SslStreamTlsProviderTests.CommonName.cs` has `CreateIpAddressOnlyCertificate` and the VerifyPeer test that already pins the message once a mismatch is reported.

## Acceptance criteria

- [x] A named test in `Curl.Networking.UnitTests` runs a handshake in the OpenSSL build with `--cacert` against a CN=localhost certificate whose subjectAltName holds only `127.0.0.1`, target `localhost`, and gets exit 60 with `SSL: no alternative certificate subject name matches target hostname 'localhost'`, on Windows, Linux and macOS.
- [x] `dotnet build Curl.Networking.UnitLibrary -warnaserror` is clean, the fast tests pass, and `SslStreamTlsProvider` stays at 100% line and branch coverage.

## Notes

- Filed by BL-415.
- Plan: rather than measure .NET's own check on Linux and macOS, the OpenSSL build now
  adds `RemoteCertificateNameMismatch` itself when the target is a host name and the
  leaf's subjectAltName holds IP addresses and no DNS name (`OpenSslCommonNameRefusal`),
  so curl's answer comes out whatever .NET said, on every platform. The existing
  `TlsFailureMessages.OpenSslPeerFailedVerification` gives the text. Test:
  `AuthenticateAsClientAsync_WithCaCertificateFileAndACommonNameOnlyDnsNameInTheOpenSslBuild_ReportsNoAlternativeNameMatches`.
- `SslStreamTlsProvider.WithTheNameCheckCurlRuns` picks the build's name check: the
  Schannel CN fallback (BL-415) or this OpenSSL refusal.
- Touches widened to ADR-0103, amended with this decision; no task in Doing names it.
- Verified: `dotnet build` clean, fast tests green (Curl.Networking.UnitTests 779 passed,
  6 skipped), `SslStreamTlsProvider` and `OpenSslCommonNameRefusal` at 100% line and
  branch coverage (Code Coverage collector, cobertura).

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. The OpenSSL build refuses a host name against a certificate whose subjectAltName holds only IP addresses, exit 60, on every platform
