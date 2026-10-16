# ADR-0563 — `Libcurl`'s `CurlEasy` sets options through the dispatch onto the engine transfer, `Perform` blocks on `PerformAsync`, and cleanup disposes the transfer once

- **Status:** Accepted
- **Date:** 2026-10-15

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2408.

## Context

ADR-0506 and phase 3 of `Documentation/Planning/Curl-Api-And-libcurl.md` give `Libcurl` a
`CurlEasy` with `SetOption`, a synchronous `Perform()` for fidelity with `curl_easy_perform`,
`PerformAsync(CancellationToken)` as the native shape, and `Dispose` as `curl_easy_cleanup`. The
seam is `IEasyTransfer` (`Curl.Api.UnitLibrary`): its `Options` property holds every option,
`PerformAsync` runs the transfer and `DisposeAsync` is cleanup. `CurlLibrary` (ADR-0556) creates the
engine transfer; `EasyOptionDispatch` (ADR-0560) sets one option on an `EasyOptions`.

## Decision

1. `CurlLibrary.EasyInit()` is `curl_easy_init`: a `CurlEasy` over `CreateEasyTransfer()` and
   `EasyOptionDispatch.Default`, added as a `CurlLibrary` part in `Libcurl/Easy`. It throws
   `ObjectDisposedException` after `curl_global_cleanup`, as the other creating methods do.
2. The handle keeps no options of its own: `SetOption` reads the transfer's `Options`, dispatches,
   and writes the changed copy back only when the answer is `Ok`, so a failing call leaves the
   options as they were, as libcurl's does. `CurlEasy.Options` reads them back.
3. `Perform()` is `PerformAsync(CancellationToken.None).AsTask().GetAwaiter().GetResult()`, the one
   sanctioned sync-over-async bridge in `Libcurl`, documented as such on the member.
4. `CurlEasy` is both `IDisposable` and `IAsyncDisposable`: `Dispose` blocks on `DisposeAsync`, and
   either disposes the transfer once; a second cleanup does nothing. libcurl leaves a call on a
   cleaned-up handle undefined; `CurlEasy` throws `ObjectDisposedException` from `SetOption`,
   `Perform`, `PerformAsync` and `Options`, the managed equivalent of refusing it.

## Consequences

- `Reset`, `DupHandle`, `Pause`, `Upkeep`, the error buffer and `GetInfo` (BL-2409 to BL-2411 and
  the info tasks) add their members to `CurlEasy` over the same transfer.
- Phase 4's `Curl.Api` wraps `CurlEasy` or the seam without a second options store to keep in step.
