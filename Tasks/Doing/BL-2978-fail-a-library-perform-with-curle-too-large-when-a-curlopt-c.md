---
id: BL-2978
title: Fail a library perform with CURLE_TOO_LARGE when a CURLOPT_COOKIEFILE has a line too long
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2971]
touches: [Curl.Engine.UnitLibrary/ManagedEasyTransfer.cs, Curl.Engine.UnitLibrary/CookieEngine.cs, Curl.Engine.UnitTests/ManagedEasyTransferTests.CookieList.cs]
requirement: none
created: 2026-10-21
completed:
---
# BL-2978 — Fail a library perform with CURLE_TOO_LARGE when a CURLOPT_COOKIEFILE has a line too long

## Goal

A `Libcurl` or `Curl.Api` handle whose `CURLOPT_COOKIEFILE` (or a `CURLOPT_COOKIELIST` `RELOAD`) reads a line longer than 4998 characters fails its perform with `CURLE_TOO_LARGE` (100), sending no request, as libcurl 8.22.0's `Curl_cookie_loadfiles` does.

## Context

BL-2971 made `TransferPerformer.LoadCookieFilesAsync` return a `TransferResult` failure with `CurlExitCode.TooLarge` when a `-b` file has a line too long, and `CurlCommandRunner` acts on it. `ManagedEasyTransfer.SetUpAsync` (`Curl.Engine.UnitLibrary/ManagedEasyTransfer.cs`) still awaits it and discards the result, and `CookieEngine.ApplyCookieListItemAsync`'s `RELOAD` discards `LoadEveryCookieFileAsync`'s `false`. Check upstream `lib/cookie.c` and `lib/transfer.c` (8.22.0) for whether `RELOAD` propagates the error before pinning it.

## Acceptance criteria

- [ ] A managed-engine perform with `EasyHttpOptions.CookieFiles` naming a file with a 4999-character line answers `CurlCode` 100 and sends no request, pinned by a test in `Curl.Engine.UnitTests`.
- [ ] A 4998-character line still loads and the perform goes ahead, pinned by a test.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Curl.Engine.UnitLibrary` keeps 100% line and branch coverage and complexity at most 10.

## Notes

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: 48f42234 138033cd b65be32d.
