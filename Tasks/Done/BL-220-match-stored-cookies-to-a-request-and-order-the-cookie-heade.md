---
id: BL-220
title: Match stored cookies to a request and order the Cookie header
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-219, BL-161]
touches: [Curl.Cookies.UnitLibrary, Curl.Cookies.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-220 — Match stored cookies to a request and order the Cookie header

## Goal

A cookie store implements `ICookieStore`, matching cookies by domain, path, secure and expiry and ordering them as curl does.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item Q2. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- `ICookieStore` from BL-161.
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [x] The Cookie header order is measured on curl 8.21.0 and pinned; expiry uses the passed time.
- [x] `dotnet build Curl.Cookies.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Cookies`.

## Notes

**Partial work from a cut-off run (2026-09-26):** local branch `factory/BL-220-wip` holds one commit of it. Start with `git cherry-pick --no-commit factory/BL-220-wip`, review it, then carry on from there rather than starting over.

- Plan item: Q2 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.

- Resumed from `factory/BL-220-wip` (lane 2's cut-off run): `CookieStore : ICookieStore`, `CookieOrigin` (host, secure-context and tail-match rules now shared with `SetCookieParser`) and `CookieStoreTests`. Reviewed, kept, then split `GetCookieHeader` into `IsSentTo` and `InSendingOrder` (a stable sort over newest-first with an `IComparer<Cookie>`), because the LINQ lambda chain measured cyclomatic complexity 14 and then 12.
- Re-measured on curl 8.21.0 (`/mingw64/bin/curl`, Schannel, libpsl) with `Record-CurlExchange.ps1 -Port 18220`, 2026-09-26; each matches its pinned test:
  - `-Connections 2 -Response` setting `a=1; Path=/`, `bbb=2; Path=/`, `cc=3; Path=/`, `z=4; Path=/p`, `y=5; Path=/p/q`, `d=6; Domain=127.0.0.1; Path=/`, `s=7; Secure; Path=/`, `e=8; Max-Age=1; Path=/`, `x=9; Path=/pq`, `w=10; Path=/P`, `-CurlArgs -b none.txt http://127.0.0.1:18220/ 'http://127.0.0.1:18220/p/q/r?x=/a'` sent `Cookie: y=5; z=4; bbb=2; cc=3; e=8; s=7; d=6; a=1` (`GetCookieHeader_OneHost_OrdersByPathThenNameThenNewestFirst`).
  - `-b in.txt` (Netscape jar in this order: host-only `hostonly=1`; `.example.test` `domcookie=2`, `parent=4`; secure `sec=5`; expired `old=6`; `r1=7`; `r2=8`; `other.example.test` `sib=9`; `upper=10`), `--connect-to ::127.0.0.1:18220 http://www.example.test/ http://www.example.test/`, response `Set-Cookie: r1=new`, sent `Cookie: hostonly=1; upper=10; r2=8; r1=7; domcookie=2; parent=4` then `Cookie: hostonly=1; upper=10; r2=8; r1=new; domcookie=2; parent=4` (`GetCookieHeader_ParentDomains_...`): a replaced cookie keeps its place.
  - `-b many.txt` with `k1`..`k200` for `127.0.0.1`: 150 cookies sent, `k150=v` first, `k1=v` last (`GetCookieHeader_ManyCookies_...`).
  - `-b long.txt` with `aaa`=4000 x, `bb`=4000 x, `c`=165 x, `dd=1`: value 8015 characters, `aaa; dd; bb` (`GetCookieHeader_LongCookies_...`, second row).
- Expiry: the store reads no clock; `GetCookieHeader` and `StoreFromResponse` drop cookies whose expiry is before the passed `now` (`GetCookieHeader_Expiry_UsesThePassedTime`).
- No ADR: every rule here is curl's measured behaviour, not a choice. Public-suffix refusal (BL-223) and cookie files (BL-221) stay with their own tasks.
- Quality: `Measure-CodeQuality.ps1 -Library Curl.Cookies.UnitLibrary` reports 100% line, 100% branch, 112 members, 0 failing, worst CRAP 10.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Backlog. Shift stopped while waiting for tokens (limit reset early); partial work saved on branch factory/BL-220-wip
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. CookieStore implements ICookieStore: matches stored cookies by domain, path, secure and passed-time expiry and orders the Cookie header as curl 8.21.0 does
