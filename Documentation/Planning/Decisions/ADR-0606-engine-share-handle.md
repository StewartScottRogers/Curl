# ADR-0606 — The engine's share handle

- **Status:** Accepted
- **Date:** 2026-10-16

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2434.

## Context

ADR-0506 put `IShareHandle` on the seam (`curl_share_setopt`'s `SHARE`, `UNSHARE`, `LOCKFUNC` and
`UNLOCKFUNC`, and `curl_share_cleanup`) and `EasyBehaviourOptions.Share` (`CURLOPT_SHARE`) on the easy
options; `ManagedCurlEngine.CreateShare` threw. ADR-0542 already shares every cache - connections, TLS
sessions, DNS, cookies, HSTS - between all handles of one engine, so within one engine a share handle
adds nothing a transfer can observe; what it adds is sharing between handles of different engines, and
the lock callbacks. The engine's caches are thread-safe on their own, and its transfers are asynchronous,
so a lock taken by a callback must never be held across an `await`: a .NET `Monitor` or `Mutex` the
application takes in its lock callback must be released on the same thread.

## Decision

1. **A share handle owns its caches.** `ManagedShareHandle` holds its own `CookieStore`, `DnsCache`,
   `TlsSessionCache`, `ConnectionCache` and `HstsTransferPolicy`. A perform whose `CURLOPT_SHARE` names
   it is attached for the perform's length (`Attach` / `Detach`) and runs over a
   `ManagedEngineSharedState` with the share's cache in place of the engine's for each kind it shares;
   the `%{xfer_id}` and `%{conn_id}` numbering, the multi owners and the performer stay the engine's.
   An `IShareHandle` of another implementation shares nothing.
2. **Which kinds, which codes.** `SHARE` and `UNSHARE` accept `COOKIE`, `DNS`, `SSL_SESSION`, `CONNECT`,
   `PSL` and `HSTS` and answer `CURLSHE_BAD_OPTION` for anything else (`NONE`, `SHARE`, unknown), as
   curl 8.22.0's `curl_share_setopt`. `PSL` is accepted and shares nothing: the engine keeps no public
   suffix list. Every option answers `CURLSHE_IN_USE` while a transfer is attached, and every call after a
   successful cleanup `CURLSHE_INVALID`.
3. **When the lock callbacks run.** Each call is a synchronous lock and unlock pair with nothing awaited in
   between, always `CURL_LOCK_ACCESS_SINGLE`: `CURL_LOCK_DATA_SHARE` around attaching and detaching a
   transfer and (with a null transfer) around `curl_share_cleanup`; each shared cache kind as the attached
   transfer takes the cache; and `CURL_LOCK_DATA_COOKIE` around every read and write the transfer's HTTP
   handler makes of the shared cookies. Loading the `CURLOPT_COOKIEFILE` files and writing the
   `CURLOPT_COOKIEJAR` are asynchronous and run unlocked, as do the connection, DNS and TLS caches' own
   operations, which are thread-safe; libcurl locks more often, but an application cannot tell the
   difference except by counting calls.
4. **Cleanup** refuses with `CURLSHE_IN_USE` while a transfer is attached, else starts closing the shared
   connections without waiting (the call is synchronous) and marks the handle freed. A transfer that
   names a freed share handle runs over its engine's caches.

## Consequences

- Handles of two engines attached to one share handle send each other's cookies and reuse each other's
  connections (`ManagedShareHandleTests`).
- `Libcurl`'s `curl_share_*` reach a working handle through `CurlLibrary.CreateShareHandle`.
