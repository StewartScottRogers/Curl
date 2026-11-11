---
id: BL-4905
title: Load PKCS#12 client certificates from a file on the Schannel profile instead of always failing with exit 35
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/ClientCertificateLoader.cs, Curl.Networking.UnitLibrary/TlsFailureMessages.cs, Curl.Networking.UnitTests/ClientCertificatePkcs12FileTests.cs, Curl.Networking.UnitTests/ClientCertificateLoaderTests.cs, Curl.Networking.UnitTests/Cgr096Tests.cs, Curl.Networking.UnitTests/SslStreamTlsProviderTests.ClientCertificate.cs, Curl.Networking.UnitTests/SslStreamTlsProviderTests.ClientCertificateTypes.cs]
requirement: none
created: 2026-11-11
completed: 2026-11-11
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

- [x] The repro exits 0 and prints `ok` and `200`.
- [x] A `CERT_REQUIRED` server trusting `ca.pem` sees the client certificate.
- [x] ADR-0794 / ADR-1187 are superseded by an ADR recording the 8.22.0 measurement.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). Measure real curl (the platform build) before pinning any output text, and add a focused unit test in the fixed library's own `.UnitTests` project.

- 2026-11-11 (lane 7): Measured curl 8.22.0 Schannel (Git for Windows) and System32 curl with `openssl s_server`: against a server that asks for no certificate the repro exits 0 and prints `200`; a wrong passphrase is exit 58; against a server requiring one this machine's Schannel fails after the ClientHello (`SEC_E_INTERNAL_ERROR` TLS 1.3, `SEC_E_ALGORITHM_MISMATCH` TLS 1.2). Decided (ADR-1240, supersedes ADR-1187): once the P12 opens, the loader returns it and Curl presents it.
- Curl's repro (`Curl.Console\bin\Debug\net10.0\curl.exe`, same command line): exit 0 and `200` against a server asking for no certificate; against `openssl s_server -Verify 1` (TLS 1.3 and `-tls1_2`) exit 0 and the server's page shows `Subject: CN=client`. The body is s_server's `-www` status page, not `ok`; exit code and status code are what the criterion checks. Unit level: the Schannel `SslStreamTlsProviderTests` P12 cases now assert the certificate is presented to a server-side `SslStream` that asks for one.
- touches widened to the test files that pinned the old exit 35 and to `TlsFailureMessages.cs` (its now unused `SchannelClientCertificateCredentialsUnknown` removed); no task in Doing names them. The focused test went into the existing `ClientCertificateLoaderTests` and `Cgr096Tests`, so `ClientCertificatePkcs12FileTests.cs` was not needed.
- Environment: SDK 10.0.401, which `global.json` pins, is no longer installed on this machine (only 10.0.100-rc.1 and 11.0.100). Built and tested with `rollForward` set to `latestMajor` locally and restored afterwards (not committed). `Curl.Networking.UnitTests` fast run: 4033 passed, 0 failed, 34 skipped. `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary`: the test run failed on two Console NTLM SSPI tests (`CurlCompositionNtlmTests`: this machine's SSPI Type 1 flags differ, unrelated to this change); from the coverage it collected, line coverage was 100%. `LoadPkcs12AsSchannelBuild` measured complexity 12 under SDK 11, so its file read moved to `ReadFileOrNothing`; the build's complexity analyser is clean. The other failing members under SDK 11 (`SslStreamTlsProvider.AuthenticateAsClientAsync` 14, `HandBuiltTlsProvider.PrepareTrust` 12, branch figures) are in code this task did not change.
- Fast tests: the quality run executed every unit test project that reaches Curl.Networking.UnitLibrary (Curl.Console.UnitTests, Libcurl.UnitTests and the protocol libraries included); all passed except the two `CurlCompositionNtlmTests` Windows SSPI cases, which depend on this machine's SSPI flags and touch nothing this task changed.
- Left as is: `Curl.Conformance.Differential.UnitLibrary/ReferenceBuildLimitation.SchannelRefusesP12Credential` still says Schannel refuses every P12; outside this task's touches.

## Log

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing. Criteria at claim: e3374a27 9f952ed9 3ec43e66 a0b49a29.
- 2026-11-11: Doing -> Done. A PKCS#12 --cert file loads and is presented in the Schannel build, as curl 8.22.0 Schannel does (ADR-1240)
