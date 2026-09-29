---
id: BL-804
title: Sign a TLS 1.0 and 1.1 CertificateVerify with an RSA client key
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-703]
touches: [Curl.Cryptography.UnitLibrary, Curl.Cryptography.UnitTests, Curl.Tls.UnitLibrary, Curl.Tls.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-804 — Sign a TLS 1.0 and 1.1 CertificateVerify with an RSA client key

## Goal

A client with an RSA certificate key answers a TLS 1.0 or 1.1 server's CertificateRequest with its certificate and a CertificateVerify signed as RFC 2246 section 7.4.3 requires, instead of an empty Certificate.

## Context

- TLS 1.0 and 1.1 sign with RSA as a PKCS #1 block type 1 over the 36-byte MD5 + SHA-1 hash with no DigestInfo (RFC 2246 section 7.4.3; RFC 4346 the same). `TlsSignatureScheme.BuildMd5Sha1Block` in `Curl.Tls.UnitLibrary` already builds that padded block, and `TlsCertificatePublicKey.VerifyRsaMd5Sha1` verifies it with a raw public-key operation.
- The BCL's `RSA.SignHash` always wraps the hash in a DigestInfo, so `RsaTlsSigningKey` cannot fit the `TlsSignatureKind.RsaMd5Sha1` rule (see its `rule.Kind != TlsSignatureKind.RsaMd5Sha1` check). Today a TLS 1.0/1.1 server that requests a certificate therefore gets an empty Certificate from an RSA-keyed client. ECDSA client keys already sign with SHA-1 at these versions.
- Build the missing piece in `Curl.Cryptography.UnitLibrary`: a raw RSA private-key operation `m^d mod n` using the CRT parameters (p, q, dP, dQ, qInv) exported from `RSAParameters`, with base blinding (a random r, `m * r^e`, then unblind by `r^-1`) and constant-time modular exponentiation. `MontgomeryModulus` (BL-739, used by `FiniteFieldDiffieHellman`) is the existing constant-time exponentiation to build on. Check the result by re-applying the public exponent before returning it (fault-attack guard), as OpenSSL does.
- Then let `RsaTlsSigningKey` sign the `RsaMd5Sha1` rule by building the block with `BuildMd5Sha1Block` and running the raw private operation; every other rule keeps using the BCL `RSA`.
- Tests use the in-memory `Tls12TestServer` in `Curl.Tls.UnitTests` (credentials in `TestServerCredential`). The handshake is BL-703's `Tls12ClientHandshake`; its decisions are in ADR-0154 (`Documentation/Planning/Decisions/ADR-0154-the-tls-1-2-client-handshake-refuses-legacy-renegotiation-ignores-hello-request-and-matches-openssls-alerts.md`).

## Acceptance criteria

- [x] `Curl.Cryptography.UnitLibrary` has the raw RSA CRT private-key operation; `Curl.Cryptography.UnitTests` show it matches a published RSA test vector (e.g. an RSASP1 vector from the PKCS #1 v2.1 test data) byte for byte, that its output equals the non-CRT `m^d mod n`, and that an input not below n is rejected.
- [x] `RsaTlsSigningKey` accepts the `RsaMd5Sha1` rule; a `Tls12SignatureTests` test shows its signature verifies with `TlsCertificatePublicKey` and equals the reference `BigInteger.ModPow(block, d, n)` computed in the test.
- [x] A `Tls12ClientHandshakeTests` test runs a TLS 1.0 handshake (and one at TLS 1.1) against `Tls12TestServer` requesting a certificate, with an RSA client certificate: the client sends the non-empty Certificate and a CertificateVerify the server verifies, and the handshake completes.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Cryptography.UnitLibrary` and `-Library Curl.Tls.UnitLibrary` each report 100% line and branch coverage and no failing member.

## Notes

- `Curl.Tls.UnitLibrary` already references `Curl.Cryptography.UnitLibrary`, so no project file changes are needed.
- Plan and decisions in ADR-0193. `RsaCrtPrivateKey` (new, public, `IDisposable`) does RSASP1 on the CRT values with Garner recombination, blinding by r^e / r^-1 (r^-1 by Fermat through the same CRT path with exponents p-2, q-2, so no variable-time inverse), and a fault check against e before writing. `MontgomeryModulus` gained `Reduce`, `Subtract`, `MultiplyModulo`, `IsBelowModulus`, `Clear`, and its set-up now doubles 1 by masked additions instead of `BigInteger` division, so it is constant-time with a secret modulus.
- Published vector: `pss-vect.txt` (PKCS #1 v2.1 draft 2 test data, via pyca/cryptography's vectors), Examples 1.1 (1024-bit) and 2.1 (1025-bit, odd limb counts). The file gives no EM, so the test takes EM = s^e mod n and checks RSASP1(EM) is the published s byte for byte.
- `RsaTlsSigningKey` exports the private parameters to sign the legacy rule; a key that refuses export (non-exportable CNG) does not fit the rule (checked once, remembered), so it still sends an empty Certificate. The old `WithoutAKeyThatCanSignTheClientSendsAnEmptyCertificate(Tls10, "rsa")` row now uses an RSA-PSS-certified key, which has no TLS 1.0 signature.
- Added `Documentation/Planning/Decisions` to `touches` for ADR-0193 and its index line; no task in Doing names it.
- Follow-up filed: BL-877 (load PKCS #12 keys exportable on Windows, `Curl.Networking.UnitLibrary`).
- Full fast run: `Curl.Networking.UnitTests`' `AuthenticateAsClientAsync_WithCertStatusAndNoStapledResponse_FailsWithExit91(False)` failed once in the full parallel run and passes on rerun (both cases); it is a loopback test outside this task's projects.
- Tests: Curl.Cryptography.UnitTests 868 passed, Curl.Tls.UnitTests 754 passed; both libraries 100% line and branch, 0 failing members.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. An RSA client certificate signs a TLS 1.0 and 1.1 CertificateVerify with the hand-built blinded CRT RsaCrtPrivateKey
