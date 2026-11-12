---
id: BL-5007
title: Require the serverAuth EKU on the hand-built TLS route for --cacert, --proxy-cacert and DoH
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/ServerCertificateVerification.cs, Curl.Networking.UnitLibrary/HandBuiltCertificateVerifier.cs, Curl.Networking.UnitTests/HandBuiltCertificateVerifierTests.cs, Curl.Networking.UnitTests/ServerCertificateVerificationTests.VerifyChain.cs]
requirement: none
created: 2026-11-12
completed:
---
# BL-5007 — Require the serverAuth EKU on the hand-built TLS route for --cacert, --proxy-cacert and DoH

## Goal

The hand-built TLS route refuses a server certificate that lacks the serverAuth extended key usage (exit 60) for the origin (`--cacert`), the HTTPS proxy (`--proxy-cacert`) and DoH, as curl does.

## Context

From the outside fitness review of 2026-11-12 (commit 2d2ad7f8b, `preview` curl-win-x64.zip; REF = curl 8.22.0 Schannel, REF2 = curl 8.18.0 OpenSSL), finding STD-027 (High, Confirmed, re-reproduced by the orchestrator).

Area: HTTPS certificate verification. The hand-built route is the default on Windows whenever `--cacert` is given without `--ssl-no-revoke`, and on the Linux build whenever `-v` or `--trace` is given. It is also selected by `--curves`, `--sigalgs`, `--no-sessionid`, `--ciphers`, `--tlsv1.0`, `--tlsv1.1`, `--tls-max` below 1.2, `--tls-earlydata`, `--ech` and `--cert-status`, and on Linux by `SSLKEYLOGFILE` (`--ssl-no-revoke --no-sessionid`, `--ssl-no-revoke --tlsv1.0` and `--ssl-no-revoke --ciphers CALG_AES_256` all exit 0 today).

Repro:
1. With openssl, create a CA `ca.pem`. Issue a leaf `ekuclient2.pem` signed by it with `subjectAltName=DNS:localhost,IP:127.0.0.1`, `extendedKeyUsage=clientAuth` (no serverAuth) and `crlDistributionPoints=URI:http://127.0.0.1:43099/ca.crl`.
2. Serve a valid DER CRL from the CA at that URL (only so the revocation check passes).
3. Serve HTTPS on 127.0.0.1:43401 with that leaf, answering `HTTP/1.1 200 OK` with body `SECRET`.
4. `curl -sS https://localhost:43401/ --cacert ca.pem`
5. HTTPS proxy variant: the same certificate on a CONNECT proxy at 43405: `curl -sS http://localhost:43020/x -x https://localhost:43405 --proxy-cacert ca.pem`
6. DoH variant: the same certificate on a DoH server at 43406: `curl -sS http://foo.test:43020/d --doh-url https://localhost:43406/dns-query --cacert ca.pem`
7. Linux release build: `curl -sS -v https://localhost:43401/ --cacert ca.pem`

Expected:
- REF exits 60 on steps 4 and 5 with `curl: (60) schannel: error 0x00000010` (CERT_TRUST_IS_NOT_VALID_FOR_USAGE).
- On step 6, REF prints `curl: (6) Could not resolve host: foo.test`.
- REF2 exits 60 on step 7 with `curl: (60) SSL certificate OpenSSL verify result: unsuitable certificate purpose (26)`.

Actual: PORT prints `SECRET` and exits 0 on every step. PORT rejects the certificate (exit 60) only on the SslStream route (Windows with `--ssl-no-revoke`; Linux without `-v`).

Where to start: `Curl.Networking.UnitLibrary/ServerCertificateVerification.cs`, `ReadChainAnchors` (about line 202): the `X509ChainPolicy` built for `--cacert` (CustomRootTrust) never calls `ApplicationPolicy.Add(<serverAuth 1.3.6.1.5.5.7.3.1>)`; only `SystemStoreChainPolicy` does. `HandBuiltCertificateVerifier.BuildChain` (about line 128) clones that policy as it is, and adds the OID itself only when `chainPolicy is null`. Fix: add the serverAuth OID to the `--cacert` policy, or add it in `BuildChain` after `Clone()`; apply it to the `--proxy-cacert` and DoH policies too. Note the Schannel message for this status is `schannel: error 0x00000010`; the text mapping is the separate task BL-5032, so here assert the exit code (60) and that the handshake is rejected, and pin the message only if the existing mapping already yields it.

## Acceptance criteria

- [ ] A unit test in `Curl.Networking.UnitTests/HandBuiltCertificateVerifierTests.cs` (a clientAuth-only leaf signed by a generated test CA, `--cacert` trust) shows the verdict is Rejected with `CurlExitCode.PeerFailedVerification` (60).
- [ ] A unit test in `Curl.Networking.UnitTests/ServerCertificateVerificationTests.VerifyChain.cs` shows the chain policy read for `--cacert` carries the serverAuth application policy, and the same for the `--proxy-cacert` and DoH policies.
- [ ] A unit test shows the OpenSSL profile reports `unsuitable certificate purpose (26)` for the clientAuth-only leaf (message as REF2 prints: `SSL certificate OpenSSL verify result: unsuitable certificate purpose (26)`), pinned after measuring curl 8.18.0 OpenSSL.
- [ ] Regression: a leaf with serverAuth (and one with no EKU extension) signed by the same CA is still accepted on the hand-built route.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). Measure real curl (the platform build) before pinning any output text. The Schannel profile (Windows) and the OpenSSL profile (Linux, macOS) have different messages for this failure; the hand-built route is used by both. The system-store trust route (no `--cacert`) was not tested in the review.

## Log

- 2026-11-12: Created.
