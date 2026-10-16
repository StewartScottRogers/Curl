# ADR-0587 — `Libcurl`'s `CurlUrlHandle` passes `curl_url_get` and `_set` through to the engine's `ICurlUrlHandle`, `UrlInit` is `curl_url`, and a cleaned-up handle throws

- **Status:** Accepted
- **Date:** 2026-10-15

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2438.

## Context

ADR-0506 and phase 3 of `Documentation/Planning/Curl-Api-And-libcurl.md` give `Libcurl` a
`CurlUrlHandle` for `curl_url`, `_cleanup`, `_dup`, `_get`, `_set` and `_strerror`. The seam already
has `ICurlUrlHandle` with `CurlUrlPart`, `CurlUrlFlags` and `CurlUrlCode` (BL-2314, ADR-0511), the
engine implements it over Curl's `CurlUrl` parser (BL-2436, BL-2437), and `CurlErrorText.Url` is
`curl_url_strerror`'s table (ADR-0562).

## Decision

1. `CurlLibrary.UrlInit()` is `curl_url`, added as a `CurlLibrary` part in `Libcurl/Url` beside
   `EasyInit` and `MultiInit`. It is named `UrlInit`, not `Url`, so it reads like its siblings and
   does not shadow the `Libcurl.Url` namespace.
2. `CurlUrlHandle` (namespace `Libcurl.Url`) is a thin wrapper: `Get` and `Set` pass the part, value
   and flags to the engine's handle and answer its code as it is. Part and flag checks (an unknown
   part, a bad port) are the parser's, so they are made once, in the engine.
3. `Duplicate` (`curl_url_dup`) wraps the engine's copy in a new, independent `CurlUrlHandle`.
4. `Dispose` is `curl_url_cleanup`: it disposes the engine handle once, and any member used afterwards
   throws `ObjectDisposedException`, as `CurlEasy`'s and `CurlMulti`'s do. Upstream's
   `CURLUE_BAD_HANDLE` and `CURLUE_BAD_PARTPOINTER` answer a `NULL` pointer, which a C# reference
   cannot be once the handle exists, so `Libcurl` never produces them.
5. `CurlUrlHandle.ErrorText` is `curl_url_strerror`, delegating to `CurlErrorText.Url`, so the
   strings stay in one table.
6. The handle exposes its live engine handle internally (`EngineHandle`) for `CURLOPT_CURLU`, which
   BL-2462 sets.

## Consequences

- `CURLOPT_CURLU` still answers `NotBuiltIn` until BL-2462 stores `EngineHandle` on the easy handle's
  options.
