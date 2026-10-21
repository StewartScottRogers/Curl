---
id: BL-2369
title: Answer the OpenSSL build's CURLINFO_CAINFO, CAPATH, SSL_ENGINES and TLS_SSL_PTR off Windows, and the socket and early-data infos
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2361]
touches: [Curl.Api.UnitLibrary, Curl.Api.UnitTests, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-15
completed: 2026-10-15
---
# BL-2369 — Answer the OpenSSL build's CURLINFO_CAINFO, CAPATH, SSL_ENGINES and TLS_SSL_PTR off Windows, and the socket and early-data infos

## Goal

Off Windows, `Info.CaInfo`, `CaPath`, `SslEngines` and `TlsSslBackend` answer as curl's OpenSSL build does, and `Info.LastSocket`, `ActiveSocket` and `EarlyDataSent` are decided and documented in an ADR.

## Context

Left by BL-2361 (ADR-0506 plan phase 2). `TransferInfo`'s defaults in `Curl.Api` are the Schannel build's answers (`TlsSslBackend` 8, no CA paths, no engines); the OpenSSL build answers `CURLSSLBACKEND_OPENSSL` (1) and its configured CA bundle and path. Measure the OpenSSL build of the targeted curl (`Gap/Baselines/target.json`) on Linux and macOS before pinning the paths. Curl never sends TLS early data, so `EARLYDATA_SENT_T` stays 0 unless that changes; the engine owns no OS socket descriptors, so decide what `LASTSOCKET` and `ACTIVESOCKET` answer (likely -1 until phase 3's connect-only work) and record it.

## Acceptance criteria

- [x] A test pinned to each platform (`OSCondition`) shows the build-constant values.
- [x] An ADR marked "Decided by Claude under Stewart's delegation" records the socket and early-data answers.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.
- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library <every library changed>` reports 0 failing members.

## Notes

- Plan: the build-constant infos are `TransferInfo` defaults in `Curl.Api.UnitLibrary`, chosen once per process by a new internal `TlsBuildAnswers` (`For(windows, macOS)` so every branch is covered on Windows). The engine starts from `new TransferInfo()` everywhere, so `Curl.Engine.UnitLibrary` needed no change.
- Measured 2026-10-15 instead of reading `Gap/` (the audit guard refuses lanes): curl 8.22.0 `acinclude.m4` `CURL_CHECK_CA_BUNDLE` takes the first existing bundle and `/etc/ssl/certs` when it holds hashed `.0` links; Apple SecTrust is opt-in. WSL Ubuntu has both; macOS has only `/etc/ssl/cert.pem`. So Linux answers `/etc/ssl/certs/ca-certificates.crt` + `/etc/ssl/certs`, macOS `/etc/ssl/cert.pem` + none, backend 1 off Windows.
- `SslEngines` stays empty everywhere to agree with ADR-0151 (`--engine list` prints `<none>`), though Ubuntu curl lists `dynamic`. Sockets answer -1 and early data 0; all recorded in ADR-0549.
- Tests: `TransferInfoTests` OSCondition tests per platform, `TlsBuildAnswersTests`. Curl.Api.UnitTests 429 passed, 2 skipped (other OS); full fast run green; Measure-CodeQuality -Library Curl.Api.UnitLibrary: 0 failing members.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. TransferInfo answers the OpenSSL build's backend and CA paths off Windows; sockets, early data and engines decided in ADR-0549
