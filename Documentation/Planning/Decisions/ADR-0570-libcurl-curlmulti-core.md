# ADR-0570 — `Libcurl`'s `CurlMulti` maps the engine's transfers back to the `CurlEasy` handles added, answers a bad handle itself, and is a partial class in namespace `Libcurl.Multi`

- **Status:** Accepted
- **Date:** 2026-10-15

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2429.

## Context

ADR-0506 and phase 3 of `Documentation/Planning/Curl-Api-And-libcurl.md` give `Libcurl` a
`CurlMulti` for `curl_multi_init`, `_add_handle`, `_remove_handle`, `_perform`, `_info_read` and
`_cleanup` over the seam's `IMultiTransfer` (`Curl.Api.UnitLibrary`). The seam speaks in
`IEasyTransfer`s, and its `CurlMultiMessage` names the engine transfer, but a `Libcurl` caller holds
`CurlEasy` handles (ADR-0563). Three more tasks (BL-2430 to BL-2432) add the wait, socket-action and
settings members, each in its own folder under `Libcurl/Multi`.

## Decision

1. `CurlLibrary.MultiInit()` is `curl_multi_init`: a `CurlMulti` over `CreateMultiTransfer()`, added
   as a `CurlLibrary` part in `Libcurl/Multi/Core`.
2. `CurlMulti` is `partial`, in namespace `Libcurl.Multi` whichever folder under `Libcurl/Multi` a
   part sits in, as the `Options` and `Info` groups keep theirs.
3. `CurlMulti` keeps a reference-keyed map from each added engine transfer to its `CurlEasy`, so
   `InfoRead` (`curl_multi_info_read`) answers `Libcurl`'s own `CurlMessage(Type, Easy, Result)` -
   upstream's `CURLMsg` with `easy_handle` the handle the caller added - or `null` when no message
   waits, with `msgs_in_queue` as an `out` value.
4. `AddHandle` and `RemoveHandle` answer `BadEasyHandle` for a `null` or cleaned-up `CurlEasy`, as
   libcurl does for a `NULL` or freed easy handle, without calling the engine. `AddHandle` answers
   `AddedAlready` for a handle this multi holds without calling the engine; whether another multi
   handle holds it is the engine's answer. The map changes only when the engine answers `Ok`.
5. `PerformAsync` passes the engine's code and running count through; there is no synchronous
   `Perform`, since `curl_multi_perform` never blocks on a transfer. `DisposeAsync` is
   `curl_multi_cleanup`: it disposes the engine transfer once, and any member used afterwards throws
   `ObjectDisposedException`, as `CurlEasy`'s do.
6. `CurlEasy` gains an internal `Transfer` property (the live engine transfer) for the map.

## Consequences

- The later multi tasks add members to the same class and reuse its map (for example
  `curl_multi_get_handles` answers the `CurlEasy` values).
- A message the engine sends about a transfer not added through this `CurlMulti` cannot happen
  through `Libcurl`; if an engine did send one, `InfoRead` throws `KeyNotFoundException`.
