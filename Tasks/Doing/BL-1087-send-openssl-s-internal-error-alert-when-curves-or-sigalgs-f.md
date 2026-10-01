---
id: BL-1087
title: Send OpenSSL's internal_error alert when --curves or --sigalgs fails before the ClientHello
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-10-01
completed:
---
# BL-1087 — Send OpenSSL's internal_error alert when --curves or --sigalgs fails before the ClientHello

## Goal

When `--curves` or `--sigalgs` leaves nothing to offer (no suitable groups, key share or signature algorithm), the OpenSSL build writes the fatal `internal_error` alert record `15 03 01 00 02 02 50` before failing with exit 35, as Ubuntu's curl 8.18.0 with OpenSSL 3.5.5 does, instead of closing without a byte.

## Context

- Measured 2026-10-01 (BL-1082) with `Record-CurlExchange.ps1 -Curl wsl.exe -ListenAddress 172.26.96.1`: `--curves '*brainpoolP256r1:P-384'` and `--curves '*brainpoolP256r1'` both leave `request.bin` holding exactly `15 03 01 00 02 02 50`; measure `--sigalgs RSA+SHA1` and `--curves '?bogus'` too before pinning them.
- Today `CurvesAndSignatureAlgorithms.Apply` returns the failure from `HandBuiltTlsProvider.Prepare` before anything is written; `HandBuiltTlsProviderTests.AuthenticateAsClientAsync_WithCurvesOrSigalgsLeavingNothingToOffer_FailsWithTheMeasuredLine` asserts only the exit code, line and disposal.
- Decide (ADR) whether the Schannel build, which applies these options only through the hand-built client, sends the same alert.

## Acceptance criteria

- [ ] `HandBuiltTlsProviderTests` pin the bytes written for each measured failure, in each build as decided.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` reports no failing member.

## Notes

## Log

- 2026-10-01: Created.
- 2026-10-01: Backlog -> Doing.
