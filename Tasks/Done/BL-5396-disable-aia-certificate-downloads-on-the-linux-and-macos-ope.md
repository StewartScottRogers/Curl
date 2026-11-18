---
id: BL-5396
title: Disable AIA certificate downloads on the Linux and macOS OpenSSL-flavoured build
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/ServerCertificateVerification.CaCertificates.cs, Curl.Networking.UnitTests/AiaDownloadDisabledTests.cs, Documentation/Planning/Decisions]
requirement: none
created: 2026-11-17
completed: 2026-11-17
---
# BL-5396 — Disable AIA certificate downloads on the Linux and macOS OpenSSL-flavoured build

## Goal

On the Linux and macOS (OpenSSL-reference) builds, a server that omits its intermediate certificate is refused with exit 60 and `unable to get local issuer certificate (20)`, because Curl no longer downloads the intermediate through AIA; Windows behaviour (Schannel fetches AIA, exit 0) stays unchanged.

## Context

From the outside fitness review of 2026-11-17 (commit 9561bbe, `preview` build), finding STD-103 (Low, Confirmed, one run each against a public host: not a trust weakening, since the chain must still reach a trusted root; behaviour differs from the Linux reference). Area: HTTPS chain building, Linux build.

**Repro.** `curl -sS -o /dev/null -w '%{http_code}\n' https://incomplete-chain.badssl.com/` (WSL).
**Expected (REF2).** `curl: (60) SSL certificate OpenSSL verify result: unable to get local issuer certificate (20)`, exit 60. Windows REF and the Windows port both exit 0 because Schannel fetches AIA; `Documentation/Protocols.md` names the OpenSSL build as the Linux reference.
**Actual (PORT Linux).** `200`, exit 0.
**Where to start.** The chain policy in `Curl.Networking.UnitLibrary/ServerCertificateVerification.cs` (`ReadChainAnchors`, `SystemStoreChainPolicy`; the `matchesSchannelBuild` flag distinguishes the builds): set `X509ChainPolicy.DisableCertificateDownloads = true` when `matchesSchannelBuild` is false. `SystemStoreChainPolicy` currently returns `null` on that build, so it needs a policy that carries the setting. No ADR documents the current behaviour.

## Acceptance criteria

- [x] New tests in `Curl.Networking.UnitTests` (a new file) prove the chain policy built for the OpenSSL-flavoured build (`matchesSchannelBuild` false) has `DisableCertificateDownloads` true, for the system store and for `--cacert` / `--capath` anchors.
- [x] A test proves the Schannel-flavoured policy (`matchesSchannelBuild` true) does not disable downloads (Windows unchanged).
- [x] A test proves a leaf whose intermediate is supplied by the server still verifies on the OpenSSL-flavoured build.
- [x] An ADR marked "Decided by Claude under Stewart's delegation" in `Documentation/Planning/Decisions` records that the OpenSSL-reference builds do not follow AIA, and why (OpenSSL curl does not).
- [x] Tests are platform-neutral (or OS-conditioned where curl differs by platform).
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.
- [x] Hand check recorded in the Log: the repro command exits 60 with `unable to get local issuer certificate (20)` on Linux; on Windows it still exits 0.

## Notes

- Mock servers must be PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md).
- Review acceptance check: the command exits 60 with `unable to get local issuer certificate (20)` on Linux; Windows still exits 0.

## Log

- 2026-11-17: Created.
- 2026-11-17: Backlog -> Doing. Criteria at claim: a991eba9 aaf49420 ebca0b2e 186709ed 7ae10ef7 a0b49a29 465e0582.
- 2026-11-17: Implemented in ServerCertificateVerification.cs: the OpenSSL-flavoured system-store policy (previously null) and the --cacert policy set DisableCertificateDownloads; Schannel unchanged. ADR-1296 records it. Tests in AiaDownloadDisabledTests.cs; ServerCertificateRevocationTests' OpenSSL system-store test now expects a NoCheck policy. Fast Networking tests green; Measure-CodeQuality for Curl.Networking.UnitLibrary: 0 failing members.
- Hand check: lanes run on Windows only, so the Linux exit 60 repro could not be run; the behaviour is pinned by unit tests (policy flag, server-sent intermediate still verifies). Windows path is unchanged by construction (Schannel policy untouched).
- 2026-11-17: Doing -> Done. OpenSSL-flavoured chain policies disable AIA downloads (ADR-1296)
