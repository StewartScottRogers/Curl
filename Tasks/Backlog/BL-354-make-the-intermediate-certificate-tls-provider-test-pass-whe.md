---
id: BL-354
title: Make the intermediate-certificate TLS provider test pass where chain building fails
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Networking.UnitTests]
requirement: none
created: 2026-09-27
completed:
---
# BL-354 — Make the intermediate-certificate TLS provider test pass where chain building fails

## Goal

`SslStreamTlsProviderTests.AuthenticateAsClientAsync_WhenTheServerSendsAnIntermediate_ReportsItAfterTheServersCertificate` passes in the fast run on every developer machine, or is moved out of it with a stated reason.

## Context

- Found by BL-280 on 2026-09-27 (dark factory lane 3, Windows 11): the test fails consistently in `dotnet test --filter "TestCategory!=Integration"` with `System.Security.Cryptography.CryptographicException: An unknown chain building error occurred.` thrown from `X509Chain.Build`. Nothing BL-280 changed reaches `Curl.Networking`.
- The test builds a certificate chain on the machine; the chain build is environment-dependent (machine certificate stores, revocation policy). Start with how the test builds its chain in `Curl.Networking.UnitTests/SslStreamTlsProviderTests.cs` - e.g. `X509ChainPolicy.RevocationMode = NoCheck` and a `CustomRootTrust` chain rather than the machine stores.

## Acceptance criteria

- [ ] The named test passes three runs in a row of `dotnet test Curl.Networking.UnitTests --filter "TestCategory!=Integration"` on Windows.
- [ ] If it touches the machine's certificate stores or the network, it carries `TestCategory=Integration` and the fast-run coverage of `Curl.Networking.UnitLibrary` stays at 100%.

## Notes

## Log

- 2026-09-27: Created.
