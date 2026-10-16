---
id: BL-2373
title: Fix flaky test SslStreamTlsProviderTests AuthenticateAsClientAsync_WithoutClientCertificateAfterAHandshakeThatPresentedOne_PresentsNone (True)
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Networking.UnitTests]
requirement: none
created: 2026-10-15
completed:
---
# BL-2373 — Fix flaky test SslStreamTlsProviderTests AuthenticateAsClientAsync_WithoutClientCertificateAfterAHandshakeThatPresentedOne_PresentsNone (True)

## Goal

`SslStreamTlsProviderTests.AuthenticateAsClientAsync_WithoutClientCertificateAfterAHandshakeThatPresentedOne_PresentsNone` (the `True` data row) passes every time in the full fast run, not only when run alone.

## Context

Found by BL-2364 on 2026-10-15 (dark factory lane 1, Windows): it failed once in `dotnet test --filter "TestCategory!=Integration"` (`Curl.Networking.UnitTests`, 1 of 3217, after 1 s) and passed when run alone with `--filter FullyQualifiedName~AuthenticateAsClientAsync_WithoutClientCertificateAfterAHandshakeThatPresentedOne_PresentsNone`. BL-2364 changed only `Curl.Engine.UnitLibrary` and its tests, which this test does not reach. The test lives in `Curl.Networking.UnitTests/SslStreamTlsProviderTests.ClientCertificate.cs`; the likely cause is state shared between parallel tests (Schannel's TLS session or credential cache across handshakes, or a timeout under load).

## Acceptance criteria

- [ ] The cause is named under Notes, and the test (or the code it tests) is changed so it no longer depends on other tests running beside it or on timing.
- [ ] `dotnet test Curl.Networking.UnitTests --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-10-15: Created.
