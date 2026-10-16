# ADR-0556 — `CurlGlobal.Init(ICurlEngine)` returns a `CurlLibrary` that counts inits, holds the engine and creates handles through internal methods each area makes public

- **Status:** Accepted
- **Date:** 2026-10-15

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2456.

## Context

ADR-0506 and phase 3 of `Documentation/Planning/Curl-Api-And-libcurl.md` have `Libcurl`'s
`CurlGlobal.Init()` return a library object that creates handles. The handle types (`CurlEasy`,
`CurlMulti`, `CurlShare`, `CurlUrlHandle`, `CurlMime`, `CurlForm`) are each their own later task in
their own folder (`Libcurl/Easy`, `Libcurl/Multi/Core`, ...), and those tasks depend on this one, so
the library object cannot name them yet. libcurl's `curl_global_init` keeps a counter: each init adds
one, each cleanup takes one away, and a cleanup with the counter at zero does nothing.
`curl_global_init_mem` fails with `CURLE_FAILED_INIT` when any of its five allocation callbacks is
NULL. `curl_global_sslset` on a single-backend build answers `CURLSSLSET_OK` when the id matches the
build's backend or the name matches it ignoring case, and `CURLSSLSET_TOO_LATE` otherwise, always
filling `avail` with the one backend. `curl_global_trace` answers `CURLE_OK` for any configuration,
skipping names it does not know.

## Decision

1. `CurlGlobal.Init(ICurlEngine engine, CurlGlobalFlags flags = Default)` returns a new
   `CurlLibrary` over that engine and counts one init on a process-wide counter (`CurlGlobal.InitCount`).
   Every `CURL_GLOBAL_*` value is accepted and kept on `CurlLibrary.Flags`: the managed engine needs
   none of the work they ask for. The engine-building `Init()` overload is BL-2457.
2. `CurlLibrary` is `IAsyncDisposable`; `DisposeAsync` is `curl_global_cleanup`. The first one counts
   the cleanup and disposes the engine (whose own dispose is the cleanup, as `ICurlEngine` says); a
   second one on the same library does nothing. One library per engine, not one shared global engine:
   a .NET program can run two engines side by side, and the counter still answers what libcurl's does.
3. `CurlLibrary` is `public sealed partial`. Its internal `CreateEasyTransfer`, `CreateMultiTransfer`,
   `CreateShareHandle`, `CreateUrlHandle`, `CreateMimeBuilder` and `CreateFormBuilder` throw
   `ObjectDisposedException` after cleanup and otherwise delegate to the engine. Each handle area's task
   adds the public creating method returning its own handle type, as a partial part of `CurlLibrary` in
   its own folder, over these. This replaces the task text's "creating methods may throw until their
   area lands": no public method exists that only throws.
4. `CurlGlobal.InitMemory` takes the five allocation callbacks as delegates (`CurlMallocCallback`,
   `CurlFreeCallback`, `CurlReallocCallback`, `CurlStrdupCallback`, `CurlCallocCallback`), throws
   `ArgumentNullException` naming a missing one where libcurl answers `CURLE_FAILED_INIT` (the method
   returns a library, not a code), and never calls them.
5. `CurlGlobal.SslSet(id, name, out available)` answers as the build Curl matches: Schannel
   (`CURLSSLBACKEND_SCHANNEL`, 8, `schannel`) on Windows, OpenSSL (1, `openssl`) elsewhere.
   `CurlGlobal.Trace(configuration)` answers `CurlCode.Ok` and changes nothing, since the engine has no
   trace components to switch yet.

## Consequences

- Area tasks touch only their own folder: a `CurlLibrary.<Area>.cs` partial part beside their handle.
- Tests inject a `GlobalInitCounter` through the internal `InitCounting` and `InitMemoryCounting`, so
  method-parallel tests never share the process-wide count.
