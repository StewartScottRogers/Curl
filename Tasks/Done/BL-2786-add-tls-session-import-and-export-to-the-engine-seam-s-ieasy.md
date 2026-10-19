---
id: BL-2786
title: Add TLS session import and export to the engine seam's IEasyTransfer over the share's or engine's TlsSessionCache
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2785]
touches: [Curl.Api.UnitLibrary/EngineContracts/IEasyTransfer.cs, Curl.Api.UnitLibrary/EngineContracts/Tls, Curl.Engine.UnitLibrary/ManagedEasyTransfer.cs, Curl.Engine.UnitLibrary/ManagedEasyTransfer.SslSessions.cs, Curl.Engine.UnitTests/ManagedEasyTransferTests.SslSessions.cs, Curl.Engine.UnitLibrary/ManagedShareHandle.cs, Curl.Api.UnitTests/EngineContracts/Tls]
requirement: none
created: 2026-10-18
completed: 2026-10-18
---
# BL-2786 — Add TLS session import and export to the engine seam's IEasyTransfer over the share's or engine's TlsSessionCache

## Goal

An `IEasyTransfer` can import one packed TLS session and export every held one, over its share handle's `TlsSessionCache` when the handle shares `CurlLockData.SslSession` and the engine's otherwise, so `Libcurl` can implement `curl_easy_ssls_import` and `curl_easy_ssls_export` (BL-2669).

## Context

Found by BL-2669: `IEasyTransfer` (`Curl.Api.UnitLibrary/EngineContracts/IEasyTransfer.cs`) has no session member, and the engine's caches (`ManagedEngineSharedState.TlsSessions`, `ManagedShareHandle.TlsSessions`, both internal) are unreachable from `Libcurl`. Add the members with default implementations answering `CurlCode.NotBuiltIn`, so existing fakes keep compiling, and a seam type in `EngineContracts/Tls` for one exported session (salt-and-hmac, packed bytes, valid-until, IETF TLS id, ALPN, max early data), mirroring curl 8.22.0's `curl_ssls_export_cb`. `ManagedEasyTransfer` implements them over the cache BL-2785's methods expose; an export callback answering other than OK stops the export with that code, as `Curl_ssl_session_export` does. Off Windows only, where Curl lists `SSLS-EXPORT`; on Windows (no `SSLS-EXPORT` in the Schannel build) answer `NotBuiltIn` as that libcurl does.

## Acceptance criteria

- [x] `IEasyTransfer` has session import and export members with `NotBuiltIn` defaults, documented, in `Curl.Api.UnitLibrary`.
- [x] `ManagedEasyTransfer` imports into and exports from the share's cache under `CurlLockData.SslSession` and the engine's otherwise, with unit tests in `Curl.Engine.UnitTests/ManagedEasyTransferTests.SslSessions.cs` covering both caches, a refused packet and a callback that stops the export.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Seam: `IEasyTransfer.ImportTlsSession(sessionKey, saltAndHmac, packedSession)` and `ExportTlsSessions(Func<ExportedTlsSession, CurlCode>)`, both defaulting to `CurlCode.NotBuiltIn`; `ExportedTlsSession` (`Curl.Api.UnitLibrary/EngineContracts/Tls`) carries `curl_ssls_export_cb`'s arguments. The callback takes no handle or user pointer: `Libcurl` closes over both.
- Engine: `ManagedEasyTransfer.SslSessions.cs` (class made `partial`) maps `TlsSessionCache.ImportSession`'s 26/43 to `ReadError`/`BadFunctionArgument` and walks `ListSessions`, stopping at the first non-OK answer as `Curl_ssl_session_export` does. The cache is the `CURLOPT_SHARE` handle's under one `SslSession` lock pair per call while it shares SSL sessions (`ManagedShareHandle.WithTlsSessions`, as `Curl_ssl_scache_lock`), else the engine's with no lock.
- Default taken: `ExportsTlsSessions` (internal, settable for tests) is `!OperatingSystem.IsWindows()`, so Windows answers `NotBuiltIn` as the Schannel build without `SSLS-EXPORT`, per the task's Context.
- Touches widened: `Curl.Engine.UnitLibrary/ManagedShareHandle.cs` (its `Shares` and `Locked` are private, so the share-cache pick needs a member there) and `Curl.Api.UnitTests/EngineContracts/Tls` (the default members need covering in their own library's tests). No task in Doing on origin/work/dark-factory names either.
- Measured: Measure-CodeQuality for Curl.Engine.UnitLibrary and Curl.Api.UnitLibrary, both 100% line and branch, 0 failing members.

## Log

- 2026-10-18: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: 0d319d3f a4536fd9 3d836d5e.
- 2026-10-18: Doing -> Done. IEasyTransfer imports and exports TLS sessions over the share's or engine's TlsSessionCache, NotBuiltIn on Windows
