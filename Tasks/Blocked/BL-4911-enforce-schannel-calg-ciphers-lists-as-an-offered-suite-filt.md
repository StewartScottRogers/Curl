---
id: BL-4911
title: Enforce Schannel CALG_* --ciphers lists as an offered-suite filter
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/SchannelAlgorithmList.cs, Curl.Networking.UnitLibrary/SslStreamTlsProvider.cs, Curl.Networking.UnitLibrary/HandBuiltTlsProvider.cs, Curl.Networking.UnitLibrary/TlsClientRouting.cs, Curl.Networking.UnitTests/SchannelAlgorithmFilterTests.cs]
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

- 2026-11-11 (lane 6): Added Curl.Networking.UnitLibrary/TlsClientRouting.cs to touches: SslStream cannot restrict suites on Windows, so a Schannel --ciphers list must route to the hand-built client; no task in Doing on origin/work/dark-factory names it.
- 2026-11-11 (lane 6): Code written, NOT built or tested (left uncommitted for the stash): SchannelAlgorithmList.OfferedSuites filters the Schannel profile class by class (key exchange, signature, cipher, hash; unnamed classes unrestricted; AEAD hash = PRF hash; no suite left = exit 35 algorithm mismatch); HandBuiltTlsProvider.SelectCipherSuites uses it; TlsClientRouting.ReasonInBuild routes Schannel build + --ciphers to the hand-built client; tests in Curl.Networking.UnitTests/SchannelAlgorithmFilterTests.cs; ADR-1238 records it. The fatal alert on the hand-built TLS 1.2 path is already REF's SEC_E_ILLEGAL_MESSAGE line (TlsFailureMessages.SchannelHandBuiltHandshakeFailure).
- 2026-11-11 (lane 6): Blocked because this host has no .NET SDK matching global.json (10.0.401, rollForward latestFeature): only 10.0.100-rc.1, 11.0.100-rc.2 and 11.0.100 are installed, so dotnet build/test fail with "A compatible .NET SDK was not found". Left: build, run Curl.Networking.UnitTests fast tests (TlsClientRoutingTests may need a data row for the new row), Measure-CodeQuality for Curl.Networking.UnitLibrary, live check of CALG_AES_128 / CALG_RSA_KEYX against a local TLS server, then Done.

## Log

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing. Criteria at claim: a9c45dfd 6a5bb400 a0b49a29.
- 2026-11-11: Doing -> Blocked. Stewart: this host has no .NET SDK matching global.json 10.0.401 (only 10.0.100-rc.1 and 11.0.x installed) - can you install SDK 10.0.4xx so the lane can build and test?
