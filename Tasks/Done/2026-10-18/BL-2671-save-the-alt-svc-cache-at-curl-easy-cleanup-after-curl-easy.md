---
id: BL-2671
title: Save the alt-svc cache at curl_easy_cleanup after curl_easy_reset so upstream case 1908 passes
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Engine.UnitLibrary/ManagedEasyTransfer.cs, Curl.Engine.UnitLibrary/ManagedCurlEngine.cs, Curl.Engine.UnitLibrary/CLAUDE.md, Curl.Engine.UnitTests/ManagedEasyTransferTests.Files.cs, Curl.Engine.UnitLibrary/TransferPerformer.Setup.cs, Curl.Engine.UnitLibrary/AltSvcTransferCache.cs, Curl.Engine.UnitTests/ManagedEasyTransferTests.Cleanup.cs, Curl.Engine.UnitTests/AltSvcTransferCacheTests.cs, Curl.Engine.UnitTests/TransferPerformerTests.Setup.cs, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-17
completed: 2026-10-18
---
# BL-2671 — Save the alt-svc cache at curl_easy_cleanup after curl_easy_reset so upstream case 1908 passes

## Goal

A handle copied with `curl_easy_duphandle` and the original, reset with `curl_easy_reset`, each save the `CURLOPT_ALTSVC` cache at `curl_easy_cleanup` as libcurl does, and upstream case 1908 passes.

## Context

Found by BL-2545 (lib1908 port). lib1908 performs with `CURLOPT_ALTSVC` set to `log/altsvc-1908`, performs again on a duplicate, cleans up the duplicate, then resets and cleans up the original; libcurl keeps the alt-svc cache across `curl_easy_reset` and writes it at cleanup. Case 1908's `<verify><file>` differs at byte 119 (line 3): expected `h1 127.0.0.1 8990 h2 3dbbdetxoyw4nsp6c3cc456oj2ays6s43ezxzsfxxri3h5xqd.example 443 TIMESTAMP 1 0`, got the end - the file holds only its two header lines, so no entry was saved (the `Debug` and `CURL_ALTSVC_HTTP` stand-in already lets the case run).

## Acceptance criteria

- [x] After `curl_easy_reset`, `curl_easy_cleanup` still saves the alt-svc entries learned before the reset to the `CURLOPT_ALTSVC` file, with a focused unit test.
- [x] Case 1908 passes through the runner on Windows and is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`, its alt-svc file free of any platform-dependent text.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.

## Notes

- 2026-10-18: touches narrowed from Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Libcurl, Libcurl.UnitTests to files, by reading ManagedEasyTransfer cleanup/duplicate/reset, TransferPerformer.WriteCookieJarAltSvcAndHstsFilesAsync and AltSvcTransferCache, so it can run beside other Engine tasks.

- 2026-10-18: Two causes, not one. (1) The libtest engine's handle host never served alt-svc over `http`, so `CURL_ALTSVC_HTTP` was ignored (`ManagedCurlEngine.HandleHostOf` now sets `AltSvcUsesOverHttp` when its environment reader has the variable). (2) The handle wrote the alt-svc file after every perform from a per-perform cache; libcurl keeps `asi` on the handle across performs and `curl_easy_reset` and saves it at cleanup only, and a duplicate loads the file afresh (empty, as it is not yet written). The handle now keeps its first cache (`handleAltSvc`) and writes it in `DisposeAsync`. Touches widened to ManagedCurlEngine.cs, Engine CLAUDE.md and ManagedEasyTransferTests.Files.cs (its alt-svc test now expects the write at cleanup); no other Doing task named them.

## Log

- 2026-10-17: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: bf0c5f01 05ce0a5e 4272a6b9.
- 2026-10-18: Doing -> Done. Case 1908 passes; alt-svc cache kept per handle and saved at cleanup
