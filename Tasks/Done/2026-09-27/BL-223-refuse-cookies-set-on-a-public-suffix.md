---
id: BL-223
title: Refuse cookies set on a public suffix
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-220, BL-222]
touches: [Curl.Cookies.UnitLibrary, Curl.Cookies.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-27
---
# BL-223 — Refuse cookies set on a public suffix

## Goal

The cookie store refuses a cookie whose Domain is a public suffix, using the embedded PSL snapshot.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item Q4. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- The reference build has the PSL feature (BL-153).
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [x] A cookie with `Domain=co.uk` is refused and one for `example.co.uk` accepted, matching curl 8.21.0 (measured).
- [x] `dotnet build Curl.Cookies.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Cookies`.

## Notes

- Plan item: Q4 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.
- Measured 2026-09-27 on curl 8.21.0 (mingw, libpsl 0.21.5) with `Record-CurlExchange.ps1`: `Set-Cookie: n<i>=v; Path=/; <Domain>` served to `curl -s -v --resolve <host>:<port>:127.0.0.1 -c - http://<host>:<port>/`. Dropped (jar empty; stderr `* cookie 'n1' dropped, domain 'www.example.co.uk' must not set cookies for 'co.uk'`): www.example.co.uk with `Domain=co.uk`, `.co.uk`, `CO.UK`; www.example.com with `Domain=com`; a.foo.ck with `Domain=foo.ck` (`*.ck`); a.github.io with `Domain=github.io` (private section); www.example.xn--55qx5d.cn with `Domain=xn--55qx5d.cn` (punycode of `公司.cn`); a host-only cookie from a 256-character host. Kept (jar line, e.g. `.example.co.uk	TRUE	/	FALSE	0	n2	v`): `Domain=example.co.uk`, `Domain=example.com`, co.uk with `Domain=co.uk` and host-only, a.www.ck with `Domain=www.ck` (`!www.ck`), a.b.zzqq with `Domain=b.zzqq`, a host-only cookie from a 255-character host. Pinned in `CookieStoreTests.PublicSuffix.cs`.
- Implementation: `PublicSuffixList` (internal) reads the embedded snapshot lazily and answers libpsl's `psl_is_cookie_domain_acceptable` (exact match accepted, otherwise the cookie domain must be longer than `psl_unregistrable_domain(host)`), with both ICANN and private rules, the implicit `*` rule, wildcards and exceptions; non-ASCII rules are also kept in punycode. curl's own 255-character limit comes first. `CookieStore.StoreFromResponse` applies it; cookie files (`-b`) are not checked, as curl passes no host for them.
- No ADR: behaviour follows measured curl and ADR-0049 already decides the snapshot. Planner/architect stages were collapsed into this run: the change is one class in one library.
- `-v` lines (`* Added cookie ...`, `* cookie ... dropped ...`) are not printed yet: CookieStore has no verbose channel. Filed as BL-367.
- Code review (2026-09-27): no defects. Not measured, left as is: a host with a trailing dot (`www.example.co.uk.`); `IsCookieDomainAcceptable` relies on `SetCookieParser` having already tail-matched the domain, as its parameter doc states.

## Log

- 2026-09-26: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. The cookie store drops a cookie set on a public suffix (Domain=co.uk from www.example.co.uk) by the embedded PSL snapshot, as curl 8.21.0 measured
