# ADR-0742 — A library perform fails with `CURLE_TOO_LARGE` on a cookie file line too long

- **Status:** Accepted
- **Date:** 2026-10-21

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2978.

## Context

BL-2971 made `TransferPerformer.LoadCookieFilesAsync` answer exit 100 (`CURLE_TOO_LARGE`) when a `-b` file holds a
line longer than 4998 characters (ADR-0739), and the curl tool's runner fails the transfer with it before connecting.
`ManagedEasyTransfer.SetUpAsync`, the setup a `Libcurl` or `Curl.Api` handle runs for itself, discarded that answer,
so a library perform went ahead with the cookies read before the long line. libcurl 8.22.0's `Curl_pretransfer`
fails the transfer with what `Curl_cookie_loadfiles` returns, before anything is sent. A `CURLOPT_COOKIELIST`
`RELOAD` calls the same `Curl_cookie_loadfiles` from `curl_easy_setopt`, whose error goes to the setopt caller; the
engine's options are an immutable record applied at perform (ADR-0657), so there is no setopt answer to give.

## Decision

1. `SetUpAsync` returns the load's failure beside the URL, and the perform answers its code (`CurlCode.TooLarge`) and
   text before the alt-svc and HSTS files are read, the context is built or anything is sent. `LastResult` is left as
   other failures before connecting leave it.
2. `CookieEngine.ApplyCookieListItemAsync`'s `RELOAD` still discards its answer. The files `RELOAD` reads are the
   handle's `CURLOPT_COOKIEFILE` list, which the perform loads right after the cookie list is applied, so the same
   long line fails the perform there with 100 and nothing is sent: the outcome libcurl's perform gives after its
   failed setopt, without inventing a setopt answer the engine has no place for.
3. The jar is not written after such a failure; libcurl writes it at `curl_easy_cleanup`, which the engine's per-perform
   jar write does not model either way.

## Consequences

`ManagedEasyTransferTests.CookieList.cs` pins a 4999-character line failing with 100 and sending nothing, through
`CURLOPT_COOKIEFILE` and through `RELOAD`, and a 4998-character line loading and sending its cookie.
