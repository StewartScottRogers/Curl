---
id: BL-5009
title: Refuse a server certificate whose keyUsage forbids TLS server use on the OpenSSL profile
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/ServerCertificateVerification.cs, Curl.Networking.UnitTests/ServerCertificateVerificationTests.VerifyChain.cs]
requirement: none
created: 2026-11-12
completed:
---
# BL-5009 — Refuse a server certificate whose keyUsage forbids TLS server use on the OpenSSL profile

## Goal

On the OpenSSL profile (Linux and macOS build), a server certificate whose keyUsage forbids TLS server use is refused with exit 60 and `unsuitable certificate purpose (26)`.

## Context

From the outside fitness review of 2026-11-12 (commit 2d2ad7f8b, `curl-linux-x64` release; REF = curl 8.22.0 Schannel, REF2 = curl 8.18.0 OpenSSL), finding STD-029 (Medium, Confirmed).

Area: HTTPS certificate verification, OpenSSL profile. Weakens security (minor).

Repro (WSL Ubuntu):
1. Issue a leaf signed by a test CA `ca.pem` with `subjectAltName=DNS:localhost,IP:127.0.0.1` and `keyUsage=critical,keyCertSign` (no digitalSignature, no keyEncipherment).
2. Serve it over HTTPS on 127.0.0.1:43404.
3. `curl -sS https://localhost:43404/ --cacert ca.pem`

Expected (REF2, OpenSSL): exit 60, `curl: (60) SSL certificate OpenSSL verify result: unsuitable certificate purpose (26)`. Windows REF (Schannel) accepts this certificate and so does Windows PORT, so Windows behaviour already matches and must not change.

Actual (Linux PORT): exit 0 and the body is delivered, with and without `-v`.

Where to start: `Curl.Networking.UnitLibrary/ServerCertificateVerification.cs`, the OpenSSL-build branch of `Judge` (about line 265; `WithTheNameCheckCurlRuns` is the sibling hook). On Linux .NET's chain build does not enforce the leaf's KeyUsage for TLS. When `!matchesSchannelBuild` and the leaf has a keyUsage extension, require digitalSignature (or keyEncipherment for RSA key exchange).

## Acceptance criteria

- [ ] A unit test in `Curl.Networking.UnitTests/ServerCertificateVerificationTests.VerifyChain.cs` (leaf with `keyUsage=keyCertSign` only, OpenSSL profile) shows exit `CurlExitCode.PeerFailedVerification` (60) with `SSL certificate OpenSSL verify result: unsuitable certificate purpose (26)`.
- [ ] A test with the Schannel profile and the same leaf shows it is still accepted (as REF 8.22.0 Schannel does).
- [ ] Regression: an OpenSSL-profile leaf with no keyUsage extension, and one with digitalSignature (and one with keyEncipherment), is still accepted.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). Measure real curl (the platform build) before pinning any output text. OpenSSL profile only; the Schannel profile deliberately keeps accepting. Tests must pass on Windows, Linux and macOS: drive the profile through the `matchesSchannelBuild` flag, not the host OS.

## Log

- 2026-11-12: Created.
