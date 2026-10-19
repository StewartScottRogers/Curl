---
id: BL-2669
title: Add curl_easy_ssls_import and curl_easy_ssls_export to Libcurl and port lib1678 over them off Windows
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2786]
touches: [Curl.Conformance.UnitLibrary/UpstreamCurlPlatform.cs, Curl.Conformance.UnitTests/UpstreamCurlPlatformTests.cs, Libcurl, Libcurl.UnitTests, Curl.Conformance.LibTest.UnitLibrary, Curl.Conformance.LibTest.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-17
completed: 2026-10-19
---
# BL-2669 — Add curl_easy_ssls_import and curl_easy_ssls_export to Libcurl and port lib1678 over them off Windows

## Goal

`Libcurl` has `curl_easy_ssls_import` and `curl_easy_ssls_export`, and `Lib1678` is the full lib1678 program off Windows, where Curl lists `SSLS-EXPORT`, so upstream case 1678 passes there.

## Context

Found by BL-2544. lib1678 imports one SSL session packet (`CURL_SPACK_VERSION` 1, a 4096-byte ticket, a 127-byte QUIC transport parameter block, a 10-byte ALPN, then one-byte second ticket, ALPN and QUIC parameters) 64 times with `curl_easy_ssls_import(easy, "example.test:443", NULL, 0, packet, len)` on a handle whose share handle shares `CURL_LOCK_DATA_SSL_SESSION`, and fails on the first import that does not answer `CURLE_OK` (writing `import N failed: code (text)`). Libcurl has no such call (`grep -r ssls Libcurl` finds nothing). BL-2544 ported the program as the build without `USE_SSLS_EXPORT` (it succeeds at once), which is right for Windows, where the Schannel build and `ManagedCurlVersion` list no `SSLS-EXPORT` and the case is skipped; off Windows Curl lists `SSLS-EXPORT` (`--ssl-sessions`, ADR-0319), so the program there must be the full one. Upstream: curl 8.22.0 `lib/vtls/vtls_spack.c`, `include/curl/easy.h`, `tests/libtest/lib1678.c`.

## Acceptance criteria

- [x] `Libcurl` has `curl_easy_ssls_import` and `curl_easy_ssls_export` over the engine's TLS session cache (or a share handle's), parsing curl 8.22.0's session pack format, with focused unit tests in `Libcurl.UnitTests`.
- [x] `Lib1678` is the full program where `SSLS-EXPORT` is listed and the program as built without it elsewhere, with unit tests in `Curl.Conformance.LibTest.UnitTests`.
- [x] Case 1678 passes through the runner where Curl lists `SSLS-EXPORT` and is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` (it is skipped on Windows).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- 2026-10-18 (lane 1): First-pass sizing before any edit. Libcurl reaches the engine only through `IEasyTransfer`, which has no TLS session member, and the engine's `TlsSessionCache` (Curl.Networking) reads and writes only whole `--ssl-sessions` files. Filed BL-2785 (cache: import one packed session under a key or salt-and-hmac, list held sessions, accept lib1678's repeated tags) and BL-2786 (seam members on `IEasyTransfer` with `NotBuiltIn` defaults, `ManagedEasyTransfer` over the share's or engine's cache; depends on BL-2785). This task then needs only `Libcurl/Easy` (`CurlEasy.SslsImport`/`SslsExport` over the seam), `Lib1678` choosing the full program where `ManagedCurlVersion` lists `SSLS-EXPORT`, and the ratchet line; nothing else missing.

- 2026-10-19 (lane 2): `CurlEasy.SslsImport`/`SslsExport` (new `CurlEasy.SslSessions.cs`, `CurlSslsExportCallback`) pass straight to `IEasyTransfer.ImportTlsSession`/`ExportTlsSessions`; a null packet goes as an empty one and a null callback answers BadFunctionArgument without the engine (upstream's `Curl_ssl_session_export` refuses a missing `export_fn`). `Lib1678` checks `curl_version_info`'s features at run time for `SSLS-EXPORT` (the compile-time `USE_SSLS_EXPORT`) and runs upstream's full body (packet from `tests/libtest/lib1678.c` at curl-8_22_0) where listed. Widened touches to `Curl.Conformance.UnitLibrary/UpstreamCurlPlatform.cs` and its test: the harness's Linux and macOS platforms did not list `SSLS-EXPORT`, so case 1678 skipped everywhere and the listed case failed; no task in Doing named either file. On Windows it is now SkippedOnThisPlatform (Inconclusive, ADR-0488). `Lib1678.Test` was split (`ShareThenImport`) to get under the complexity gate. Fast run: one unrelated Schannel P12 handshake failure in Curl.Networking.UnitTests, filed as BL-2792.

## Log

- 2026-10-17: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: 7eadc98c 342afcda 2e924e0c 3d836d5e.
- 2026-10-18: Doing -> Backlog. Needs BL-2786 (engine seam for TLS session import and export), which needs BL-2785 (TlsSessionCache single-session import and export)
- 2026-10-19: Backlog -> Doing. Criteria at claim: 7eadc98c 342afcda 2e924e0c 3d836d5e.
- 2026-10-19: Doing -> Done. Libcurl has curl_easy_ssls_import and curl_easy_ssls_export; lib1678 is the full program where SSLS-EXPORT is listed and case 1678 is on the ratchet
