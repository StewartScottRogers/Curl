---
id: BL-771
title: Make the intermediate-certificate TLS test build its chain on Windows
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Networking.UnitTests]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-771 — Make the intermediate-certificate TLS test build its chain on Windows

## Goal

`AuthenticateAsClientAsync_WhenTheServerSendsAnIntermediate_ReportsItAfterTheServersCertificate` passes on the Windows dark factory machine, as every other fast test does.

## Context

- Found by BL-222 (2026-09-27) on lane 2: `dotnet test --filter "TestCategory!=Integration"` fails this one test every run with `System.Security.Cryptography.CryptographicException: An unknown chain building error occurred.` thrown from `X509Chain.Build`.
- Test: `Curl.Networking.UnitTests/SslStreamTlsProviderTests.PeerCertificates.cs:35`, added by c12942b (BL-303). It builds a root, an intermediate (`CN=BL303 Intermediate`) and a leaf in memory; the chain build fails on this machine, likely a certificate-extension or chain-policy detail Windows' chain engine rejects rather than a product defect.
- BL-222 touched only `Curl.Cookies.UnitLibrary`, which Networking does not reference, so the failure predates it.

## Acceptance criteria

- [x] `dotnet test Curl.Networking.UnitTests --filter "TestCategory!=Integration"` passes on Windows with 0 failures.
- [x] The test still asserts the server's certificate comes first and the intermediate after it.

## Notes

- Already fixed before this run. A second task filed under the same ID (`Tasks/Done/2026-09-27_1215/BL-354-make-the-intermediate-certificate-tls-provider-test-pass-whe.md`, from BL-280's report of the same failure) landed the fix in 2b8e665 and 16ed1b3. Cause: on Windows `SslStreamCertificateContext.Create` writes the intermediate into the CurrentUser `CA` store. Same-named intermediates left there by earlier runs made `X509Chain.Build` pick the wrong issuer. Fix: the test removes its intermediate in `finally`, names its authorities with a fresh GUID per run, and carries `TestCategory("Integration")` because it writes the user's store. No code change was needed in this run.
- Verified 2026-09-27 on lane 1: `dotnet test Curl.Networking.UnitTests --filter "TestCategory!=Integration"` gave 682 passed, 6 skipped, 0 failed. The test run on its own (`--filter "FullyQualifiedName~WhenTheServerSendsAnIntermediate"`) passed. No `BL303` certificate was left in `Cert:\CurrentUser\CA`. The full fast run passed in every test project.
- Criterion 2: the test still asserts `PeerCertificates[0]` is the leaf and `PeerCertificates[1]` is the intermediate (`SslStreamTlsProviderTests.PeerCertificates.cs`).
- The duplicate ID came about because two lanes each allocated BL-354 from their own copy of the board. The archived copy is not live, so the script accepts this one.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. Intermediate-certificate TLS test passes on Windows; fix already landed in 2b8e665/16ed1b3, verified here
- 2026-09-28: Renumbered from BL-354 to BL-771; the ID was shared with Tasks/Done/2026-09-27_1215/BL-354-make-the-intermediate-certificate-tls-provider-test-pass-whe.md, which keeps it.
