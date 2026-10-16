# ADR-0594 — The managed engine's multi handle: add, remove, perform and info_read

- **Status:** Accepted
- **Date:** 2026-10-15

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2424.

## Context

`ICurlEngine.CreateMulti` returned nothing but `NotSupportedException` in `ManagedCurlEngine`.
BL-2424 builds the multi handle's event loop - `curl_multi_add_handle`, `_remove_handle`, `_perform`,
`_info_read` and `_cleanup` - over the seam's `IMultiTransfer` (BL-2313). Wait, poll, wakeup and the
timeout are BL-2425's, the socket-action API BL-2426's, and get_handles, waitfds, get_offt and the
notifications BL-2427's. The managed easy handle already runs a whole transfer as one
`PerformAsync` over the engine's shared connection, TLS session and DNS caches (ADR-0542).

## Decision

1. `ManagedMultiTransfer` runs each added transfer as its own `IEasyTransfer.PerformAsync`, started
   by the first `PerformAsync` of the multi handle after the add, so the transfers run at once and
   share the engine's connections through their easy handles; it drives any `IEasyTransfer`, which is
   how its tests run fake transfers. libcurl's state machine is not reproduced: the managed transfer is
   already asynchronous, and the multi handle only starts it and notices it finished.
2. `PerformAsync` never waits. It starts what is new, queues one `CURLMSG_DONE` message with the
   transfer's `CurlCode` for each perform that has completed since the last call, in the multi
   handle's add order, and answers `running_handles` as the transfers not yet done. An exception a
   perform throws (a bug, not a curl error) comes out of the call rather than being turned into a code.
3. `TryReadInfo` takes messages oldest first and reports the number left, as `curl_multi_info_read`.
4. A transfer is in one multi handle of an engine at a time: the engine's shared state holds each
   added transfer's owner by reference, so a second add, to any of the engine's multi handles,
   answers `AddedAlready`. `Remove` answers `Ok` for a transfer in no multi handle and
   `BadEasyHandle` for one in another (libcurl's `multi_remove_handle`), cancels the transfer's perform
   if it still runs and drops its unread message, as libcurl does; a removed transfer may be added
   again and starts afresh. A `null` transfer is `BadEasyHandle`.
5. `DisposeAsync` is `curl_multi_cleanup`: it removes every transfer, cancels the running performs
   and waits for them to end, ignoring how they ended; after it every call answers `BadHandle` and
   no message is left.
6. The members other tasks own throw `NotSupportedException` naming the task, so no half-built wait
   or socket API is mistaken for libcurl's.

## Consequences

`Libcurl`'s `CurlMulti` and, through it, `Curl.Console`'s `--parallel` (BL-2453) can drive the
managed engine. Until BL-2425 a caller polls `PerformAsync`; waiting on transfers comes with
`curl_multi_wait` and `curl_multi_poll`.
