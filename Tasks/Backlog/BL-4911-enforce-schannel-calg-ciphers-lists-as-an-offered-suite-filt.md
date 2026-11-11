---
id: BL-4911
title: Enforce Schannel CALG_* --ciphers lists as an offered-suite filter
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/SchannelAlgorithmList.cs, Curl.Networking.UnitLibrary/SslStreamTlsProvider.cs, Curl.Networking.UnitLibrary/HandBuiltTlsProvider.cs, Curl.Networking.UnitTests/SchannelAlgorithmFilterTests.cs]
requirement: none
created: 2026-11-11
completed:
---
# BL-4911 — Enforce Schannel CALG_* --ciphers lists as an offered-suite filter

## Goal

`--ciphers CALG_...` limits the offered cipher suites the way curl 8.22.0 Schannel does.

## Context

From the outside fitness review of 2026-11-11 (commit 6ce7cfcf, the `preview` `curl-win-x64.zip`, Windows 11 x64; REF = curl 8.22.0 Schannel), finding STD-008 (Medium, confirmed: reproduced at least twice).

- Repro: TLS server on 18301 whose body reports its negotiated version and cipher. `curl -sS https://localhost:18301/ --cacert ca.pem --ssl-no-revoke --ciphers CALG_AES_128`, then `CALG_AES_128:CALG_SHA_256`, then `CALG_RSA_KEYX`.
- Expected: REF 8.22.0: the first two give `TLSv1.2 ECDHE-RSA-AES128-GCM-SHA256`; `CALG_RSA_KEYX` exits 35 with `schannel: next InitializeSecurityContext failed: SEC_E_ILLEGAL_MESSAGE (0x80090326) - This error usually occurs when a fatal SSL/TLS alert is received ...`.
- Actual: All three exit 0 with `TLSv1.2 ECDHE-RSA-AES256-GCM-SHA384`: only the cap to TLS 1.2 is applied.
- Where to start: `SchannelAlgorithmList.cs` only validates and calls `WithLegacyCredentialRange`; the ALG_IDs are never applied in `SslStreamTlsProvider.cs` (~line 148) or `HandBuiltTlsProvider.cs` (~line 128).

## Acceptance criteria

- [ ] `CALG_AES_128` negotiates `ECDHE-RSA-AES128-GCM-SHA256`.
- [ ] `CALG_RSA_KEYX` exits 35 with REF's message.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). Measure real curl (the platform build) before pinning any output text, and add a focused unit test in the fixed library's own `.UnitTests` project.

## Log

- 2026-11-11: Created.
