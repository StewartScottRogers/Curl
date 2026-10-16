# ADR-0645 — `Curl.Api` keeps cookies and HSTS in client-side stores applied at perform, alt-svc in its cache file, and shares them through one share handle

- **Status:** Accepted
- **Date:** 2026-10-16

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2507.

## Context

`CurlClient` makes a new easy handle for every request and disposes it when the transfer ends, so
nothing a transfer learns survives on the handle. libcurl gives a program three ways to keep and
inspect that state: `CURLOPT_COOKIELIST` and `CURLINFO_COOKIELIST` for cookies, the HSTS read and
write callbacks for HSTS, and only a file (`CURLOPT_ALTSVC`) for alt-svc. A share handle
(`CURLSHOPT_SHARE`) shares cookies and HSTS (`CURL_LOCK_DATA_COOKIE`, `CURL_LOCK_DATA_HSTS`)
between transfers running at the same time; alt-svc cannot be shared.

## Decision

- `CurlStores` (`Curl.Api/Stores`) holds a `CurlCookieStore`, a `CurlHstsStore` and a
  `CurlAltSvcStore`, and owns one `IShareHandle` of its engine sharing cookies and HSTS. A client
  uses `CurlClientOptions.Stores` when set (shared with any other client naming them, disposed by
  the caller), or else its own, made over its engine the first time `CurlClient.Stores`,
  `.Cookies`, `.Hsts` or `.AltSvc` is read and disposed with the client. Until then the client's
  handles are bound to nothing, so the cookie engine and HSTS stay off, as in libcurl.
- Once a client has stores, `CreateEasy` wraps each handle in `StoreBoundEasyTransfer`. Its
  `PerformAsync` applies the stores last, so they win over the request's own options:
  `CURLOPT_SHARE` at the share handle; the cookie engine on (an empty `CURLOPT_COOKIEFILE` when
  none is set) with `CURLOPT_COOKIELIST` `ALL` then every stored Netscape line, so each transfer
  starts with exactly the store's cookies; `CURLHSTS_ENABLE` with a read callback that preloads the
  stored entries and a write callback that replaces them with the cache handed over at cleanup;
  and the alt-svc cache file with its `CURLOPT_ALTSVC_CTRL` once `CurlAltSvcStore.Use` names one.
  Its `DisposeAsync`, after a perform, replaces the stored cookies with `CURLINFO_COOKIELIST`.
- Cookies are Netscape cookie-file lines (seven tab-separated fields), the form libcurl reports;
  `Add` refuses anything else. HSTS entries are the seam's `HstsEntry`, one per host name. Load
  and save read and write curl 8.22.0's own file formats and headers (copied, as `CurlCodeText`
  copies the error text, because `Curl.Api` references only the seam and the engine).
- The alt-svc store is its cache file: `GetEntriesAsync`, `AddAsync`, `ClearAsync`, `LoadAsync`
  and `SaveAsync` read and write that file, because libcurl has no in-memory alt-svc interface.

## Consequences

- Clients sharing stores see each other's cookies and HSTS entries at their next transfer, and
  concurrent transfers see them live through the share handle. The last transfer to finish
  decides the stored cookies.
- The managed engine does not yet apply `CURLOPT_COOKIELIST` (BL-2520), so with the real engine
  stored cookies reach a transfer only once that lands; HSTS callbacks and alt-svc files already
  work. `Curl.Api`'s tests use fakes of the seam, as every `Curl.Api` task does.
