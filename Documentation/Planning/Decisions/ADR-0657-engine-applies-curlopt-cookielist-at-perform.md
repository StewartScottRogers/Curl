# ADR-0657 — The engine applies `CURLOPT_COOKIELIST` at perform

- **Status:** Accepted
- **Date:** 2026-10-16

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2520.

## Context

`Curl.Api`'s `CurlCookieStore` (BL-2507, ADR-0645) starts every transfer from `CURLOPT_COOKIELIST` `ALL` and one
Netscape line per stored cookie, and reads the result back from `CURLINFO_COOKIELIST`. Libcurl's setter collects the
items in `EasyHttpOptions.CookieList`, but `ManagedEasyTransfer` never read them, so with the real engine the stored
cookies were never sent. libcurl 8.22.0 applies each item as it is set (`cookielist` in `lib/setopt.c`): `ALL`
(`Curl_cookie_clearall`), `SESS` (`Curl_cookie_clearsess`), `FLUSH` (`Curl_flush_cookies`, writing the jar),
`RELOAD` (`Curl_cookie_loadfiles`), each compared with `curl_strequal`; anything else is added with
`Curl_cookie_add`, as a header when it starts `Set-Cookie:` and as a Netscape line otherwise, creating the handle's
cookies first when it has none. The engine's options are an immutable record read at perform, so there is no setopt
moment to apply them at.

## Decision

1. A perform from the options with no `ToolInputs` applies the items, in the order set, in `SetUpAsync` before the
   cookie files load (`CookieEngine.ApplyCookieListAsync`): libcurl applies them at setopt, before the perform's
   `Curl_pretransfer` loads `CURLOPT_COOKIEFILE`. The perform's own file load still runs afterwards, as libcurl's
   does on every perform.
2. An item that is not a command turns the cookie engine on, as it creates libcurl's cookies; commands alone do
   not, and while the engine is off (no cookie file, jar or cookie line) every item is ignored, as libcurl's commands
   do nothing on a handle with no cookies.
3. `ALL` and `SESS` are `CookieStore.RemoveAll` and `RemoveSessionCookies` (new in `Curl.Cookies`); `ALL` keeps the
   `CURLOPT_COOKIE` strings, which libcurl keeps apart from its cookie list. `FLUSH` writes the jar as the cleanup
   does (`-` to the host's print target), nothing without a jar. `RELOAD` loads every cookie file again, whatever the
   URL's scheme. A cookie line goes through `CookieStore.LoadCookieFile`, the cookie-file reader, which takes both a
   Netscape line and a `Set-Cookie:` line as libcurl's file loader does.
4. The items stay in the options, so a later perform of the same handle applies them again. Libcurl's setter appends
   to the list on every `curl_easy_setopt`, so this is what a handle that re-performs without `curl_easy_reset` gets;
   for `CurlCookieStore`, which sets `ALL` first, it changes nothing.

## Consequences

- Through the managed engine, `CurlCookieStore`'s cookies are sent and `CURLINFO_COOKIELIST` reflects the list.
- Libcurl applies `noexpire` to a line added through `CURLOPT_COOKIELIST`; the engine removes a line that has already
  expired, as the cookie-file reader does. No caller depends on adding an expired cookie.
- Tests: `ManagedEasyTransferTests.CookieList.cs` and `CookieStoreTests.Removal.cs`.
