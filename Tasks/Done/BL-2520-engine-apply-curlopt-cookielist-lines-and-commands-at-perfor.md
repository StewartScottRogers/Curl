---
id: BL-2520
title: Engine: apply CURLOPT_COOKIELIST lines and commands at perform
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Cookies.UnitLibrary, Curl.Cookies.UnitTests]
requirement: none
created: 2026-10-16
completed: 2026-10-16
---
# BL-2520 — Engine: apply CURLOPT_COOKIELIST lines and commands at perform

## Goal

A `ManagedCurlEngine` transfer whose `EasyHttpOptions.CookieList` holds cookie lines and commands applies them to its cookie engine before the request is sent, as libcurl 8.22.0 does with `CURLOPT_COOKIELIST`.

## Context

Found by BL-2507 (ADR-0645): `Curl.Api`'s `CurlCookieStore` starts every transfer from `CURLOPT_COOKIELIST` `ALL` then one Netscape line per stored cookie, and reads the result back from `CURLINFO_COOKIELIST`. `ManagedEasyTransfer` fills `TransferInfo.CookieList` but never reads `Options.Http.CookieList`, so with the real engine the stored cookies are not sent (tests of `Curl.Api` use fakes, so they pass). libcurl's commands: `ALL` (erase every cookie), `SESS` (erase session cookies), `FLUSH` (write the jar), `RELOAD` (load the cookie files); any other line is a Netscape cookie-file line or a `Set-Cookie:` header line. Each item applies in order; the cookie engine starts as `CookieEngine.FromHttpOptions` makes it. Libcurl's setter (`Libcurl/Options/Http/EasyHttpOptionSetters.cs`) already collects the list.

## Acceptance criteria

- [x] Engine unit tests show `ALL`, `SESS`, `FLUSH`, `RELOAD`, a Netscape line and a `Set-Cookie:` line in `CookieList` each change the cookies the next request sends and `TransferInfo.CookieList`, as libcurl 8.22.0 does.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary` reports 0 failing members.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.

## Notes

- Touches widened to `Curl.Cookies.UnitLibrary` and `Curl.Cookies.UnitTests`: `CookieStore` had no way to remove
  cookies, which `ALL` and `SESS` need, so it gains `RemoveAll` and `RemoveSessionCookies` (under its lock). No task
  in Doing on `origin/work/dark-factory` named either project (BL-2517, BL-2556 checked).
- Decisions in ADR-0656: the items apply in `SetUpAsync` before the cookie files load (libcurl applies them at
  setopt, before `Curl_pretransfer` loads the files); a non-command item turns the cookie engine on; with the
  engine off every item is ignored; `RELOAD` loads the files again (the perform's own load still follows, as
  libcurl loads them on every perform); a line goes through the cookie-file reader, which reads Netscape and
  `Set-Cookie:` lines alike. The items stay in the options, so a re-perform applies them again.
- Not measured against a libcurl program: the engine follows `lib/setopt.c` and `lib/cookie.c` of 8.22.0 as read.

## Log

- 2026-10-16: Created.
- 2026-10-16: Backlog -> Doing. Criteria at claim: baf0dd60 4272a6b9 c4aff572 8738c7f6.
- 2026-10-16: Doing -> Done. ManagedEasyTransfer applies CURLOPT_COOKIELIST (ALL, SESS, FLUSH, RELOAD, Netscape and Set-Cookie lines) to its cookie engine before the request (ADR-0656)
