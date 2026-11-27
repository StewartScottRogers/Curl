---
id: BL-6062
title: Answer exit 60 unspecified certificate verification error when .NET's chain build throws on Linux
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-6061]
touches: [Curl.Networking.UnitLibrary/SslStreamTlsProvider.cs, Curl.Networking.UnitLibrary/HandBuiltCertificateVerifier.cs, Curl.Networking.UnitLibrary/ServerCertificateVerification.CaCertificates.cs, Curl.Networking.UnitTests/ServerCertificateVerificationTests.UnspecifiedChainError.cs]
requirement: none
created: 2026-11-26
completed:
---
# BL-6062 — Answer exit 60 unspecified certificate verification error when .NET's chain build throws on Linux

## Goal

On Linux, a server chain that OpenSSL answers with `X509_V_ERR_UNSPECIFIED` (an rfc822Name constraint that does not decode or is too long, over a SmtpUTF8Mailbox) ends in exit 60 `unspecified certificate verification error (1)`, as the OpenSSL build of curl answers it (ADR-1442).

## Context

Found by BL-6060 (CI run 45917093887, Ubuntu): .NET's `X509Chain.Build` throws `CryptographicException` ("OpenSSL mapping function MapOpenSsl30Code reported unhandled error code '1'.") for such a chain, so Curl's own name constraint check (`CertificateNameConstraints`) is never reached. In `SslStreamTlsProvider` the exception thrown out of the handshake is caught as any failure, which reads as exit 35; `HandBuiltCertificateVerifier.BuildChain`, `JudgeCertificateOfClientCertificateRequest` and `RebuildAgainstAnchors` call `Build` unguarded. BL-6061 fixes the `VerifyPeer_...` test of the same cause and may already change some of this: read what it did first. `CertificateNameConstraints.FindViolationAmong` checks certificates without a platform-built chain. Tests must stand in for the throwing build, since Windows does not throw.

## Acceptance criteria

- [ ] A unit test in `Curl.Networking.UnitTests` in which the OpenSSL build's chain build throws `CryptographicException` gets `CurlExitCode.PeerFailedVerification` and the message curl gives verify result 1, through both the `SslStream` and the hand-built TLS provider paths.
- [ ] `dotnet build` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` reports no failing member.

## Notes

## Log

- 2026-11-26: Created.
