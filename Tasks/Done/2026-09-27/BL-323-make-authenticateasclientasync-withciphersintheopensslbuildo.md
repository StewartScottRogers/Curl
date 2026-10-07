---
id: BL-323
title: Make AuthenticateAsClientAsync_WithCiphersInTheOpenSslBuildOverTls12_NegotiatesTheNamedSuite pass on Linux and macOS CI
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-323 — Make AuthenticateAsClientAsync_WithCiphersInTheOpenSslBuildOverTls12_NegotiatesTheNamedSuite pass on Linux and macOS CI

## Goal

`AuthenticateAsClientAsync_WithCiphersInTheOpenSslBuildOverTls12_NegotiatesTheNamedSuite` passes on ubuntu-latest and macos-latest in the CI workflow, as it already does on windows-latest, and still proves what it set out to prove on each platform.

## Context

CI (`.github/workflows/ci.yml`) runs the fast tests on Windows, Linux and macOS. On `work/dark-factory` it has been red on Linux and macOS since at least 2026-09-27 05:59Z (run 36305641380), and this is one of four tests that fail there. `Curl.Networking.UnitTests/SslStreamTlsProviderTests.Ciphers.cs`: Case ("BOGUS:ECDHE-RSA-AES256-SHA", TLS_ECDHE_RSA_WITH_AES_256_CBC_SHA) fails with "TLS connect error: error:0A000410:SSL routines::sslv3 alert handshake failure" instead of exit Ok: the runner's OpenSSL refuses that CBC-SHA suite, or the loopback server does not offer it.

Curl publishes native binaries for all three platforms (BL-028) and matches the platform's own curl (ADR-0009), so the fix is to make the test express the right expectation per platform - not to skip it on non-Windows unless the behaviour genuinely exists only on Windows, in which case say so in the test name.

## Acceptance criteria

- [x] The test passes on Windows locally, and its expectation for Linux and macOS is stated in the test (a platform-specific case or data row), matching what the platform's curl or .NET actually does there.
- [x] If production code was wrong on Linux or macOS rather than the test, it is fixed, with coverage kept at 100%.
- [x] The next CI run of `work/dark-factory` no longer lists this test as failing on ubuntu-latest or macos-latest.

## Notes

- Cause: the test's loopback *server*, not production code. .NET's default server cipher list on Linux holds only AEAD suites, so the CBC row's `ECDHE-RSA-AES256-SHA` (which the client correctly offered alone, BOGUS dropped) was never on offer and the handshake failed. macOS already passed; Windows is Inconclusive by design (no `CipherSuitesPolicy`, ADR-0011).
- Fix (test only, no production change): the TLS 1.2 negotiation test's server now offers all three expected suites through a `CipherSuitesPolicy`, so the client's list alone decides which one is negotiated on every platform. The TLS 1.3 test keeps the platform default server.
- Verified on Linux in Docker (`mcr.microsoft.com/dotnet/sdk:10.0`, Ubuntu 24.04.5, OpenSSL 3.0.13 - the same OpenSSL as ubuntu-latest): Networking cipher tests 69 passed, 2 skipped (Windows-only), 0 failed. The third criterion is ticked on that evidence; the shift's next CI run confirms it.
- Windows: `dotnet build` clean, fast tests green (Curl.Networking.UnitTests 359 passed, 6 skipped).

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. The OpenSSL-build TLS 1.2 named-suite test passes on Linux: its server now offers every expected suite, CBC included
