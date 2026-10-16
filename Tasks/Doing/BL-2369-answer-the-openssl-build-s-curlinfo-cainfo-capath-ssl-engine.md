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
completed:
---
# BL-2369 — Answer the OpenSSL build's CURLINFO_CAINFO, CAPATH, SSL_ENGINES and TLS_SSL_PTR off Windows, and the socket and early-data infos

## Goal

Off Windows, `Info.CaInfo`, `CaPath`, `SslEngines` and `TlsSslBackend` answer as curl's OpenSSL build does, and `Info.LastSocket`, `ActiveSocket` and `EarlyDataSent` are decided and documented in an ADR.

## Context

Left by BL-2361 (ADR-0506 plan phase 2). `TransferInfo`'s defaults in `Curl.Api` are the Schannel build's answers (`TlsSslBackend` 8, no CA paths, no engines); the OpenSSL build answers `CURLSSLBACKEND_OPENSSL` (1) and its configured CA bundle and path. Measure the OpenSSL build of the targeted curl (`Gap/Baselines/target.json`) on Linux and macOS before pinning the paths. Curl never sends TLS early data, so `EARLYDATA_SENT_T` stays 0 unless that changes; the engine owns no OS socket descriptors, so decide what `LASTSOCKET` and `ACTIVESOCKET` answer (likely -1 until phase 3's connect-only work) and record it.

## Acceptance criteria

- [ ] A test pinned to each platform (`OSCondition`) shows the build-constant values.
- [ ] An ADR marked "Decided by Claude under Stewart's delegation" records the socket and early-data answers.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library <every library changed>` reports 0 failing members.

## Notes

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
