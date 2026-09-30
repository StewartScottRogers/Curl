---
id: BL-965
title: Match curl's Negotiate over HTTPS when the server certificate's signature names no RFC 5929 hash
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-915]
touches: [Curl.Authentication.UnitLibrary, Curl.Authentication.UnitTests, Documentation/Planning/Decisions/ADR-0234-negotiate-carries-the-https-server-certificate-to-the-hand-built-kerberos-for-tls-server-end-point-bindings.md]
requirement: none
created: 2026-09-29
completed: 2026-09-29
---
# BL-965 — Match curl's Negotiate over HTTPS when the server certificate's signature names no RFC 5929 hash

## Goal

Hand-built Negotiate over HTTPS does what curl 8.18.0 (OpenSSL, MIT) is measured doing when the server certificate is signed with RSASSA-PSS, Ed25519, Ed448 or SHA-224, instead of ADR-0234's provisional "send no bindings".

## Context

- BL-915's `TlsServerEndPointChannelBindings.Of` (Curl.Authentication.UnitLibrary) knows the RSA PKCS #1, ECDSA and DSA signature OIDs with MD5, SHA-1, SHA-256, SHA-384 and SHA-512, and gives `null` (no bindings) for any other (ADR-0234).
- curl's OpenSSL `Curl_ssl_get_channel_binding` looks the digest up from the signature's NID (`OBJ_find_sigid_algs`, `EVP_get_digestbynid`) and, as read from source, fails when it finds none; the Negotiate step may then fail the transfer. SHA-224 is a real digest there, and the BCL has no SHA-224 (hand-build it in this library if curl uses it). For RSASSA-PSS the digest sits in the algorithm parameters.
- Measure with `Record-CurlExchange.ps1` (extend it to serve HTTPS with a chosen certificate if needed) on curl 8.18.0 OpenSSL + MIT (WSL) before pinning the exit code and `-v` text.

## Acceptance criteria

- [x] The measurement for a PSS and an Ed25519 certificate is recorded in ADR-0234 (amended) or a new ADR.
- [x] `Curl.Authentication.UnitTests` tests pin the measured behaviour for each of those certificates, and SHA-224's bindings if curl sends them.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Authentication.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Filed by BL-915.
- **Measured** (ADR-0234 amendment): curl 8.18.0 OpenSSL + MIT in WSL, served by `openssl s_server -HTTP` (a 401 Negotiate file) because `Record-CurlExchange.ps1 -Tls` serves through Schannel, which cannot serve Ed25519/Ed448; throwaway script `~/bl965.sh` in WSL, nothing committed. PSS, Ed25519, Ed448: no request sent, `* Could not find digest algorithm UNDEF (NID 0)`, `curl: (91) ...`, exit 91 - with and without a ticket. SHA-224 RSA: token sent, exit 0.
- **Design.** `TlsServerEndPointChannelBindings.Of` reads the signature OID with `System.Formats.Asn1` (no X509Certificate2, so Ed25519/Ed448 parse on every platform) and throws `HttpAuthenticationFailedException(SslInvalidCertStatus, ...)`, which the HTTP handler already turns into the transfer failure. An OID OpenSSL does not know gives "Unable to find digest NID for certificate signature algorithm" from curl source (s_server refused the patched certificate, so unmeasured). `HandBuiltKerberosSecurityContext` computes bindings before the ticket, as measured. SHA-224 hand-built as `Sha224` (NIST vectors + OpenSSL hash of the measured certificate).
- Added the ADR-0234 file to `touches` to amend it; no task in Doing names it.
- Follow-up filed: BL-980 (SHA-3 signature OIDs).
- Tests: `Sha224Tests`, `TlsServerEndPointChannelBindingsTests` (PSS, Ed25519, Ed448, unknown OID, SHA-224 RSA/ECDSA/DSA), `HandBuiltSecurityContextFactoryTests.ChannelBindings_RsaPssServerCertificateWithoutTicket_FailsWithExit91BeforeLookingForOne`. Curl.Authentication.UnitTests 699 passed; Measure-CodeQuality 100% line, 100% branch, 0 failing.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. Hand-built Negotiate over HTTPS fails with curl's measured exit 91 for PSS, Ed25519 and Ed448 server certificates and binds SHA-224 ones with a hand-built SHA-224
