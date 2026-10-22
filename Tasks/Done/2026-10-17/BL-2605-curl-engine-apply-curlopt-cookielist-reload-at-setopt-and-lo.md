---
id: BL-2605
title: Curl.Engine: apply CURLOPT_COOKIELIST RELOAD at setopt and lock cookies for CURLINFO_COOKIELIST (test506 reload lines)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2603]
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Libcurl, Libcurl.UnitTests]
requirement: none
created: 2026-10-17
completed: 2026-10-17
---
# BL-2605 — Curl.Engine: apply CURLOPT_COOKIELIST RELOAD at setopt and lock cookies for CURLINFO_COOKIELIST (test506 reload lines)

## Goal

A handle that never performs answers `CURLOPT_COOKIELIST` `RELOAD` and `CURLINFO_COOKIELIST` as libcurl 8.22.0 does over a share: `RELOAD` loads `CURLOPT_COOKIEFILE`'s files into the shared cookies at setopt under one cookie lock pair, and `CURLINFO_COOKIELIST` takes one more cookie lock pair and lists the shared store's cookies, so test506 passes through the runner.

## Context

BL-2603 made test506's perform lines match; `<stdout>` now differs at line 132: after `CURLOPT_COOKIELIST RELOAD` libcurl writes a second cookie pair (`lock:   cookie [Pigs in space]: 58`, from `curl_easy_getinfo(CURLINFO_COOKIELIST)`) and then lists the 8 cookies the jar written by run 3 holds; Curl lists none and `lib506` writes `reloading cookies from '...' failed` to stderr. Today `CURLOPT_COOKIELIST` items are applied only inside a perform (`ManagedEasyTransfer.SetUpAsync`, `CookieEngine.ApplyCookieListAsync`, BL-2520), and `CURLINFO_COOKIELIST` is filled from the last perform's `Info`. Compare with upstream's lib/setopt.c (`cookielist`), lib/cookie.c (`Curl_cookie_loadfiles`, `Curl_cookie_list`) and lib/getinfo.c. Also check test506's `<file>` (the jar `%LOGDIR/jar506`) matches once the list does.

## Acceptance criteria

- [x] test506 passes through the runner and is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`, or the lines still differing are filed as their own task.
- [x] A focused unit test in `Curl.Engine.UnitTests` pins the `RELOAD` and `CURLINFO_COOKIELIST` lock pairs and the listed cookies (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Over a share that shares cookies, `ManagedEasyTransfer.Options` now applies each new `CURLOPT_COOKIELIST` item at setopt under its own cookie lock pair (`ManagedShareHandle.ApplyCookieListItems`, `CookieEngine.OverSharedCookies`); a perform skips the items already applied. `Info.CookieList` over such a share is a lazy `ShareCookieListLines`, read under one cookie lock pair (ADR-0667).
- Added `Libcurl` and `Libcurl.UnitTests` to `touches` (no task in Doing named them): the lock must fall at the getinfo call, before `lib506` writes `loaded cookies:`, so `Libcurl`'s `CURLINFO_COOKIELIST` reader now copies the list, as libcurl hands back a new slist; `GetInfo_Cookielist_ReadsTheCookieLines` became `GetInfo_Cookielist_ReadsACopyOfTheCookieLines`.
- Listing order is the share's store order (as `CookieEngine.ListLines`), which after `ALL` and `RELOAD` is the jar file's order, as test506 expects.
- Setopt is synchronous, so a `RELOAD` or `FLUSH` there blocks on the file system, as `ManagedFormBuilder.Serialize` does (ADR-0667). Handles without a cookie-sharing share keep BL-2520's perform-time application.
- Unit tests: `ManagedShareHandleTests.Options_CookieListReloadThenGetInfo_LoadsTheFileAtSetoptAndListsUnderOneMoreCookieLock`, `ShareCookieListLinesTests`.
- test506 passes through the runner (`CURL_UPSTREAM_CASE=506`), its jar `<file>` included. Fast tests green; Measure-CodeQuality for Curl.Engine.UnitLibrary and Libcurl: 0 failing members.

## Log

- 2026-10-17: Created.
- 2026-10-17: Backlog -> Doing. Criteria at claim: 4e518969 d822afa7 3d836d5e.
- 2026-10-17: Doing -> Done. CURLOPT_COOKIELIST items apply at setopt over a cookie-sharing share and CURLINFO_COOKIELIST lists its cookies under one cookie lock; test506 passes
