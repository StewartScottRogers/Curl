# ADR-0667: CURLOPT_COOKIELIST applies at setopt over a cookie-sharing share, and CURLINFO_COOKIELIST locks when read

- Status: Accepted
- Date: 2026-10-17
- Decided by Claude under Stewart's delegation
- Task: BL-2605 (amends ADR-0657 and ADR-0606)

## Context

libcurl 8.22.0 applies each `CURLOPT_COOKIELIST` item as it is set (`cookielist` in `lib/setopt.c`), under the
share's cookie lock, and `curl_easy_getinfo(CURLINFO_COOKIELIST)` takes the cookie lock once more and copies the
cookies into a new slist (`lib/getinfo.c`). Upstream test506 shows both: a handle that never performs sets
`ALL`, `CURLOPT_COOKIEFILE` and `RELOAD` over a share, then reads `CURLINFO_COOKIELIST` and lists the 8 cookies
the jar holds, the getinfo's lock pair coming before `loaded cookies:`. Curl applied the items only inside a perform
(BL-2520) and filled `CURLINFO_COOKIELIST` from the last perform, so the handle listed nothing.

`curl_easy_setopt` is synchronous while the engine's file system is asynchronous.

## Decision

- While a handle is attached to a share handle that shares cookies and every earlier item was applied,
  `ManagedEasyTransfer.Options` applies each newly added item to the share's cookies at once, each under its own
  cookie lock pair (`ManagedShareHandle.ApplyCookieListItems`, `CookieEngine.OverSharedCookies`), blocking on a
  `RELOAD`'s files or a `FLUSH`'s jar as `ManagedFormBuilder.Serialize` blocks for `curl_formget`. A later perform
  applies only the items not yet applied. Without such a share nothing changes: the items are applied in the perform.
- While a handle is attached to such a share, `Info.CookieList` is a `ShareCookieListLines`: the share's cookies in
  the order stored, read under one cookie lock pair the first time the list is read. `Libcurl`'s
  `CURLINFO_COOKIELIST` reader copies the list, as libcurl hands back a new slist, so the lock is taken at the
  getinfo call. Reading any other `Info` member takes no lock.

## Consequences

test506 passes through the runner. Handles without a cookie-sharing share keep BL-2520's perform-time behaviour;
moving those to setopt as well is left until an upstream case needs it, as their effect is only observable
through a later perform, which already applies them first.
