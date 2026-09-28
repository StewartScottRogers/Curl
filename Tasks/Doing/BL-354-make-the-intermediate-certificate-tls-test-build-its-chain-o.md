---
id: BL-354
title: Make the intermediate-certificate TLS test build its chain on Windows
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Networking.UnitTests]
requirement: none
created: 2026-09-27
completed:
---
# BL-354 — Make the intermediate-certificate TLS test build its chain on Windows

## Goal

`AuthenticateAsClientAsync_WhenTheServerSendsAnIntermediate_ReportsItAfterTheServersCertificate` passes on the Windows dark factory machine, as every other fast test does.

## Context

- Found by BL-222 (2026-09-27) on lane 2: `dotnet test --filter "TestCategory!=Integration"` fails this one test every run with `System.Security.Cryptography.CryptographicException: An unknown chain building error occurred.` thrown from `X509Chain.Build`.
- Test: `Curl.Networking.UnitTests/SslStreamTlsProviderTests.PeerCertificates.cs:35`, added by c12942b (BL-303). It builds a root, an intermediate (`CN=BL303 Intermediate`) and a leaf in memory; the chain build fails on this machine, likely a certificate-extension or chain-policy detail Windows' chain engine rejects rather than a product defect.
- BL-222 touched only `Curl.Cookies.UnitLibrary`, which Networking does not reference, so the failure predates it.

## Acceptance criteria

- [ ] `dotnet test Curl.Networking.UnitTests --filter "TestCategory!=Integration"` passes on Windows with 0 failures.
- [ ] The test still asserts the server's certificate comes first and the intermediate after it.

## Notes

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
