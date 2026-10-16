---
id: BL-2520
title: Engine: apply CURLOPT_COOKIELIST lines and commands at perform
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-16
completed:
---
# BL-2520 — Engine: apply CURLOPT_COOKIELIST lines and commands at perform

## Goal

A `ManagedCurlEngine` transfer whose `EasyHttpOptions.CookieList` holds cookie lines and commands applies them to its cookie engine before the request is sent, as libcurl 8.22.0 does with `CURLOPT_COOKIELIST`.

## Context

Found by BL-2507 (ADR-0641): `Curl.Api`'s `CurlCookieStore` starts every transfer from `CURLOPT_COOKIELIST` `ALL` then one Netscape line per stored cookie, and reads the result back from `CURLINFO_COOKIELIST`. `ManagedEasyTransfer` fills `TransferInfo.CookieList` but never reads `Options.Http.CookieList`, so with the real engine the stored cookies are not sent (tests of `Curl.Api` use fakes, so they pass). libcurl's commands: `ALL` (erase every cookie), `SESS` (erase session cookies), `FLUSH` (write the jar), `RELOAD` (load the cookie files); any other line is a Netscape cookie-file line or a `Set-Cookie:` header line. Each item applies in order; the cookie engine starts as `CookieEngine.FromHttpOptions` makes it. Libcurl's setter (`Libcurl/Options/Http/EasyHttpOptionSetters.cs`) already collects the list.

## Acceptance criteria

- [ ] Engine unit tests show `ALL`, `SESS`, `FLUSH`, `RELOAD`, a Netscape line and a `Set-Cookie:` line in `CookieList` each change the cookies the next request sends and `TransferInfo.CookieList`, as libcurl 8.22.0 does.
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary` reports 0 failing members.
- [ ] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.

## Notes

## Log

- 2026-10-16: Created.
