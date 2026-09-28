---
id: BL-460
title: Print curl's -v refusal lines for Set-Cookie lines in a -b cookie file
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-443]
touches: [Curl.Cookies.UnitLibrary, Curl.Cookies.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-27
completed:
---
# BL-460 — Print curl's -v refusal lines for Set-Cookie lines in a -b cookie file

## Goal

Under `-v`, loading a `-b` cookie file reports each `Set-Cookie:` line curl 8.21.0 refuses with the `* ` line curl prints for it, as `CookieStore.StoreFromResponse` does for a received header since BL-443.

## Context

- Found in BL-443. Measured 2026-09-27 on curl 8.21.0 (mingw) with `Record-CurlExchange.ps1`: `curl -s -v -b cf.txt -c - http://127.0.0.1:<port>/`, where `cf.txt` held `Set-Cookie: f=v; Pa<TAB>th=/; X=<0x01>` and `Set-Cookie: g=v; X=<0x01>` (LF line endings). curl printed `* invalid octets in value, cookie dropped` once (for `g`) and sent `Cookie: f=v`.
- `SetCookieParser.Parse(headerValue, requestUrl, now, out refusal)` already gives the line for a received header; `ParseFromCookieFile` discards it. `CookieStore.LoadCookieFile` / `LoadCookieFileAsync` take no `ITransferEvents`; `Curl.Console\CookieEngine.cs` calls them.
- Before pinning, measure which refusals print for a file line (a file line has no request, so `Secure` and `Domain` never refuse), and whether a refused Netscape tab-separated line prints anything.

## Acceptance criteria

- [ ] A test in `Curl.Cookies.UnitTests` asserts loading a file with `Set-Cookie: g=v; X=<0x01>` reports exactly `invalid octets in value, cookie dropped` to a recording `ITransferEvents`, and one with a clean file reports nothing.
- [ ] The measurements in Context are recorded in this task's Notes with command and stderr bytes, and each is pinned in a test.
- [ ] `Curl.Console` passes its transfer events to the cookie file load; a `Curl.Console.UnitTests` test shows the `* ` line on stderr under `-v`.
- [ ] `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Cookies` and `Curl.Console`.

## Notes

## Log

- 2026-09-27: Created.
