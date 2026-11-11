---
id: BL-4905
title: Load PKCS#12 client certificates from a file on the Schannel profile instead of always failing with exit 35
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/ClientCertificateLoader.cs, Curl.Networking.UnitTests/ClientCertificatePkcs12FileTests.cs]
requirement: none
created: 2026-11-11
completed:
---
# BL-4905 — Load PKCS#12 client certificates from a file on the Schannel profile instead of always failing with exit 35

## Goal

`--cert client.p12:secret --cert-type P12` works on Windows as it does in curl 8.22.0 Schannel.

## Context

From the outside fitness review of 2026-11-11 (commit 6ce7cfcf, the `preview` `curl-win-x64.zip`, Windows 11 x64; REF = curl 8.22.0 Schannel), finding STD-002 (High, confirmed: reproduced at least twice).

- Repro: Make a CA, a server cert (SAN localhost,127.0.0.1) and a clientAuth cert; `openssl pkcs12 -export ... -out client.p12 -passout pass:secret`. TLS server on 127.0.0.1:18301 that does not request a client cert and replies `200 OK` body `ok`. `curl -sS https://localhost:18301/ --cacert ca.pem --ssl-no-revoke --cert client.p12:secret --cert-type P12 -w "%{http_code}\n"`
- Expected: REF 8.22.0: exit 0, prints `ok` then `200`.
- Actual: Exit 35, `000`, no ClientHello; `curl: (35) schannel: AcquireCredentialsHandle failed: SEC_E_UNKNOWN_CREDENTIALS (0x8009030d) - The credentials supplied to the package were not recognized`. Same without `--cert-type`, with legacy 3DES P12 and with an EC key. The cert-store form works.
- Where to start: `ClientCertificateLoader.cs` Schannel P12 branch (~lines 167-183): after `LoadPkcs12` succeeds it unconditionally returns `ConnectResult.Failed(SslConnectError, SchannelClientCertificateCredentialsUnknown)` (ADR-0794 / ADR-1187). Re-measure curl 8.22.0 and supersede those ADRs.

## Acceptance criteria

- [ ] The repro exits 0 and prints `ok` and `200`.
- [ ] A `CERT_REQUIRED` server trusting `ca.pem` sees the client certificate.
- [ ] ADR-0794 / ADR-1187 are superseded by an ADR recording the 8.22.0 measurement.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). Measure real curl (the platform build) before pinning any output text, and add a focused unit test in the fixed library's own `.UnitTests` project.

## Log

- 2026-11-11: Created.
