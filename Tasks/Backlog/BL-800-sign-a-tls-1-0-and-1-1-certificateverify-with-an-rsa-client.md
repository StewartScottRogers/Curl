---
id: BL-800
title: Sign a TLS 1.0 and 1.1 CertificateVerify with an RSA client key
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-703]
touches: [Curl.Cryptography.UnitLibrary, Curl.Cryptography.UnitTests, Curl.Tls.UnitLibrary, Curl.Tls.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-800 — Sign a TLS 1.0 and 1.1 CertificateVerify with an RSA client key

## Goal

A client with an RSA certificate key answers a TLS 1.0 or 1.1 server's CertificateRequest with its certificate and a CertificateVerify signed as RFC 2246 section 7.4.3 requires, instead of an empty Certificate.

## Context

- TLS 1.0 and 1.1 sign with RSA as a PKCS #1 block type 1 over the 36-byte MD5 + SHA-1 hash with no DigestInfo (RFC 2246 section 7.4.3; RFC 4346 the same). `TlsSignatureScheme.BuildMd5Sha1Block` in `Curl.Tls.UnitLibrary` already builds that padded block, and `TlsCertificatePublicKey.VerifyRsaMd5Sha1` verifies it with a raw public-key operation.
- The BCL's `RSA.SignHash` always wraps the hash in a DigestInfo, so `RsaTlsSigningKey` cannot fit the `TlsSignatureKind.RsaMd5Sha1` rule (see its `rule.Kind != TlsSignatureKind.RsaMd5Sha1` check). Today a TLS 1.0/1.1 server that requests a certificate therefore gets an empty Certificate from an RSA-keyed client. ECDSA client keys already sign with SHA-1 at these versions.
- Build the missing piece in `Curl.Cryptography.UnitLibrary`: a raw RSA private-key operation `m^d mod n` using the CRT parameters (p, q, dP, dQ, qInv) exported from `RSAParameters`, with base blinding (a random r, `m * r^e`, then unblind by `r^-1`) and constant-time modular exponentiation. `MontgomeryModulus` (BL-739, used by `FiniteFieldDiffieHellman`) is the existing constant-time exponentiation to build on. Check the result by re-applying the public exponent before returning it (fault-attack guard), as OpenSSL does.
- Then let `RsaTlsSigningKey` sign the `RsaMd5Sha1` rule by building the block with `BuildMd5Sha1Block` and running the raw private operation; every other rule keeps using the BCL `RSA`.
- Tests use the in-memory `Tls12TestServer` in `Curl.Tls.UnitTests` (credentials in `TestServerCredential`). The handshake is BL-703's `Tls12ClientHandshake`; its decisions are in ADR-0152 (`Documentation/Planning/Decisions/ADR-0152-the-tls-1-2-client-handshake-refuses-legacy-renegotiation-ignores-hello-request-and-matches-openssls-alerts.md`).

## Acceptance criteria

- [ ] `Curl.Cryptography.UnitLibrary` has the raw RSA CRT private-key operation; `Curl.Cryptography.UnitTests` show it matches a published RSA test vector (e.g. an RSASP1 vector from the PKCS #1 v2.1 test data) byte for byte, that its output equals the non-CRT `m^d mod n`, and that an input not below n is rejected.
- [ ] `RsaTlsSigningKey` accepts the `RsaMd5Sha1` rule; a `Tls12SignatureTests` test shows its signature verifies with `TlsCertificatePublicKey` and equals the reference `BigInteger.ModPow(block, d, n)` computed in the test.
- [ ] A `Tls12ClientHandshakeTests` test runs a TLS 1.0 handshake (and one at TLS 1.1) against `Tls12TestServer` requesting a certificate, with an RSA client certificate: the client sends the non-empty Certificate and a CertificateVerify the server verifies, and the handshake completes.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Cryptography.UnitLibrary` and `-Library Curl.Tls.UnitLibrary` each report 100% line and branch coverage and no failing member.

## Notes

- `Curl.Tls.UnitLibrary` already references `Curl.Cryptography.UnitLibrary`, so no project file changes are needed.

## Log

- 2026-09-28: Created.
