---
id: BL-2507
title: Curl.Api: cookie, HSTS and alt-svc stores
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2503]
touches: [Curl.Api/Stores, Curl.Api.UnitTests/Stores, Curl.Api/Client/CurlClient.cs, Curl.Api/Messages/CurlClient.SendAsync.cs, Curl.Api/CLAUDE.md]
requirement: none
created: 2026-10-16
completed: 2026-10-16
---
# BL-2507 — Curl.Api: cookie, HSTS and alt-svc stores

## Goal

A client can load, save and inspect its cookies, HSTS entries and alt-svc cache (`CurlClient.Cookies`, `.Hsts`, `.AltSvc`), and can share them between clients through `IShareHandle`.

## Context

Phase 4 of `Documentation/Planning/Curl-Api-And-libcurl.md`; ADR-0506. `Curl.Api` references `Curl.Api.UnitLibrary` (the seam: `ICurlEngine`, `IEasyTransfer`, `EasyOptions`, `TransferInfo`, callbacks) only until the engine-building task; it never references `Libcurl`. Code lives in `Curl.Api/<Area>` with tests in `Curl.Api.UnitTests/<Area>`, namespace `Curl.Api`, BCL only, native-AOT safe (no reflection). Async with `CancellationToken` everywhere; no `.Result` or `.Wait()`. Maps to the easy cookie, HSTS and alt-svc options, the HSTS read and write callbacks, the cookie list, and the share handle (`ICurlEngine.CreateShare()`).

## Acceptance criteria

- [x] Tests cover adding, listing, clearing, loading from and saving to a path, the HSTS callbacks and two clients sharing one store.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Api` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30); the tests use fakes of the seam interfaces, never the network.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [x] `Curl.Api/CLAUDE.md` is kept true.

## Notes

- Design (ADR-0645, decided under Stewart's delegation): `CurlStores` (`Curl.Api/Stores`) holds `CurlCookieStore` (Netscape lines), `CurlHstsStore` (`HstsEntry`s) and `CurlAltSvcStore` (the alt-svc cache file; libcurl has no in-memory alt-svc API), and owns one `IShareHandle` sharing cookies and HSTS. `CurlClientOptions.Stores` shares them between clients; otherwise `CurlClient.Stores`/`.Cookies`/`.Hsts`/`.AltSvc` makes the client's own on first use (cookie engine and HSTS stay off until then, as in libcurl), disposed with the client. `StoreBoundEasyTransfer` applies the stores at perform (so they win over request options) and keeps `CURLINFO_COOKIELIST` at dispose; the HSTS write callback refreshes HSTS at cleanup.
- Touches widened (no task in Doing on `origin/work/dark-factory` names these): `Curl.Api/Client/CurlClient.cs` (`CreateEasy` binds handles to the stores; `DisposeAsync` disposes the client's own stores), `Curl.Api/CLAUDE.md` (criterion 5), and `Curl.Api/Messages/CurlClient.SendAsync.cs`: its `await` inside `catch` left a compiler-made rethrow branch (line 60) no test can reach, so `Measure-CodeQuality.ps1 -Library Curl.Api` failed it even with a test through that path; the setup failure is now caught into a variable and rethrown through `DisposeUnstartedThenRethrowAsync`, behaviour unchanged.
- Found: the managed engine never applies `CURLOPT_COOKIELIST` (`ManagedEasyTransfer` only fills `TransferInfo.CookieList`), so with the real engine stored cookies do not reach a transfer yet; filed BL-2520. HSTS callbacks and alt-svc files already work in the engine.
- Measured: `Measure-CodeQuality.ps1 -Library Curl.Api` 0 failing members, 100% line and branch (326 members, worst CRAP 10). `dotnet build` clean, 0 warnings; fast tests all passed (Curl.Api.UnitTests 752 passed, 2 skipped as before; Curl.Conformance.UnitTests 3130 passed); `PassingUpstreamCases.txt` unchanged.

## Log

- 2026-10-16: Created.
- 2026-10-16: Backlog -> Doing. Criteria at claim: aa455d79 4272a6b9 5393a01b 8738c7f6 ae118507.
- 2026-10-16: Doing -> Done. CurlClient.Cookies, .Hsts and .AltSvc load, save, list and clear their stores, applied to every transfer and shared between clients through one IShareHandle
