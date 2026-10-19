---
id: BL-2786
title: Add TLS session import and export to the engine seam's IEasyTransfer over the share's or engine's TlsSessionCache
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2785]
touches: [Curl.Api.UnitLibrary/EngineContracts/IEasyTransfer.cs, Curl.Api.UnitLibrary/EngineContracts/Tls, Curl.Engine.UnitLibrary/ManagedEasyTransfer.cs, Curl.Engine.UnitLibrary/ManagedEasyTransfer.SslSessions.cs, Curl.Engine.UnitTests/ManagedEasyTransferTests.SslSessions.cs]
requirement: none
created: 2026-10-18
completed:
---
# BL-2786 — Add TLS session import and export to the engine seam's IEasyTransfer over the share's or engine's TlsSessionCache

## Goal

An `IEasyTransfer` can import one packed TLS session and export every held one, over its share handle's `TlsSessionCache` when the handle shares `CurlLockData.SslSession` and the engine's otherwise, so `Libcurl` can implement `curl_easy_ssls_import` and `curl_easy_ssls_export` (BL-2669).

## Context

Found by BL-2669: `IEasyTransfer` (`Curl.Api.UnitLibrary/EngineContracts/IEasyTransfer.cs`) has no session member, and the engine's caches (`ManagedEngineSharedState.TlsSessions`, `ManagedShareHandle.TlsSessions`, both internal) are unreachable from `Libcurl`. Add the members with default implementations answering `CurlCode.NotBuiltIn`, so existing fakes keep compiling, and a seam type in `EngineContracts/Tls` for one exported session (salt-and-hmac, packed bytes, valid-until, IETF TLS id, ALPN, max early data), mirroring curl 8.22.0's `curl_ssls_export_cb`. `ManagedEasyTransfer` implements them over the cache BL-2785's methods expose; an export callback answering other than OK stops the export with that code, as `Curl_ssl_session_export` does. Off Windows only, where Curl lists `SSLS-EXPORT`; on Windows (no `SSLS-EXPORT` in the Schannel build) answer `NotBuiltIn` as that libcurl does.

## Acceptance criteria

- [ ] `IEasyTransfer` has session import and export members with `NotBuiltIn` defaults, documented, in `Curl.Api.UnitLibrary`.
- [ ] `ManagedEasyTransfer` imports into and exports from the share's cache under `CurlLockData.SslSession` and the engine's otherwise, with unit tests in `Curl.Engine.UnitTests/ManagedEasyTransferTests.SslSessions.cs` covering both caches, a refused packet and a callback that stops the export.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-18: Created.
