---
id: BL-2507
title: Curl.Api: cookie, HSTS and alt-svc stores
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2503]
touches: [Curl.Api/Stores, Curl.Api.UnitTests/Stores]
requirement: none
created: 2026-10-16
completed:
---
# BL-2507 — Curl.Api: cookie, HSTS and alt-svc stores

## Goal

A client can load, save and inspect its cookies, HSTS entries and alt-svc cache (`CurlClient.Cookies`, `.Hsts`, `.AltSvc`), and can share them between clients through `IShareHandle`.

## Context

Phase 4 of `Documentation/Planning/Curl-Api-And-libcurl.md`; ADR-0506. `Curl.Api` references `Curl.Api.UnitLibrary` (the seam: `ICurlEngine`, `IEasyTransfer`, `EasyOptions`, `TransferInfo`, callbacks) only until the engine-building task; it never references `Libcurl`. Code lives in `Curl.Api/<Area>` with tests in `Curl.Api.UnitTests/<Area>`, namespace `Curl.Api`, BCL only, native-AOT safe (no reflection). Async with `CancellationToken` everywhere; no `.Result` or `.Wait()`. Maps to the easy cookie, HSTS and alt-svc options, the HSTS read and write callbacks, the cookie list, and the share handle (`ICurlEngine.CreateShare()`).

## Acceptance criteria

- [ ] Tests cover adding, listing, clearing, loading from and saving to a path, the HSTS callbacks and two clients sharing one store.
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Api` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30); the tests use fakes of the seam interfaces, never the network.
- [ ] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [ ] `Curl.Api/CLAUDE.md` is kept true.

## Notes

## Log

- 2026-10-16: Created.
- 2026-10-16: Backlog -> Doing. Criteria at claim: aa455d79 4272a6b9 5393a01b 8738c7f6 ae118507.
